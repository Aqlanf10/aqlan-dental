using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AqlanDentalPro.IntegrationTests;

/// <summary>
/// W11 acceptance tests. These require PostgreSQL because the guarantees are
/// implemented with partial unique indexes, check constraints, and triggers.
/// </summary>
public sealed class JournalLedgerSafetyTests : IClassFixture<TestWebAppFactory>, IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;
    private Guid _branchId;
    private Guid _userId;

    public JournalLedgerSafetyTests(TestWebAppFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();

        await using var db = await _factory.CreateDbContextAsync();
        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            Name = "W11 journal branch",
            IsMain = true,
            IsActive = true
        };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "w11.admin",
            Role = UserRole.Admin,
            BranchId = branch.Id,
            PasswordHash = "not-used-in-tests",
            PasswordSalt = "not-used-in-tests",
            IsActive = true,
            MustChangePassword = false
        };

        db.AddRange(branch, user);
        await db.SaveChangesAsync();
        _branchId = branch.Id;
        _userId = user.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DatabaseRejectsLineWithDebitAndCreditEvenOutsideApplicationService()
    {
        var entry = await CreatePostedEntryAsync(DateOnly.FromDateTime(DateTime.UtcNow));

        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "JournalLines"
                ("Id", "JournalEntryId", "AccountType", "AccountId", "Debit", "Credit",
                 "BranchId", "CreatedAt", "UpdatedAt", "IsActive")
            VALUES
                (@id, @entryId, 'Expense', @accountId, 10, 10,
                 @branchId, now(), now(), TRUE);
            """;
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("entryId", entry.Id);
        command.Parameters.AddWithValue("accountId", Guid.NewGuid());
        command.Parameters.AddWithValue("branchId", _branchId);

        var act = async () => await command.ExecuteNonQueryAsync();
        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task TenConcurrentReservationsProduceUniqueEntryNumbers()
    {
        var reservations = Enumerable.Range(0, 10).Select(async _ =>
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IJournalEntryService>();
            return await service.GenerateEntryNumberAsync();
        });

        var numbers = await Task.WhenAll(reservations);

        numbers.Should().OnlyHaveUniqueItems();
        numbers.Should().OnlyContain(number => number.StartsWith($"JE-{DateTime.UtcNow:yyyyMMdd}-"));
    }

    [Fact]
    public async Task DuplicateSourceIdentityIsRejectedByDatabase()
    {
        var sourceId = Guid.NewGuid();
        await CreatePostedEntryAsync(DateOnly.FromDateTime(DateTime.UtcNow), sourceId);

        await using var db = await _factory.CreateDbContextAsync();
        var service = new AqlanDentalPro.Infrastructure.Services.JournalEntryService(
            db,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AqlanDentalPro.Infrastructure.Services.JournalEntryService>.Instance);

        var act = async () => await service.CreateEntryAsync(
            FinancialDocumentType.Expense,
            sourceId,
            "duplicate source",
            DateOnly.FromDateTime(DateTime.UtcNow),
            _branchId,
            _userId,
            null,
            null,
            BalancedLines());

        var exception = await act.Should().ThrowAsync<DbUpdateException>();
        exception.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task ClosedPeriodIsImmutableButOfficialReversalPostsInOpenPeriod()
    {
        var closedDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var original = await CreatePostedEntryAsync(closedDate);

        await using (var db = await _factory.CreateDbContextAsync())
        {
            db.AccountingPeriods.Add(new AccountingPeriod
            {
                BranchId = _branchId,
                Name = "Closed W11 day",
                StartDate = closedDate,
                EndDate = closedDate,
                Status = "Closed",
                ClosedAt = DateTime.UtcNow,
                ClosedBy = _userId
            });
            await db.SaveChangesAsync();
        }

        await using (var db = await _factory.CreateDbContextAsync())
        {
            var stored = await db.JournalEntries.SingleAsync(entry => entry.Id == original.Id);
            stored.Description = "forbidden mutation";
            var act = async () => await db.SaveChangesAsync();
            var exception = await act.Should().ThrowAsync<DbUpdateException>();
            exception.Which.InnerException.Should().BeOfType<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        }

        await using (var db = await _factory.CreateDbContextAsync())
        {
            var service = new AqlanDentalPro.Infrastructure.Services.JournalEntryService(
                db,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<AqlanDentalPro.Infrastructure.Services.JournalEntryService>.Instance);
            var reversal = await service.CreateReversalEntryAsync(original.Id, "W11 correction", _userId);
            reversal.IsReversal.Should().BeTrue();
            reversal.EntryDate.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
            reversal.ReversalOfEntryId.Should().Be(original.Id);
        }

        await using (var db = await _factory.CreateDbContextAsync())
        {
            var stored = await db.JournalEntries.SingleAsync(entry => entry.Id == original.Id);
            stored.ReversedByEntryId.Should().NotBeNull();
        }
    }

    private async Task<JournalEntry> CreatePostedEntryAsync(DateOnly date, Guid? sourceId = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var service = new AqlanDentalPro.Infrastructure.Services.JournalEntryService(
            db,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AqlanDentalPro.Infrastructure.Services.JournalEntryService>.Instance);
        var entry = await service.CreateEntryAsync(
            FinancialDocumentType.Expense,
            sourceId ?? Guid.NewGuid(),
            "W11 test entry",
            date,
            _branchId,
            _userId,
            null,
            null,
            BalancedLines());
        await service.PostEntryAsync(entry.Id);
        return entry;
    }

    private IEnumerable<(JournalAccountType accountType, Guid accountId, decimal debit, decimal credit, string? description)> BalancedLines()
    {
        yield return (JournalAccountType.Expense, _branchId, 100m, 0m, "expense");
        yield return (JournalAccountType.OwnerEquity, _branchId, 0m, 100m, "offset");
    }
}
