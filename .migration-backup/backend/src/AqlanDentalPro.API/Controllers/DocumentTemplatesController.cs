using System.Text.Json;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class DocumentTemplateDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "general";
    public string? Description { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool RequiresSignature { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class UpsertDocumentTemplateRequest
{
    public string Title { get; init; } = string.Empty;
    public string Category { get; init; } = "general";
    public string? Description { get; init; }
    public string Content { get; init; } = string.Empty;
    public bool RequiresSignature { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class RenderDocumentTemplateRequest
{
    public Guid? PatientId { get; init; }
    public Dictionary<string, string?>? Values { get; init; }
}

[ApiController]
[Route("api/document-templates")]
[Authorize(Policy = "AdminOnly")]
public class DocumentTemplatesController(AppDbContext db) : ControllerBase
{
    private const string SettingKey = "templates.document_templates";
    private const string Category = "templates";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
    {
        var templates = await LoadTemplatesAsync();
        if (!includeInactive)
            templates = templates.Where(t => t.IsActive).ToList();

        return Ok(templates.OrderBy(t => t.Category).ThenBy(t => t.Title));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var template = (await LoadTemplatesAsync()).FirstOrDefault(t => t.Id == id);
        return template is null
            ? NotFound(new { message = "القالب غير موجود" })
            : Ok(template);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertDocumentTemplateRequest req)
    {
        var validation = ValidateRequest(req);
        if (validation is not null)
            return BadRequest(new { message = validation });

        var templates = await LoadTemplatesAsync();
        var template = new DocumentTemplateDto
        {
            Id = Guid.NewGuid(),
            Title = req.Title.Trim(),
            Category = NormalizeCategory(req.Category),
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            Content = req.Content.Trim(),
            RequiresSignature = req.RequiresSignature,
            IsActive = req.IsActive,
            UpdatedAt = DateTime.UtcNow
        };

        templates.Add(template);
        await SaveTemplatesAsync(templates);

        return CreatedAtAction(nameof(GetById), new { id = template.Id }, template);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertDocumentTemplateRequest req)
    {
        var validation = ValidateRequest(req);
        if (validation is not null)
            return BadRequest(new { message = validation });

        var templates = await LoadTemplatesAsync();
        var template = templates.FirstOrDefault(t => t.Id == id);
        if (template is null)
            return NotFound(new { message = "القالب غير موجود" });

        template.Title = req.Title.Trim();
        template.Category = NormalizeCategory(req.Category);
        template.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        template.Content = req.Content.Trim();
        template.RequiresSignature = req.RequiresSignature;
        template.IsActive = req.IsActive;
        template.UpdatedAt = DateTime.UtcNow;

        await SaveTemplatesAsync(templates);
        return Ok(template);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var templates = await LoadTemplatesAsync();
        var template = templates.FirstOrDefault(t => t.Id == id);
        if (template is null)
            return NotFound(new { message = "القالب غير موجود" });

        template.IsActive = false;
        template.UpdatedAt = DateTime.UtcNow;
        await SaveTemplatesAsync(templates);
        return Ok(new { message = "تم تعطيل القالب" });
    }

    [HttpPost("{id:guid}/render")]
    public async Task<IActionResult> Render(Guid id, [FromBody] RenderDocumentTemplateRequest req)
    {
        var template = (await LoadTemplatesAsync()).FirstOrDefault(t => t.Id == id);
        if (template is null)
            return NotFound(new { message = "القالب غير موجود" });

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Today"] = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            ["ClinicName"] = await GetSettingValueAsync("clinicName")
                           ?? await GetSettingValueAsync("ClinicName")
                           ?? "مركز الدكتور عقلان الكامل",
        };

        if (req.PatientId.HasValue)
        {
            var patient = await db.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == req.PatientId.Value);
            if (patient is null)
                return NotFound(new { message = "المريض غير موجود" });

            values["PatientName"] = string.Join(" ", new[] { patient.FirstName, patient.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
            values["PatientFileNumber"] = patient.PatientNumber;
            values["PatientPhone"] = patient.Phone;
        }

        if (req.Values is not null)
        {
            foreach (var (key, value) in req.Values)
            {
                if (!string.IsNullOrWhiteSpace(key))
                    values[key.Trim()] = value;
            }
        }

        var content = template.Content;
        foreach (var (key, value) in values)
            content = content.Replace($"{{{{{key}}}}}", value ?? "", StringComparison.OrdinalIgnoreCase);

        return Ok(new
        {
            template.Id,
            template.Title,
            template.Category,
            template.RequiresSignature,
            Content = content,
            Values = values
        });
    }

    private static string? ValidateRequest(UpsertDocumentTemplateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return "اسم القالب مطلوب";
        if (req.Title.Length > 160)
            return "اسم القالب يجب ألا يتجاوز 160 حرفاً";
        if (string.IsNullOrWhiteSpace(req.Content))
            return "محتوى القالب مطلوب";
        if (req.Content.Length > 12000)
            return "محتوى القالب كبير جداً";
        return null;
    }

    private static string NormalizeCategory(string? category) =>
        string.IsNullOrWhiteSpace(category) ? "general" : category.Trim().ToLowerInvariant();

    private async Task<string?> GetSettingValueAsync(string key) =>
        await db.Settings
            .AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

    private async Task<List<DocumentTemplateDto>> LoadTemplatesAsync()
    {
        var raw = await db.Settings
            .AsNoTracking()
            .Where(s => s.Key == SettingKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(raw))
            return DefaultTemplates();

        try
        {
            return JsonSerializer.Deserialize<List<DocumentTemplateDto>>(raw, JsonOptions) ?? DefaultTemplates();
        }
        catch (JsonException)
        {
            return DefaultTemplates();
        }
    }

    private async Task SaveTemplatesAsync(List<DocumentTemplateDto> templates)
    {
        var setting = await db.Settings.FirstOrDefaultAsync(s => s.Key == SettingKey);
        if (setting is null)
        {
            setting = new Setting
            {
                Key = SettingKey,
                Category = Category
            };
            db.Settings.Add(setting);
        }

        setting.Value = JsonSerializer.Serialize(templates, JsonOptions);
        setting.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private static List<DocumentTemplateDto> DefaultTemplates() =>
    [
        new()
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Title = "موافقة علاج عامة",
            Category = "consent",
            Description = "نموذج موافقة مختصر يمكن تخصيصه لكل مريض.",
            Content = """
                أقر أنا {{PatientName}} رقم ملف {{PatientFileNumber}} بأن الطبيب شرح لي خطة العلاج والمخاطر المتوقعة والبدائل المتاحة.

                التاريخ: {{Today}}
                المركز: {{ClinicName}}
                التوقيع: ____________________
                """,
            RequiresSignature = true,
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        },
        new()
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Title = "تعليمات بعد العلاج",
            Category = "instructions",
            Description = "تعليمات عامة بعد الجلسة العلاجية.",
            Content = """
                عزيزي/عزيزتي {{PatientName}},

                يرجى الالتزام بالتعليمات التي شرحها الطبيب، ومراجعة المركز عند حدوث ألم شديد أو تورم غير طبيعي.

                {{ClinicName}} — {{Today}}
                """,
            RequiresSignature = false,
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        }
    ];
}
