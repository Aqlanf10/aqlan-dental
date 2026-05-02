using AqlanDentalPro.Application.DTOs.PatientPortal;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AqlanDentalPro.Infrastructure.Services;

public class PatientPortalService(AppDbContext db, IConfiguration config) : IPatientPortalService
{
    public async Task<(bool success, string? error)> SendVerificationCodeAsync(string phoneNumber)
    {
        var normalizedPhone = NormalizePhone(phoneNumber);
        var phoneVariants = GetPhoneVariants(phoneNumber);
        var patient = await db.Patients.FirstOrDefaultAsync(p =>
            phoneVariants.Contains(p.Phone) || phoneVariants.Contains(p.WhatsApp));
        if (patient == null)
            return (false, "رقم الهاتف غير مسجل في النظام");

        var account = await db.PatientAccounts.FirstOrDefaultAsync(a => a.PatientId == patient.Id);
        if (account == null)
        {
            account = new PatientAccount
            {
                PatientId = patient.Id,
                PhoneNumber = normalizedPhone,
                IsActive = true
            };
            db.PatientAccounts.Add(account);
        }

        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        account.VerificationCode = code;
        account.VerificationCodeExpiry = DateTime.UtcNow.AddMinutes(10);

        await db.SaveChangesAsync();
        // TODO: Integrate with SMS gateway (e.g., Twilio, Yemen SMS provider)
        return (true, null);
    }

    public async Task<(PatientAuthResponse? response, string? error)> VerifyCodeAsync(string phoneNumber, string code)
    {
        var normalizedPhone = NormalizePhone(phoneNumber);
        var phoneVariants = GetPhoneVariants(phoneNumber);
        var account = await db.PatientAccounts
            .Include(a => a.Patient)
                .ThenInclude(p => p!.PrimaryDoctor)
            .FirstOrDefaultAsync(a => phoneVariants.Contains(a.PhoneNumber));

        if (account == null)
            return (null, "الحساب غير موجود");
        if (account.VerificationCode != code)
            return (null, "رمز التحقق غير صحيح");
        if (account.VerificationCodeExpiry < DateTime.UtcNow)
            return (null, "انتهت صلاحية رمز التحقق");

        account.IsVerified = true;
        account.LastLogin = DateTime.UtcNow;
        account.VerificationCode = null;
        account.VerificationCodeExpiry = null;

        var accessToken = GeneratePatientToken(account);
        var refreshToken = GenerateRefreshToken();
        account.RefreshToken = refreshToken;
        account.RefreshTokenExpiry = DateTime.UtcNow.AddDays(30);

        await db.SaveChangesAsync();

        return (new PatientAuthResponse
        {
            AccessToken = accessToken,
            Profile = MapProfile(account.Patient, account)
        }, null);
    }

    public async Task<PatientPortalDashboardDto> GetDashboardAsync(Guid patientId)
    {
        var patient = await db.Patients
            .Include(p => p.PrimaryDoctor)
            .FirstOrDefaultAsync(p => p.Id == patientId);
        if (patient == null) throw new InvalidOperationException("المريض غير موجود");

        var account = await db.PatientAccounts.FirstOrDefaultAsync(a => a.PatientId == patientId);

        var now = DateOnly.FromDateTime(DateTime.Today);
        var nextAppt = await db.Appointments
            .Include(a => a.Doctor)
            .Where(a => a.PatientId == patientId && a.AppointmentDate >= now
                && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed))
            .OrderBy(a => a.AppointmentDate).ThenBy(a => a.StartTime)
            .FirstOrDefaultAsync();

