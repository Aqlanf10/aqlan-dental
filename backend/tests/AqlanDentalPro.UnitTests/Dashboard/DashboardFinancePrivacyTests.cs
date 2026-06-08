using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Dashboard;

public class DashboardFinancePrivacyTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static DashboardService CreateService(AppDbContext db, Guid branchId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(u => u.IsAdmin).Returns(false);
        currentUser.SetupGet(u => u.Role).Returns(UserRole.GeneralDentist);
        currentUser.SetupGet(u => u.BranchId).Returns(branchId);
        currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        return new DashboardService(db, currentUser.Object);
    }

    [Fact]
    public async Task GetStatsAsync_WhenFinanceHidden_DoesNotExposeMonthlyRevenue()
    {
        await using var db = CreateDb();
        var branchId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        db.Branches.Add(new Branch { Id = branchId, Name = "Main" });
        db.Patients.Add(new Patient { Id = patientId, FirstName = "Ali", BranchId = branchId, IsActive = true });
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            BranchId = branchId,
            Amount = 5000,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, branchId);

        var stats = await service.GetStatsAsync(includeFinance: false);

        stats.TotalRevenueMTD.Should().Be(0, "doctor-facing dashboard stats must not expose financial totals");
    }

    [Fact]
    public async Task GetStatsAsync_WhenFinanceHidden_DoesNotExposeOverdueContractCount()
    {
        await using var db = CreateDb();
        var branchId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        db.Branches.Add(new Branch { Id = branchId, Name = "Main" });
        db.Patients.Add(new Patient { Id = patientId, FirstName = "Ali", BranchId = branchId, IsActive = true });
        db.Contracts.Add(new Contract
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            TotalAmount = 12000,
            DownPayment = 0,
            InstallmentsCount = 12,
            InstallmentAmount = 1000,
            StartDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(-3)),
            Status = ContractStatus.Active,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, branchId);

        var stats = await service.GetStatsAsync(includeFinance: false);

        stats.OverdueContractsCount.Should().Be(0, "doctor-facing dashboard stats must not expose overdue financial counts");
    }

    [Fact]
    public async Task GetChartsAsync_WhenFinanceHidden_ReturnsZeroRevenueSeries()
    {
        await using var db = CreateDb();
        var branchId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        db.Branches.Add(new Branch { Id = branchId, Name = "Main" });
        db.Patients.Add(new Patient { Id = patientId, FirstName = "Ali", BranchId = branchId, IsActive = true });
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            BranchId = branchId,
            Amount = 5000,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, branchId);

        var charts = await service.GetChartsAsync(includeFinance: false);

        charts.RevenueByDay.Should().HaveCount(30);
        charts.RevenueByDay.Should().OnlyContain(p => p.Amount == 0);
    }
}
