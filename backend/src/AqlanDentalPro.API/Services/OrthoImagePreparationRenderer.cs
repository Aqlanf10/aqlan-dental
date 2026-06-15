using AqlanDentalPro.Domain.Entities;
using SkiaSharp;

namespace AqlanDentalPro.API.Services;

/// <summary>
/// Renders a non-destructive "prepared" copy of an orthodontic clinical photo by
/// baking the saved <see cref="OrthoImagePreparation"/> adjustments (crop region,
/// zoom, rotation, flips, brightness/contrast, aspect ratio) into a new image file.
/// The original upload is never modified. The result URL is stored in
/// <see cref="OrthoImagePreparation.PreparedImageUrl"/> and consumed by the PDF/PPTX
/// reports. Every failure mode returns <c>null</c> — a render must never block a save
/// or break report generation (the deck falls back to the original photo).
/// </summary>
public sealed class OrthoImagePreparationRenderer(ILogger<OrthoImagePreparationRenderer> logger)
{
    private const int MaxLongEdge = 1600;
    private const int JpegQuality = 88;

    /// <summary>
    /// Bakes the preparation into a new "/uploads/{guid}.jpg" file and returns its
    /// relative URL, or <c>null</c> when the original is unavailable/unreadable or
    /// rendering fails (caller keeps the previous PreparedImageUrl semantics).
    /// </summary>
    public string? Render(OrthoClinicalPhoto photo, OrthoImagePreparation prep)
    {
        try
        {
            var sourcePath = CephReportPdfGenerator.ResolveUploadFilePath(photo.PhotoUrl);
            if (sourcePath is null) return null;

            var bytes = File.ReadAllBytes(sourcePath);
            using var data = SKData.CreateCopy(bytes);
            using var source = DecodeRobust(data);
            if (source is null) return null;

            using var output = RenderToImage(source, prep);
            if (output is null) return null;

            using var encoded = output.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
            if (encoded is null) return null;

            var dir = ResolveUploadsDirectory();
            var fileName = $"prepared-{Guid.NewGuid():N}.jpg";
            File.WriteAllBytes(Path.Combine(dir, fileName), encoded.ToArray());
            return $"/uploads/{fileName}";
        }
        catch (Exception ex)
        {
            // Best-effort: a render failure must not surface to the user or block the save.
            logger.LogWarning(ex, "Failed to render prepared image for photo {PhotoId}", photo.Id);
            return null;
        }
    }

