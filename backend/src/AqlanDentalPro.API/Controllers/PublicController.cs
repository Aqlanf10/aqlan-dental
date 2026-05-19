using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.EntityFrameworkCore;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api")]
[EnableCors("AllowPublicApi")]
public class PublicController : ControllerBase
{
    private readonly AppDbContext _db;
    public PublicController(AppDbContext db) => _db = db;

    /// <summary>إعدادات الموقع العامة (للصفحة الرئيسية)</summary>
    [HttpGet("public/website-settings")]
    [AllowAnonymous]
    public async Task<IActionResult> GetWebsiteSettings()
    {
        var websiteKeys = new[]
        {
            "website.clinicName", "website.heroTitle", "website.heroSubtitle",
            "website.marketingSlogan", "website.aboutText", "website.phone",
            "website.whatsapp", "website.address", "website.workingHours",
            "website.facebook", "website.instagram", "website.logoUrl",
            "website.heroImageUrl", "website.servicesSectionTitle",
            "website.bookingButtonText", "website.whatsappButtonText"
        };

        var settings = await _db.Settings
            .AsNoTracking()
            .Where(s => s.Category == "website")
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        // Build response with fallback defaults
        var result = new Dictionary<string, string?>
        {
            ["clinicName"]           = settings.GetValueOrDefault("website.clinicName")           ?? "مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان",
            ["heroTitle"]            = settings.GetValueOrDefault("website.heroTitle")            ?? "ابتسامة تجمع بين دقة العلم ولمسة الفن",
            ["heroSubtitle"]         = settings.GetValueOrDefault("website.heroSubtitle")         ?? "مركز الدكتور عقلان الكامل يقدم رعاية متكاملة في تقويم وزراعة وتجميل الأسنان، مع تشخيص دقيق وخطط علاج واضحة ومتابعة مستمرة لكل حالة.",
            ["marketingSlogan"]      = settings.GetValueOrDefault("website.marketingSlogan")      ?? "قيادة طبية… وابتسامة بثقة",
            ["aboutText"]            = settings.GetValueOrDefault("website.aboutText")            ?? "يقدم مركز الدكتور عقلان الكامل خدمات تخصصية شاملة في تقويم وزراعة وتجميل الأسنان، معتمدين على تشخيص دقيق، وخطط علاج واضحة، ومتابعة مستمرة للحالات للمساعدة في الوصول إلى نتائج علاجية دقيقة ومناسبة لكل حالة.",
            ["phone"]                = settings.GetValueOrDefault("website.phone")                ?? "04-253028",
            ["whatsapp"]             = settings.GetValueOrDefault("website.whatsapp")             ?? "967770245745",
            ["address"]              = settings.GetValueOrDefault("website.address")              ?? "تعز، اليمن — شارع التحرير الأعلى",
            ["workingHours"]         = settings.GetValueOrDefault("website.workingHours")         ?? "السبت – الخميس: 8 ص – 8 م",
            ["facebook"]             = settings.GetValueOrDefault("website.facebook")             ?? "",
            ["instagram"]            = settings.GetValueOrDefault("website.instagram")            ?? "",
            ["logoUrl"]              = settings.GetValueOrDefault("website.logoUrl")              ?? "",
            ["heroImageUrl"]         = settings.GetValueOrDefault("website.heroImageUrl")         ?? "",
            ["servicesSectionTitle"] = settings.GetValueOrDefault("website.servicesSectionTitle") ?? "حلول طبية متكاملة لابتسامة صحية وواثقة",
            ["bookingButtonText"]    = settings.GetValueOrDefault("website.bookingButtonText")    ?? "احجز موعدك الآن",
            ["whatsappButtonText"]   = settings.GetValueOrDefault("website.whatsappButtonText")   ?? "تواصل عبر الواتساب",
        };

        return Ok(result);
    }

