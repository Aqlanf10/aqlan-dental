using AqlanDentalPro.Application.Common;
using AqlanDentalPro.Application.DTOs.PatientPortal;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Common;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net.Http.Json;

namespace AqlanDentalPro.Infrastructure.Services;

public class PatientPortalService(AppDbContext db, IConfiguration config, IHttpClientFactory httpClientFactory, IPatientAccountLinkingService linkingService, IFinanceReadService financeReadService, ILogger<PatientPortalService> logger) : IPatientPortalService
{
    public async Task<(PatientAuthResponse? response, string? error)> LoginAsync(string username, string password)
    {
        var account = await db.PatientAccounts
            .Include(a => a.Patient)
                .ThenInclude(p => p!.PrimaryDoctor)
            .FirstOrDefaultAsync(a => a.Username == username);

        if (account == null)
            return (null, "اسم المستخدم أو كلمة المرور غير صحيحة");

        if (string.IsNullOrEmpty(account.PasswordHash) || string.IsNullOrEmpty(account.PasswordSalt))
            return (null, "حسابك غير مفعّل بعد. تواصل مع العيادة للحصول على بيانات الدخول.");

        // C-04 FIX: Verify password with Argon2id using constant-time comparison
        var hash = AuthService.HashPassword(password, account.PasswordSalt);
        if (!CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(hash),
            Convert.FromBase64String(account.PasswordHash)))
            return (null, "اسم المستخدم أو كلمة المرور غير صحيحة");

        // Patient navigation may be null if the patient is archived (soft-delete query filter)
        if (account.Patient == null || !account.Patient.IsActive)
            return (null, "تم تعطيل هذا الحساب");

        // Ensure the PatientAccount has a LinkedUserId for messaging integration
        await linkingService.EnsureLinkedUserAsync(account.PatientId);

        account.IsVerified = true;
        account.LastLogin = DateTime.UtcNow;
        account.VerificationCode = null;
        account.VerificationCodeExpiry = null;

        var accessToken = GeneratePatientToken(account);
        // SEC-09: generate plaintext token for the client, persist only the SHA-256 hash.
        var refreshToken = GenerateRefreshToken();
        account.RefreshTokenHash = HashToken(refreshToken);
        account.RefreshTokenExpiry = DateTime.UtcNow.AddDays(30);

        await db.SaveChangesAsync();

