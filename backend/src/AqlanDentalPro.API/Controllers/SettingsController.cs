using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var settings = await db.Settings
            .OrderBy(s => s.Category).ThenBy(s => s.Key)
            .Select(s => new { s.Key, s.Value, s.Category })
            .ToListAsync();

        var dict = settings.ToDictionary(s => s.Key, s => s.Value);
        return Ok(dict);
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Update(string key, [FromBody] UpdateSettingRequest req)
    {
        var setting = await db.Settings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null)
        {
            db.Settings.Add(new Domain.Entities.Setting
            {
                Key = key,
                Value = req.Value,
                Category = req.Category,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            setting.Value = req.Value;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return Ok(new { key, value = req.Value });
    }
}

public class UpdateSettingRequest
{
    public string? Value { get; set; }
    public string? Category { get; set; }
}
