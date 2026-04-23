using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

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
                u.Role,
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
}
