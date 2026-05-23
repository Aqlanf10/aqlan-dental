using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Email statistics and monitoring. Admin-only access.
/// Provides daily email counts, failure tracking, and limit alerts.
/// </summary>
[ApiController]
[Route("api/email-stats")]
[Authorize(Policy = "AdminOnly")]
public class EmailStatsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;

    public EmailStatsController(AppDbContext db, IEmailService emailService)
    {
        _db = db;
        _emailService = emailService;
    }

    /// <summary>
    /// Returns email statistics summary: daily count, limit, failure rate, recent emails.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetStats()
    {
        var todayStart = DateTime.UtcNow.Date;
        var yesterdayStart = todayStart.AddDays(-1);
        var weekStart = todayStart.AddDays(-7);

        // ── Today's stats ──
        var todaySent = await _db.EmailLogs.CountAsync(e => e.IsSent && e.CreatedAt >= todayStart);
        var todayFailed = await _db.EmailLogs.CountAsync(e => !e.IsSent && e.CreatedAt >= todayStart);
        var todayByCategory = await _db.EmailLogs
            .Where(e => e.CreatedAt >= todayStart)
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key, Sent = g.Count(x => x.IsSent), Failed = g.Count(x => !x.IsSent) })
            .ToListAsync();

        // ── Yesterday's stats (for comparison) ──
        var yesterdaySent = await _db.EmailLogs.CountAsync(e => e.IsSent && e.CreatedAt >= yesterdayStart && e.CreatedAt < todayStart);

        // ── Weekly stats ──
        var weekSent = await _db.EmailLogs.CountAsync(e => e.IsSent && e.CreatedAt >= weekStart);
        var weekFailed = await _db.EmailLogs.CountAsync(e => !e.IsSent && e.CreatedAt >= weekStart);

        // ── Resend daily limit info ──
        var dailyLimit = 100; // Resend free tier
        var limitPercentage = (int)Math.Round((double)todaySent / dailyLimit * 100);
        var isNearLimit = todaySent >= dailyLimit - 10;
        var isAtLimit = todaySent >= dailyLimit;

        // ── Recent emails (last 20) ──
        var recentEmails = await _db.EmailLogs
            .OrderByDescending(e => e.CreatedAt)
            .Take(20)
            .Select(e => new
            {
                e.Id,
                e.ToEmail,
                e.Subject,
                e.Category,
                e.Provider,
                e.IsSent,
                e.ErrorMessage,
                e.CreatedAt,
                e.RelatedEntityType,
                e.RelatedEntityId
            })
            .ToListAsync();

        return Ok(new
        {
            today = new
            {
                sent = todaySent,
                failed = todayFailed,
                byCategory = todayByCategory
            },
            yesterday = new { sent = yesterdaySent },
            week = new { sent = weekSent, failed = weekFailed },
            limit = new
            {
                dailyLimit,
                used = todaySent,
                remaining = Math.Max(0, dailyLimit - todaySent),
                percentage = limitPercentage,
                isNearLimit,
                isAtLimit
            },
            recentEmails
        });
    }

    /// <summary>
    /// Returns email statistics for a specific date range.
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var start = (fromDate ?? DateTime.UtcNow.AddDays(-7)).Date;
        var end = (toDate ?? DateTime.UtcNow).Date.AddDays(1);

        var query = _db.EmailLogs
            .Where(e => e.CreatedAt >= start && e.CreatedAt < end);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(e => e.Category == category);

        var total = await query.CountAsync();

        var emails = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                e.ToEmail,
                e.Subject,
                e.Category,
                e.Provider,
                e.IsSent,
                e.ErrorMessage,
                e.ExternalId,
                e.CreatedAt,
                e.RelatedEntityType,
                e.RelatedEntityId
            })
            .ToListAsync();

        return Ok(new
        {
            total,
            page,
            pageSize,
            emails
        });
    }
}