    [HttpGet("public/queue")]
    [AllowAnonymous]
    public async Task<IActionResult> GetQueue()
    {
        // H6 FIX: Use UTC for consistent timezone handling
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var items = await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.AppointmentDate == today && a.Status != AppointmentStatus.Cancelled)
            .OrderBy(a => a.StartTime)
            .Select(a => new
            {
                a.Id,
                // H6 FIX: Safe substring — prevents crash if LastName is null or empty
                PatientDisplayName = a.Patient.FirstName + " " + (string.IsNullOrEmpty(a.Patient.LastName) ? "" : a.Patient.LastName.Substring(0, 1)) + ".",
                AppointmentType = a.AppointmentType ?? "—",
                StartTime = a.StartTime.ToString(@"hh\:mm"),
                // H6 FIX: Safe EndTime — prevents crash if EndTime is null
                EndTime = a.EndTime.ToString(@"hh\:mm"),
                DoctorName = (string?)(a.Doctor != null ? a.Doctor.Name : null),
                DoctorColor = (string?)(a.Doctor != null ? a.Doctor.Color : null),
                Status = a.Status.ToString()
            })
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>الخدمات المتاحة للحجز العام</summary>
    [HttpGet("public/booking-services")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBookingServices()
    {
        var services = await _db.ClinicServices
            .AsNoTracking()
            .Where(s => s.ShowInBooking && s.IsActive)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.ArabicName)
            .Select(s => new { s.Id, s.ArabicName, s.EnglishName, s.Code, s.DefaultDurationMinutes, Category = s.Category.ToString() })
            .ToListAsync();
        return Ok(services);
    }

    /// <summary>قائمة الأطباء العامة (للحجز العام)</summary>
    [HttpGet("public/doctors")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDoctors()
    {
        var doctors = await _db.Doctors
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name, d.Specialty, d.Color, d.AvatarInitials })
            .ToListAsync();
        return Ok(doctors);
    }

    /// <summary>الأوقات المتاحة لطبيب في يوم محدد (للحجز العام)</summary>
    [HttpGet("public/doctors/{doctorId:guid}/slots")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailableSlots(Guid doctorId, [FromQuery] string date)
    {
        if (!DateOnly.TryParse(date, out var dateOnly))
            return BadRequest(new { message = "تنسيق التاريخ غير صحيح" });

        if (dateOnly < DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequest(new { message = "لا يمكن الحجز في تاريخ سابق" });

        var dotnetDow = (int)dateOnly.DayOfWeek;
        var schedule = await _db.DoctorSchedules
            .FirstOrDefaultAsync(ds => ds.DoctorId == doctorId && ds.DayOfWeek == dotnetDow
                && ds.IsActive && ds.IsWorking);

        if (schedule is null)
            return Ok(new { available = false, slots = Array.Empty<string>() });

        var bookedSlots = await _db.Appointments
            .Where(a => a.DoctorId == doctorId && a.AppointmentDate == dateOnly && a.IsActive
                && a.Status != AppointmentStatus.Cancelled
                && a.Status != AppointmentStatus.NoShow)
            .Select(a => new { a.StartTime, a.EndTime })
            .ToListAsync();

        var slots = new List<string>();
        var current = schedule.StartTime;
        var slotMinutes = schedule.SlotDurationMinutes;

        while (current.AddMinutes(slotMinutes) <= schedule.EndTime)
        {
            if (schedule.BreakStart.HasValue && schedule.BreakEnd.HasValue &&
                current >= schedule.BreakStart.Value && current < schedule.BreakEnd.Value)
            {
                current = schedule.BreakEnd.Value;
                continue;
            }

            var slotEnd = current.AddMinutes(slotMinutes);
            var isBooked = bookedSlots.Any(b => current < b.EndTime && slotEnd > b.StartTime);
            if (!isBooked) slots.Add(current.ToString("HH:mm"));
            current = current.AddMinutes(slotMinutes);
        }

        return Ok(new { available = true, slots, slotDuration = schedule.SlotDurationMinutes });
    }
}
