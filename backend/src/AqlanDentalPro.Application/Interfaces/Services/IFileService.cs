namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IFileService
{
    /// <summary>Upload a file and return its relative URL path.</summary>
    Task<string> UploadAsync(Stream fileStream, string fileName, string subDirectory, CancellationToken ct = default);
    
    /// <summary>Delete a file by its relative URL path.</summary>
    Task DeleteAsync(string relativeUrl, CancellationToken ct = default);
    
    /// <summary>Get the absolute URL for a relative path.</summary>
    string GetAbsoluteUrl(string relativeUrl);
}