        var totalAppts = await db.Appointments.CountAsync(a => a.PatientId == patientId);
        var upcomingAppts = await db.Appointments.CountAsync(a => a.PatientId == patientId
            && a.AppointmentDate >= now
            && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed));
        var completedTreatments = await db.GeneralTreatments.CountAsync(t => t.PatientId == patientId);

        var totalPaid = await db.Payments.Where(p => p.PatientId == patientId).SumAsync(p => (decimal?)p.Amount) ?? 0;
        var totalOutstanding = await db.Contracts
            .Where(c => c.PatientId == patientId && c.Status == "active")
            .Include(c => c.Payments)
            .Select(c => c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount))
            .SumAsync(r => (decimal?)r) ?? 0;
        var totalAmount = totalPaid + totalOutstanding;
        var activeContracts = await db.Contracts.CountAsync(c => c.PatientId == patientId && c.Status == "active");

        var recentPayments = await db.Payments
            .Include(p => p.Receipt)
            .Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .Select(p => new PatientPaymentDto
            {
                Id = p.Id,
                Amount = p.Amount,
                PaymentMethod = p.PaymentMethod ?? "نقدي",
                ServiceDescription = p.ServiceDescription,
                ReceiptNumber = p.Receipt != null ? p.Receipt.ReceiptNumber : null,
                CreatedAt = p.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        // Latest prescription
        var latestPrescription = await db.Prescriptions
            .Include(p => p.Doctor)
            .Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        return new PatientPortalDashboardDto
        {
            Profile = MapProfile(patient, account),
            NextAppointment = nextAppt != null ? MapAppointment(nextAppt) : null,
            TotalAppointments = totalAppts,
            UpcomingAppointments = upcomingAppts,
            CompletedTreatments = completedTreatments,
            Finance = new PatientFinancialSummaryDto
            {
                TotalAmount = totalAmount,
                TotalPaid = totalPaid,
                TotalOutstanding = totalOutstanding,
                ActiveContracts = activeContracts,
                RecentPayments = recentPayments
            },
            LatestPrescription = latestPrescription != null ? MapPrescription(latestPrescription) : null,
            ClinicInfo = GetClinicInfo()
        };
    }

    public async Task<PatientPortalProfileDto> GetProfileAsync(Guid patientId)
    {
        var patient = await db.Patients
            .Include(p => p.PrimaryDoctor)
            .FirstOrDefaultAsync(p => p.Id == patientId);
        if (patient == null) throw new InvalidOperationException("المريض غير موجود");

        var account = await db.PatientAccounts.FirstOrDefaultAsync(a => a.PatientId == patientId);
        return MapProfile(patient, account);
    }

    public async Task<(PatientPortalProfileDto? result, string? error)> UpdateProfileAsync(Guid patientId, PatientProfileUpdateDto req)
    {
        var patient = await db.Patients.FindAsync(patientId);
        if (patient == null) return (null, "المريض غير موجود");

        // Only allow updating safe fields
        if (req.Phone != null)
        {
            patient.Phone = NormalizePhone(req.Phone);
            patient.NormalizedPhone = patient.Phone;
        }
        if (req.WhatsApp != null)
        {
            patient.WhatsApp = NormalizePhone(req.WhatsApp);
            patient.NormalizedWhatsApp = patient.WhatsApp;
        }
        if (req.Address != null)
        {
            patient.Address = req.Address;
        }

        await db.SaveChangesAsync();

        var account = await db.PatientAccounts.FirstOrDefaultAsync(a => a.PatientId == patientId);
        return (MapProfile(patient, account), null);
    }

    public async Task<List<PatientAppointmentDto>> GetAppointmentsAsync(Guid patientId, int limit = 20)
    {
        var now = DateOnly.FromDateTime(DateTime.Today);
        var appointments = await db.Appointments
            .Include(a => a.Doctor)
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.AppointmentDate).ThenByDescending(a => a.StartTime)
            .Take(limit)
            .ToListAsync();

        return appointments.Select(a => MapAppointment(a, now)).ToList();
    }

    public async Task<(PatientAppointmentDto? result, string? error)> RequestAppointmentAsync(Guid patientId, PatientAppointmentRequestDto req)
    {
        var patient = await db.Patients.FindAsync(patientId);
        if (patient == null) return (null, "المريض غير موجود");

        var date = DateOnly.Parse(req.AppointmentDate);
        var start = TimeOnly.Parse(req.StartTime);
        var end = start.AddMinutes(30);

        // Cannot book in the past
        if (date < DateOnly.FromDateTime(DateTime.Today))
            return (null, "لا يمكن حجز موعد في تاريخ سابق");

        var doctorId = req.DoctorId ?? patient.PrimaryDoctorId;
        if (doctorId == null) return (null, "لم يتم تحديد الطبيب");

        var doctor = await db.Doctors.FindAsync(doctorId.Value);
        if (doctor == null) return (null, "الطبيب غير موجود");

        var hasConflict = await db.Appointments.AnyAsync(a =>
            a.DoctorId == doctorId.Value &&
            a.AppointmentDate == date &&
            a.Status != AppointmentStatus.Cancelled &&
            a.StartTime < end && a.EndTime > start);

        if (hasConflict) return (null, "يوجد تعارض في المواعيد مع هذا الطبيب");

        var appointment = new Appointment
        {
            PatientId = patientId,
            DoctorId = doctorId.Value,
            AppointmentDate = date,
            StartTime = start,
            EndTime = end,
            DurationMinutes = 30,
            AppointmentType = req.AppointmentType,
            Status = AppointmentStatus.Scheduled,
            Notes = req.Notes,
            ConfirmationSent = false
        };

        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        await db.Entry(appointment).Reference(a => a.Doctor).LoadAsync();

        return (MapAppointment(appointment), null);
    }

    public async Task<(bool success, string? error)> CancelAppointmentAsync(Guid patientId, Guid appointmentId)
    {
        var appointment = await db.Appointments.FindAsync(appointmentId);
        if (appointment == null) return (false, "الموعد غير موجود");
        if (appointment.PatientId != patientId) return (false, "غير مصرح بهذا الإجراء");

        // Cannot cancel completed appointments
        if (appointment.Status == AppointmentStatus.Completed)
            return (false, "لا يمكن إلغاء موعد مكتمل");

        // Cannot cancel appointments in progress
        if (appointment.Status == AppointmentStatus.InProgress)
            return (false, "لا يمكن إلغاء موعد جارٍ التنفيذ");

        // Cannot cancel past appointments
        if (appointment.AppointmentDate < DateOnly.FromDateTime(DateTime.Today))
            return (false, "لا يمكن إلغاء موعد سابق");

        appointment.Status = AppointmentStatus.Cancelled;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<List<PatientTreatmentDto>> GetTreatmentsAsync(Guid patientId, int limit = 20)
    {
        return await db.GeneralTreatments
            .Include(t => t.Doctor)
            .Include(t => t.Visit)
            .Where(t => t.PatientId == patientId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .Select(t => new PatientTreatmentDto
            {
                Id = t.Id,
                TreatmentType = t.TreatmentType,
                ToothNumber = t.ToothNumber,
                MaterialUsed = t.MaterialUsed,
                DoctorName = t.Doctor != null ? t.Doctor.Name : null,
                VisitDate = t.Visit != null ? t.Visit.VisitDate.ToString("yyyy-MM-dd") : null,
                Specialty = t.Visit != null ? t.Visit.Specialty.ToString() : null,
                CreatedAt = t.CreatedAt.ToString("yyyy-MM-dd"),
                Notes = t.Notes
            })
            .ToListAsync();
    }

    public async Task<List<PatientVisitDto>> GetVisitsAsync(Guid patientId, int limit = 20)
    {
        return await db.Visits
            .Include(v => v.Doctor)
            .Include(v => v.GeneralTreatments)
            .Where(v => v.PatientId == patientId)
            .OrderByDescending(v => v.VisitDate)
            .Take(limit)
            .Select(v => new PatientVisitDto
            {
                Id = v.Id,
                VisitDate = v.VisitDate.ToString("yyyy-MM-dd"),
                VisitType = v.VisitType,
                Specialty = v.Specialty.ToString(),
                DoctorName = v.Doctor != null ? v.Doctor.Name : null,
                ChiefComplaint = v.ChiefComplaint,
                TreatmentDone = v.TreatmentDone,
                Instructions = v.Instructions,
                NextVisitPlan = v.NextVisitPlan,
                NextVisitDate = v.NextVisitDate.HasValue ? v.NextVisitDate.Value.ToString("yyyy-MM-dd") : null,
                TreatmentCount = v.GeneralTreatments.Count
            })
            .ToListAsync();
    }

    public async Task<List<PatientPrescriptionDto>> GetPrescriptionsAsync(Guid patientId, int limit = 20)
    {
        var prescriptions = await db.Prescriptions
            .Include(p => p.Doctor)
            .Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return prescriptions.Select(MapPrescription).ToList();
    }

    public async Task<PatientFinancialSummaryDto> GetFinancialSummaryAsync(Guid patientId)
    {
        var totalPaid = await db.Payments.Where(p => p.PatientId == patientId).SumAsync(p => (decimal?)p.Amount) ?? 0;

        var contracts = await db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.PatientId == patientId)
            .ToListAsync();

        var activeContracts = contracts.Where(c => c.Status == "active").ToList();
        var totalOutstanding = activeContracts
            .Sum(c => Math.Max(0, c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount)));
        var totalAmount = contracts.Sum(c => c.TotalAmount - c.DiscountAmount);

        var contractDtos = activeContracts.Select(c => new PatientContractDto
        {
            Id = c.Id,
            Specialty = c.Specialty,
            TotalAmount = c.TotalAmount - c.DiscountAmount,
            PaidAmount = c.Payments.Sum(p => p.Amount),
            RemainingAmount = Math.Max(0, c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount)),
            Status = c.Status,
            StartDate = c.StartDate.HasValue ? c.StartDate.Value.ToString("yyyy-MM-dd") : null
        }).ToList();

        var recentPayments = await db.Payments
            .Include(p => p.Receipt)
            .Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(20)
            .Select(p => new PatientPaymentDto
            {
                Id = p.Id,
                Amount = p.Amount,
                PaymentMethod = p.PaymentMethod ?? "نقدي",
                ServiceDescription = p.ServiceDescription,
                ReceiptNumber = p.Receipt != null ? p.Receipt.ReceiptNumber : null,
                CreatedAt = p.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return new PatientFinancialSummaryDto
        {
            TotalAmount = totalAmount,
            TotalPaid = totalPaid,
            TotalOutstanding = totalOutstanding,
            ActiveContracts = activeContracts.Count,
            Contracts = contractDtos,
            RecentPayments = recentPayments
        };
    }

    public async Task<List<PatientDoctorDto>> GetDoctorsAsync()
    {
        return await db.Doctors
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new PatientDoctorDto
            {
                Id = d.Id,
                Name = d.Name,
                Specialty = d.Specialty
            })
            .ToListAsync();
    }

    public Task<PatientClinicInfoDto> GetClinicInfoAsync()
    {
        return Task.FromResult(GetClinicInfo());
    }

    public async Task<Guid?> GetPatientIdByPhoneAsync(string phoneNumber)
    {
        var normalizedPhone = NormalizePhone(phoneNumber);
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Phone == normalizedPhone || p.WhatsApp == normalizedPhone);
        return patient?.Id;
    }

    // ── Private Helpers ──────────────────────────────────────────────────────

    private static PatientAppointmentDto MapAppointment(Appointment a, DateOnly? now = null)
    {
        var today = now ?? DateOnly.FromDateTime(DateTime.Today);
        var canCancel = a.AppointmentDate >= today
            && a.Status != AppointmentStatus.Completed
            && a.Status != AppointmentStatus.InProgress
            && a.Status != AppointmentStatus.Cancelled
            && a.Status != AppointmentStatus.NoShow;

        return new PatientAppointmentDto
        {
            Id = a.Id,
            AppointmentDate = a.AppointmentDate.ToString("yyyy-MM-dd"),
            StartTime = a.StartTime.ToString("HH:mm"),
            EndTime = a.EndTime.ToString("HH:mm"),
            AppointmentType = a.AppointmentType,
            DoctorName = a.Doctor.Name,
            Status = a.Status.ToString(),
            Notes = a.Notes,
            CanCancel = canCancel
        };
    }

    private static PatientPrescriptionDto MapPrescription(Prescription p)
    {
        return new PatientPrescriptionDto
        {
            Id = p.Id,
            Diagnosis = p.Diagnosis,
            Drugs = ParseDrugs(p.Drugs),
            Instructions = p.Notes,
            DoctorName = p.Doctor?.Name ?? "",
            CreatedAt = p.CreatedAt.ToString("yyyy-MM-dd")
        };
    }

    private static List<PrescriptionDrugDto> ParseDrugs(JsonDocument? drugs)
    {
        if (drugs == null) return [];
        try
        {
            var root = drugs.RootElement;
            if (root.ValueKind != JsonValueKind.Array) return [];

            var result = new List<PrescriptionDrugDto>();
            foreach (var item in root.EnumerateArray())
            {
                result.Add(new PrescriptionDrugDto
                {
                    Name = item.TryGetProperty("name", out var n) ? n.GetString() ?? ""
                         : item.TryGetProperty("medication", out var m) ? m.GetString() ?? ""
                         : "",
                    Dosage = item.TryGetProperty("dose", out var d) ? d.GetString()
                          : item.TryGetProperty("dosage", out var d2) ? d2.GetString() : null,
                    Frequency = item.TryGetProperty("frequency", out var f) ? f.GetString() : null,
                    Duration = item.TryGetProperty("duration", out var du) ? du.GetString() : null,
                    Notes = item.TryGetProperty("notes", out var nt) ? nt.GetString() : null
                });
            }
            return result;
        }
        catch
        {
            return [];
        }
    }

    private static PatientClinicInfoDto GetClinicInfo()
    {
        // These can be moved to a Settings table in a future sprint
        return new PatientClinicInfoDto
        {
            ClinicName = "مركز د. عقلان الكامل لطب وتقويم الأسنان",
            Phone = "+967123456789",
            WhatsApp = "+967123456789",
            Address = "اليمن",
            WorkingHours = "السبت - الأربعاء: ٩ ص - ٩ م"
        };
    }

    private static string NormalizePhone(string phone)
    {
        var cleaned = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (cleaned.StartsWith("7") && cleaned.Length == 9)
            cleaned = "+967" + cleaned;
        else if (cleaned.StartsWith("0") && cleaned.Length == 10)
            cleaned = "+967" + cleaned[1..];
        return cleaned;
    }

    /// <summary>
    /// Returns all possible phone formats for matching against stored data.
    /// Handles: +967770111001, 770111001, 0770111001, 967770111001
    /// </summary>
    private static List<string> GetPhoneVariants(string phone)
    {
        var variants = new List<string>();
        var cleaned = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());

        // Add as-is
        variants.Add(cleaned);

        // If starts with +967, also add without prefix
        if (cleaned.StartsWith("+967"))
        {
            variants.Add(cleaned[4..]);           // 770111001
            variants.Add("0" + cleaned[4..]);     // 0770111001
            variants.Add(cleaned[1..]);            // 967770111001
        }
        // If starts with 967 without +, also add variants
        else if (cleaned.StartsWith("967"))
        {
            variants.Add("+" + cleaned);          // +967770111001
            variants.Add(cleaned[3..]);            // 770111001
            variants.Add("0" + cleaned[3..]);      // 0770111001
        }
        // If starts with 0, also add variants
        else if (cleaned.StartsWith("0"))
        {
            variants.Add("+967" + cleaned[1..]);  // +967770111001
            variants.Add(cleaned[1..]);            // 770111001
        }
        // If starts with 7 (Yemen local format), also add variants
        else if (cleaned.StartsWith("7"))
        {
            variants.Add("+967" + cleaned);       // +967770111001
            variants.Add("0" + cleaned);           // 0770111001
            variants.Add("967" + cleaned);         // 967770111001
        }

        return variants.Distinct().ToList();
    }

    private string GeneratePatientToken(PatientAccount account)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, account.PatientId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("patientId", account.PatientId.ToString()),
            new Claim(ClaimTypes.Role, "Patient"),
            new Claim("portal", "true")
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    private static PatientPortalProfileDto MapProfile(Patient patient, PatientAccount? account = null) => new()
    {
        Id = patient.Id,
        PatientNumber = patient.PatientNumber,
        FullName = $"{patient.FirstName} {patient.MiddleName} {patient.LastName}".Replace("  ", " ").Trim(),
        Phone = patient.Phone,
        WhatsApp = patient.WhatsApp,
        Gender = patient.Gender?.ToString(),
        Age = patient.DateOfBirth.HasValue ? CalculateAge(patient.DateOfBirth.Value) : null,
        DateOfBirth = patient.DateOfBirth.HasValue ? patient.DateOfBirth.Value.ToString("yyyy-MM-dd") : null,
        Address = patient.Address,
        PrimaryDoctorName = patient.PrimaryDoctor?.Name,
        AccountStatus = account?.IsVerified == true ? "active" : account != null ? "pending" : "none",
        LastLogin = account?.LastLogin.HasValue == true ? account.LastLogin!.Value.ToString("yyyy-MM-dd HH:mm") : null
    };

    private static int CalculateAge(DateOnly dob)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - dob.Year;
        if (dob > today.AddYears(-age)) age--;
        return age;
    }
}
