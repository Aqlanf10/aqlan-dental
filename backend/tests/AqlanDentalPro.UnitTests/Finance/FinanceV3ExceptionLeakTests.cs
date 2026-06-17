using System.IO;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// Security regression: finance HTTP responses must never leak exception
/// internals (message / inner / stack). Two diagnostic 500 handlers in
/// FinanceV3Controller previously returned ex.Message + InnerException + a
/// StackTrace slice; they now log server-side and return a generic Arabic
/// message. This source-check keeps them from regressing.
/// </summary>
public class FinanceV3ExceptionLeakTests
{
    private static string? FindControllerSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            foreach (var rel in new[]
            {
                Path.Combine("backend", "src", "AqlanDentalPro.API", "Controllers", "FinanceV3Controller.cs"),
                Path.Combine("src", "AqlanDentalPro.API", "Controllers", "FinanceV3Controller.cs"),
            })
            {
                var candidate = Path.Combine(dir.FullName, rel);
                if (File.Exists(candidate)) return candidate;
            }
            dir = dir.Parent;
        }
        return null;
    }

    [Fact]
    public void FinanceV3Controller_DoesNotLeakExceptionInternals()
    {
        var path = FindControllerSource();
        path.Should().NotBeNull("the FinanceV3Controller source must be locatable for the leak check");

        var src = File.ReadAllText(path!);
        src.Should().NotContain("ex.StackTrace", "500 responses must not leak the stack trace to clients");
        src.Should().NotContain("error = ex.Message", "500 responses must not leak the exception message to clients");
        src.Should().NotContain("inner = ex.InnerException", "500 responses must not leak inner-exception details to clients");
    }
}
