using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AqlanDentalPro.Infrastructure.Data;

/// <summary>
/// Design-time factory used only by `dotnet ef` CLI tooling (migrations add/update).
/// Reads the same configuration sources as the running app (appsettings.json +
/// environment variables, e.g. ConnectionStrings__DefaultConnection) instead of a
/// hardcoded connection string, so design-time tooling works against whatever
/// database the current environment is configured for (dev, CI, etc.) without
/// baking any environment-specific credentials into source control.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No connection string configured. Design-time EF tooling requires " +
                "ConnectionStrings__DefaultConnection (env var) or ConnectionStrings:DefaultConnection " +
                "(appsettings.json) to be set. Refusing to fall back to a hardcoded/insecure default.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new AppDbContext(optionsBuilder.Options);
    }
}
