using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentValidation;
using Konscious.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text;

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
[Authorize]
public class UsersController(
    AppDbContext db,
    ICurrentUserService currentUser,
    IConfiguration config) : ControllerBase
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        if (await db.Users.AnyAsync(u => u.Username == req.Username))
            return Conflict(new { message = "اسم المستخدم مستخدم بالفعل" });

        if (!Enum.TryParse<UserRole>(req.Role, out var role))
            return BadRequest(new { message = "الدور غير صالح" });

        var user = new User
        {
            Username = req.Username,
            Email = req.Email,
            PasswordHash = HashPassword(req.Password),
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
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var userId = currentUser.UserId;
        if (userId == Guid.Empty) return Unauthorized();

        var user = await db.Users.FindAsync(userId);
        if (user is null) return NotFound(new { message = "المستخدم غير موجود" });

        if (user.PasswordHash != HashPassword(req.CurrentPassword))
            return BadRequest(new { message = "كلمة المرور الحالية غير صحيحة" });

        user.PasswordHash = HashPassword(req.NewPassword);
        await db.SaveChangesAsync();
        return Ok(new { message = "تم تغيير كلمة المرور بنجاح" });
    }

    private string HashPassword(string password)
    {
        var salt = config["Security:Argon2Salt"] ?? "AqlanDentalSalt!";
        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = Encoding.UTF8.GetBytes(salt),
            DegreeOfParallelism = 1,
            MemorySize = 65536,
            Iterations = 3
        };
        return Convert.ToBase64String(argon2.GetBytes(32));
    }
}
