using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// DB-02: Verifies that xmin concurrency tokens are configured on CashierSession,
/// Invoice, and Payment — mirroring the FIN-06 Treasury fix. The tests inspect the
/// EF Core runtime model metadata: they do NOT enforce concurrency at runtime in the
/// InMemory provider (xmin is PostgreSQL-only), but they ensure the entity
/// configurations are applied. If a future refactor drops the UseXminAsConcurrencyToken
/// call, these tests will fail before the lost-update protection silently disappears.
/// </summary>
public class Db02ConcurrencyTokenTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IReadOnlyList<IProperty> GetConcurrencyTokens(IEntityType? entityType)
        => entityType?.GetProperties().Where(p => p.IsConcurrencyToken).ToList()
           ?? new List<IProperty>();

    [Fact]
    public void CashierSession_HasXminConcurrencyToken()
    {
        using var db = CreateContext();
        var entityType = db.Model.FindEntityType(typeof(CashierSession));
        entityType.Should().NotBeNull("CashierSession must be registered in the EF model");

        var concurrencyTokens = GetConcurrencyTokens(entityType);
        concurrencyTokens.Should().ContainSingle(
            p => p.Name == "xmin",
            "CashierSession must use PostgreSQL xmin as its concurrency token (DB-02 — prevents lost-update on close/reconcile)");
    }

    [Fact]
    public void Invoice_HasXminConcurrencyToken()
    {
        using var db = CreateContext();
        var entityType = db.Model.FindEntityType(typeof(Invoice));
        entityType.Should().NotBeNull("Invoice must be registered in the EF model");

        var concurrencyTokens = GetConcurrencyTokens(entityType);
        concurrencyTokens.Should().ContainSingle(
            p => p.Name == "xmin",
            "Invoice must use PostgreSQL xmin as its concurrency token (DB-02 — prevents lost-update on update/issue/cancel)");
    }

    [Fact]
    public void Payment_HasXminConcurrencyToken()
    {
        using var db = CreateContext();
        var entityType = db.Model.FindEntityType(typeof(Payment));
        entityType.Should().NotBeNull("Payment must be registered in the EF model");

        var concurrencyTokens = GetConcurrencyTokens(entityType);
        concurrencyTokens.Should().ContainSingle(
            p => p.Name == "xmin",
            "Payment must use PostgreSQL xmin as its concurrency token (DB-02 — prevents lost-update on create/update/delete)");
    }

    [Fact]
    public void Treasury_StillHasXminConcurrencyToken()
    {
        // FIN-06 regression — the Treasury xmin token must NOT be lost by DB-02 changes.
        using var db = CreateContext();
        var entityType = db.Model.FindEntityType(typeof(Treasury));
        entityType.Should().NotBeNull("Treasury must be registered in the EF model");

        var concurrencyTokens = GetConcurrencyTokens(entityType);
        concurrencyTokens.Should().ContainSingle(
            p => p.Name == "xmin",
            "Treasury must continue to use PostgreSQL xmin as its concurrency token (FIN-06 regression — concurrent treasury updates must still throw)");
    }
}
