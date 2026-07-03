using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// Regression test for a live production 500 found during owner QA on
/// GET /api/contracts?status=active: EF Core's global soft-delete query filter
/// applies even to Include-d navigations, so a Contract whose Patient was later
/// soft-deleted (IsActive = false) can materialize with Patient == null despite
/// the FK being required (LEFT JOIN filtered out, row not excluded). The old
/// mappers did `c.Patient.FirstName` unconditionally and threw NullReferenceException.
///
/// Note: the InMemory provider does not fully replicate the relational LEFT JOIN
/// nuance that triggers the null navigation on Npgsql, so these tests mainly guard
/// against a regression of the null-guard itself, not a full repro of the original
/// crash mechanism — the fix (null-conditional PatientName/PatientNumber, matching
/// the existing FinanceMappers.MapPayment convention) is defensive either way.
/// </summary>
public class ContractServiceSoftDeletedPatientTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ContractService CreateService(AppDbContext db)
    {
        var currentUser = new Mock<AqlanDentalPro.Application.Interfaces.Services.ICurrentUserService>();
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);
        currentUser.SetupGet(c => c.BranchId).Returns((Guid?)null);
        return new ContractService(db, currentUser.Object);
    }

    [Fact]
    public async Task GetContractsAsync_ContractWithSoftDeletedPatient_DoesNotThrow()
    {
        using var db = CreateContext();

        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = "P-0001",
            FirstName = "محذوف",
            LastName = "تجريبي",
            IsActive = false, // soft-deleted — EF's global filter will null out Contract.Patient
        };
        db.Patients.Add(patient);

        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            TotalAmount = 1000,
            Status = ContractStatus.Active,
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var act = async () => await service.GetContractsAsync(1, 20, null, null);

        await act.Should().NotThrowAsync(
            "a soft-deleted patient must not crash the contracts list — the row should be skipped or shown safely, never a 500");
    }

    [Fact]
    public async Task GetContractByIdAsync_ContractWithSoftDeletedPatient_DoesNotThrow()
    {
        using var db = CreateContext();

        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = "P-0002",
            FirstName = "محذوف",
            LastName = "تجريبي",
            IsActive = false,
        };
        db.Patients.Add(patient);

        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            TotalAmount = 1000,
            Status = ContractStatus.Active,
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var act = async () => await service.GetContractByIdAsync(contract.Id);

        await act.Should().NotThrowAsync(
            "a soft-deleted patient must not crash the contract detail view — never a 500");
    }
}
