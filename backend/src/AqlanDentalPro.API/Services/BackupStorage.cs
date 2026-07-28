namespace AqlanDentalPro.API.Services;

internal static class BackupStorage
{
    private const string RailwayPersistentRoot = "/data/uploads";

    public static string GetDirectory(IWebHostEnvironment environment)
    {
        var configuredPath = Environment.GetEnvironmentVariable("BACKUP_STORAGE_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.GetFullPath(configuredPath);

        if (Directory.Exists(RailwayPersistentRoot))
            return Path.Combine(RailwayPersistentRoot, "backups");

        return Path.Combine(environment.ContentRootPath, "backups");
    }
}
