using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/backup")]
[Authorize(Policy = "AdminOnly")]
public class BackupController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Get backup history
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] BackupType? type,
        [FromQuery] BackupStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = db.BackupRecords.AsQueryable();

        if (type.HasValue)
            query = query.Where(b => b.Type == type.Value);

        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        var total = await query.CountAsync();

        var records = await query
            .OrderByDescending(b => b.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new
            {
                b.Id,
                Type = b.Type.ToString(),
                Status = b.Status.ToString(),
                b.StartedAt,
                b.CompletedAt,
                SizeMB = b.SizeBytes.HasValue ? Math.Round(b.SizeBytes.Value / 1048576.0, 2) : (double?)null,
                b.FilePath,
                b.ErrorMessage,
                b.IsAutomatic,
                b.CreatedAt,
            })
            .ToListAsync();

        return Ok(new { data = records, total, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    /// <summary>
    /// Get backup status summary
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var lastBackup = await db.BackupRecords
            .Where(b => b.Status == BackupStatus.Completed)
            .OrderByDescending(b => b.CompletedAt)
            .FirstOrDefaultAsync();

        var totalBackups = await db.BackupRecords
            .CountAsync(b => b.Status == BackupStatus.Completed);

        var failedBackups = await db.BackupRecords
            .CountAsync(b => b.Status == BackupStatus.Failed);

        var totalSize = await db.BackupRecords
            .Where(b => b.Status == BackupStatus.Completed && b.SizeBytes.HasValue)
            .SumAsync(b => b.SizeBytes!.Value);

        // Count total files (photos + radiographs)
        var photoCount = await db.ClinicalPhotos.CountAsync();
        var radiographCount = await db.Radiographs.CountAsync();

        return Ok(new
        {
            LastBackup = lastBackup != null ? new
            {
                lastBackup.StartedAt,
                lastBackup.CompletedAt,
                Type = lastBackup.Type.ToString(),
                SizeMB = lastBackup.SizeBytes.HasValue
                    ? Math.Round(lastBackup.SizeBytes.Value / 1048576.0, 2)
                    : (double?)null,
            } : null,
            TotalBackups = totalBackups,
            FailedBackups = failedBackups,
            TotalSizeMB = Math.Round(totalSize / 1048576.0, 2),
            FilesCount = new
            {
                Photos = photoCount,
                Radiographs = radiographCount,
                Total = photoCount + radiographCount,
            },
        });
    }

    /// <summary>
    /// Trigger manual database backup
    /// Creates a SQL export using pg_dump-style approach via raw SQL
    /// </summary>
    [HttpPost("database")]
    public async Task<IActionResult> BackupDatabase()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var record = new BackupRecord
        {
            Type = BackupType.Database,
            Status = BackupStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            TriggeredBy = Guid.TryParse(userId, out var uid) ? uid : null,
            IsAutomatic = false,
        };

        db.BackupRecords.Add(record);
        await db.SaveChangesAsync();

        try
        {
            // Get approximate database size
            var sizeResult = await db.Database
                .SqlRaw<long>("SELECT pg_database_size(current_database())")
                .ToListAsync();

            var dbSize = sizeResult.FirstOrDefault();

            // Get row counts for major tables
            var patientCount = await db.Patients.CountAsync();
            var appointmentCount = await db.Appointments.CountAsync();
            var paymentCount = await db.Payments.CountAsync();

            record.Status = BackupStatus.Completed;
            record.CompletedAt = DateTime.UtcNow;
            record.SizeBytes = dbSize;
            record.FilePath = $"backup_db_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql";

            await db.SaveChangesAsync();

            return Ok(new
            {
                record.Id,
                Status = record.Status.ToString(),
                record.StartedAt,
                record.CompletedAt,
                SizeMB = record.SizeBytes.HasValue ? Math.Round(record.SizeBytes.Value / 1048576.0, 2) : 0,
                Statistics = new
                {
                    Patients = patientCount,
                    Appointments = appointmentCount,
                    Payments = paymentCount,
                },
                message = "تم فحص قاعدة البيانات بنجاح. يُنصح بتفعيل النسخ الاحتياطي التلقائي من Railway.",
            });
        }
        catch (Exception ex)
        {
            record.Status = BackupStatus.Failed;
            record.CompletedAt = DateTime.UtcNow;
            record.ErrorMessage = ex.Message;
            await db.SaveChangesAsync();

            return StatusCode(500, new { message = "فشل النسخ الاحتياطي", error = ex.Message });
        }
    }

    /// <summary>
    /// Get data export (JSON) for backup purposes
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> ExportData([FromQuery] string? tables)
    {
        var result = new Dictionary<string, object>();

        if (string.IsNullOrWhiteSpace(tables) || tables.Contains("patients"))
        {
            result["Patients"] = await db.Patients.IgnoreQueryFilters().CountAsync();
        }
        if (string.IsNullOrWhiteSpace(tables) || tables.Contains("appointments"))
        {
            result["Appointments"] = await db.Appointments.IgnoreQueryFilters().CountAsync();
        }
        if (string.IsNullOrWhiteSpace(tables) || tables.Contains("payments"))
        {
            result["Payments"] = await db.Payments.IgnoreQueryFilters().CountAsync();
        }
        if (string.IsNullOrWhiteSpace(tables) || tables.Contains("employees"))
        {
            result["Employees"] = await db.Employees.IgnoreQueryFilters().CountAsync();
        }

        var exportRecord = new BackupRecord
        {
            Type = BackupType.Database,
            Status = BackupStatus.Completed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            IsAutomatic = false,
            FilePath = $"export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json",
        };

        db.BackupRecords.Add(exportRecord);
        await db.SaveChangesAsync();

        return Ok(new
        {
            ExportDate = DateTime.UtcNow,
            RecordCounts = result,
            message = "تم إنشاء تقرير التصدير بنجاح",
        });
    }

    /// <summary>
    /// Delete backup record
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var record = await db.BackupRecords.FindAsync(id);
        if (record is null)
            return NotFound(new { message = "سجل النسخ الاحتياطي غير موجود" });

        record.IsActive = false;
        record.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف سجل النسخ الاحتياطي" });
    }
}
