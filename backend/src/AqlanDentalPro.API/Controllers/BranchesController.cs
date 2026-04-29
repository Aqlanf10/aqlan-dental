using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class CreateBranchRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public bool IsMain { get; init; } = false;
}

public sealed class CreateBranchRequestValidator : AbstractValidator<CreateBranchRequest>
{
    public CreateBranchRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم الفرع مطلوب")
            .MaximumLength(200).WithMessage("اسم الفرع يجب ألا يتجاوز 200 حرف");
        RuleFor(x => x.Phone)
            .MaximumLength(50).WithMessage("رقم الهاتف يجب ألا يتجاوز 50 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("العنوان يجب ألا يتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Address));
    }
}

public sealed class UpdateBranchRequest
{
    public string? Name { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public bool? IsMain { get; init; }
}

public sealed class UpdateBranchRequestValidator : AbstractValidator<UpdateBranchRequest>
{
    public UpdateBranchRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم الفرع لا يمكن أن يكون فارغاً")
            .MaximumLength(200).WithMessage("اسم الفرع يجب ألا يتجاوز 200 حرف")
            .When(x => x.Name is not null);
        RuleFor(x => x.Phone)
            .MaximumLength(50).WithMessage("رقم الهاتف يجب ألا يتجاوز 50 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("العنوان يجب ألا يتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Address));
    }
}

[ApiController]
[Route("api/branches")]
[Authorize(Policy = "AdminOnly")]
public class BranchesController(AppDbContext db) : ControllerBase
{
    // GET /api/branches
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var branches = await db.Branches
            .OrderBy(b => b.Name)
            .Select(b => new
            {
                b.Id,
                b.Name,
                b.Address,
                b.Phone,
                b.IsMain,
                b.IsActive,
                UserCount = b.Users.Count,
                CreatedAt = b.CreatedAt.ToString("yyyy-MM-dd"),
            })
            .ToListAsync();

        return Ok(branches);
    }

    // GET /api/branches/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var branch = await db.Branches
            .Where(b => b.Id == id)
            .Select(b => new
            {
                b.Id,
                b.Name,
                b.Address,
                b.Phone,
                b.IsMain,
                b.IsActive,
                b.CreatedAt,
                b.UpdatedAt,
                UserCount = b.Users.Count,
                DoctorCount = b.Doctors.Count,
                PatientCount = b.Patients.Count,
                AppointmentCount = b.Appointments.Count,
            })
            .FirstOrDefaultAsync();

        if (branch is null) return NotFound(new { message = "الفرع غير موجود" });

        return Ok(branch);
    }

    // POST /api/branches
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBranchRequest req)
    {
        // If this branch is set as main, unset any existing main branch
        if (req.IsMain)
        {
            var currentMain = await db.Branches.Where(b => b.IsMain).ToListAsync();
            foreach (var b in currentMain) b.IsMain = false;
        }

        var branch = new Branch
        {
            Name = req.Name,
            Address = req.Address,
            Phone = req.Phone,
            IsMain = req.IsMain,
        };

        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        return Created($"/api/branches/{branch.Id}", new
        {
            branch.Id,
            branch.Name,
            branch.Address,
            branch.Phone,
            branch.IsMain,
        });
    }

    // PUT /api/branches/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBranchRequest req)
    {
        var branch = await db.Branches.FindAsync(id);
        if (branch is null) return NotFound(new { message = "الفرع غير موجود" });

        if (req.Name is not null) branch.Name = req.Name;
        if (req.Address is not null) branch.Address = req.Address;
        if (req.Phone is not null) branch.Phone = req.Phone;
        if (req.IsMain.HasValue)
        {
            if (req.IsMain.Value)
            {
                var currentMain = await db.Branches.Where(b => b.IsMain && b.Id != id).ToListAsync();
                foreach (var b in currentMain) b.IsMain = false;
            }
            branch.IsMain = req.IsMain.Value;
        }

        await db.SaveChangesAsync();
        return Ok(new { message = "تم تحديث الفرع بنجاح" });
    }

    // DELETE /api/branches/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var branch = await db.Branches.FindAsync(id);
        if (branch is null) return NotFound(new { message = "الفرع غير موجود" });

        branch.IsActive = false;
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف الفرع بنجاح" });
    }
}
