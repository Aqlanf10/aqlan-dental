using System.IO;

using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Caches the clinic logo (<c>Fonts/logo.png</c>) as a byte array so that PDF
/// generators do not hit the filesystem on every render (CLIN-12).
/// </summary>
/// <remarks>
/// The logo file is shipped with the application and never changes at runtime, so
/// a one-time lazy read is correct and preferable to per-request async I/O. The
/// first call reads the file (small PNG, sub-millisecond on warm cache); every
/// subsequent call returns the cached <see cref="byte"/>[] reference. When no
/// logo file exists, callers receive <c>false</c> from <see cref="TryGetLogo"/>
/// and the report renders with the clinic-name-only fallback (no broken image).
/// </remarks>
public static class PdfLogoCache
{
    private static readonly Lazy<byte[]?> _logo = new(LoadLogo, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// The cached logo bytes, or <c>null</c> when no logo file was found.
    /// </summary>
    public static byte[]? LogoBytes => _logo.Value;

    /// <summary>
    /// Tries to get the cached logo bytes. Returns <c>false</c> (with an empty
    /// out parameter) when the logo file is absent — callers must skip the
    /// <c>Image(...)</c> element in that case so QuestPDF does not throw.
    /// </summary>
    public static bool TryGetLogo(out byte[] bytes)
    {
        // CORE-REQ-006 — prefer the logo the clinic configured over the one compiled into the
        // build. Several report generators are static and hold no DbContext, so they cannot
        // resolve it themselves; the snapshot below is refreshed by ResolveAsync, which runs
        // whenever clinic identity is resolved. Worst case a document renders once with the
        // shipped logo before any identity resolution has happened in the process — which is
        // still better than the previous behaviour, where the configured logo never appeared
        // on any PDF at all.
        byte[]? configured;
        lock (CacheLock) configured = _configuredBytes;

        if (configured is { Length: > 0 })
        {
            bytes = configured;
            return true;
        }

        var b = _logo.Value;
        if (b is null || b.Length == 0)
        {
            bytes = Array.Empty<byte>();
            return false;
        }

        bytes = b;
        return true;
    }

    /// <summary>
    /// CORE-REQ-006 — the logo an administrator actually configured, falling back to the one
    /// shipped with the build.
    ///
    /// <para>
    /// The clinic can upload a logo at <c>/settings/website</c>, and it appears on the public
    /// site immediately. Every PDF, however, kept printing <c>Fonts/logo.png</c> — the file
    /// compiled into the deployment — because that was the only source this cache knew, and it
    /// was held in a <see cref="Lazy{T}"/> that never re-reads. So the clinic's own reports
    /// carried a logo the owner had already replaced, and no amount of re-uploading changed it.
    /// </para>
    /// <para>
    /// Resolution order is: the configured <c>website.logoUrl</c> upload, then the shipped
    /// file. Bytes are cached against the setting's value, so an unchanged configuration costs
    /// no file I/O per render — the property <c>CLIN-12</c> introduced this cache for — while a
    /// changed one takes effect on the next document without a restart.
    /// </para>
    /// </summary>
    public static async Task<byte[]?> ResolveAsync(AppDbContext db, CancellationToken ct = default)
    {
        string configured;
        try
        {
            configured = await db.Settings
                .Where(setting => setting.Key == LogoSettingKey)
                .Select(setting => setting.Value)
                .FirstOrDefaultAsync(ct) ?? "";
        }
        catch
        {
            // A settings read must never be what stops a receipt printing.
            return LogoBytes;
        }

        configured = configured.Trim();
        if (configured.Length == 0) return LogoBytes;

        lock (CacheLock)
        {
            if (_configuredKey == configured) return _configuredBytes ?? LogoBytes;
        }

        var bytes = ReadUploadedFile(configured);

        lock (CacheLock)
        {
            _configuredKey = configured;
            _configuredBytes = bytes;
        }

        // A configured-but-unreadable logo falls back rather than printing nothing: a missing
        // file is a broken upload, not an instruction to strip identity from the document.
        return bytes ?? LogoBytes;
    }

    private const string LogoSettingKey = "website.logoUrl";

    private static readonly object CacheLock = new();
    private static string? _configuredKey;
    private static byte[]? _configuredBytes;

    /// <summary>
    /// Resolves an uploaded logo URL to bytes, using the same directory priority and the same
    /// path-traversal guard as every other upload read: UPLOADS_PATH, then wwwroot/uploads,
    /// then the temp fallback.
    /// </summary>
    private static byte[]? ReadUploadedFile(string url)
    {
        var marker = url.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase);
        if (marker < 0) return null;

        var fileName = Uri.UnescapeDataString(url[(marker + "/uploads/".Length)..]);
        if (fileName.Length == 0 || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
            return null;

        var dirs = new List<string>();
        var envPath = Environment.GetEnvironmentVariable("UPLOADS_PATH");
        if (!string.IsNullOrWhiteSpace(envPath)) dirs.Add(envPath);
        dirs.Add(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads"));
        dirs.Add(Path.Combine(Path.GetTempPath(), "aqlan-uploads"));

        foreach (var dir in dirs)
        {
            try
            {
                var path = Path.Combine(dir, fileName);
                if (File.Exists(path)) return File.ReadAllBytes(path);
            }
            catch
            {
                // Invalid path characters etc. — try the next candidate.
            }
        }

        return null;
    }

    /// <summary>Test seam: forgets the configured-logo cache.</summary>
    public static void ResetConfiguredCache()
    {
        lock (CacheLock)
        {
            _configuredKey = null;
            _configuredBytes = null;
        }
    }

    private static byte[]? LoadLogo()
    {
        var paths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Fonts", "logo.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "Fonts", "logo.png"),
        };

        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                    return File.ReadAllBytes(path);
            }
            catch
            {
                // Invalid path / IO error — try next candidate.
            }
        }

        return null;
    }
}
