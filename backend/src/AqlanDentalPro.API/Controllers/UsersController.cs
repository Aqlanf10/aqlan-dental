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

[ApiController]
[Route("api/users")]
[Authorize(Policy = "AdminOnly")]
public class UsersController(
    AppDbContext db,
    ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await db.Users
            .OrderBy(u => u.Username)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.Email,
                Role = u.Role.ToString(),
                u.BranchId,
                u.IsActive,
                LastLoginAt = u.LastLogin,
                DoctorName = db.Doctors
                    .Where(d => d.UserId == u.Id)
                    .Select(d => d.Name)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>قائمة المستخدمين للرسائل — متاح لجميع الأدوار مع تصفية حسب الصلاحيات</summary>
    [HttpGet("contacts")]
    [Authorize]
    public async Task<IActionResult> GetContacts([FromServices] MessagingService messagingService)
    {
        var users = await db.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.Username)
            .Select(u => new
            {
                u.Id,
                u.Username,
                Role = u.Role.ToString(),
                DoctorName = db.Doctors
                    .Where(d => d.UserId == u.Id)
                    .Select(d => d.Name)
                    .FirstOrDefault(),
                DoctorColor = db.Doctors
                    .Where(d => d.UserId == u.Id)
                    .Select(d => d.Color)
                    .FirstOrDefault(),
                DoctorInitials = db.Doctors
                    .Where(d => d.UserId == u.Id)
                    .Select(d => d.AvatarInitials)
                    .FirstOrDefault()
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        if (await db.Users.AnyAsync(u => u.Username == req.Username))
            return Conflict(new { message = "اسم المستخدم مستخدم بالفعل" });

        if (!Enum.TryParse<UserRole>(req.Role, out var role))
            return BadRequest(new { message = "الدور غير صالح" });

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

        if (!string.IsNullOrWhiteSpace(req.DoctorName))
        {
            db.Doctors.Add(new Doctor
            {
                UserId = user.Id,
                Name = req.DoctorName,
                Specialty = req.DoctorSpecialty,
                Color = req.DoctorColor ?? "#0E7490",
                AvatarInitials = req.DoctorName.Split(' ').FirstOrDefault()?.Substring(0, 1) ?? "د",
            });
        }

        await db.SaveChangesAsync();
        return Created($"/api/users/{user.Id}", new { user.Id, user.Username, Role = user.Role.ToString() });
    }

    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateUserRoleRequest req)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound(new { message = "المستخدم غير موجود" });

        if (!Enum.TryParse<UserRole>(req.Role, out var role))
            return BadRequest(new { message = "الدور غير صالح" });

        user.Role = role;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> ToggleStatus(Guid id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound(new { message = "المستخدم غير موجود" });

        user.IsActive = !user.IsActive;
        await db.SaveChangesAsync();
        return NoContent();
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

        await db.SaveChangesAsync();
        return Ok(new { message = "تم تغيير كلمة المرور بنجاح" });
    }
}
