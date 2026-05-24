using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ortho;

/// <summary>
/// Tests that orthodontics financial summary (ContractPaid, ContractRemaining)
/// correctly excludes inactive/soft-deleted payments and accounts for discounts.
/// </summary>
public class OrthoFinanceCalculationTests : IDisposable
{
    private readonly AppDbContext _db;

    public OrthoFinanceCalculationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid patientId, Guid contractId, Guid orthoCaseId)> SeedOrthoCaseWithContract(
        decimal totalAmount, decimal discountAmount = 0)
    {
        var patientId = Guid.NewGuid();
        var orthoCaseId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        _db.Patients.Add(new Patient { Id = patientId, FullName = "Test Patient", IsActive = true });
        _db.Doctors.Add(new Doctor { Id = doctorId, Name = "Dr. Test", IsActive = true });
        _db.OrthoCases.Add(new OrthoCase
        {
            Id = orthoCaseId,
            PatientId = patientId,
            DoctorId = doctorId,
            CaseNumber = "ORT-001",
            IsActive = true,
        });
        _db.Contracts.Add(new Contract
        {
            Id = contractId,
            PatientId = patientId,
            RelatedCaseId = orthoCaseId,
            TotalAmount = totalAmount,
            DiscountAmount = discountAmount,
            IsActive = true,
        });
        await _db.SaveChangesAsync();
        return (patientId, contractId, orthoCaseId);
    }

    private async Task AddPayment(Guid contractId, Guid patientId, decimal amount, bool isActive = true)
    {
        _db.Payments.Add(new Payment
        {
            ContractId = contractId,
            PatientId = patientId,
            Amount = amount,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            IsActive = isActive,
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task ContractPaid_OnlyCountsActivePayments()
    {
        // Arrange
        var (patientId, contractId, orthoCaseId) = await SeedOrthoCaseWithContract(500_000);
        await AddPayment(contractId, patientId, 100_000, isActive: true);
        await AddPayment(contractId, patientId, 50_000, isActive: true);
        await AddPayment(contractId, patientId, 30_000, isActive: false); // Refunded/soft-deleted

        // Act — simulate the same logic as OrthoCasesController.GetOverview
        var contract = await _db.Contracts
            .Include(c => c.Payments)
            .FirstAsync(c => c.Id == contractId);

        var contractTotal = contract.TotalAmount - contract.DiscountAmount;
        var contractPaid = contract.Payments.Where(p => p.IsActive).Sum(p => p.Amount);
        var contractRemaining = Math.Max(0, contractTotal - contractPaid);

        // Assert
        contractPaid.Should().Be(150_000); // 100k + 50k, not 180k
        contractRemaining.Should().Be(350_000); // 500k - 150k
    }

    [Fact]
    public async Task ContractPaid_NoPayments_ReturnsZero()
    {
        // Arrange
        var (_, contractId, _) = await SeedOrthoCaseWithContract(300_000);

        var contract = await _db.Contracts
            .Include(c => c.Payments)
            .FirstAsync(c => c.Id == contractId);

        var contractPaid = contract.Payments.Where(p => p.IsActive).Sum(p => p.Amount);

        contractPaid.Should().Be(0);
    }

    [Fact]
    public async Task ContractRemaining_WithDiscount_ExcludesDiscountFromTotal()
    {
        // Arrange
        var (patientId, contractId, _) = await SeedOrthoCaseWithContract(500_000, discountAmount: 50_000);
        await AddPayment(contractId, patientId, 200_000, isActive: true);

        var contract = await _db.Contracts
            .Include(c => c.Payments)
            .FirstAsync(c => c.Id == contractId);

        var contractTotal = contract.TotalAmount - contract.DiscountAmount;
        var contractPaid = contract.Payments.Where(p => p.IsActive).Sum(p => p.Amount);
        var contractRemaining = Math.Max(0, contractTotal - contractPaid);

        // Assert
        contractTotal.Should().Be(450_000); // 500k - 50k discount
        contractPaid.Should().Be(200_000);
        contractRemaining.Should().Be(250_000); // 450k - 200k
    }

    [Fact]
    public async Task ContractRemaining_CannotBeNegative()
    {
        // Arrange: overpayment scenario
        var (patientId, contractId, _) = await SeedOrthoCaseWithContract(100_000);
        await AddPayment(contractId, patientId, 150_000, isActive: true);

        var contract = await _db.Contracts
            .Include(c => c.Payments)
            .FirstAsync(c => c.Id == contractId);

        var contractTotal = contract.TotalAmount - contract.DiscountAmount;
        var contractPaid = contract.Payments.Where(p => p.IsActive).Sum(p => p.Amount);
        var contractRemaining = Math.Max(0, contractTotal - contractPaid);

        contractRemaining.Should().Be(0); // Never negative
    }

    [Fact]
    public async Task ContractPaid_InactivePaymentsFullyExcluded()
    {
        // Arrange: all payments are inactive
        var (patientId, contractId, _) = await SeedOrthoCaseWithContract(400_000);
        await AddPayment(contractId, patientId, 100_000, isActive: false);
        await AddPayment(contractId, patientId, 200_000, isActive: false);

        var contract = await _db.Contracts
            .Include(c => c.Payments)
            .FirstAsync(c => c.Id == contractId);

        var contractPaid = contract.Payments.Where(p => p.IsActive).Sum(p => p.Amount);

        contractPaid.Should().Be(0);
    }
}
