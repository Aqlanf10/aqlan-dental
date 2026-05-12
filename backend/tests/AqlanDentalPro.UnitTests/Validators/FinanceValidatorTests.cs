using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Validators;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Validators;

public class FinanceValidatorTests
{
    private readonly CreateContractRequestValidator _contractValidator = new();
    private readonly CreatePaymentRequestValidator _paymentValidator = new();

    // ─── CreateContractRequestValidator ────────────────────────────────────

    [Fact]
    public void ValidContract_ShouldPass()
    {
        var req = new CreateContractRequest
        {
            PatientId = Guid.NewGuid(),
            Specialty = "تقويم أسنان",
            TotalAmount = 50000,
            DownPayment = 10000,
            InstallmentsCount = 12,
            StartDate = "2026-06-01"
        };

        var result = _contractValidator.Validate(req);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void TotalAmount_Zero_ShouldFail()
    {
        var req = new CreateContractRequest
        {
            PatientId = Guid.NewGuid(),
            Specialty = "تقويم أسنان",
            TotalAmount = 0,
            StartDate = "2026-06-01"
        };

        var result = _contractValidator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TotalAmount");
    }

    [Fact]
    public void TotalAmount_Negative_ShouldFail()
    {
        var req = new CreateContractRequest
        {
            PatientId = Guid.NewGuid(),
            Specialty = "تقويم أسنان",
            TotalAmount = -100,
            StartDate = "2026-06-01"
        };

        var result = _contractValidator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TotalAmount");
    }

    [Fact]
    public void DownPayment_ExceedsTotalAmount_ShouldFail()
    {
        var req = new CreateContractRequest
        {
            PatientId = Guid.NewGuid(),
            Specialty = "تقويم أسنان",
            TotalAmount = 10000,
            DownPayment = 15000,
            StartDate = "2026-06-01"
        };

        var result = _contractValidator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DownPayment");
    }

    [Fact]
    public void InstallmentsCount_Negative_ShouldFail()
    {
        var req = new CreateContractRequest
        {
            PatientId = Guid.NewGuid(),
            Specialty = "تقويم أسنان",
            TotalAmount = 50000,
            InstallmentsCount = -1,
            StartDate = "2026-06-01"
        };

        var result = _contractValidator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "InstallmentsCount");
    }

    [Fact]
    public void InstallmentsCount_Exceeds60_ShouldFail()
    {
        var req = new CreateContractRequest
        {
            PatientId = Guid.NewGuid(),
            Specialty = "تقويم أسنان",
            TotalAmount = 50000,
            InstallmentsCount = 61,
            StartDate = "2026-06-01"
        };

        var result = _contractValidator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "InstallmentsCount");
    }

    [Fact]
    public void StartDate_InvalidFormat_ShouldFail()
    {
        var req = new CreateContractRequest
        {
            PatientId = Guid.NewGuid(),
            Specialty = "تقويم أسنان",
            TotalAmount = 50000,
            StartDate = "not-a-date"
        };

        var result = _contractValidator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StartDate");
    }

    [Fact]
    public void StartDate_Null_ShouldPass()
    {
        var req = new CreateContractRequest
        {
            PatientId = Guid.NewGuid(),
            Specialty = "تقويم أسنان",
            TotalAmount = 50000,
            StartDate = null
        };

        var result = _contractValidator.Validate(req);
        result.IsValid.Should().BeTrue();
    }

    // ─── CreatePaymentRequestValidator ─────────────────────────────────────

    [Fact]
    public void ValidPayment_ShouldPass()
    {
        var req = new CreatePaymentRequest
        {
            PatientId = Guid.NewGuid(),
            Amount = 5000,
            PaymentMethod = "cash"
        };

        var result = _paymentValidator.Validate(req);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Amount_Zero_ShouldFail()
    {
        var req = new CreatePaymentRequest
        {
            PatientId = Guid.NewGuid(),
            Amount = 0,
            PaymentMethod = "cash"
        };

        var result = _paymentValidator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Amount");
    }

    [Fact]
    public void Amount_Negative_ShouldFail()
    {
        var req = new CreatePaymentRequest
        {
            PatientId = Guid.NewGuid(),
            Amount = -500,
            PaymentMethod = "cash"
        };

        var result = _paymentValidator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Amount");
    }

    [Theory]
    [InlineData("invalid_method")]
    [InlineData("")]
    [InlineData("credit")]
    public void InvalidPaymentMethod_ShouldFail(string method)
    {
        var req = new CreatePaymentRequest
        {
            PatientId = Guid.NewGuid(),
            Amount = 5000,
            PaymentMethod = method
        };

        var result = _paymentValidator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PaymentMethod");
    }

    [Theory]
    [InlineData("cash")]
    [InlineData("bank_transfer")]
    [InlineData("card")]
    [InlineData("check")]
    [InlineData("other")]
    public void ValidPaymentMethods_ShouldPass(string method)
    {
        var req = new CreatePaymentRequest
        {
            PatientId = Guid.NewGuid(),
            Amount = 5000,
            PaymentMethod = method
        };

        var result = _paymentValidator.Validate(req);
        result.IsValid.Should().BeTrue();
    }
}
