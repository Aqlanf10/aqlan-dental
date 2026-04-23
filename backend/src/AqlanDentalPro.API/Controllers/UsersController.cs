using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Konscious.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace AqlanDentalPro.API.Controllers;

public record CreateUserRequest(
    string Username,
    string Password,
    string Role,
    string? Email,
    string? DoctorName,
    string? DoctorSpecialty,
    string? DoctorColor
);

public record UpdateUserRoleRequest(string Role);

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(AppDbContext db) : ControllerBase
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
        if (user == null) return NotFound();

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
        if (user == null) return NotFound();

        user.IsActive = !user.IsActive;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string HashPassword(string password)
    {
        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = Encoding.UTF8.GetBytes("AqlanDentalSalt!"),
            DegreeOfParallelism = 1,
            MemorySize = 65536,
            Iterations = 3
        };
        return Convert.ToBase64String(argon2.GetBytes(32));
    }
}
