using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.Extensions.Configuration;

namespace AqlanDentalPro.Infrastructure.Services;

public class FileService : IFileService
{
    private readonly string _basePath;
    private readonly string _baseUrl;

    private static readonly HashSet<string> AllowedSubDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "clinical-photos", "radiographs", "ceph-xrays", "documents"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
        ".pdf", ".doc", ".docx",
        ".dcm", ".dicom"
    };

    public FileService(IConfiguration configuration)
    {
        _basePath = configuration["FileStorage:BasePath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        _baseUrl = configuration["FileStorage:BaseUrl"]
            ?? "/uploads";
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string subDirectory, CancellationToken ct = default)
    {
        if (!AllowedSubDirectories.Contains(subDirectory))
            throw new ArgumentException($"Invalid sub-directory '{subDirectory}'. Allowed: {string.Join(", ", AllowedSubDirectories)}");

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            throw new ArgumentException($"File extension '{ext}' is not allowed.");

        var uniqueName = $"{Guid.NewGuid()}{ext}";
        var dirPath = Path.Combine(_basePath, subDirectory);

        Directory.CreateDirectory(dirPath);

        var fullPath = Path.Combine(dirPath, uniqueName);

        await using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fileStream.CopyToAsync(fs, ct);
        }

        var relativeUrl = $"{_baseUrl.TrimEnd('/')}/{subDirectory}/{uniqueName}";
        return relativeUrl;
    }

    public Task DeleteAsync(string relativeUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
            return Task.CompletedTask;

        // Convert relative URL to file-system path
        var relativePath = relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        // Remove the base URL prefix if present (e.g., "uploads/")
        var fullPath = Path.Combine(_basePath, "..", relativePath);
        fullPath = Path.GetFullPath(fullPath);

        // Security: ensure the resolved path is under _basePath
        var basePathRoot = Path.GetFullPath(_basePath);
        if (!fullPath.StartsWith(basePathRoot, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Attempted to delete a file outside the storage directory.");

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    public string GetAbsoluteUrl(string relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
            return string.Empty;

        // If already an absolute URL, return as-is
        if (relativeUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            relativeUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return relativeUrl;

        return relativeUrl;
    }
}