    /// <summary>Deletes a previously rendered prepared file (best-effort).</summary>
    public void DeletePrepared(string? preparedImageUrl)
    {
        try
        {
            var path = CephReportPdfGenerator.ResolveUploadFilePath(preparedImageUrl);
            if (path is not null && File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete prepared image {Url}", preparedImageUrl);
        }
    }

    private static SKBitmap? DecodeRobust(SKData data)
    {
        // SKBitmap.Decode(SKData) can return null for some encodings; SKCodec is more robust.
        using var codec = SKCodec.Create(data);
        if (codec is null) return null;
        var bitmap = SKBitmap.Decode(codec);
        return bitmap;
    }

    internal static SKImage? RenderToImage(SKBitmap source, OrthoImagePreparation prep)
    {
        if (source.Width <= 0 || source.Height <= 0) return null;

        // 1) Prepared crop region (fractions) → pixels, then fold zoom as a centered tighten.
        var (cx, cy, cw, ch) = SanitizeCrop(prep);
        var zoom = (double)prep.Zoom;
        if (zoom is < 1 or > 8 || double.IsNaN(zoom)) zoom = 1;
        if (zoom > 1)
        {
            var nw = cw / zoom;
            var nh = ch / zoom;
            cx += (cw - nw) / 2;
            cy += (ch - nh) / 2;
            cw = nw; ch = nh;
        }

        var srcRect = new SKRect(
            (float)(cx * source.Width),
            (float)(cy * source.Height),
            (float)((cx + cw) * source.Width),
            (float)((cy + ch) * source.Height));
        if (srcRect.Width < 1 || srcRect.Height < 1) return null;

        // 2) Output box from the requested aspect ratio (fallback: the cropped region's ratio).
        var aspect = ResolveAspect(prep.AspectRatio, srcRect.Width / srcRect.Height);
        var (outW, outH) = OutputSize(aspect);

        var info = new SKImageInfo(outW, outH, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        // 3) Cover the box with the cropped region, honoring rotation + flips.
        var rotation = Math.Clamp(prep.RotationDegrees, -180, 180);
        var radians = rotation * Math.PI / 180.0;
        var rotationCover = Math.Abs(Math.Cos(radians)) + Math.Abs(Math.Sin(radians)); // ≥ 1
        var coverScale = Math.Max(outW / srcRect.Width, outH / srcRect.Height) * (float)rotationCover;

        using var paint = new SKPaint
        {
            FilterQuality = SKFilterQuality.High,
            ColorFilter = BrightnessContrastFilter(prep.Brightness, prep.Contrast),
        };

        canvas.Save();
        canvas.Translate(outW / 2f, outH / 2f);
        canvas.RotateDegrees(rotation);
        canvas.Scale(prep.FlipHorizontal ? -1 : 1, prep.FlipVertical ? -1 : 1);
        canvas.Scale(coverScale, coverScale);
        var destRect = new SKRect(-srcRect.Width / 2f, -srcRect.Height / 2f, srcRect.Width / 2f, srcRect.Height / 2f);
        using (var image = SKImage.FromBitmap(source))
            canvas.DrawImage(image, srcRect, destRect, paint);
        canvas.Restore();

        return surface.Snapshot();
    }

    private static (double X, double Y, double Width, double Height) SanitizeCrop(OrthoImagePreparation prep)
    {
        var x = Clamp01((double)prep.CropX);
        var y = Clamp01((double)prep.CropY);
        var w = Clamp01((double)prep.CropWidth);
        var h = Clamp01((double)prep.CropHeight);
        if (w <= 0 || h <= 0 || x + w > 1.0001 || y + h > 1.0001)
            return (0, 0, 1, 1);
        return (x, y, w, h);
    }

    // Maps "16:9", "4:5", "1:1" … to width/height; "Original"/unknown → the fallback ratio.
    private static double ResolveAspect(string? aspectRatio, double fallback)
    {
        if (string.IsNullOrWhiteSpace(aspectRatio) ||
            aspectRatio.Equals("Original", StringComparison.OrdinalIgnoreCase))
            return fallback > 0 ? fallback : 1;

        var parts = aspectRatio.Split(':', '/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], out var w) && double.TryParse(parts[1], out var h) &&
            w > 0 && h > 0)
            return w / h;

        return fallback > 0 ? fallback : 1;
    }

    private static (int Width, int Height) OutputSize(double aspect)
    {
        aspect = Math.Clamp(aspect, 0.25, 4.0);
        return aspect >= 1
            ? (MaxLongEdge, Math.Max(1, (int)Math.Round(MaxLongEdge / aspect)))
            : (Math.Max(1, (int)Math.Round(MaxLongEdge * aspect)), MaxLongEdge);
    }

    private static SKColorFilter? BrightnessContrastFilter(int brightness, int contrast)
    {
        var b = Math.Clamp(brightness, -100, 100);
        var c = Math.Clamp(contrast, -100, 100);
        if (b == 0 && c == 0) return null;

        // Contrast factor around mid-grey, then brightness as an additive offset (0..255 scale).
        var contrastFactor = 1f + (c / 100f);
        var translate = (b / 100f * 255f) + (255f * 0.5f * (1f - contrastFactor));
        var matrix = new[]
        {
            contrastFactor, 0, 0, 0, translate,
            0, contrastFactor, 0, 0, translate,
            0, 0, contrastFactor, 0, translate,
            0, 0, 0, 1, 0,
        };
        return SKColorFilter.CreateColorMatrix(matrix);
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0, 1);

    // Mirrors UploadsController.ResolveUploadsDirectory: UPLOADS_PATH → wwwroot/uploads → temp.
    internal static string ResolveUploadsDirectory()
    {
        var envPath = Environment.GetEnvironmentVariable("UPLOADS_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            Directory.CreateDirectory(envPath);
            return envPath;
        }

        var primary = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        try
        {
            Directory.CreateDirectory(primary);
            var probe = Path.Combine(primary, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return primary;
        }
        catch
        {
            var fallback = Path.Combine(Path.GetTempPath(), "aqlan-uploads");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }
}
