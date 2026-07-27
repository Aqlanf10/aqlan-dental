using AqlanDentalPro.Application.DTOs.BookingRequests;
using AqlanDentalPro.Application.DTOs.Common;
using AqlanDentalPro.Application.Exceptions;
using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Application.DTOs.Patients;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

public class BookingRequestService(
    AppDbContext db,
    PatientService patientService,
    IAppointmentRepository appointmentRepository,
    ILogger<BookingRequestService> logger) : IBookingRequestService
{
    // Clinic working hours: Saturday-Thursday 08:00-20:00, Friday closed
    private static readonly TimeOnly ClinicOpen = new(8, 0);
    private static readonly TimeOnly ClinicClose = new(20, 0);
    private const int DefaultSlotDurationMinutes = 30;
    // CORE-APPT-006: the hard-coded "Asia/Aden" / "Arab Standard Time" IDs that
    // used to live here are gone — the clinic timezone comes from the configured
    // ClinicTimeProvider (settings key `clinic.timezone`, SEQ-13) like everywhere else.

    // Booking request statuses that block a time slot
    private static readonly HashSet<BookingRequestStatus> BlockingStatuses =
    [
        BookingRequestStatus.Pending,
        BookingRequestStatus.Reviewed,
        BookingRequestStatus.Confirmed
    ];

    // Appointment statuses that block a time slot (Cancelled & NoShow do NOT block)
    private static readonly HashSet<AppointmentStatus> BlockingAppointmentStatuses =
    [
        AppointmentStatus.Scheduled,
        AppointmentStatus.Confirmed,
        AppointmentStatus.Arrived,
        AppointmentStatus.Waiting,
        AppointmentStatus.Called,
        AppointmentStatus.InRoom,
        AppointmentStatus.InProgress,
        AppointmentStatus.Completed
    ];

    public async Task<BookingRequestDto> CreateAsync(CreateBookingRequestDto dto)
    {
        // ── Strong validation ──────────────────────────────────────────────

        // PreferredDate is required
        if (string.IsNullOrWhiteSpace(dto.PreferredDate))
            throw new ArgumentException("التاريخ المفضل مطلوب");

        // PreferredTime is required
        if (string.IsNullOrWhiteSpace(dto.PreferredTime))
            throw new ArgumentException("الوقت المفضل مطلوب");

        // Parse and validate date
        if (!DateOnly.TryParse(dto.PreferredDate, out var parsedDate))
            throw new ArgumentException("صيغة التاريخ غير صحيحة");

        var clinicNow = GetClinicNow();
        var today = DateOnly.FromDateTime(clinicNow);

        // PreferredDate cannot be past
        if (parsedDate < today)
            throw new ArgumentException("لا يمكن اختيار تاريخ سابق");

        // Friday blocked
        if (parsedDate.DayOfWeek == DayOfWeek.Friday)
            throw new ArgumentException("المركز مغلق يوم الجمعة");

        // Validate email format if provided
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(dto.Email);
                if (addr.Address != dto.Email.Trim())
                    throw new ArgumentException("صيغة البريد الإلكتروني غير صحيحة");
            }
            catch (FormatException)
            {
                throw new ArgumentException("صيغة البريد الإلكتروني غير صحيحة");
            }
        }

        // DoctorId validation: if provided, verify the doctor exists and is active
        if (dto.DoctorId.HasValue)
        {
            var doctorExists = await db.Doctors.AnyAsync(d => d.Id == dto.DoctorId.Value && d.IsActive);
            if (!doctorExists)
                throw new ArgumentException("الطبيب المحدد غير موجود أو غير نشط");
        }

        // PreferredTime must be a valid available slot
        if (!await IsSlotAvailableAsync(dto.PreferredDate, dto.PreferredTime, dto.DoctorId))
            throw new SlotNotAvailableException("هذا الوقت لم يعد متاحًا، يرجى اختيار وقت آخر.");

        // ── Same-day duplicate prevention ──────────────────────────────────
        var normalizedPhone = NormalizePhone(dto.PhoneNumber);
        var normalizedName = NormalizeName(dto.PatientName);
        var targetDate = dto.PreferredDate.Trim();

        // Fetch matching-date active requests to client-evaluate name normalization
        var sameDateRequests = await db.BookingRequests
            .Where(r => r.IsActive
                     && r.PreferredDate == targetDate
                     && BlockingStatuses.Contains(r.Status)
                     && r.ConvertedToAppointmentId == null)
            .Select(r => new { r.PhoneNumber, r.PatientName })
            .ToListAsync();

        var duplicateOnSameDate = sameDateRequests.Any(r =>
            NormalizePhone(r.PhoneNumber) == normalizedPhone
            || NormalizeName(r.PatientName) == normalizedName);

        if (duplicateOnSameDate)
        {
            throw new DuplicateBookingRequestException(
                "لديك طلب حجز سابق لنفس اليوم قيد المراجعة، سيتم التواصل معك قريبًا. لا داعي لإرسال طلب جديد.");
        }

        // ── Create entity ──────────────────────────────────────────────────
        var entity = new BookingRequest
        {
            PatientName = dto.PatientName.Trim(),
            PhoneNumber = dto.PhoneNumber.Trim(),
            Email = dto.Email?.Trim(),
            ServiceType = dto.ServiceType?.Trim(),
            PreferredDate = dto.PreferredDate?.Trim(),
            PreferredTime = dto.PreferredTime?.Trim(),
            Notes = dto.Notes?.Trim(),
            DoctorId = dto.DoctorId
        };

        db.BookingRequests.Add(entity);
        await db.SaveChangesAsync();

        // Look up doctor name for the response DTO
        string? doctorName = null;
        if (dto.DoctorId.HasValue)
        {
            doctorName = await db.Doctors
                .Where(d => d.Id == dto.DoctorId.Value)
                .Select(d => d.Name)
                .FirstOrDefaultAsync();
        }

        return ToDto(entity, doctorName);
    }

    public async Task<List<BookingRequestDto>> GetAllAsync(string? statusFilter)
    {
        var query = db.BookingRequests.AsQueryable();

        if (!string.IsNullOrWhiteSpace(statusFilter) &&
            Enum.TryParse<BookingRequestStatus>(statusFilter, ignoreCase: true, out var status))
        {
            query = query.Where(r => r.Status == status);
        }

        var items = await query
            .Include(r => r.Doctor)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return items.Select(r => ToDto(r, r.Doctor?.Name)).ToList();
    }

    public async Task<PaginatedResponse<BookingRequestDto>> GetAllPaginatedAsync(string? statusFilter, int page, int pageSize)
    {
        var query = db.BookingRequests.AsQueryable();

        if (!string.IsNullOrWhiteSpace(statusFilter) &&
            Enum.TryParse<BookingRequestStatus>(statusFilter, ignoreCase: true, out var status))
        {
            query = query.Where(r => r.Status == status);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .Include(r => r.Doctor)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedResponse<BookingRequestDto>
        {
            Data = items.Select(r => ToDto(r, r.Doctor?.Name)),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<BookingRequestDto?> GetByIdAsync(Guid id)
    {
        var entity = await db.BookingRequests
            .Include(r => r.Doctor)
            .FirstOrDefaultAsync(r => r.Id == id);
        return entity == null ? null : ToDto(entity, entity.Doctor?.Name);
    }

    public async Task<BookingRequestDto?> UpdateStatusAsync(Guid id, UpdateBookingRequestStatusDto dto, Guid reviewedBy)
    {
        var entity = await db.BookingRequests
            .Include(r => r.Doctor)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null) return null;

        if (!Enum.TryParse<BookingRequestStatus>(dto.Status, ignoreCase: true, out var status))
            return null;

        entity.Status = status;
        entity.StaffNotes = dto.StaffNotes?.Trim();
        entity.ReviewedBy = reviewedBy;
        entity.ReviewedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return ToDto(entity, entity.Doctor?.Name);
    }

    public async Task<BookingAvailabilityResponseDto> GetAvailabilityAsync(string date, string? serviceType, Guid? doctorId = null)
    {
        // Parse and validate date
        if (!DateOnly.TryParse(date, out var parsedDate))
        {
            return new BookingAvailabilityResponseDto(date, serviceType, [], false, "صيغة التاريخ غير صحيحة");
        }

        var clinicNow = GetClinicNow();
        var today = DateOnly.FromDateTime(clinicNow);

        // Check for past date
        if (parsedDate < today)
        {
            return new BookingAvailabilityResponseDto(date, serviceType, [], false, "لا يمكن اختيار تاريخ سابق");
        }

        // Check for Friday
        if (parsedDate.DayOfWeek == DayOfWeek.Friday)
        {
            return new BookingAvailabilityResponseDto(date, serviceType, [], true, "المركز مغلق يوم الجمعة");
        }

        // If doctorId is provided, use doctor-specific logic
        if (doctorId.HasValue)
        {
            return await GetDoctorAvailabilityAsync(date, serviceType, parsedDate, today, clinicNow, doctorId.Value);
        }

        // ── Clinic-wide availability (existing logic) ──────────────────────
        var slots = GenerateTimeSlots(DefaultSlotDurationMinutes, ClinicOpen, ClinicClose);

        // Get existing appointments for this date that block slots
        var appointmentTimes = await db.Appointments
            .Where(a => a.AppointmentDate == parsedDate
                     && BlockingAppointmentStatuses.Contains(a.Status))
            .Select(a => new { a.StartTime, a.EndTime })
            .ToListAsync();

        // Get existing booking requests for this date that block slots
        var bookingRequestTimes = await db.BookingRequests
            .Where(r => r.PreferredDate == date
                     && BlockingStatuses.Contains(r.Status)
                     && r.ConvertedToAppointmentId == null
                     && r.PreferredTime != null)
            .Select(r => r.PreferredTime!)
            .ToListAsync();

        // Mark unavailable slots
        var result = new List<BookingAvailabilitySlotDto>();
        foreach (var slot in slots)
        {
            var slotTime = TimeOnly.Parse(slot);
            var slotEnd = slotTime.AddMinutes(DefaultSlotDurationMinutes);

            var isPastSlotToday = parsedDate == today && slotTime <= TimeOnly.FromDateTime(clinicNow);
            var isBlockedByAppointment = appointmentTimes.Any(a =>
                a.StartTime < slotEnd && a.EndTime > slotTime);
            var isBlockedByBookingRequest = bookingRequestTimes.Any(brTime =>
                IsSameSlotTime(brTime, slot));

            if (isPastSlotToday)
                result.Add(new BookingAvailabilitySlotDto(slot, false, "انتهى الوقت"));
            else if (isBlockedByAppointment || isBlockedByBookingRequest)
                result.Add(new BookingAvailabilitySlotDto(slot, false, "محجوز"));
            else
                result.Add(new BookingAvailabilitySlotDto(slot, true));
        }

        return new BookingAvailabilityResponseDto(date, serviceType, result);
    }

    public async Task<bool> IsSlotAvailableAsync(string date, string time, Guid? doctorId = null)
    {
        // Validate date
        if (!DateOnly.TryParse(date, out var parsedDate))
            return false;

        var clinicNow = GetClinicNow();
        var today = DateOnly.FromDateTime(clinicNow);

        // Past date
        if (parsedDate < today)
            return false;

        // Friday
        if (parsedDate.DayOfWeek == DayOfWeek.Friday)
            return false;

        // Find the matching 24h slot format
        var slot24h = NormalizeTo24h(time);
        if (slot24h == null)
            return false;

        var slotTime = TimeOnly.Parse(slot24h);

        // If doctorId is provided, use doctor-specific slot duration
        int slotDuration = DefaultSlotDurationMinutes;
        if (doctorId.HasValue)
        {
            var dayOfWeek = (int)parsedDate.DayOfWeek; // 0=Sunday ... 6=Saturday
            var schedule = await db.DoctorSchedules
                .FirstOrDefaultAsync(s => s.DoctorId == doctorId.Value && s.DayOfWeek == dayOfWeek && s.IsWorking);
            if (schedule == null)
                return false; // Doctor not working this day
            slotDuration = schedule.SlotDurationMinutes > 0 ? schedule.SlotDurationMinutes : DefaultSlotDurationMinutes;
        }

        var slotEnd = slotTime.AddMinutes(slotDuration);

        // Reject same-day slots that have already started or passed in clinic local time.
        if (parsedDate == today && slotTime <= TimeOnly.FromDateTime(clinicNow))
            return false;

        // Check appointments — filter by doctor if doctorId provided
        var appointmentQuery = db.Appointments
            .Where(a => a.AppointmentDate == parsedDate
                     && BlockingAppointmentStatuses.Contains(a.Status)
                     && a.StartTime < slotEnd
                     && a.EndTime > slotTime);
        if (doctorId.HasValue)
            appointmentQuery = appointmentQuery.Where(a => a.DoctorId == doctorId.Value);

        var hasAppointmentConflict = await appointmentQuery.AnyAsync();
        if (hasAppointmentConflict)
            return false;

        // Check booking requests (fetch to client first — IsSameSlotTime cannot be translated to SQL)
        var bookingQuery = db.BookingRequests
            .Where(r => r.PreferredDate == date
                     && BlockingStatuses.Contains(r.Status)
                     && r.ConvertedToAppointmentId == null
                     && r.PreferredTime != null);
        if (doctorId.HasValue)
            bookingQuery = bookingQuery.Where(r => r.DoctorId == doctorId.Value);

        var conflictingBookingTimes = await bookingQuery
            .Select(r => r.PreferredTime!)
            .ToListAsync();

        var hasBookingConflict = conflictingBookingTimes.Any(t => IsSameSlotTime(t, slot24h));

        return !hasBookingConflict;
    }

    public async Task<BookingRequestDto?> ConvertToAppointmentAsync(Guid bookingRequestId, ConvertBookingRequestToAppointmentDto dto, Guid convertedBy)
    {
        // 1. Find the booking request
        var bookingRequest = await db.BookingRequests
            .Include(r => r.Doctor)
            .FirstOrDefaultAsync(r => r.Id == bookingRequestId);

        if (bookingRequest == null)
            return null;

        // 2. Verify it's in Confirmed status
        if (bookingRequest.Status != BookingRequestStatus.Confirmed)
            throw new ArgumentException("يجب أن يكون الطلب في حالة مؤكد لتحويله إلى موعد");

        // 3. Verify it hasn't been converted already
        if (bookingRequest.ConvertedToAppointmentId.HasValue)
            throw new ArgumentException("تم تحويل هذا الطلب بالفعل إلى موعد");

        // ── F4 FIX: Auto find-or-create patient if PatientId not provided ──
        // Previously, ConvertToAppointment required a PatientId from the caller.
        // For new patients (typical public booking use case), staff had to manually
        // create the patient first — a step that was often missed, creating orphan
        // confirmed bookings. Now we auto-resolve the patient:
        Guid patientId = dto.PatientId;

        if (patientId == Guid.Empty)
        {
            // Try to find existing patient by phone number
            // Use PhoneNormalizer (adds 967 prefix for Yemen numbers) so the lookup
            // matches the NormalizedPhone/NormalizedWhatsApp columns in the Patients table.
            var normalizedPhone = AqlanDentalPro.Application.Services.PhoneNormalizer.Normalize(bookingRequest.PhoneNumber);
            Patient? existingPatient = null;

            if (!string.IsNullOrWhiteSpace(normalizedPhone))
            {
                existingPatient = await db.Patients
                    .FirstOrDefaultAsync(p => p.IsActive &&
                        (p.NormalizedPhone == normalizedPhone || p.NormalizedWhatsApp == normalizedPhone));
            }

            if (existingPatient != null)
            {
                patientId = existingPatient.Id;
            }
            else
            {
                // CORE-PAT-010: this used to hand-roll `new Patient { … }` +
                // SaveChanges, skipping PatientService.CreateAsync entirely. The
                // resulting record had NO PatientNumber (so the SECOND conversion
                // ever made hit the unique index and 500'd), no NormalizedPhone
                // (so the dedupe lookup above could never find it again — every
                // return visit created another duplicate file), no branch and no
                // portal account. Route through the one real creation path.
                var nameParts = bookingRequest.PatientName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var createReq = new CreatePatientRequest
                {
                    FirstName = nameParts.Length > 0 ? nameParts[0] : "مريض",
                    MiddleName = nameParts.Length > 2 ? string.Join(" ", nameParts[1..^1]) : null,
                    LastName = nameParts.Length > 1 ? nameParts[^1] : "غير محدد",
                    Phone = bookingRequest.PhoneNumber?.Trim(),
                    WhatsApp = bookingRequest.PhoneNumber?.Trim(),
                    ReferralSource = "طلب حجز من الموقع",
                    PrimaryDoctorId = bookingRequest.DoctorId,
                    DentalHistory = !string.IsNullOrWhiteSpace(bookingRequest.Notes)
                        ? new DentalHistoryDto { Notes = $"ملاحظات طلب الحجز: {bookingRequest.Notes}" }
                        : null
                };

                var created = await patientService.CreateAsync(createReq);
                patientId = created.Id;

                logger.LogInformation(
                    "F4: Auto-created patient {PatientId} ({PatientNumber}) from booking request {BookingRequestId}",
                    patientId, created.PatientNumber, bookingRequestId);
            }
        }

        // 4-7. Create the Appointment (use resolved patientId instead of dto.PatientId),
        // link the booking request, and save — all atomically under the same
        // per-doctor/per-room advisory lock the direct booking path uses.
        //
        // CORE-APPT-004: this conversion path used to be a plain check-then-save
        // with no transaction/lock at all (so two concurrent conversions for the
        // same doctor/slot could both pass) and NO room-conflict check whatsoever
        // despite storing ClinicRoomId — a real double-booking gap distinct from,
        // and unprotected by, the guard AppointmentService.CreateAsync uses.
        var appointment = new Appointment
        {
            PatientId = patientId,
            DoctorId = dto.DoctorId,
            AppointmentDate = dto.AppointmentDate,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            DurationMinutes = dto.DurationMinutes,
            AppointmentType = dto.AppointmentType ?? bookingRequest.ServiceType ?? "عام",
            Notes = bookingRequest.Notes,
            CreatedBy = convertedBy,
            ServiceId = dto.ServiceId,
            ClinicRoomId = dto.ClinicRoomId
        };

        // Non-relational providers (InMemory tests) have no transaction/advisory
        // lock — the conflict logic below is identical, only the cross-process
        // race protection (which needs PostgreSQL) is unavailable.
        var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        try
        {
            if (await appointmentRepository.HasConflictUnderLockAsync(appointment))
            {
                var roomConflict = appointment.ClinicRoomId.HasValue &&
                    await appointmentRepository.HasRoomConflictAsync(
                        appointment.ClinicRoomId.Value, appointment.AppointmentDate, appointment.StartTime, appointment.EndTime);
                throw new ArgumentException(roomConflict
                    ? "الغرفة محجوزة في هذا الوقت"
                    : "يوجد موعد آخر في نفس الوقت لهذا الطبيب");
            }

            db.Appointments.Add(appointment);

            // Set BookingRequest.ConvertedToAppointmentId
            bookingRequest.ConvertedToAppointmentId = appointment.Id;

            // Update StaffNotes
            var existingNotes = string.IsNullOrWhiteSpace(bookingRequest.StaffNotes)
                ? ""
                : bookingRequest.StaffNotes + " | ";
            bookingRequest.StaffNotes = existingNotes + "تم تحويل الطلب إلى موعد";

            await db.SaveChangesAsync();
            if (tx != null) await tx.CommitAsync();
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync();
            throw;
        }
        finally
        {
            if (tx != null) await tx.DisposeAsync();
        }

        // 8. If appointment is today, also add patient to the clinic queue
        // CORE-PAT-017: compare against the CLINIC day, not the UTC day —
        // Yemen is UTC+3, so between 00:00 and 03:00 clinic time a same-day
        // booking conversion silently skipped the queue insert.
        var today = ClinicTimeProvider.ClinicToday();
        if (dto.AppointmentDate == today)
        {
            var activeStatuses = new HashSet<ClinicQueueStatus>
            {
                ClinicQueueStatus.Waiting,
                ClinicQueueStatus.Called,
                ClinicQueueStatus.InRoom,
                ClinicQueueStatus.InProgress
            };

            var existingQueueItem = await db.ClinicQueueItems
                .AnyAsync(q => q.AppointmentId == appointment.Id
                            && q.QueueDate == today
                            && activeStatuses.Contains(q.Status)
                            && q.IsActive);

            if (!existingQueueItem)
            {
                var queueItem = new ClinicQueueItem
                {
                    PatientId = patientId,
                    AppointmentId = appointment.Id,
                    DoctorId = dto.DoctorId,
                    Status = ClinicQueueStatus.Waiting,
                    QueueDate = today,
                    AddedByUserId = convertedBy
                };
                db.ClinicQueueItems.Add(queueItem);
                await db.SaveChangesAsync();
            }
        }

        return ToDto(bookingRequest, bookingRequest.Doctor?.Name);
    }

    // ── Private helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Doctor-specific availability: checks DoctorSchedule, uses doctor's SlotDurationMinutes,
    /// checks against doctor's existing appointments and booking requests, skips break times.
    /// </summary>
    private async Task<BookingAvailabilityResponseDto> GetDoctorAvailabilityAsync(
        string date, string? serviceType, DateOnly parsedDate, DateOnly today, DateTime clinicNow, Guid doctorId)
    {
        // Check if doctor exists and is active
        var doctor = await db.Doctors
            .Where(d => d.Id == doctorId && d.IsActive)
            .Select(d => new { d.Name })
            .FirstOrDefaultAsync();

        if (doctor == null)
        {
            return new BookingAvailabilityResponseDto(date, serviceType, [], false,
                "الطبيب المحدد غير موجود أو غير نشط", doctorId, null);
        }

        // Check if the doctor has a DoctorSchedule for that day of week
        var dayOfWeek = (int)parsedDate.DayOfWeek; // 0=Sunday ... 6=Saturday
        var schedule = await db.DoctorSchedules
            .FirstOrDefaultAsync(s => s.DoctorId == doctorId && s.DayOfWeek == dayOfWeek && s.IsWorking);

        if (schedule == null)
        {
            return new BookingAvailabilityResponseDto(date, serviceType, [], false,
                "لا توجد أوقات متاحة لهذا الطبيب في هذا اليوم.", doctorId, doctor.Name);
        }

        var slotDuration = schedule.SlotDurationMinutes > 0 ? schedule.SlotDurationMinutes : DefaultSlotDurationMinutes;
        var slots = GenerateTimeSlots(slotDuration, schedule.StartTime, schedule.EndTime);

        // Get existing appointments for this doctor on this date that block slots
        var appointmentTimes = await db.Appointments
            .Where(a => a.AppointmentDate == parsedDate
                     && a.DoctorId == doctorId
                     && BlockingAppointmentStatuses.Contains(a.Status))
            .Select(a => new { a.StartTime, a.EndTime })
            .ToListAsync();

        // Get existing booking requests for this doctor on this date that block slots
        var bookingRequestTimes = await db.BookingRequests
            .Where(r => r.PreferredDate == date
                     && r.DoctorId == doctorId
                     && BlockingStatuses.Contains(r.Status)
                     && r.ConvertedToAppointmentId == null
                     && r.PreferredTime != null)
            .Select(r => r.PreferredTime!)
            .ToListAsync();

        // Mark unavailable slots
        var result = new List<BookingAvailabilitySlotDto>();
        foreach (var slot in slots)
        {
            var slotTime = TimeOnly.Parse(slot);
            var slotEnd = slotTime.AddMinutes(slotDuration);

            // Same-day past slots
            var isPastSlotToday = parsedDate == today && slotTime <= TimeOnly.FromDateTime(clinicNow);

            // Break time check
            var isInBreak = schedule.BreakStart.HasValue && schedule.BreakEnd.HasValue
                && slotTime < schedule.BreakEnd && slotEnd > schedule.BreakStart;

            // Appointment conflict
            var isBlockedByAppointment = appointmentTimes.Any(a =>
                a.StartTime < slotEnd && a.EndTime > slotTime);

            // Booking request conflict
            var isBlockedByBookingRequest = bookingRequestTimes.Any(brTime =>
                IsSameSlotTime(brTime, slot));

            if (isPastSlotToday)
                result.Add(new BookingAvailabilitySlotDto(slot, false, "انتهى الوقت"));
            else if (isInBreak)
                result.Add(new BookingAvailabilitySlotDto(slot, false, "استراحة"));
            else if (isBlockedByAppointment || isBlockedByBookingRequest)
                result.Add(new BookingAvailabilitySlotDto(slot, false, "محجوز"));
            else
                result.Add(new BookingAvailabilitySlotDto(slot, true));
        }

        return new BookingAvailabilityResponseDto(date, serviceType, result, false, null, doctorId, doctor.Name);
    }

    /// <summary>
    /// Generates time slots with the given duration between start and end times.
    /// </summary>
    private static List<string> GenerateTimeSlots(int durationMinutes, TimeOnly start, TimeOnly end)
    {
        var slots = new List<string>();
        var current = start;
        while (current < end)
        {
            var slotEnd = current.AddMinutes(durationMinutes);
            if (slotEnd > end) break;
            slots.Add(current.ToString("HH:mm"));
            current = slotEnd;
        }
        return slots;
    }

    /// <summary>
    /// Returns the current clinic-local time.
    /// <para>
    /// CORE-APPT-006: this used to resolve the timezone itself from two hard-coded
    /// IDs ("Asia/Aden" / "Arab Standard Time") with its own fallback chain,
    /// independent of the timezone SEQ-13 made configurable via the
    /// `clinic.timezone` setting. The two happened to agree, so nothing was
    /// visibly broken — but changing that setting would have moved the whole
    /// system's idea of "today" while leaving public booking (availability, past-date
    /// rejection, same-day slot cutoff) silently on Aden. One source of truth now.
    /// </para>
    /// </summary>
    private static DateTime GetClinicNow() => ClinicTimeProvider.ClinicNow();

    // Delegates to shared BookingUtilities (extracted for testability — P9-2)
    private static string? NormalizeTo24h(string time) => BookingUtilities.NormalizeTo24h(time);
    private static bool IsSameSlotTime(string preferredTime, string slot24h) => BookingUtilities.IsSameSlotTime(preferredTime, slot24h);
    private static string NormalizePhone(string? phone) => BookingUtilities.NormalizePhone(phone);
    private static string NormalizeName(string? name) => BookingUtilities.NormalizeName(name);

    private static BookingRequestDto ToDto(BookingRequest r, string? doctorName = null) => new(
        r.Id,
        r.PatientName,
        r.PhoneNumber,
        r.Email,
        r.ServiceType,
        r.PreferredDate,
        r.PreferredTime,
        r.Notes,
        r.Status.ToString(),
        r.StaffNotes,
        r.CreatedAt,
        r.ReviewedAt,
        r.DoctorId,
        doctorName,
        r.ConvertedToAppointmentId
    );
}
