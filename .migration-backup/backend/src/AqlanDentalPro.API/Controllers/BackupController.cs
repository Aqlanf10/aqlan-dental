using System.Text;
using System.Text.Json;
using System.Data;
using AqlanDentalPro.API.Services;
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
public class BackupController(AppDbContext db, IWebHostEnvironment env) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

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

        // Count total records in key tables
        var patientCount = await db.Patients.CountAsync();
        var appointmentCount = await db.Appointments.CountAsync();
        var visitCount = await db.Visits.CountAsync();
        var paymentCount = await db.Payments.CountAsync();

        return Ok(new
        {
            lastBackup = lastBackup != null ? new
            {
                lastBackup.StartedAt,
                lastBackup.CompletedAt,
                type = lastBackup.Type.ToString(),
                sizeMB = lastBackup.SizeBytes.HasValue
                    ? Math.Round(lastBackup.SizeBytes.Value / 1048576.0, 2)
                    : (double?)null,
            } : null,
            totalBackups,
            failedBackups,
            totalSizeMB = Math.Round(totalSize / 1048576.0, 2),
            filesCount = new
            {
                photos = photoCount,
                radiographs = radiographCount,
                total = photoCount + radiographCount,
            },
            recordCounts = new
            {
                patients = patientCount,
                appointments = appointmentCount,
                visits = visitCount,
                payments = paymentCount,
            },
        });
    }

    /// <summary>
    /// Trigger manual database backup — exports all data as JSON and saves to file
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
            var backupData = await ExportAllDataAsync();
            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(backupData, JsonOptions);

            // SEC-08: Encrypt the backup before writing to disk. Backups contain full PHI
            // (patient names, phones, DOBs, salaries). Plaintext on disk = any filesystem
            // exposure leaks everything. Encryption uses AES-GCM with a key from
            // BACKUP_ENCRYPTION_KEY env var. In Development without a key, falls back to
            // plaintext with a warning; in Production, missing key throws.
            byte[] fileBytes;
            bool isEncrypted;
            try
            {
                var key = BackupEncryption.LoadKeyFromEnvironment();
                fileBytes = BackupEncryption.Encrypt(jsonBytes, key);
                isEncrypted = true;
            }
            catch (InvalidOperationException) when (!env.IsProduction())
            {
                // Dev fallback: no encryption key set — write plaintext for convenience.
                fileBytes = jsonBytes;
                isEncrypted = false;
            }

            var sizeBytes = fileBytes.Length;

            // Save backup file to disk
            var backupDir = BackupStorage.GetDirectory(env);
            Directory.CreateDirectory(backupDir);
            var fileName = isEncrypted
                ? $"backup_db_{DateTime.UtcNow:yyyyMMdd_HHmmss}.enc"
                : $"backup_db_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(backupDir, fileName);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            record.Status = BackupStatus.Completed;
            record.CompletedAt = DateTime.UtcNow;
            record.SizeBytes = sizeBytes;
            record.FilePath = fileName;
            // Record encryption status in ErrorMessage field (repurposed) to avoid a migration.
            // Format: "encrypted" or "plaintext-dev".
            record.ErrorMessage = isEncrypted ? "encrypted" : "plaintext-dev";

            await db.SaveChangesAsync();

            return Ok(new
            {
                record.Id,
                status = record.Status.ToString(),
                record.StartedAt,
                record.CompletedAt,
                sizeMB = Math.Round(sizeBytes / 1048576.0, 2),
                fileName,
                recordCounts = backupData.TryGetValue("counts", out var c) ? c : null,
                message = "تم إنشاء النسخة الاحتياطية بنجاح",
            });
        }
        catch (Exception ex)
        {
            record.Status = BackupStatus.Failed;
            record.CompletedAt = DateTime.UtcNow;
            record.ErrorMessage = ex.Message?.Length > 500 ? ex.Message[..500] : ex.Message;
            await db.SaveChangesAsync();

            return StatusCode(500, new { message = "فشل النسخ الاحتياطي. راجع سجل النسخ الاحتياطية لمعرفة التفاصيل." });
        }
    }

    /// <summary>
    /// Download a backup file by ID
    /// </summary>
    [HttpGet("download/{id:guid}")]
    public async Task<IActionResult> DownloadBackup(Guid id)
    {
        var record = await db.BackupRecords.FindAsync(id);
        if (record is null)
            return NotFound(new { message = "سجل النسخ الاحتياطي غير موجود" });

        if (record.Status != BackupStatus.Completed || string.IsNullOrWhiteSpace(record.FilePath))
            return BadRequest(new { message = "النسخة الاحتياطية غير متاحة للتحميل" });

        var backupDir = BackupStorage.GetDirectory(env);
        var filePath = Path.Combine(backupDir, record.FilePath);

        if (!System.IO.File.Exists(filePath))
            return NotFound(new { message = "ملف النسخة الاحتياطية غير موجود على الخادم" });

        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);

        // SEC-08: Decrypt the backup before returning it to the admin. The on-disk format is
        // encrypted (.enc files) or plaintext-dev (.json files created in Dev without a key).
        byte[] downloadBytes;
        var isEncrypted = record.FilePath.EndsWith(".enc", StringComparison.OrdinalIgnoreCase)
                          || record.ErrorMessage == "encrypted";
        if (isEncrypted)
        {
            try
            {
                var key = BackupEncryption.LoadKeyFromEnvironment();
                downloadBytes = BackupEncryption.Decrypt(fileBytes, key);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "فشل فك تشفير النسخة الاحتياطية — تحقق من إعداد BACKUP_ENCRYPTION_KEY" });
            }
        }
        else
        {
            // Plaintext (Dev fallback) — return as-is.
            downloadBytes = fileBytes;
        }

        var contentType = "application/json";
        // Return a .json download name regardless of on-disk extension so the admin gets a
        // readable file.
        var downloadName = Path.GetFileNameWithoutExtension(record.FilePath) + ".json";

        return File(downloadBytes, contentType, downloadName);
    }

    /// <summary>
    /// Validate a backup file for restore (dry-run only — no actual restore for safety)
    /// </summary>
    [HttpPost("validate/{id:guid}")]
    public async Task<IActionResult> ValidateBackup(Guid id)
    {
        var record = await db.BackupRecords.FindAsync(id);
        if (record is null)
            return NotFound(new { message = "سجل النسخة الاحتياطي غير موجود" });

        if (record.Status != BackupStatus.Completed || string.IsNullOrWhiteSpace(record.FilePath))
            return BadRequest(new { message = "النسخة الاحتياطية غير متاحة" });

        var backupDir = BackupStorage.GetDirectory(env);
        var filePath = Path.Combine(backupDir, record.FilePath);

        if (!System.IO.File.Exists(filePath))
            return NotFound(new { message = "ملف النسخة الاحتياطية غير موجود على الخادم" });

        try
        {
            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var isEncrypted = record.FilePath.EndsWith(".enc", StringComparison.OrdinalIgnoreCase)
                              || record.ErrorMessage == "encrypted";
            var jsonBytes = isEncrypted
                ? BackupEncryption.Decrypt(fileBytes, BackupEncryption.LoadKeyFromEnvironment())
                : fileBytes;
            using var doc = JsonDocument.Parse(jsonBytes);
            var root = doc.RootElement;

            var tables = new List<string>();
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    tables.Add($"{prop.Name}: {prop.Value.GetArrayLength()} records");
                }
            }

            return Ok(new
            {
                record.Id,
                record.StartedAt,
                record.CompletedAt,
                sizeMB = record.SizeBytes.HasValue ? Math.Round(record.SizeBytes.Value / 1048576.0, 2) : 0,
                tables,
                message = "النسخة الاحتياطية صالحة. للاستعادة، تواصل مع مسؤول النظام.",
            });
        }
        catch (JsonException ex)
        {
            return BadRequest(new { message = "ملف النسخة الاحتياطية تالف أو غير صالح" });
        }
    }

    /// <summary>
    /// Get data export counts for backup purposes
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> ExportData([FromQuery] string? tables)
    {
        var result = new Dictionary<string, int>();

        if (string.IsNullOrWhiteSpace(tables) || tables.Contains("patients"))
            result["Patients"] = await db.Patients.IgnoreQueryFilters().CountAsync();
        if (string.IsNullOrWhiteSpace(tables) || tables.Contains("appointments"))
            result["Appointments"] = await db.Appointments.IgnoreQueryFilters().CountAsync();
        if (string.IsNullOrWhiteSpace(tables) || tables.Contains("visits"))
            result["Visits"] = await db.Visits.IgnoreQueryFilters().CountAsync();
        if (string.IsNullOrWhiteSpace(tables) || tables.Contains("payments"))
            result["Payments"] = await db.Payments.IgnoreQueryFilters().CountAsync();
        if (string.IsNullOrWhiteSpace(tables) || tables.Contains("employees"))
            result["Employees"] = await db.Employees.IgnoreQueryFilters().CountAsync();
        if (string.IsNullOrWhiteSpace(tables) || tables.Contains("contracts"))
            result["Contracts"] = await db.Contracts.IgnoreQueryFilters().CountAsync();
        if (string.IsNullOrWhiteSpace(tables) || tables.Contains("invoices"))
            result["Invoices"] = await db.Invoices.IgnoreQueryFilters().CountAsync();

        return Ok(new
        {
            exportDate = DateTime.UtcNow,
            recordCounts = result,
        });
    }

    /// <summary>
    /// Delete backup record and its file
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var record = await db.BackupRecords.FindAsync(id);
        if (record is null)
            return NotFound(new { message = "سجل النسخ الاحتياطي غير موجود" });

        // Delete the actual file from disk
        if (!string.IsNullOrWhiteSpace(record.FilePath))
        {
            var backupDir = BackupStorage.GetDirectory(env);
            var filePath = Path.Combine(backupDir, record.FilePath);
            if (System.IO.File.Exists(filePath))
            {
                try { System.IO.File.Delete(filePath); } catch { /* ignore */ }
            }
        }

        record.IsActive = false;
        record.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف سجل النسخ الاحتياطي وملفه" });
    }

    /// <summary>
    /// Export all data as a serializable dictionary (uses anonymous types — no dynamic)
    /// </summary>
    private async Task<Dictionary<string, object>> ExportAllDataAsync()
    {
        var data = new Dictionary<string, object>
        {
            ["exportDate"] = DateTime.UtcNow,
            ["version"] = "1.1",
        };

        var counts = new Dictionary<string, int>();

        // Use PostgreSQL's row-to-JSON conversion instead of EF projections. A backup is
        // most important while the production schema is behind the current application
        // model; EF would otherwise request newly mapped columns that do not exist yet
        // and make it impossible to take the pre-migration backup.
        var tables = new (string Table, string Key)[]
        {
            ("Patients", "patients"),
            ("Appointments", "appointments"),
            ("Visits", "visits"),
            ("Payments", "payments"),
            ("Contracts", "contracts"),
            ("Invoices", "invoices"),
            ("Employees", "employees"),
            ("Doctors", "doctors"),
            ("Branches", "branches"),
            ("ClinicServices", "clinicServices"),
            ("OperationalExpenses", "operationalExpenses"),
            ("Treasuries", "treasuries"),
            ("CashFlowTransactions", "cashFlowTransactions"),
            ("JournalEntries", "journalEntries"),
            ("JournalLines", "journalLines"),
            ("Suppliers", "suppliers"),
            ("SupplierBills", "supplierBills"),
            ("LabOrders", "labOrders"),
            ("LabPayables", "labPayables"),
        };

        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            foreach (var (table, key) in tables)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"""
                    SELECT COALESCE(jsonb_agg(to_jsonb(row_data)), '[]'::jsonb)::text
                    FROM "{table}" AS row_data
                    """;

                var json = Convert.ToString(await command.ExecuteScalarAsync()) ?? "[]";
                using var document = JsonDocument.Parse(json);
                var rows = document.RootElement.Clone();
                data[key] = rows;
                counts[key] = rows.GetArrayLength();
            }
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }

        data["counts"] = counts;
        return data;
    }
}
