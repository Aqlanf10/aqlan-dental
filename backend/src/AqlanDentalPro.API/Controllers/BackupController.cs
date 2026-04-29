using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.Diagnostics;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/backup")]
[Authorize(Policy = "AdminOnly")]
public class BackupController(IConfiguration configuration) : ControllerBase
{
    // POST /api/backup/database
    [HttpPost("database")]
    public async Task<IActionResult> BackupDatabase()
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
            return StatusCode(500, new { message = "سلسلة اتصال قاعدة البيانات غير موجودة" });

        // Parse connection string to extract parameters
        var (host, port, database, username, password) = ParseConnectionString(connectionString);

        var fileName = $"backup_{database}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql";
        var tempPath = Path.Combine(Path.GetTempPath(), fileName);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "pg_dump",
                Arguments = $"--host={host} --port={port} --dbname={database} --username={username} --no-password --format=plain --file=\"{tempPath}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            // Set PGPASSWORD environment variable for authentication
            startInfo.Environment["PGPASSWORD"] = password;

            using var process = Process.Start(startInfo);
            if (process is null)
                return StatusCode(500, new { message = "فشل في بدء عملية النسخ الاحتياطي" });

            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                if (error.Contains("command not found") || error.Contains("not recognized"))
                    return StatusCode(500, new { message = "أداة pg_dump غير متوفرة على الخادم" });

                return StatusCode(500, new { message = "فشل في إنشاء النسخة الاحتياطية", error });
            }

            if (!System.IO.File.Exists(tempPath))
                return StatusCode(500, new { message = "فشل في إنشاء ملف النسخة الاحتياطية" });

            var bytes = await System.IO.File.ReadAllBytesAsync(tempPath);

            // Clean up temp file
            try { System.IO.File.Delete(tempPath); } catch { /* ignore cleanup errors */ }

            return File(bytes, "application/sql", fileName);
        }
        catch (Win32Exception ex) when (ex.Message.Contains("cannot find") || ex.Message.Contains("not found"))
        {
            return StatusCode(500, new { message = "أداة pg_dump غير متوفرة على الخادم. يرجى تثبيت PostgreSQL client tools." });
        }
        catch (Exception ex)
        {
            // Clean up temp file on error
            try { if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath); } catch { }

            return StatusCode(500, new { message = "حدث خطأ أثناء إنشاء النسخة الاحتياطية", error = ex.Message });
        }
    }

    private static (string host, string port, string database, string username, string password) ParseConnectionString(string connectionString)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].ToLowerInvariant(), p => p[1]);

        var host = parts.GetValueOrDefault("host", "localhost");
        var port = parts.GetValueOrDefault("port", "5432");
        var database = parts.GetValueOrDefault("database", "aqlandental");
        var username = parts.GetValueOrDefault("username", "postgres");
        var password = parts.GetValueOrDefault("password", "");

        return (host, port, database, username, password);
    }
}
