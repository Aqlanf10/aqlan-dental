using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class CreateEmployeeRequest
{
    public string FullName { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? NationalId { get; init; }
    public string? Position { get; init; }
    public Guid? BranchId { get; init; }
    public DateTime? HireDate { get; init; }
    public decimal? BaseSalary { get; init; }
    public string? EmergencyContact { get; init; }
    public string? EmergencyPhone { get; init; }
    public string? Notes { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? Password { get; init; }
    public string Role { get; init; } = string.Empty;
}

public sealed class UpdateEmployeeRequest
{
    public string? FullName { get; init; }
    public string? Phone { get; init; }
    public string? NationalId { get; init; }
    public string? Position { get; init; }
    public Guid? BranchId { get; init; }
    public DateTime? HireDate { get; init; }
    public decimal? BaseSalary { get; init; }
    public string? EmergencyContact { get; init; }
    public string? EmergencyPhone { get; init; }
    public string? Notes { get; init; }
}

[ApiController]
[Route("api/employees")]
[Authorize(Policy = "AdminOnly")]
public class EmployeesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] Guid? branchId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = db.Employees
            .Include(e => e.User)
            .Include(e => e.Branch)
            .AsQueryable();

        // Search by name or username
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(e =>
                e.FullName.ToLower().Contains(s) ||
                e.User.Username.ToLower().Contains(s));
        }

        // Filter by role
        if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<UserRole>(role, out var userRole))
        {
            query = query.Where(e => e.User.Role == userRole);
        }

        // Filter by branch
        if (branchId.HasValue)
        {
            query = query.Where(e => e.BranchId == branchId.Value);
        }

        var total = await query.CountAsync();

        var employees = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                e.UserId,
                e.FullName,
                e.Phone,
                e.NationalId,
                e.Position,
                e.BranchId,
                BranchName = e.Branch != null ? e.Branch.Name : null,
                e.HireDate,
                e.BaseSalary,
                e.EmergencyContact,
                e.EmergencyPhone,
                e.Notes,
                e.User.Username,
                Role = e.User.Role.ToString(),
                UserActive = e.User.IsActive,
                e.IsActive,
                e.CreatedAt,
            })
            .ToListAsync();

        return Ok(new
        {
            data = employees,
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(total / (double)pageSize),
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var employee = await db.Employees
            .Include(e => e.User)
            .Include(e => e.Branch)
            .Where(e => e.Id == id)
            .Select(e => new
            {
                e.Id,
                e.UserId,
                e.FullName,
                e.Phone,
                e.NationalId,
                e.Position,
                e.BranchId,
                BranchName = e.Branch != null ? e.Branch.Name : null,
                e.HireDate,
                e.BaseSalary,
                e.EmergencyContact,
                e.EmergencyPhone,
                e.Notes,
                e.User.Username,
                Role = e.User.Role.ToString(),
                UserActive = e.User.IsActive,
                e.IsActive,
                e.CreatedAt,
                e.UpdatedAt,
            })
            .FirstOrDefaultAsync();

        if (employee is null)
            return NotFound(new { message = "الموظف غير موجود" });

        return Ok(employee);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest req)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(req.FullName))
            return BadRequest(new { message = "الاسم الكامل مطلوب" });

        if (string.IsNullOrWhiteSpace(req.Username))
            return BadRequest(new { message = "اسم المستخدم مطلوب" });

        if (!Enum.TryParse<UserRole>(req.Role, out var role))
            return BadRequest(new { message = "الدور غير صالح" });

        if (role == UserRole.Patient)
            return BadRequest(new { message = "لا يمكن إنشاء حساب موظف بدور مريض" });

        // Check username uniqueness
        var usernameExists = await db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.Username == req.Username.Trim());
        if (usernameExists)
            return Conflict(new { message = "اسم المستخدم مستخدم بالفعل" });

        // Validate branch if provided
        if (req.BranchId.HasValue)
        {
            var branchExists = await db.Branches
                .AnyAsync(b => b.Id == req.BranchId.Value);
            if (!branchExists)
                return BadRequest(new { message = "الفرع غير موجود" });
        }

        // Create User account
        var password = req.Password ?? "Aqlan@2024";
        if (password.Length < 8)
            return BadRequest(new { message = "كلمة المرور يجب أن تكون 8 أحرف على الأقل" });

        var salt = AuthService.GenerateSalt();
        var hash = AuthService.HashPassword(password, salt);

        var user = new User
        {
            Username = req.Username.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = role,
            BranchId = req.BranchId,
        };
        db.Users.Add(user);

        // Create Employee record
        var employee = new Employee
        {
            UserId = user.Id,
            FullName = req.FullName.Trim(),
            Phone = req.Phone?.Trim(),
            NationalId = req.NationalId?.Trim(),
            Position = req.Position?.Trim(),
            BranchId = req.BranchId,
            HireDate = req.HireDate,
            BaseSalary = req.BaseSalary,
            EmergencyContact = req.EmergencyContact?.Trim(),
            EmergencyPhone = req.EmergencyPhone?.Trim(),
            Notes = req.Notes?.Trim(),
        };
        db.Employees.Add(employee);

        await db.SaveChangesAsync();

        return Created($"/api/employees/{employee.Id}", new
        {
            employee.Id,
            employee.UserId,
            employee.FullName,
            employee.Phone,
            employee.NationalId,
            employee.Position,
            employee.BranchId,
            employee.HireDate,
            employee.BaseSalary,
            Username = user.Username,
            Role = user.Role.ToString(),
            employee.IsActive,
            employee.CreatedAt,
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmployeeRequest req)
    {
        var employee = await db.Employees
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (employee is null)
            return NotFound(new { message = "الموظف غير موجود" });

        // Validate branch if provided
        if (req.BranchId.HasValue && req.BranchId != employee.BranchId)
        {
            var branchExists = await db.Branches
                .AnyAsync(b => b.Id == req.BranchId.Value);
            if (!branchExists)
                return BadRequest(new { message = "الفرع غير موجود" });
        }

        // Update fields
        if (req.FullName is not null)
            employee.FullName = req.FullName.Trim();

        if (req.Phone is not null)
            employee.Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();

        if (req.NationalId is not null)
            employee.NationalId = string.IsNullOrWhiteSpace(req.NationalId) ? null : req.NationalId.Trim();

        if (req.Position is not null)
            employee.Position = string.IsNullOrWhiteSpace(req.Position) ? null : req.Position.Trim();

        if (req.BranchId.HasValue)
            employee.BranchId = req.BranchId;

        if (req.HireDate.HasValue)
            employee.HireDate = req.HireDate;

        if (req.BaseSalary.HasValue)
            employee.BaseSalary = req.BaseSalary.Value;

        if (req.EmergencyContact is not null)
            employee.EmergencyContact = string.IsNullOrWhiteSpace(req.EmergencyContact) ? null : req.EmergencyContact.Trim();

        if (req.EmergencyPhone is not null)
            employee.EmergencyPhone = string.IsNullOrWhiteSpace(req.EmergencyPhone) ? null : req.EmergencyPhone.Trim();

        if (req.Notes is not null)
            employee.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();

        // Sync branch to user if changed
        if (req.BranchId.HasValue && employee.User.BranchId != req.BranchId)
        {
            employee.User.BranchId = req.BranchId;
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            employee.Id,
            employee.UserId,
            employee.FullName,
            employee.Phone,
            employee.NationalId,
            employee.Position,
            employee.BranchId,
            employee.HireDate,
            employee.BaseSalary,
            employee.EmergencyContact,
            employee.EmergencyPhone,
            employee.Notes,
            Username = employee.User.Username,
            Role = employee.User.Role.ToString(),
            UserActive = employee.User.IsActive,
            employee.IsActive,
            employee.UpdatedAt,
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var employee = await db.Employees.FindAsync(id);
        if (employee is null)
            return NotFound(new { message = "الموظف غير موجود" });

        // Soft delete only the employee, NOT the user account
        employee.IsActive = false;
        employee.DeletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف الموظف بنجاح" });
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> ToggleStatus(Guid id)
    {
        var employee = await db.Employees.FindAsync(id);
        if (employee is null)
            return NotFound(new { message = "الموظف غير موجود" });

        employee.IsActive = !employee.IsActive;
        await db.SaveChangesAsync();

        return Ok(new { message = employee.IsActive ? "تم تفعيل الموظف بنجاح" : "تم تعطيل الموظف بنجاح" });
    }
}
