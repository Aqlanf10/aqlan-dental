using AqlanDentalPro.Application.DTOs.PatientPortal;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
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

public class PatientPortalService(
    AppDbContext db,
    IConfiguration config,
    IWhatsAppService whatsappService) : IPatientPortalService
{
    // ── Auth: Username/Password Login ──────────────────────────────────────

    public async Task<(PatientAuthResponse? response, string? error)> LoginAsync(string username, string password)
    {
        var account = await db.PatientAccounts
            .Include(a => a.Patient)
                .ThenInclude(p => p!.PrimaryDoctor)
            .FirstOrDefaultAsync(a => a.Username == username);

        if (account == null)
            return (null, "اسم المستخدم أو كلمة المرور غير صحيحة");

        if (!account.PortalAccountActive)
            return (null, "حسابك معطّل، تواصل مع العيادة");

        // Verify password using Argon2id
        if (!VerifyPassword(password, account.PasswordHash, account.PasswordSalt))
            return (null, "اسم المستخدم أو كلمة المرور غير صحيحة");

        account.IsVerified = true;
        account.LastLogin = DateTime.UtcNow;
        account.VerificationCode = null;
        account.VerificationCodeExpiry = null;

        // Generate JWT
        var accessToken = GeneratePatientToken(account);
        var refreshToken = GenerateRefreshToken();
        account.RefreshToken = refreshToken;
        account.RefreshTokenExpiry = DateTime.UtcNow.AddDays(30);

        await db.SaveChangesAsync();

        return (new PatientAuthResponse
        {
            AccessToken = accessToken,
            Profile = MapProfile(account.Patient),
            MustChangePassword = account.MustChangePassword
        }, null);
    }

    // ── Request Credentials via WhatsApp ──────────────────────────────────

    public async Task<(bool success, string? error)> RequestCredentialsViaWhatsAppAsync(string phoneNumber)
    {
        var normalizedPhone = NormalizePhone(phoneNumber);

        // Find patient with this phone number
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Phone == normalizedPhone || p.WhatsApp == normalizedPhone);
        if (patient == null)
            return (false, "رقم الهاتف غير مسجل في النظام");

        // Ensure patient has a portal account
        var (creationResult, creationError) = await EnsurePortalAccountAsync(patient.Id);
        if (creationResult == null)
            return (false, creationError ?? "فشل في إنشاء حساب البوابة");

        // Reset password to generate a new temporary one
        var (resetResult, resetError) = await ResetPasswordAsync(patient.Id);
        if (resetResult == null)
            return (false, resetError ?? "فشل في إعادة تعيين كلمة المرور");

        // Send credentials via WhatsApp
        try
        {
            await whatsappService.SendMessageAsync(new Application.DTOs.WhatsApp.SendMessageRequest
            {
                PatientId = patient.Id,
                TemplateType = "portal_credentials",
                CustomMessage = $"مرحباً {patient.FirstName}\n\nبيانات الدخول لبوابة المريض:\nاسم المستخدم: {resetResult.Username}\nكلمة المرور: {resetResult.TemporaryPassword}\n\nرابط البوابة: https://portal.aqlandental.com\n\nيُنصح بتغيير كلمة المرور بعد الدخول"
            });
        }
        catch
        {
            // WhatsApp sending failed, but account is created
            return (true, null);
        }

        return (true, null);
    }

    // ── Portal Account Management ─────────────────────────────────────────

    public async Task<(PatientPortalAccountInfoDto? info, string? error)> GetPortalAccountInfoAsync(Guid patientId)
    {
        var patient = await db.Patients.FindAsync(patientId);
        if (patient == null)
            return (null, "المريض غير موجود");

        var account = await db.PatientAccounts.FirstOrDefaultAsync(a => a.PatientId == patientId);

        return (new PatientPortalAccountInfoDto
        {
            PatientId = patientId,
            Username = account?.Username ?? patient.PatientNumber,
            AccountActive = account?.PortalAccountActive ?? false,
            MustChangePassword = account?.MustChangePassword ?? true,
            LastLogin = account?.LastLogin,
            HasPortalAccount = account != null
        }, null);
    }

    public async Task<(PatientPasswordResetResponseDto? result, string? error)> ResetPasswordAsync(Guid patientId)
    {
        var account = await db.PatientAccounts.FirstOrDefaultAsync(a => a.PatientId == patientId);
        if (account == null)
        {
            // Create account if it doesn't exist
            var (creationResult, creationError) = await EnsurePortalAccountAsync(patientId);
            if (creationResult == null)
                return (null, creationError ?? "فشل في إنشاء حساب البوابة");
            account = await db.PatientAccounts.FirstAsync(a => a.PatientId == patientId);
        }

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
            Username = account.Username,
            Message = "تم إعادة تعيين كلمة المرور بنجاح. اعرض الكلمة للمريض الآن، لن تظهر مرة أخرى."
        }, null);
    }

    public async Task<(PatientAccountCreationResult? result, string? error)> EnsurePortalAccountAsync(Guid patientId)
    {
        var patient = await db.Patients.FindAsync(patientId);
        if (patient == null)
            return (null, "المريض غير موجود");

        var existingAccount = await db.PatientAccounts.FirstOrDefaultAsync(a => a.PatientId == patientId);
        if (existingAccount != null)
            return (new PatientAccountCreationResult
            {
                Username = existingAccount.Username,
                TemporaryPassword = "" // Already has account, no password to show
            }, null);

        // Create new portal account
        var username = patient.PatientNumber;
        var tempPassword = GenerateTemporaryPassword();
        var salt = AuthService.GenerateSalt();
        var hash = AuthService.HashPassword(tempPassword, salt);

        var account = new PatientAccount
        {
            PatientId = patientId,
            PhoneNumber = patient.Phone ?? patient.WhatsApp ?? "",
            Username = username,
            PasswordHash = hash,
            PasswordSalt = salt,
            MustChangePassword = true,
            PortalAccountActive = true,
            IsVerified = false,
            IsActive = true
        };

        db.PatientAccounts.Add(account);

        // Also create a User record for messaging system integration
        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (existingUser == null)
        {
            var linkedUser = new User
            {
                Username = username,
                PasswordHash = hash,
                PasswordSalt = salt,
                Role = UserRole.Patient,
                Phone = patient.Phone,
                BranchId = patient.BranchId,
                IsActive = true
            };
            db.Users.Add(linkedUser);
            await db.SaveChangesAsync();
            account.LinkedUserId = linkedUser.Id;
        }
        else
        {
            account.LinkedUserId = existingUser.Id;
        }

        await db.SaveChangesAsync();

        return (new PatientAccountCreationResult
        {
            Username = username,
            TemporaryPassword = tempPassword
        }, null);
    }

    // ── Dashboard & Data ──────────────────────────────────────────────────

    public async Task<PatientPortalDashboardDto> GetDashboardAsync(Guid patientId)
    {
        var patient = await db.Patients
            .Include(p => p.PrimaryDoctor)
            .FirstOrDefaultAsync(p => p.Id == patientId);

        if (patient == null) throw new InvalidOperationException("المريض غير موجود");

        var now = DateOnly.FromDateTime(DateTime.Today);
        var nextAppt = await db.Appointments
            .Include(a => a.Doctor)
            .Where(a => a.PatientId == patientId && a.AppointmentDate >= now && a.Status == AppointmentStatus.Scheduled)
            .OrderBy(a => a.AppointmentDate).ThenBy(a => a.StartTime)
            .FirstOrDefaultAsync();

        var totalAppts = await db.Appointments.CountAsync(a => a.PatientId == patientId);
        var completedTreatments = await db.GeneralTreatments.CountAsync(t => t.PatientId == patientId);

        var totalPaid = await db.Payments.Where(p => p.PatientId == patientId).SumAsync(p => (decimal?)p.Amount) ?? 0;
        var totalOutstanding = await db.Contracts
            .Where(c => c.PatientId == patientId && c.Status == "active")
            .Include(c => c.Payments)
            .Select(c => c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount))
            .SumAsync(r => (decimal?)r) ?? 0;
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
                ReceiptNumber = p.Receipt != null ? p.Receipt.ReceiptNumber : null,
                CreatedAt = p.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return new PatientPortalDashboardDto
        {
            Profile = MapProfile(patient),
            NextAppointment = nextAppt != null ? new PatientAppointmentDto
            {
                Id = nextAppt.Id,
                AppointmentDate = nextAppt.AppointmentDate.ToString("yyyy-MM-dd"),
                StartTime = nextAppt.StartTime.ToString("HH:mm"),
                EndTime = nextAppt.EndTime.ToString("HH:mm"),
                AppointmentType = nextAppt.AppointmentType,
                DoctorName = nextAppt.Doctor.Name,
                Status = nextAppt.Status.ToString(),
                Notes = nextAppt.Notes
            } : null,
            TotalAppointments = totalAppts,
            CompletedTreatments = completedTreatments,
            Finance = new PatientFinancialSummaryDto
            {
                TotalPaid = totalPaid,
                TotalOutstanding = totalOutstanding,
                ActiveContracts = activeContracts,
                RecentPayments = recentPayments
            }
        };
    }

    public async Task<List<PatientAppointmentDto>> GetAppointmentsAsync(Guid patientId, int limit = 20)
    {
        return await db.Appointments
            .Include(a => a.Doctor)
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.AppointmentDate).ThenByDescending(a => a.StartTime)
            .Take(limit)
            .Select(a => new PatientAppointmentDto
            {
                Id = a.Id,
                AppointmentDate = a.AppointmentDate.ToString("yyyy-MM-dd"),
                StartTime = a.StartTime.ToString("HH:mm"),
                EndTime = a.EndTime.ToString("HH:mm"),
                AppointmentType = a.AppointmentType,
                DoctorName = a.Doctor.Name,
                Status = a.Status.ToString(),
                Notes = a.Notes
            })
            .ToListAsync();
    }

    public async Task<(PatientAppointmentDto? result, string? error)> RequestAppointmentAsync(Guid patientId, PatientAppointmentRequestDto req)
    {
        var patient = await db.Patients.FindAsync(patientId);
        if (patient == null) return (null, "المريض غير موجود");

        var date = DateOnly.Parse(req.AppointmentDate);
        var start = TimeOnly.Parse(req.StartTime);
        var end = start.AddMinutes(30);

        // Find doctor - use primary doctor or specified one
        var doctorId = req.DoctorId ?? patient.PrimaryDoctorId;
        if (doctorId == null) return (null, "لم يتم تحديد الطبيب");

        var doctor = await db.Doctors.FindAsync(doctorId.Value);
        if (doctor == null) return (null, "الطبيب غير موجود");

        // Check for conflicts
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

        return (new PatientAppointmentDto
        {
            Id = appointment.Id,
            AppointmentDate = appointment.AppointmentDate.ToString("yyyy-MM-dd"),
            StartTime = appointment.StartTime.ToString("HH:mm"),
            EndTime = appointment.EndTime.ToString("HH:mm"),
            AppointmentType = appointment.AppointmentType,
            DoctorName = appointment.Doctor.Name,
            Status = appointment.Status.ToString(),
            Notes = appointment.Notes
        }, null);
    }

    public async Task<(bool success, string? error)> CancelAppointmentAsync(Guid patientId, Guid appointmentId)
    {
        var appointment = await db.Appointments.FindAsync(appointmentId);
        if (appointment == null) return (false, "الموعد غير موجود");
        if (appointment.PatientId != patientId) return (false, "غير مصرح بهذا الإجراء");

        appointment.Status = AppointmentStatus.Cancelled;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<List<PatientTreatmentDto>> GetTreatmentsAsync(Guid patientId, int limit = 20)
    {
        return await db.GeneralTreatments
            .Include(t => t.Doctor)
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
                CreatedAt = t.CreatedAt.ToString("yyyy-MM-dd"),
                Notes = t.Notes
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

        return prescriptions.Select(p => new PatientPrescriptionDto
        {
            Id = p.Id,
            MedicationName = ExtractFirstDrugName(p.Drugs),
            Dosage = null,
            Frequency = null,
            Duration = null,
            Instructions = p.Notes,
            DoctorName = p.Doctor?.Name ?? "",
            CreatedAt = p.CreatedAt.ToString("yyyy-MM-dd")
        }).ToList();
    }

    public async Task<PatientFinancialSummaryDto> GetFinancialSummaryAsync(Guid patientId)
    {
        var totalPaid = await db.Payments.Where(p => p.PatientId == patientId).SumAsync(p => (decimal?)p.Amount) ?? 0;
        var totalOutstanding = await db.Contracts
            .Where(c => c.PatientId == patientId && c.Status == "active")
            .Include(c => c.Payments)
            .Select(c => c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount))
            .SumAsync(r => (decimal?)r) ?? 0;
        var activeContracts = await db.Contracts.CountAsync(c => c.PatientId == patientId && c.Status == "active");

        var recentPayments = await db.Payments
            .Include(p => p.Receipt)
            .Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(10)
            .Select(p => new PatientPaymentDto
            {
                Id = p.Id,
                Amount = p.Amount,
                PaymentMethod = p.PaymentMethod ?? "نقدي",
                ReceiptNumber = p.Receipt != null ? p.Receipt.ReceiptNumber : null,
                CreatedAt = p.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return new PatientFinancialSummaryDto
        {
            TotalPaid = totalPaid,
            TotalOutstanding = totalOutstanding,
            ActiveContracts = activeContracts,
            RecentPayments = recentPayments
        };
    }

    public async Task<Guid?> GetPatientIdByPhoneAsync(string phoneNumber)
    {
        var normalizedPhone = NormalizePhone(phoneNumber);
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Phone == normalizedPhone || p.WhatsApp == normalizedPhone);
        return patient?.Id;
    }

    // ── Private Helpers ──────────────────────────────────────────────────────

    private static string NormalizePhone(string phone)
    {
        // Remove spaces, dashes, parentheses
        var cleaned = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());

        // Convert Yemen numbers: 7XX → +9677XX
        if (cleaned.StartsWith("7") && cleaned.Length == 9)
            cleaned = "+967" + cleaned;
        else if (cleaned.StartsWith("0") && cleaned.Length == 10)
            cleaned = "+967" + cleaned[1..];

        return cleaned;
    }

    private static string GenerateTemporaryPassword()
    {
        // Generate an 8-character temporary password with letters and digits
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        var bytes = RandomNumberGenerator.GetBytes(8);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }

    private static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        try
        {
            var hash = AuthService.HashPassword(password, storedSalt);
            return hash == storedHash;
        }
        catch
        {
            return false;
        }
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
            new("portal", "true")
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
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    private static PatientPortalProfileDto MapProfile(Patient patient) => new()
    {
        Id = patient.Id,
        PatientNumber = patient.PatientNumber,
        FullName = $"{patient.FirstName} {patient.MiddleName} {patient.LastName}".Replace("  ", " ").Trim(),
        Phone = patient.Phone,
        Gender = patient.Gender?.ToString(),
        Age = patient.DateOfBirth.HasValue ? CalculateAge(patient.DateOfBirth.Value) : null,
        PrimaryDoctorName = patient.PrimaryDoctor?.Name
    };

    private static int CalculateAge(DateOnly dob)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - dob.Year;
        if (dob > today.AddYears(-age)) age--;
        return age;
    }

    private static string ExtractFirstDrugName(JsonDocument? drugs)
    {
        if (drugs == null) return "";
        try
        {
            var root = drugs.RootElement;
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var first = root[0];
                if (first.TryGetProperty("name", out var nameEl))
                    return nameEl.GetString() ?? "";
                if (first.TryGetProperty("medication", out var medEl))
                    return medEl.GetString() ?? "";
            }
            return root.GetRawText().Truncate(80);
        }
        catch
        {
            return "";
        }
    }
}

file static class StringExtensions
{
    public static string Truncate(this string value, int maxLength) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= maxLength ? value : value[..maxLength] + "…";
}
