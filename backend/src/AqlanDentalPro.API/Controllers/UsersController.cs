using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace AqlanDentalPro.API.Controllers;

public sealed class CreateUserRequest
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? DoctorName { get; init; }
    public string? DoctorSpecialty { get; init; }
    public string? DoctorColor { get; init; }
}

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("اسم المستخدم مطلوب")
            .MinimumLength(3).WithMessage("اسم المستخدم يجب أن يكون 3 أحرف على الأقل")
            .MaximumLength(50).WithMessage("اسم المستخدم يجب ألا يتجاوز 50 حرفاً")
            .Matches(@"^[a-zA-Z0-9_]+$").WithMessage("اسم المستخدم يجب أن يحتوي على أحرف وأرقام وشرطة سفلية فقط");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("كلمة المرور مطلوبة")
            .MinimumLength(8).WithMessage("كلمة المرور يجب أن تكون 8 أحرف على الأقل");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("الدور مطلوب")
            .Must(r => Enum.TryParse<UserRole>(r, out _)).WithMessage("الدور غير صالح");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("البريد الإلكتروني غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public sealed class UpdateUserRoleRequest
{
    public string Role { get; init; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("كلمة المرور الحالية مطلوبة");
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("كلمة المرور الجديدة مطلوبة")
            .MinimumLength(8).WithMessage("كلمة المرور الجديدة يجب أن تكون 8 أحرف على الأقل")
            .NotEqual(x => x.CurrentPassword).WithMessage("كلمة المرور الجديدة يجب أن تختلف عن الحالية");
    }
}

public sealed class UpdateUserRequest
{
    public string? Username { get; init; }
    public string? Email { get; init; }
    public string? Role { get; init; }
    public string? DoctorName { get; init; }
    public string? DoctorSpecialty { get; init; }
    public string? DoctorColor { get; init; }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Username)
            .MinimumLength(3).WithMessage("اسم المستخدم يجب أن يكون 3 أحرف على الأقل")
            .MaximumLength(50).WithMessage("اسم المستخدم يجب ألا يتجاوز 50 حرفاً")
            .Matches(@"^[a-zA-Z0-9_]+$").WithMessage("اسم المستخدم يجب أن يحتوي على أحرف وأرقام وشرطة سفلية فقط")
            .When(x => !string.IsNullOrWhiteSpace(x.Username));

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("البريد الإلكتروني غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Role)
            .Must(r => Enum.TryParse<UserRole>(r, out _)).WithMessage("الدور غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.Role));
    }
}

public sealed class RejectResetRequest
{
    public string? Notes { get; init; }
}

public sealed class UpdateRolePermissionsRequest
{
    public string Resource { get; init; } = string.Empty;
    public bool CanView { get; init; }
    public bool CanCreate { get; init; }
    public bool CanEdit { get; init; }
    public bool CanDelete { get; init; }
    public bool CanExport { get; init; }
    public bool CanApprove { get; init; }
}

/// <summary>
/// Request body for updating role permissions using flat permission keys (e.g. "patients.view", "finance.create").
/// </summary>
public sealed class UpdateRolePermissionsBody
{
    public List<string> Permissions { get; init; } = new();
}

/// <summary>
/// A single permission definition returned by GET /api/permissions.
/// Describes one possible permission key (resource + action) with a human-readable label.
/// </summary>
public sealed class PermissionDefinitionDto
{
    public string Resource { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}

public sealed class PatientPortalAccountListItemDto
{
    public Guid PatientId { get; init; }
    public string PatientNumber { get; init; } = string.Empty;
    public string PatientName { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string Username { get; init; } = string.Empty;
    public bool AccountActive { get; init; }
    public bool MustChangePassword { get; init; }
    public DateTime? LastLogin { get; init; }
    public Guid? LinkedUserId { get; init; }
}

[ApiController]
[Route("api/users")]
[Authorize(Policy = "AdminOnly")]
public class UsersController(
    AppDbContext db,
    ICurrentUserService currentUser,
    IAuditService auditService,
    ILoginAttemptService loginAttemptService,
    ITokenService tokenService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        // N+1 FIX: Single query with LeftJoin instead of correlated subquery per user
        var users = await (from u in db.Users.IgnoreQueryFilters()
                           where u.Role != UserRole.Patient
                           join d in db.Doctors on u.Id equals d.UserId into dj
                           from doctor in dj.DefaultIfEmpty()
                           orderby u.Username
                           select new
                           {
                               u.Id,
                               u.Username,
                               u.Email,
                               Role = u.Role.ToString(),
                               u.BranchId,
                               u.IsActive,
                               LastLoginAt = u.LastLogin,
                               DoctorName = doctor != null ? doctor.Name : null,
                               u.DeletedAt
                           })
                          .ToListAsync();

        return Ok(users);
    }

    [HttpGet("patient-portal-accounts")]
    public async Task<ActionResult<List<PatientPortalAccountListItemDto>>> GetPatientPortalAccounts()
    {
        var accounts = await db.PatientAccounts
            .IgnoreQueryFilters()
            .Include(a => a.Patient)
            .OrderBy(a => a.Patient.PatientNumber)
            .Select(a => new PatientPortalAccountListItemDto
            {
                PatientId = a.PatientId,
                PatientNumber = a.Patient.PatientNumber,
                PatientName = (a.Patient.FirstName + " " +
                               (a.Patient.MiddleName ?? "") + " " +
                               a.Patient.LastName).Replace("  ", " ").Trim(),
                Phone = a.Patient.Phone ?? a.PhoneNumber,
                Username = a.Username ?? a.Patient.PatientNumber,
                AccountActive = a.IsActive && a.PortalAccountActive,
                MustChangePassword = a.MustChangePassword,
                LastLogin = a.LastLogin,
                LinkedUserId = a.LinkedUserId
            })
            .ToListAsync();

        return Ok(accounts);
    }

    /// <summary>قائمة المستخدمين للرسائل — متاحة لجميع أدوار الطاقم (لا بوابة المرضى) مع تصفية حسب الصلاحيات</summary>
    [HttpGet("contacts")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> GetContacts([FromServices] MessagingService messagingService)
    {
        // N+1 FIX: Single query with LeftJoin instead of 3 correlated subqueries per user
        var users = await (from u in db.Users
                           where u.IsActive
                           join d in db.Doctors on u.Id equals d.UserId into dj
                           from doctor in dj.DefaultIfEmpty()
                           orderby u.Username
                           select new
                           {
                               u.Id,
                               u.Username,
                               Role = u.Role.ToString(),
                               DoctorName = doctor != null ? doctor.Name : null,
                               DoctorColor = doctor != null ? doctor.Color : null,
                               DoctorInitials = doctor != null ? doctor.AvatarInitials : null
                           })
                          .ToListAsync();

        // H8 FIX: Batch messaging permissions — single DB call instead of N+1
        var userIds = users.Select(u => u.Id).ToList();
        var messagingPermissions = await messagingService.CanMessageUsersBatchAsync(userIds);

        var filtered = users.Select(u => new
        {
            u.Id,
            u.Username,
            u.Role,
            u.DoctorName,
            u.DoctorColor,
            u.DoctorInitials,
            CanMessage = messagingPermissions.TryGetValue(u.Id, out var can) && can
        });

        return Ok(filtered);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await (from u in db.Users
                          join d in db.Doctors on u.Id equals d.UserId into dj
                          from doctor in dj.DefaultIfEmpty()
                          where u.Id == id && u.Role != UserRole.Patient
                          select new
                          {
                              u.Id,
                              u.Username,
                              u.Email,
                              Role = u.Role.ToString(),
                              u.BranchId,
                              u.IsActive,
                              u.MustChangePassword,
                              LastLoginAt = u.LastLogin,
                              u.DeletedAt,
                              DoctorId = doctor != null ? (Guid?)doctor.Id : null,
                              DoctorName = doctor != null ? doctor.Name : null,
                              DoctorSpecialty = doctor != null ? doctor.Specialty : null,
                              DoctorColor = doctor != null ? doctor.Color : null,
                              DoctorInitials = doctor != null ? doctor.AvatarInitials : null
                          })
                         .FirstOrDefaultAsync();

        if (user is null) return NotFound(new { message = "المستخدم غير موجود" });
        return Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == req.Username))
            return Conflict(new { message = "اسم المستخدم مستخدم بالفعل" });

        if (!Enum.TryParse<UserRole>(req.Role, out var role))
            return BadRequest(new { message = "الدور غير صالح" });

        if (role == UserRole.Patient)
            return BadRequest(new { message = "حسابات المرضى تدار من تبويب حسابات بوابة المرضى وليس من مستخدمي الطاقم" });

        // Generate unique salt for each user
        var salt = AuthService.GenerateSalt();
        var hash = AuthService.HashPassword(req.Password, salt);

        var user = new User
        {
            Username = req.Username,
            Email = req.Email,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = role,
        };
        db.Users.Add(user);

        if (IsDoctorRole(role))
        {
            var doctorName = string.IsNullOrWhiteSpace(req.DoctorName) ? req.Username : req.DoctorName.Trim();
            db.Doctors.Add(new Doctor
            {
                UserId = user.Id,
                Name = doctorName,
                Specialty = req.DoctorSpecialty,
                Color = req.DoctorColor ?? "#0E7490",
                AvatarInitials = doctorName.Split(' ').FirstOrDefault()?.Substring(0, 1) ?? "د",
            });
        }

        await db.SaveChangesAsync();

        await auditService.LogAsync(AuditAction.Create, "users", user.Id, newData: new { user.Username, Role = user.Role.ToString(), user.Email });

        return Created($"/api/users/{user.Id}", new { user.Id, user.Username, Role = user.Role.ToString() });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest req)
    {
        var user = await db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Doctor)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null) return NotFound(new { message = "المستخدم غير موجود" });
        if (user.Role == UserRole.Patient)
            return BadRequest(new { message = "حسابات المرضى تدار من تبويب حسابات بوابة المرضى وليس من مستخدمي الطاقم" });

        var oldData = new { user.Username, user.Email, Role = user.Role.ToString() };

        // Validate unique username (excluding self)
        if (!string.IsNullOrWhiteSpace(req.Username) && req.Username != user.Username)
        {
            if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == req.Username))
                return Conflict(new { message = "اسم المستخدم مستخدم بالفعل" });
            user.Username = req.Username;
        }

        // Validate email format
        if (req.Email != null && req.Email != user.Email)
        {
            if (!string.IsNullOrWhiteSpace(req.Email))
            {
                // Basic email validation
                try
                {
                    var addr = new System.Net.Mail.MailAddress(req.Email);
                    if (addr.Address != req.Email) throw new FormatException();
                }
                catch
                {
                    return BadRequest(new { message = "البريد الإلكتروني غير صالح" });
                }
            }
            user.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email;
        }

        // Handle role change
        if (!string.IsNullOrWhiteSpace(req.Role) && Enum.TryParse<UserRole>(req.Role, out var newRole) && newRole != user.Role)
        {
            if (newRole == UserRole.Patient)
                return BadRequest(new { message = "لا يمكن تحويل مستخدم طاقم إلى حساب مريض من هذه الشاشة" });

            // Prevent editing the last active admin's role to non-admin
            if (user.Role == UserRole.Admin && newRole != UserRole.Admin)
            {
                var activeAdminCount = await db.Users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive);
                if (activeAdminCount <= 1)
                    return BadRequest(new { message = "لا يمكن تغيير دور آخر مدير نشط" });
            }

            var oldRole = user.Role;
            user.Role = newRole;

            // If changing role from doctor to non-doctor, keep Doctor record but mark inactive
            if (IsDoctorRole(oldRole) && !IsDoctorRole(newRole) && user.Doctor != null)
            {
                user.Doctor.IsActive = false;
            }
            // If changing role to doctor and no Doctor record exists, create one
            else if (!IsDoctorRole(oldRole) && IsDoctorRole(newRole) && user.Doctor == null)
            {
                var doctorName = req.DoctorName ?? user.Username;
                db.Doctors.Add(new Doctor
                {
                    UserId = user.Id,
                    Name = doctorName,
                    Specialty = req.DoctorSpecialty,
                    Color = req.DoctorColor ?? "#0E7490",
                    AvatarInitials = doctorName.Split(' ').FirstOrDefault()?.Substring(0, 1) ?? "د",
                });
            }
        }

        // Update doctor profile fields
        if (user.Doctor != null)
        {
            if (!string.IsNullOrWhiteSpace(req.DoctorName)) user.Doctor.Name = req.DoctorName;
            if (req.DoctorSpecialty != null) user.Doctor.Specialty = req.DoctorSpecialty;
            if (req.DoctorColor != null) user.Doctor.Color = req.DoctorColor;
        }

        await db.SaveChangesAsync();

        var newData = new { user.Username, user.Email, Role = user.Role.ToString() };
        await auditService.LogAsync(AuditAction.Update, "users", user.Id, oldData, newData);

        return Ok(new { user.Id, user.Username, Role = user.Role.ToString(), user.Email });
    }

    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateUserRoleRequest req)
    {
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound(new { message = "المستخدم غير موجود" });
        if (user.Role == UserRole.Patient)
            return BadRequest(new { message = "حسابات المرضى تدار من تبويب حسابات بوابة المرضى وليس من مستخدمي الطاقم" });

        if (!Enum.TryParse<UserRole>(req.Role, out var role))
            return BadRequest(new { message = "الدور غير صالح" });
        if (role == UserRole.Patient)
            return BadRequest(new { message = "لا يمكن تحويل مستخدم طاقم إلى حساب مريض من هذه الشاشة" });

        // Prevent changing last admin's role
        if (user.Role == UserRole.Admin && role != UserRole.Admin)
        {
            var activeAdminCount = await db.Users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive);
            if (activeAdminCount <= 1)
                return BadRequest(new { message = "لا يمكن تغيير دور آخر مدير نشط" });
        }

        var oldRole = user.Role.ToString();
        user.Role = role;
        await db.SaveChangesAsync();

        await auditService.LogAsync(AuditAction.Update, "users", user.Id,
            oldData: new { Role = oldRole },
            newData: new { Role = role.ToString() });

        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> ToggleStatus(Guid id)
    {
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound(new { message = "المستخدم غير موجود" });
        if (user.Role == UserRole.Patient)
            return BadRequest(new { message = "حسابات المرضى تدار من تبويب حسابات بوابة المرضى وليس من مستخدمي الطاقم" });

        // Prevent deactivating the last active admin
        if (user.IsActive && user.Role == UserRole.Admin)
        {
            var activeAdminCount = await db.Users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive);
            if (activeAdminCount <= 1)
                return BadRequest(new { message = "لا يمكن تعطيل آخر مدير نشط" });
        }

        user.IsActive = !user.IsActive;

        var action = user.IsActive ? AuditAction.UserActivated : AuditAction.UserDeactivated;
        await db.SaveChangesAsync();

        await auditService.LogAsync(action, "users", user.Id,
            newData: new { IsActive = user.IsActive });

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound(new { message = "المستخدم غير موجود" });
        if (user.Role == UserRole.Patient)
            return BadRequest(new { message = "حسابات المرضى تدار من تبويب حسابات بوابة المرضى وليس من مستخدمي الطاقم" });

        // Prevent self-deletion
        if (currentUser.UserId == id)
            return BadRequest(new { message = "لا يمكنك حذف حسابك الخاص" });

        // Prevent deleting the last active Admin
        if (user.Role == UserRole.Admin && user.IsActive)
        {
            var activeAdminCount = await db.Users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive);
            if (activeAdminCount <= 1)
                return BadRequest(new { message = "لا يمكن حذف آخر مدير نشط" });
        }

        user.IsActive = false;
        user.DeletedAt = DateTime.UtcNow;
        user.DeletedBy = currentUser.UserId;

        await db.SaveChangesAsync();

        await auditService.LogAsync(AuditAction.Delete, "users", user.Id,
            newData: new { DeletedBy = currentUser.UserId });

        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id)
    {
        var user = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null) return NotFound(new { message = "المستخدم غير موجود" });
        if (user.Role == UserRole.Patient)
            return BadRequest(new { message = "حسابات المرضى تدار من تبويب حسابات بوابة المرضى وليس من مستخدمي الطاقم" });

        if (user.IsActive)
            return BadRequest(new { message = "المستخدم نشط بالفعل" });

        user.IsActive = true;
        user.DeletedAt = null;
        user.DeletedBy = null;

        await db.SaveChangesAsync();

        await auditService.LogAsync(AuditAction.UserRestored, "users", user.Id);

        return Ok(new { message = "تم استعادة المستخدم بنجاح", user.Id, user.Username });
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id)
    {
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound(new { message = "المستخدم غير موجود" });
        if (user.Role == UserRole.Patient)
            return BadRequest(new { message = "حسابات المرضى تدار من تبويب حسابات بوابة المرضى وليس من مستخدمي الطاقم" });

        // Generate 12-char temporary password with uppercase+lowercase+digits+special
        var tempPassword = GenerateTemporaryPassword();

        // Hash with Argon2id + new per-user salt
        var salt = AuthService.GenerateSalt();
        var hash = AuthService.HashPassword(tempPassword, salt);

        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.MustChangePassword = true;

        // Reset login lockout
        await loginAttemptService.ResetFailedAttemptsAsync(user.Username);

        // Revoke all refresh tokens
        await tokenService.RevokeAllRefreshTokensAsync(user.Id);

        await db.SaveChangesAsync();

        // Audit log: PasswordResetByAdmin — do NOT log the temp password
        await auditService.LogAsync(AuditAction.PasswordResetByAdmin, "users", user.Id,
            newData: new { MustChangePassword = true });

        return Ok(new { message = "تم إعادة تعيين كلمة المرور بنجاح", tempPassword });
    }

    [HttpPost("me/change-password")]
    [Authorize] // Any authenticated user can change their own password
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var userId = currentUser.UserId;
        if (userId == Guid.Empty) return Unauthorized();

        var user = await db.Users.FindAsync(userId);
        if (user is null) return NotFound(new { message = "المستخدم غير موجود" });

        // Verify current password with user's unique salt
        var currentHash = AuthService.HashPassword(req.CurrentPassword, user.PasswordSalt);
        if (currentHash != user.PasswordHash)
            return BadRequest(new { message = "كلمة المرور الحالية غير صحيحة" });

        // Generate new salt for new password
        var newSalt = AuthService.GenerateSalt();
        var newHash = AuthService.HashPassword(req.NewPassword, newSalt);
        user.PasswordHash = newHash;
        user.PasswordSalt = newSalt;
        user.MustChangePassword = false;

        await db.SaveChangesAsync();

        await auditService.LogAsync(AuditAction.PasswordChange, "users", user.Id);

        return Ok(new { message = "تم تغيير كلمة المرور بنجاح" });
    }

    // ── Password Reset Request Management ──────────────────────────────────

    [HttpGet("password-reset-requests")]
    public async Task<IActionResult> GetPasswordResetRequests()
    {
        var requests = await db.PasswordResetRequests
            .Where(r => r.Status == "Pending")
            .Include(r => r.User)
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => new
            {
                r.Id,
                r.UserId,
                Username = r.User != null ? r.User.Username : null,
                r.UsernameOrEmail,
                r.RequestedAt,
                r.Status,
                r.Notes
            })
            .ToListAsync();

        return Ok(requests);
    }

    [HttpPost("password-reset-requests/{id:guid}/approve")]
    public async Task<IActionResult> ApproveResetRequest(Guid id)
    {
        var request = await db.PasswordResetRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request is null) return NotFound(new { message = "طلب إعادة التعيين غير موجود" });
        if (request.Status != "Pending") return BadRequest(new { message = "الطلب ليس بحالة الانتظار" });

        if (request.User is null)
            return BadRequest(new { message = "المستخدم المرتبط بالطلب غير موجود" });

        // Generate temp password
        var tempPassword = GenerateTemporaryPassword();

        // Reset user password
        var salt = AuthService.GenerateSalt();
        var hash = AuthService.HashPassword(tempPassword, salt);

        request.User.PasswordHash = hash;
        request.User.PasswordSalt = salt;
        request.User.MustChangePassword = true;

        // Update request status
        request.Status = "Approved";
        request.ApprovedByUserId = currentUser.UserId;
        request.ApprovedAt = DateTime.UtcNow;

        // Reset login lockout
        await loginAttemptService.ResetFailedAttemptsAsync(request.User.Username);

        // Revoke all refresh tokens
        await tokenService.RevokeAllRefreshTokensAsync(request.User.Id);

        await db.SaveChangesAsync();

        await auditService.LogAsync(AuditAction.PasswordResetByAdmin, "users", request.User.Id,
            newData: new { MustChangePassword = true, ResetRequestId = id });

        return Ok(new { message = "تمت الموافقة على الطلب وإعادة تعيين كلمة المرور", tempPassword });
    }

    [HttpPost("password-reset-requests/{id:guid}/reject")]
    public async Task<IActionResult> RejectResetRequest(Guid id, [FromBody] RejectResetRequest? req)
    {
        var request = await db.PasswordResetRequests.FindAsync(id);
        if (request is null) return NotFound(new { message = "طلب إعادة التعيين غير موجود" });
        if (request.Status != "Pending") return BadRequest(new { message = "الطلب ليس بحالة الانتظار" });

        request.Status = "Rejected";
        request.ApprovedByUserId = currentUser.UserId;
        request.ApprovedAt = DateTime.UtcNow;
        request.Notes = req?.Notes;

        await db.SaveChangesAsync();

        await auditService.LogAsync(AuditAction.Update, "password_reset_requests", id,
            newData: new { Status = "Rejected", RejectedBy = currentUser.UserId });

        return Ok(new { message = "تم رفض الطلب" });
    }

    // ── Permissions Management ─────────────────────────────────────────────

    private static readonly (string Action, string LabelAr)[] AllActions =
    [
        ("view", "عرض"),
        ("create", "إنشاء"),
        ("edit", "تعديل"),
        ("delete", "حذف"),
        ("export", "تصدير"),
        ("approve", "اعتماد")
    ];

    private static readonly Dictionary<string, string> ResourceLabelAr = new()
    {
        ["patients"] = "المرضى",
        ["ortho"] = "التقويم",
        ["general_dentistry"] = "طب الأسنان العام",
        ["surgery"] = "الجراحة",
        ["appointments"] = "المواعيد",
        ["finance"] = "المدفوعات",
        ["reports"] = "التقارير",
        ["users"] = "المستخدمون",
        ["settings"] = "الإعدادات",
        ["ai"] = "الذكاء الاصطناعي",
        ["user_management"] = "إدارة المستخدمين",
        ["password_reset_requests"] = "طلبات إعادة تعيين كلمة المرور",
        ["impersonation"] = "الانتحال",
        ["daily_operations"] = "التشغيل اليومي",
        ["booking_requests"] = "طلبات الحجز",
        ["clinic_queue"] = "الطابور",
        ["clinic_display"] = "شاشة النداء",
        ["patient_journey"] = "رحلة المرضى",
        ["visits"] = "الزيارات",
        ["checkout"] = "جاهز للدفع",
        ["invoices"] = "الفواتير",
        ["rooms"] = "الغرف / الكراسي",
        ["lab_orders"] = "طلبات المعمل",
        ["labs"] = "إدارة المعامل",
        ["lab_work_types"] = "أنواع أعمال المعمل",
        ["lab_work_prices"] = "تسعير المعامل",
        ["lab_payables"] = "مستحقات المعامل",
        ["lab_reports"] = "تقارير المعامل",
    };

    /// <summary>
    /// Returns all permission definitions (resource + action → key + label).
    /// This is the catalog of all possible permission keys, NOT role-specific data.
    /// </summary>
    [HttpGet("/api/permissions")]
    public async Task<IActionResult> GetAllPermissions()
    {
        // Get distinct resources that exist in the DB so we only list what's seeded
        var resources = await db.RolePermissions
            .Select(rp => rp.Resource)
            .Distinct()
            .OrderBy(r => r)
            .ToListAsync();

        var definitions = new List<PermissionDefinitionDto>();
        foreach (var resource in resources)
        {
            var resourceLabel = ResourceLabelAr.TryGetValue(resource, out var rl) ? rl : resource;
            foreach (var (action, actionLabel) in AllActions)
            {
                definitions.Add(new PermissionDefinitionDto
                {
                    Resource = resource,
                    Action = action,
                    Key = $"{resource}.{action}",
                    Label = $"{resourceLabel} - {actionLabel}"
                });
            }
        }

        return Ok(definitions);
    }

    /// <summary>
    /// Returns the set of enabled permission keys for a given role.
    /// Shape: { role: string, permissions: string[] }
    /// </summary>
    [HttpGet("/api/roles/{role}/permissions")]
    public async Task<IActionResult> GetRolePermissions(string role)
    {
        // Validate role
        if (!Enum.TryParse<UserRole>(role, out _))
            return BadRequest(new { message = "الدور غير صالح" });

        var rows = await db.RolePermissions
            .Where(p => p.Role == role)
            .ToListAsync();

        var permissionKeys = new List<string>();
        foreach (var perm in rows)
        {
            if (perm.CanView) permissionKeys.Add($"{perm.Resource}.view");
            if (perm.CanCreate) permissionKeys.Add($"{perm.Resource}.create");
            if (perm.CanEdit) permissionKeys.Add($"{perm.Resource}.edit");
            if (perm.CanDelete) permissionKeys.Add($"{perm.Resource}.delete");
            if (perm.CanExport) permissionKeys.Add($"{perm.Resource}.export");
            if (perm.CanApprove) permissionKeys.Add($"{perm.Resource}.approve");
        }

        return Ok(new Application.DTOs.Auth.UserPermissionsDto
        {
            Role = role,
            Permissions = permissionKeys
        });
    }

    /// <summary>
    /// Updates role permissions from a flat list of permission keys (e.g. "patients.view", "finance.create").
    /// The backend converts these back to RolePermission entity upserts.
    /// </summary>
    [HttpPut("/api/roles/{role}/permissions")]
    public async Task<IActionResult> UpdateRolePermissions(string role, [FromBody] UpdateRolePermissionsBody body)
    {
        // Validate role
        if (!Enum.TryParse<UserRole>(role, out _))
            return BadRequest(new { message = "الدور غير صالح" });

        var permissionKeys = body.Permissions ?? [];

        // Group permission keys by resource
        var resourceActions = new Dictionary<string, HashSet<string>>();
        foreach (var key in permissionKeys)
        {
            var lastDot = key.LastIndexOf('.');
            if (lastDot <= 0) continue; // skip invalid keys
            var resource = key[..lastDot];
            var action = key[(lastDot + 1)..];
            if (!resourceActions.TryGetValue(resource, out var actions))
            {
                actions = [];
                resourceActions[resource] = actions;
            }
            actions.Add(action);
        }

        // Load all existing role permissions for this role
        var existingPerms = await db.RolePermissions
            .Where(p => p.Role == role)
            .ToListAsync();

        var existingGroups = existingPerms
            .GroupBy(p => p.Resource)
            .ToList();

        var duplicatePerms = new List<RolePermission>();
        var existingByResource = new Dictionary<string, RolePermission>();
        foreach (var group in existingGroups)
        {
            var ordered = group
                .OrderByDescending(p => p.UpdatedAt)
                .ThenByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .ToList();

            existingByResource[group.Key] = ordered[0];
            duplicatePerms.AddRange(ordered.Skip(1));
        }

        if (duplicatePerms.Count > 0)
        {
            db.RolePermissions.RemoveRange(duplicatePerms);
        }

        // Guard: prevent removing all critical user_management permissions from Admin role.
        // This must be checked for TWO cases:
        //   1. user_management IS in the request but with no core actions (view/create/edit/delete)
        //   2. user_management is OMITTED from the request — the "missing resources" loop
        //      would set all flags to false, silently stripping Admin's user_management access.
        var isAdmin = string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);
        var hasUserManagementInRequest = resourceActions.TryGetValue("user_management", out var umActions);

        if (isAdmin)
        {
            var adminUmRow = existingByResource.GetValueOrDefault("user_management");

            if (hasUserManagementInRequest)
            {
                // Case 1: user_management is present but all core actions disabled
                if (!umActions!.Contains("view") && !umActions.Contains("create")
                    && !umActions.Contains("edit") && !umActions.Contains("delete"))
                {
                    return BadRequest(new { message = "لا يمكن إزالة جميع صلاحيات إدارة المستخدمين من دور المدير" });
                }
            }
            else if (adminUmRow != null)
            {
                // Case 2: user_management is omitted from the request but exists in DB.
                // Preserve the existing user_management row — do NOT set all flags to false.
                // This prevents silently stripping Admin's critical permissions.
                resourceActions["user_management"] = new HashSet<string>
                {
                    "view", "create", "edit", "delete",
                    adminUmRow.CanExport ? "export" : null!,
                    adminUmRow.CanApprove ? "approve" : null!
                }.Where(a => a != null).ToHashSet();
            }
        }

        // Update existing or add new RolePermission rows
        foreach (var (resource, actions) in resourceActions)
        {
            if (existingByResource.TryGetValue(resource, out var existing))
            {
                existing.CanView = actions.Contains("view");
                existing.CanCreate = actions.Contains("create");
                existing.CanEdit = actions.Contains("edit");
                existing.CanDelete = actions.Contains("delete");
                existing.CanExport = actions.Contains("export");
                existing.CanApprove = actions.Contains("approve");
            }
            else
            {
                db.RolePermissions.Add(new RolePermission
                {
                    Role = role,
                    Resource = resource,
                    CanView = actions.Contains("view"),
                    CanCreate = actions.Contains("create"),
                    CanEdit = actions.Contains("edit"),
                    CanDelete = actions.Contains("delete"),
                    CanExport = actions.Contains("export"),
                    CanApprove = actions.Contains("approve")
                });
            }
        }

        // For resources that exist in DB but are NOT in the request, set all flags to false.
        // NOTE: Admin's user_management is already injected into resourceActions above,
        // so it won't reach this path.
        foreach (var existing in existingByResource.Values)
        {
            if (!resourceActions.ContainsKey(existing.Resource))
            {
                existing.CanView = false;
                existing.CanCreate = false;
                existing.CanEdit = false;
                existing.CanDelete = false;
                existing.CanExport = false;
                existing.CanApprove = false;
            }
        }

        await db.SaveChangesAsync();

        await auditService.LogAsync(AuditAction.PermissionsChanged, "role_permissions",
            newData: new { Role = role, PermissionKeys = permissionKeys },
            details: $"Updated permissions for role: {role}");

        return Ok(new { message = "تم تحديث الصلاحيات بنجاح" });
    }

    // ── Helper Methods ─────────────────────────────────────────────────────

    private static bool IsDoctorRole(UserRole role) =>
        role is UserRole.Orthodontist or UserRole.GeneralDentist or UserRole.OralSurgeon;

    private static string GenerateTemporaryPassword()
    {
        const string uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lowercase = "abcdefghjkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%&*";
        const string all = uppercase + lowercase + digits + special;

        var bytes = RandomNumberGenerator.GetBytes(12);
        var chars = new char[12];

        // Ensure at least one of each category
        chars[0] = uppercase[bytes[0] % uppercase.Length];
        chars[1] = lowercase[bytes[1] % lowercase.Length];
        chars[2] = digits[bytes[2] % digits.Length];
        chars[3] = special[bytes[3] % special.Length];

        // Fill remaining with random from all
        for (int i = 4; i < 12; i++)
        {
            chars[i] = all[bytes[i] % all.Length];
        }

        // Shuffle using Fisher-Yates
        for (int i = chars.Length - 1; i > 0; i--)
        {
            var j = bytes[i] % (i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
