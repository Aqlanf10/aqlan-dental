using System.IO;

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
        var b = _logo.Value;
        if (b is null || b.Length == 0)
        {
            bytes = Array.Empty<byte>();
            return false;
        }

        bytes = b;
        return true;
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
