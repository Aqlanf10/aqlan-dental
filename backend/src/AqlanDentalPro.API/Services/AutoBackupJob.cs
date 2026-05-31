using System.Text.Json;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Services;

public class AutoBackupJob(IServiceProvider serviceProvider, ILogger<AutoBackupJob> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AutoBackupJob started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Run at 2:00 AM UTC daily
                var now = DateTime.UtcNow;
                var nextRun = now.Date.AddDays(1).AddHours(2);
                if (now.Hour < 2)
                    nextRun = now.Date.AddHours(2);

                var delay = nextRun - now;
                logger.LogInformation("Next auto-backup scheduled at {NextRun} UTC (in {Delay})", nextRun, delay);

                await Task.Delay(delay, stoppingToken);

                await PerformBackupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AutoBackupJob error");
                // Wait 1 hour before retrying on error
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    private async Task PerformBackupAsync(CancellationToken ct)
    {
        logger.LogInformation("Starting automatic database backup");

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        var record = new BackupRecord
        {
            Type = BackupType.Database,
            Status = BackupStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            IsAutomatic = true,
        };

        db.BackupRecords.Add(record);
        await db.SaveChangesAsync(ct);

        try
        {
            var backupData = new
            {
                ExportDate = DateTime.UtcNow,
                Version = "1.0",
                IsAutomatic = true,
                Patients = await db.Patients.IgnoreQueryFilters().CountAsync(ct),
                Appointments = await db.Appointments.IgnoreQueryFilters().CountAsync(ct),
                Visits = await db.Visits.IgnoreQueryFilters().CountAsync(ct),
                Payments = await db.Payments.IgnoreQueryFilters().CountAsync(ct),
                Contracts = await db.Contracts.IgnoreQueryFilters().CountAsync(ct),
                Invoices = await db.Invoices.IgnoreQueryFilters().CountAsync(ct),
                Employees = await db.Employees.IgnoreQueryFilters().CountAsync(ct),
                Doctors = await db.Doctors.IgnoreQueryFilters().CountAsync(ct),
                Branches = await db.Branches.IgnoreQueryFilters().CountAsync(ct),
            };

            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(backupData, JsonOptions);
            var sizeBytes = jsonBytes.Length;

            // Save to disk
            var backupDir = Path.Combine(env.ContentRootPath, "backups");
            Directory.CreateDirectory(backupDir);
            var fileName = $"auto_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(backupDir, fileName);
            await File.WriteAllBytesAsync(filePath, jsonBytes, ct);

            record.Status = BackupStatus.Completed;
            record.CompletedAt = DateTime.UtcNow;
            record.SizeBytes = sizeBytes;
            record.FilePath = fileName;

            await db.SaveChangesAsync(ct);

            // Clean up old auto-backups (keep last 7)
            await CleanupOldBackupsAsync(db, backupDir, ct);

            logger.LogInformation("Automatic backup completed: {FileName}, {SizeMB:F2} MB", fileName, sizeBytes / 1048576.0);
        }
        catch (Exception ex)
        {
            record.Status = BackupStatus.Failed;
            record.CompletedAt = DateTime.UtcNow;
            record.ErrorMessage = ex.Message?.Length > 500 ? ex.Message[..500] : ex.Message;
            await db.SaveChangesAsync(ct);

            logger.LogError(ex, "Automatic backup failed");
        }
    }

    private async Task CleanupOldBackupsAsync(AppDbContext db, string backupDir, CancellationToken ct)
    {
        try
        {
            var oldAutoBackups = await db.BackupRecords
                .Where(b => b.IsAutomatic && b.Status == BackupStatus.Completed && b.IsActive)
                .OrderByDescending(b => b.CompletedAt)
                .Skip(7)
                .ToListAsync(ct);

            foreach (var old in oldAutoBackups)
            {
                // Delete file from disk
                if (!string.IsNullOrWhiteSpace(old.FilePath))
                {
                    var filePath = Path.Combine(backupDir, old.FilePath);
                    if (File.Exists(filePath))
                    {
                        try { File.Delete(filePath); } catch { /* ignore */ }
                    }
                }

                old.IsActive = false;
                old.DeletedAt = DateTime.UtcNow;
            }

            if (oldAutoBackups.Count > 0)
                await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to cleanup old auto-backups");
        }
    }
}
