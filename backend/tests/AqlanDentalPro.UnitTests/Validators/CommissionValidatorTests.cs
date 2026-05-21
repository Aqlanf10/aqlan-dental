using AqlanDentalPro.Application.DTOs.Commission;
using AqlanDentalPro.Application.Validators;
using AqlanDentalPro.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Validators;

/// <summary>
/// FluentValidation tests for commission DTO validators.
/// Verifies that negative costs and out-of-range percentages fail validation
/// at the model level, producing 400 Bad Request before reaching the service.
/// </summary>
public class CommissionValidatorTests
{
    private readonly UpdateServiceCommissionDefaultsRequestValidator _defaultsValidator = new();
    private readonly UpdateLineItemCommissionRequestValidator _lineItemValidator = new();
    private readonly RecordCommissionPaymentRequestValidator _paymentValidator = new();

    // ── UpdateServiceCommissionDefaultsRequestValidator ────────────────────────

    [Fact]
    public void Defaults_NegativeMaterialCost_ShouldFail()
    {
        var req = new UpdateServiceCommissionDefaultsRequest(
            DefaultMaterialCost: -1m,
            DefaultMaterialCostType: MaterialCostType.FixedAmount,
            DefaultLabCost: 0m,
            DefaultDoctorCommissionPercentage: 30m,
            CommissionBaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        var result = _defaultsValidator.Validate(req);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DefaultMaterialCost");
    }

    [Fact]
    public void Defaults_NegativeLabCost_ShouldFail()
    {
        var req = new UpdateServiceCommissionDefaultsRequest(
            DefaultMaterialCost: 0m,
            DefaultMaterialCostType: MaterialCostType.FixedAmount,
            DefaultLabCost: -500m,
            DefaultDoctorCommissionPercentage: 30m,
            CommissionBaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        var result = _defaultsValidator.Validate(req);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DefaultLabCost");
    }

    [Fact]
    public void Defaults_PercentageOver100_ShouldFail()
    {
        var req = new UpdateServiceCommissionDefaultsRequest(
            DefaultMaterialCost: 0m,
            DefaultMaterialCostType: MaterialCostType.FixedAmount,
            DefaultLabCost: 0m,
            DefaultDoctorCommissionPercentage: 101m,
            CommissionBaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        var result = _defaultsValidator.Validate(req);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DefaultDoctorCommissionPercentage");
    }

    [Fact]
    public void Defaults_PercentageNegative_ShouldFail()
    {
        var req = new UpdateServiceCommissionDefaultsRequest(
            DefaultMaterialCost: 0m,
            DefaultMaterialCostType: MaterialCostType.FixedAmount,
            DefaultLabCost: 0m,
            DefaultDoctorCommissionPercentage: -5m,
            CommissionBaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        var result = _defaultsValidator.Validate(req);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DefaultDoctorCommissionPercentage");
    }

    [Fact]
    public void Defaults_ValidInput_ShouldPass()
    {
        var req = new UpdateServiceCommissionDefaultsRequest(
            DefaultMaterialCost: 2000m,
            DefaultMaterialCostType: MaterialCostType.FixedAmount,
            DefaultLabCost: 3000m,
            DefaultDoctorCommissionPercentage: 35m,
            CommissionBaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        var result = _defaultsValidator.Validate(req);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Defaults_ZeroCosts_ShouldPass()
    {
        var req = new UpdateServiceCommissionDefaultsRequest(
            DefaultMaterialCost: 0m,
            DefaultMaterialCostType: MaterialCostType.FixedAmount,
            DefaultLabCost: 0m,
            DefaultDoctorCommissionPercentage: 0m,
            CommissionBaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        var result = _defaultsValidator.Validate(req);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Defaults_Boundary100Percentage_ShouldPass()
    {
        var req = new UpdateServiceCommissionDefaultsRequest(
            DefaultMaterialCost: 0m,
            DefaultMaterialCostType: MaterialCostType.FixedAmount,
            DefaultLabCost: 0m,
            DefaultDoctorCommissionPercentage: 100m,
            CommissionBaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        var result = _defaultsValidator.Validate(req);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Defaults_NullPercentage_ShouldPass()
    {
        var req = new UpdateServiceCommissionDefaultsRequest(
            DefaultMaterialCost: 0m,
            DefaultMaterialCostType: MaterialCostType.FixedAmount,
            DefaultLabCost: 0m,
            DefaultDoctorCommissionPercentage: null,
            CommissionBaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        var result = _defaultsValidator.Validate(req);

        result.IsValid.Should().BeTrue("null percentage means 'use doctor default', which is valid");
    }

    // ── UpdateLineItemCommissionRequestValidator ───────────────────────────────

    [Fact]
    public void LineItem_NegativeMaterialCost_ShouldFail()
    {
        var req = new UpdateLineItemCommissionRequest(
            MaterialCost: -1m, LabCost: null, OtherDirectCost: null,
            DoctorCommissionPercentage: null, CommissionBaseRule: null,
            DoctorId: null, CommissionNotes: null);

        var result = _lineItemValidator.Validate(req);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MaterialCost");
    }

    [Fact]
    public void LineItem_NegativeLabCost_ShouldFail()
    {
        var req = new UpdateLineItemCommissionRequest(
            MaterialCost: null, LabCost: -100m, OtherDirectCost: null,
            DoctorCommissionPercentage: null, CommissionBaseRule: null,
            DoctorId: null, CommissionNotes: null);

        var result = _lineItemValidator.Validate(req);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LabCost");
    }

    [Fact]
    public void LineItem_PercentageOver100_ShouldFail()
    {
        var req = new UpdateLineItemCommissionRequest(
            MaterialCost: null, LabCost: null, OtherDirectCost: null,
            DoctorCommissionPercentage: 150m, CommissionBaseRule: null,
            DoctorId: null, CommissionNotes: null);

        var result = _lineItemValidator.Validate(req);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DoctorCommissionPercentage");
    }

    [Fact]
    public void LineItem_ValidPartialUpdate_ShouldPass()
    {
        var req = new UpdateLineItemCommissionRequest(
            MaterialCost: 5000m, LabCost: null, OtherDirectCost: null,
            DoctorCommissionPercentage: null, CommissionBaseRule: null,
            DoctorId: null, CommissionNotes: "ملاحظة");

        var result = _lineItemValidator.Validate(req);

        result.IsValid.Should().BeTrue();
    }

    // ── RecordCommissionPaymentRequestValidator ───────────────────────────────

    [Fact]
    public void Payment_ZeroAmount_ShouldFail()
    {
        var req = new RecordCommissionPaymentRequest(
            DoctorId: Guid.NewGuid(), Amount: 0m,
            PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            PaymentMethod: "cash", ReferenceNumber: null,
            Notes: null, LineItemIds: null);

        var result = _paymentValidator.Validate(req);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Amount");
    }

    [Fact]
    public void Payment_NegativeAmount_ShouldFail()
    {
        var req = new RecordCommissionPaymentRequest(
            DoctorId: Guid.NewGuid(), Amount: -100m,
            PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            PaymentMethod: "cash", ReferenceNumber: null,
            Notes: null, LineItemIds: null);

        var result = _paymentValidator.Validate(req);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Amount");
    }

    [Fact]
    public void Payment_EmptyDoctorId_ShouldFail()
    {
        var req = new RecordCommissionPaymentRequest(
            DoctorId: Guid.Empty, Amount: 5000m,
            PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            PaymentMethod: "cash", ReferenceNumber: null,
            Notes: null, LineItemIds: null);

        var result = _paymentValidator.Validate(req);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DoctorId");
    }

    [Fact]
    public void Payment_ValidInput_ShouldPass()
    {
        var req = new RecordCommissionPaymentRequest(
            DoctorId: Guid.NewGuid(), Amount: 5000m,
            PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            PaymentMethod: "cash", ReferenceNumber: null,
            Notes: null, LineItemIds: null);

        var result = _paymentValidator.Validate(req);

        result.IsValid.Should().BeTrue();
    }
}