        return (new PatientAuthResponse
        {
            AccessToken = accessToken,
            Profile = MapProfile(account.Patient, account),
            MustChangePassword = account.MustChangePassword,
            RefreshToken = refreshToken
        }, null);
    }

    public async Task<(bool success, string? error)> ForgotPasswordAsync(string phoneNumber)
    {
        var normalizedPhone = NormalizePhone(phoneNumber);
        var phoneVariants = GetPhoneVariants(phoneNumber);
        var patient = await db.Patients.FirstOrDefaultAsync(p =>
            phoneVariants.Contains(p.Phone ?? "") || phoneVariants.Contains(p.WhatsApp ?? "") ||
            phoneVariants.Contains(p.NormalizedPhone ?? "") || phoneVariants.Contains(p.NormalizedWhatsApp ?? ""));
        if (patient == null)
            return (false, "رقم الهاتف غير مسجل في النظام");

        var account = await db.PatientAccounts.FirstOrDefaultAsync(a => a.PatientId == patient.Id);
        if (account == null)
            return (false, "لا يوجد حساب بوابة لهذا المريض. تواصل مع العيادة.");

        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        account.VerificationCode = code;
        account.VerificationCodeExpiry = DateTime.UtcNow.AddMinutes(10);

        await db.SaveChangesAsync();

        // Send the code via WhatsApp
        var whatsappPhone = patient.WhatsApp ?? patient.Phone;
        if (!string.IsNullOrEmpty(whatsappPhone))
        {
            await SendOtpViaWhatsAppAsync(NormalizePhone(whatsappPhone), code, "رمز إعادة تعيين كلمة المرور");
        }

        return (true, null);
    }

    public async Task<(PatientAuthResponse? response, string? error)> ResetPasswordAsync(string phoneNumber, string code, string newPassword)
    {
        var normalizedPhone = NormalizePhone(phoneNumber);
        var phoneVariants = GetPhoneVariants(phoneNumber);
        var account = await db.PatientAccounts
            .Include(a => a.Patient)
                .ThenInclude(p => p!.PrimaryDoctor)
            .FirstOrDefaultAsync(a => a.PatientId != Guid.Empty && // ensure join
                (phoneVariants.Contains(a.PhoneNumber ?? "") ||
                 (a.Patient != null && phoneVariants.Contains(a.Patient.Phone ?? "")) ||
                 (a.Patient != null && phoneVariants.Contains(a.Patient.WhatsApp ?? ""))));

        if (account == null)
            return (null, "الحساب غير موجود");

        if (account.Patient == null)
            return (null, "تم تعطيل هذا الحساب");

        // C-04 FIX: Constant-time OTP comparison
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(account.VerificationCode ?? ""),
            Encoding.UTF8.GetBytes(code ?? "")))
            return (null, "رمز التحقق غير صحيح");

        if (account.VerificationCodeExpiry < DateTime.UtcNow)
            return (null, "انتهت صلاحية رمز التحقق");

        // SEC-11: enforce centralized password complexity policy.
        var (valid, policyError) = PasswordPolicy.Validate(newPassword);
        if (!valid)
            return (null, policyError);

        // Update password
        var salt = AuthService.GenerateSalt();
        var hash = AuthService.HashPassword(newPassword, salt);
        account.PasswordSalt = salt;
        account.PasswordHash = hash;
        account.MustChangePassword = false; // Password was just set by the user
        account.VerificationCode = null;
        account.VerificationCodeExpiry = null;
        account.IsVerified = true;
        account.LastLogin = DateTime.UtcNow;

        var accessToken = GeneratePatientToken(account);
        // SEC-09: persist SHA-256 hash; return plaintext to client once.
        var refreshToken = GenerateRefreshToken();
        account.RefreshTokenHash = HashToken(refreshToken);
        account.RefreshTokenExpiry = DateTime.UtcNow.AddDays(30);

        await db.SaveChangesAsync();

        return (new PatientAuthResponse
        {
            AccessToken = accessToken,
            Profile = MapProfile(account.Patient, account),
            MustChangePassword = false,
            RefreshToken = refreshToken
        }, null);
    }

    public async Task<(string username, string plainPassword)> EnsurePatientAccountAsync(Guid patientId, string patientNumber, string? phone)
    {
        var account = await db.PatientAccounts
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.PatientId == patientId);
        if (account != null)
        {
            // Account already exists - do NOT return existing password
            // But ensure the LinkedUserId is set for messaging system integration
            await linkingService.EnsureLinkedUserAsync(account.PatientId);
            await db.SaveChangesAsync();
            return (account.Username ?? patientNumber, "");
        }

        // Generate a random 8-character password (secure, no plain-text storage)
        var plainPassword = GenerateTemporaryPassword();
        var salt = AuthService.GenerateSalt();
        var hash = AuthService.HashPassword(plainPassword, salt);

        account = new PatientAccount
        {
            PatientId = patientId,
            PhoneNumber = phone ?? "",
            Username = patientNumber,
            PasswordHash = hash,
            PasswordSalt = salt,
            MustChangePassword = true,
            PortalAccountActive = true,
            IsVerified = true,
            IsActive = true
        };

        db.PatientAccounts.Add(account);
        // CORE-PAT-013: save FIRST. EnsureLinkedUserAsync queries the database
        // for the PatientAccount; called before SaveChanges it could not see the
        // just-added row, silently bailed, and LinkedUserId stayed null — so the
        // email typed at registration was thrown away (SetPatientEmailAsync
        // no-ops without a linked user) and email reminders never fired.
        await db.SaveChangesAsync();

        // Ensure LinkedUserId for messaging — use the unified linking service
        await linkingService.EnsureLinkedUserAsync(account.PatientId);

        logger.LogInformation("Created PatientAccount for patient {PatientId} with username {Username}", patientId, patientNumber);

        return (patientNumber, plainPassword);
    }

    public async Task<PatientPortalCredentialsDto?> GetPatientCredentialsAsync(Guid patientId)
    {
        var account = await db.PatientAccounts.FirstOrDefaultAsync(a => a.PatientId == patientId);
        if (account == null) return new PatientPortalCredentialsDto { HasPortalAccount = false };

        return new PatientPortalCredentialsDto
        {
            Username = account.Username ?? "",
            AccountActive = account.PortalAccountActive,
            MustChangePassword = account.MustChangePassword,
            LastLogin = account.LastLogin,
            HasPortalAccount = true
        };
    }

    public async Task<(PatientPasswordResetResponseDto? result, string? error)> StaffResetPasswordAsync(Guid patientId)
    {
        var account = await db.PatientAccounts.FirstOrDefaultAsync(a => a.PatientId == patientId);
        if (account == null)
            return (null, "لا يوجد حساب بوابة لهذا المريض");

        // Generate new temporary password
        var tempPassword = GenerateTemporaryPassword();
        var salt = AuthService.GenerateSalt();
        var hash = AuthService.HashPassword(tempPassword, salt);

        account.PasswordHash = hash;
        account.PasswordSalt = salt;
        account.MustChangePassword = true;
        account.PortalAccountActive = true;

        await db.SaveChangesAsync();

        return (new PatientPasswordResetResponseDto
        {
            TemporaryPassword = tempPassword,
            Username = account.Username ?? "",
            Message = "تم إعادة تعيين كلمة المرور بنجاح. اعرض الكلمة للمريض الآن، لن تظهر مرة أخرى."
        }, null);
    }

    public async Task<(PatientAuthResponse? response, string? error)> ChangePasswordAsync(Guid patientId, string currentPassword, string newPassword)
    {
        var account = await db.PatientAccounts
            .Include(a => a.Patient)
                .ThenInclude(p => p!.PrimaryDoctor)
            .FirstOrDefaultAsync(a => a.PatientId == patientId);

        if (account == null)
            return (null, "الحساب غير موجود");

        if (account.Patient == null)
            return (null, "تم تعطيل هذا الحساب");

        if (string.IsNullOrEmpty(account.PasswordHash) || string.IsNullOrEmpty(account.PasswordSalt))
            return (null, "حسابك غير مفعّل بعد");

        // C-04 FIX: Verify current password with constant-time comparison
        var hash = AuthService.HashPassword(currentPassword, account.PasswordSalt);
        if (!CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(hash),
            Convert.FromBase64String(account.PasswordHash)))
            return (null, "كلمة المرور الحالية غير صحيحة");

        // SEC-11: enforce centralized password complexity policy.
        var (valid, policyError) = PasswordPolicy.Validate(newPassword);
        if (!valid)
            return (null, policyError);

        // Update password
        var salt = AuthService.GenerateSalt();
        var newHash = AuthService.HashPassword(newPassword, salt);
        account.PasswordSalt = salt;
        account.PasswordHash = newHash;
        account.MustChangePassword = false;

        var accessToken = GeneratePatientToken(account);
        // SEC-09: persist SHA-256 hash; return plaintext to client once.
        var refreshToken = GenerateRefreshToken();
        account.RefreshTokenHash = HashToken(refreshToken);
        account.RefreshTokenExpiry = DateTime.UtcNow.AddDays(30);

        await db.SaveChangesAsync();

        return (new PatientAuthResponse
        {
            AccessToken = accessToken,
            Profile = MapProfile(account.Patient, account),
            MustChangePassword = false,
            RefreshToken = refreshToken
        }, null);
    }

    public async Task<(PatientAuthResponse? response, string? error)> RefreshTokenAsync(Guid patientId, string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return (null, "توكن غير صالح");

        // SEC-09: look up the account by the SHA-256 hash of the received token.
        // A DB-compromise attacker only has the hash and cannot derive a usable token.
        var hash = HashToken(refreshToken);
        var account = await db.PatientAccounts
            .Include(a => a.Patient)
                .ThenInclude(p => p!.PrimaryDoctor)
            .FirstOrDefaultAsync(a => a.PatientId == patientId && a.RefreshTokenHash == hash);

        if (account == null)
            return (null, "جلسة منتهية، يرجى تسجيل الدخول مجددًا");

        if (account.Patient == null)
            return (null, "تم تعطيل هذا الحساب");

        if (account.RefreshTokenExpiry == null || account.RefreshTokenExpiry < DateTime.UtcNow)
            return (null, "جلسة منتهية، يرجى تسجيل الدخول مجددًا");

        // Defense in depth: constant-time comparison of the computed hash against
        // the stored hash. The DB WHERE clause already matched, but this guards
        // against any future code path that loosens the lookup predicate.
        if (!FixedTimeEqualsHash(account.RefreshTokenHash, hash))
            return (null, "توكن غير صالح");

        // SEC-09: rotation — issue a fresh refresh token on every refresh so a
        // stolen-then-replaced token cannot be replayed. The old hash is overwritten.
        var accessToken = GeneratePatientToken(account);
        var newRefreshToken = GenerateRefreshToken();
        account.RefreshTokenHash = HashToken(newRefreshToken);
        account.RefreshTokenExpiry = DateTime.UtcNow.AddDays(30);

        await db.SaveChangesAsync();

        return (new PatientAuthResponse
        {
            AccessToken = accessToken,
            Profile = MapProfile(account.Patient, account),
            MustChangePassword = account.MustChangePassword,
            RefreshToken = newRefreshToken
        }, null);
    }

    /// <summary>
    /// SEC-09: clears the persisted refresh-token hash so any stolen-then-logged-out
    /// token can no longer be used to refresh. The client is responsible for
    /// discarding its local copy; the backend can only invalidate its own half.
    /// </summary>
    public async Task LogoutAsync(Guid patientId)
    {
        var account = await db.PatientAccounts.FirstOrDefaultAsync(a => a.PatientId == patientId);
        if (account == null) return;

        account.RefreshTokenHash = null;
        account.RefreshTokenExpiry = null;
        await db.SaveChangesAsync();
    }

    public async Task<PatientPortalDashboardDto> GetDashboardAsync(Guid patientId)
    {
        var patient = await db.Patients
            .Include(p => p.PrimaryDoctor)
            .FirstOrDefaultAsync(p => p.Id == patientId);
        if (patient == null) throw new InvalidOperationException("المريض غير موجود");

        var account = await db.PatientAccounts.FirstOrDefaultAsync(a => a.PatientId == patientId);

        var now = ClinicTimeProvider.ClinicToday();
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

        var contracts = await db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.PatientId == patientId)
            .ToListAsync();

        var activeContractsList = contracts.Where(c => c.Status == ContractStatus.Active).ToList();

        // CORE-PAT-012: the portal used to run its OWN balance math — active
        // contracts only, invoices completely ignored, p.Amount instead of the
        // multi-currency-safe AppliedAmount. A patient billed by invoice saw
        // "المتبقي: 0" while reception saw the real debt, and stopped paying.
        // The canonical clinic-side calculation is the single source of truth.
        var finance = await financeReadService.GetPatientFinanceSummaryAsync(patientId);
        var totalPaid = finance.TotalPaid;
        var totalOutstanding = finance.OutstandingBalance;
        var totalAmount = finance.TotalTreatmentCost;
        var activeContracts = activeContractsList.Count;

        // FIX: Only show active payments in portal (includes payments from
        // ALL contracts + orphan payments for full payment history)
        var recentPayments = await db.Payments
            .Include(p => p.Receipt)
            .Where(p => p.PatientId == patientId && p.IsActive)
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
            ClinicInfo = await ResolveClinicInfoAsync()
        };
    }

    public async Task<PatientPortalProfileDto> GetProfileAsync(Guid patientId)
    {
        var patient = await db.Patients
            .Include(p => p.PrimaryDoctor)
            .FirstOrDefaultAsync(p => p.Id == patientId);
        if (patient == null) throw new InvalidOperationException("المريض غير موجود");

        var account = await db.PatientAccounts.FirstOrDefaultAsync(a => a.PatientId == patientId);
        var linkedUserEmail = await GetLinkedUserEmailAsync(account);
        return MapProfile(patient, account, linkedUserEmail);
    }

    public async Task<(PatientPortalProfileDto? result, string? error)> UpdateProfileAsync(Guid patientId, PatientProfileUpdateDto req)
    {
        var patient = await db.Patients.FindAsync(patientId);
        if (patient == null) return (null, "المريض غير موجود");

        // CORE-PAT-009: the portal used to write NormalizedPhone as "+967…" while
        // every staff path writes PhoneNormalizer's "967…". The unique index,
        // check-duplicate and phone search all compare with equality, so a
        // portal-edited number silently escaped duplicate detection and a second
        // patient file got created for the same person. The portal now uses the
        // same canonical normalizer and the same duplicate guard as staff edits.
        if (req.Phone != null)
        {
            var normalizedPhone = PhoneNormalizer.Normalize(req.Phone);
            if (normalizedPhone != null)
            {
                var existingPhone = await db.Patients.FirstOrDefaultAsync(p =>
                    p.Id != patientId &&
                    (p.NormalizedPhone == normalizedPhone || p.NormalizedWhatsApp == normalizedPhone));
                if (existingPhone != null)
                    return (null, "رقم الهاتف مستخدم مسبقاً لملف آخر — يرجى مراجعة الاستقبال");
            }
            patient.Phone = req.Phone.Trim();
            patient.NormalizedPhone = normalizedPhone;
        }
        if (req.WhatsApp != null)
        {
            var normalizedWhatsApp = PhoneNormalizer.Normalize(req.WhatsApp);
            if (normalizedWhatsApp != null)
            {
                var existingWA = await db.Patients.FirstOrDefaultAsync(p =>
                    p.Id != patientId &&
                    (p.NormalizedWhatsApp == normalizedWhatsApp || p.NormalizedPhone == normalizedWhatsApp));
                if (existingWA != null)
                    return (null, "رقم الواتساب مستخدم مسبقاً لملف آخر — يرجى مراجعة الاستقبال");
            }
            patient.WhatsApp = req.WhatsApp.Trim();
            patient.NormalizedWhatsApp = normalizedWhatsApp;
        }
        if (req.Address != null)
        {
            patient.Address = req.Address;
        }

        // Set email on the linked User record
        if (req.Email != null)
        {
            var account = await db.PatientAccounts.FirstOrDefaultAsync(a => a.PatientId == patientId);
            if (account?.LinkedUserId != null)
            {
                var linkedUser = await db.Users.FindAsync(account.LinkedUserId.Value);
                if (linkedUser != null)
                {
                    linkedUser.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
                }
            }
        }

        await db.SaveChangesAsync();

        var acct = await db.PatientAccounts.FirstOrDefaultAsync(a => a.PatientId == patientId);
        var linkedUserEmail = await GetLinkedUserEmailAsync(acct);
        return (MapProfile(patient, acct, linkedUserEmail), null);
    }

    public async Task<List<PatientAppointmentDto>> GetAppointmentsAsync(Guid patientId, int limit = 20)
    {
        limit = Math.Max(1, Math.Min(limit, 100));
        var now = ClinicTimeProvider.ClinicToday();
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
        if (date < ClinicTimeProvider.ClinicToday())
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
        if (appointment.AppointmentDate < ClinicTimeProvider.ClinicToday())
            return (false, "لا يمكن إلغاء موعد سابق");

        appointment.Status = AppointmentStatus.Cancelled;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<List<PatientTreatmentDto>> GetTreatmentsAsync(Guid patientId, int limit = 20)
    {
        limit = Math.Max(1, Math.Min(limit, 100));
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
        limit = Math.Max(1, Math.Min(limit, 100));
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
        limit = Math.Max(1, Math.Min(limit, 100));
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
        var contracts = await db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.PatientId == patientId)
            .ToListAsync();

        var activeContracts = contracts.Where(c => c.Status == ContractStatus.Active).ToList();

        // CORE-PAT-012: the portal used to run its OWN balance math — active
        // contracts only, invoices completely ignored, p.Amount instead of the
        // multi-currency-safe AppliedAmount. A patient billed by invoice saw
        // "المتبقي: 0" while reception saw the real debt, and stopped paying.
        // The canonical clinic-side calculation is the single source of truth.
        var finance = await financeReadService.GetPatientFinanceSummaryAsync(patientId);
        var totalPaid = finance.TotalPaid;
        var totalOutstanding = finance.OutstandingBalance;
        var totalAmount = finance.TotalTreatmentCost;

        var contractDtos = activeContracts.Select(c => new PatientContractDto
        {
            Id = c.Id,
            Specialty = c.Specialty,
            TotalAmount = c.TotalAmount - c.DiscountAmount,
            // FIX: Per-contract paid only from linked active payments
            PaidAmount = c.Payments.Where(p => p.IsActive).Sum(p => p.Amount),
            RemainingAmount = Math.Max(0, c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.Amount)),
            Status = c.Status.ToString(),
            StartDate = c.StartDate.HasValue ? c.StartDate.Value.ToString("yyyy-MM-dd") : null
        }).ToList();

        // FIX: Only show active payments in portal
        var recentPayments = await db.Payments
            .Include(p => p.Receipt)
            .Where(p => p.PatientId == patientId && p.IsActive)
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
        return ResolveClinicInfoAsync();
    }

    public async Task<Guid?> GetPatientIdByPhoneAsync(string phoneNumber)
    {
        var normalizedPhone = NormalizePhone(phoneNumber);
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Phone == normalizedPhone || p.WhatsApp == normalizedPhone);
        return patient?.Id;
    }

    public async Task<PatientAccountInfoDto?> GetPatientInfoForAccountCreationAsync(Guid patientId)
    {
        var patient = await db.Patients.FindAsync(patientId);
        if (patient == null) return null;

        return new PatientAccountInfoDto
        {
            PatientNumber = patient.PatientNumber,
            Phone = patient.Phone
        };
    }

    public async Task SetPatientEmailAsync(Guid patientId, string? email)
    {
        var account = await db.PatientAccounts.FirstOrDefaultAsync(a => a.PatientId == patientId);
        if (account == null) return;
        if (account.LinkedUserId == null)
        {
            // CORE-PAT-013: repair accounts created before the ordering fix
            // above instead of silently discarding the doctor's input. The
            // linking service shares this scoped DbContext, so the account
            // entity reflects the new link immediately.
            account.LinkedUserId = await linkingService.EnsureLinkedUserAsync(patientId);
        }
        if (account.LinkedUserId == null) return;
        var linkedUser = await db.Users.FindAsync(account.LinkedUserId.Value);
        if (linkedUser != null)
        {
            linkedUser.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
            await db.SaveChangesAsync();
        }
    }

    public async Task<string?> GetPatientEmailAsync(Guid patientId)
    {
        var account = await db.PatientAccounts.FirstOrDefaultAsync(a => a.PatientId == patientId);
        return await GetLinkedUserEmailAsync(account);
    }

    // ── Private Helpers ──────────────────────────────────────────────────────

    private static PatientAppointmentDto MapAppointment(Appointment a, DateOnly? now = null)
    {
        var today = now ?? ClinicTimeProvider.ClinicToday();
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

    // MS-TASK-006: clinic info shown in the patient portal comes from the same
    // website.* Settings keys the public site uses — no hardcoding (the old
    // static version even carried a wrong placeholder phone number).
    private async Task<PatientClinicInfoDto> ResolveClinicInfoAsync()
    {
        var keys = new[]
        {
            "website.clinicName", "website.phone", "website.whatsapp",
            "website.address", "website.workingHours",
        };
        var settings = await db.Settings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value);
        string Get(string key, string fallback) =>
            settings.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;

        return new PatientClinicInfoDto
        {
            ClinicName = Get("website.clinicName", FinanceClinicIdentity.DefaultName),
            Phone = Get("website.phone", "04-253028"),
            WhatsApp = Get("website.whatsapp", "967770245745"),
            Address = Get("website.address", FinanceClinicIdentity.DefaultLocation),
            WorkingHours = Get("website.workingHours", "السبت – الخميس: 8 ص – 8 م")
        };
    }

    // CORE-PAT-009: gateway-dialing format ONLY (E.164 with "+"). Never store
    // this — stored numbers use PhoneNormalizer.Normalize ("967…"), the same
    // canonical form every staff path writes.
    private static string NormalizePhone(string phone)
    {
        var canonical = PhoneNormalizer.Normalize(phone);
        return canonical is null ? phone : "+" + canonical;
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

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.PatientId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("patientId", account.PatientId.ToString()),
            new(ClaimTypes.Role, "Patient"),
            new("portal", "true"),
            new("mustChangePassword", account.MustChangePassword.ToString().ToLowerInvariant())
        };

        // Include linked user ID for messaging system integration
        if (account.LinkedUserId.HasValue)
        {
            claims.Add(new Claim("userId", account.LinkedUserId.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            // C-05 FIX: Reduced patient token expiry from 7 days to 1 day
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        // SEC-09: 64 random bytes (512 bits) of entropy from the cryptographic RNG,
        // base64url-safe encoded. Well above the 32-byte minimum in the audit spec.
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// SEC-09: SHA-256 hash of the refresh token, returned as lowercase hex.
    /// Used for storage and lookup — the plaintext token is NEVER persisted.
    /// </summary>
    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// SEC-09: constant-time comparison of two hex-encoded SHA-256 hashes.
    /// Prevents timing attacks on the hash comparison step.
    /// </summary>
    private static bool FixedTimeEqualsHash(string? stored, string computed)
    {
        if (string.IsNullOrEmpty(stored)) return false;
        var a = Encoding.UTF8.GetBytes(stored);
        var b = Encoding.UTF8.GetBytes(computed);
        if (a.Length != b.Length) return false;
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    // SEC-05 FIX: Generate a secure temporary password (10 chars with special characters)
    // Matches the strength pattern used in EmployeesController.GenerateSecureTemporaryPassword
    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghjkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%";
        const string all = upper + lower + digits + special;

        var bytes = RandomNumberGenerator.GetBytes(10);
        var chars = new char[10];

        // Ensure at least one of each category
        chars[0] = upper[bytes[0] % upper.Length];
        chars[1] = lower[bytes[1] % lower.Length];
        chars[2] = digits[bytes[2] % digits.Length];
        chars[3] = special[bytes[3] % special.Length];

        for (var i = 4; i < 10; i++)
            chars[i] = all[bytes[i] % all.Length];

        // Fisher-Yates shuffle
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = bytes[i] % (i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    private static PatientPortalProfileDto MapProfile(Patient patient, PatientAccount? account = null, string? email = null) => new()
    {
        Id = patient.Id,
        PatientNumber = patient.PatientNumber,
        FullName = $"{patient.FirstName} {patient.MiddleName} {patient.LastName}".Replace("  ", " ").Trim(),
        Phone = patient.Phone,
        Email = email,
        WhatsApp = patient.WhatsApp,
        Gender = patient.Gender?.ToString(),
        Age = patient.DateOfBirth.HasValue ? CalculateAge(patient.DateOfBirth.Value) : null,
        DateOfBirth = patient.DateOfBirth.HasValue ? patient.DateOfBirth.Value.ToString("yyyy-MM-dd") : null,
        Address = patient.Address,
        PrimaryDoctorName = patient.PrimaryDoctor?.Name,
        AccountStatus = account?.IsVerified == true ? "active" : account != null ? "pending" : "none",
        LastLogin = account?.LastLogin.HasValue == true ? account.LastLogin!.Value.ToString("yyyy-MM-dd HH:mm") : null,
        Username = account?.Username
    };

    private async Task SendOtpViaWhatsAppAsync(string phone, string code, string? purpose = null)
    {
        try
        {
            var apiUrl = config["WhatsApp:ApiUrl"];
            var apiToken = config["WhatsApp:ApiToken"];

            if (string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(apiToken))
            {
                // Demo mode: log the code instead of sending
                logger.LogInformation("WhatsApp OTP (demo mode): Code {Code} for phone {Phone} - {Purpose}", code, phone, purpose ?? "verification");
                return;
            }

            var client = httpClientFactory.CreateClient("WhatsApp");
            var clinicName = (await ResolveClinicInfoAsync()).ClinicName;
            var messageBody = purpose != null
                ? $"{purpose} الخاص بك في {clinicName} هو: {code}\nهذا الرمز صالح لمدة 10 دقائق."
                : $"رمز التحقق الخاص بك في {clinicName} هو: {code}\nهذا الرمز صالح لمدة 10 دقائق.";

            var payload = new
            {
                messaging_product = "whatsapp",
                to = phone,
                type = "text",
                text = new
                {
                    body = messageBody
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/messages")
            {
                Headers = { { "Authorization", $"Bearer {apiToken}" } },
                Content = JsonContent.Create(payload)
            };

            var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("WhatsApp OTP sent successfully to {Phone}", phone);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync();
                logger.LogWarning("WhatsApp OTP failed for {Phone}: {Status} - {Body}", phone, response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send WhatsApp OTP to {Phone}", phone);
        }
    }

    /// <summary>
    /// Gets the email from the linked User account for a patient, if available.
    /// </summary>
    private async Task<string?> GetLinkedUserEmailAsync(PatientAccount? account)
    {
        if (account?.LinkedUserId == null) return null;
        var linkedUser = await db.Users.FindAsync(account.LinkedUserId.Value);
        return linkedUser?.Email;
    }

    // CORE-PAT-003: one shared implementation (Domain/Common/PatientAge), on the
    // clinic day rather than the server's local day.
    private static int CalculateAge(DateOnly dob) =>
        PatientAge.Calculate(dob, ClinicTimeProvider.ClinicToday()) ?? 0;
}
