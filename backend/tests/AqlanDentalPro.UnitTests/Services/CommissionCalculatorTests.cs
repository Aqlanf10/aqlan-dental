using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Services;

/// <summary>
/// Unit tests for CommissionCalculator — pure formula verification.
/// No DB, no DI.
/// </summary>
public class CommissionCalculatorTests
{
    // ── AfterDiscountAndCosts (default rule) ──────────────────────────────────

    [Fact]
    public void Calculate_AfterDiscountAndCosts_FullExample()
    {
        // Arrange: treatment 100,000 | discount 10,000 | material 5,000 | lab 8,000 | doctor 40%
        var input = new CommissionCalculator.Input(
            TotalPrice: 100_000m,
            LineDiscountAmount: 10_000m,
            MaterialCost: 5_000m,
            LabCost: 8_000m,
            OtherDirectCost: 0m,
            DoctorCommissionPercentage: 40m,
            BaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        // Act
        var result = CommissionCalculator.Calculate(input);

        // Assert
        result.CommissionBase.Should().Be(90_000m);           // 100k − 10k
        result.NetCommissionableAmount.Should().Be(77_000m);  // 90k − 5k − 8k
        result.DoctorCommissionAmount.Should().Be(30_800m);   // 77k × 40%
        result.CenterShareAmount.Should().Be(46_200m);        // 77k − 30.8k
    }

    [Fact]
    public void Calculate_AfterDiscount_OnlyDeductsDiscount()
    {
        var input = new CommissionCalculator.Input(
            TotalPrice: 100_000m,
            LineDiscountAmount: 10_000m,
            MaterialCost: 5_000m,
            LabCost: 8_000m,
            OtherDirectCost: 2_000m,
            DoctorCommissionPercentage: 30m,
            BaseRule: CommissionBaseRule.AfterDiscount);

        var result = CommissionCalculator.Calculate(input);

        // Costs are NOT deducted for AfterDiscount rule
        result.CommissionBase.Should().Be(90_000m);
        result.NetCommissionableAmount.Should().Be(90_000m);   // costs not deducted
        result.DoctorCommissionAmount.Should().Be(27_000m);    // 90k × 30%
        result.CenterShareAmount.Should().Be(63_000m);
    }

    [Fact]
    public void Calculate_GrossAmount_IgnoresDiscountAndCosts()
    {
        var input = new CommissionCalculator.Input(
            TotalPrice: 100_000m,
            LineDiscountAmount: 10_000m,
            MaterialCost: 5_000m,
            LabCost: 8_000m,
            OtherDirectCost: 0m,
            DoctorCommissionPercentage: 25m,
            BaseRule: CommissionBaseRule.GrossAmount);

        var result = CommissionCalculator.Calculate(input);

        result.CommissionBase.Should().Be(100_000m);
        result.NetCommissionableAmount.Should().Be(100_000m);
        result.DoctorCommissionAmount.Should().Be(25_000m);
        result.CenterShareAmount.Should().Be(75_000m);
    }

    [Fact]
    public void Calculate_ZeroPercentage_YieldsZeroCommission()
    {
        var input = new CommissionCalculator.Input(
            TotalPrice: 50_000m,
            LineDiscountAmount: 0m,
            MaterialCost: 0m,
            LabCost: 0m,
            OtherDirectCost: 0m,
            DoctorCommissionPercentage: 0m,
            BaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        var result = CommissionCalculator.Calculate(input);

        result.NetCommissionableAmount.Should().Be(50_000m);
        result.DoctorCommissionAmount.Should().Be(0m);
        result.CenterShareAmount.Should().Be(50_000m);
    }

    [Fact]
    public void Calculate_CostsExceedGross_NetFlooredAtZero()
    {
        // Pathological case: costs > price (e.g., heavily discounted service)
        var input = new CommissionCalculator.Input(
            TotalPrice: 10_000m,
            LineDiscountAmount: 0m,
            MaterialCost: 8_000m,
            LabCost: 5_000m,
            OtherDirectCost: 0m,
            DoctorCommissionPercentage: 40m,
            BaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        var result = CommissionCalculator.Calculate(input);

        result.NetCommissionableAmount.Should().Be(0m);   // floored, not negative
        result.DoctorCommissionAmount.Should().Be(0m);
        result.CenterShareAmount.Should().Be(0m);
    }

    [Fact]
    public void Calculate_PercentageClampedAt100()
    {
        var input = new CommissionCalculator.Input(
            TotalPrice: 10_000m,
            LineDiscountAmount: 0m,
            MaterialCost: 0m,
            LabCost: 0m,
            OtherDirectCost: 0m,
            DoctorCommissionPercentage: 150m, // invalid > 100
            BaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        var result = CommissionCalculator.Calculate(input);

        result.DoctorCommissionAmount.Should().Be(10_000m);  // clamped to 100%
        result.CenterShareAmount.Should().Be(0m);
    }

    // ── Proportional commission (OnPaymentCollection mode) ───────────────────

    [Fact]
    public void ProportionalCommission_HalfPaid()
    {
        // Invoice net = 100k | doctor = 40k | patient paid 50%
        var payable = CommissionCalculator.ProportionalCommission(40_000m, 0.5m);
        payable.Should().Be(20_000m);
    }

    [Fact]
    public void ProportionalCommission_FullyPaid()
    {
        var payable = CommissionCalculator.ProportionalCommission(40_000m, 1m);
        payable.Should().Be(40_000m);
    }

    [Fact]
    public void ProportionalCommission_RatioClamped()
    {
        // Ratio > 1 should be treated as 1
        var payable = CommissionCalculator.ProportionalCommission(40_000m, 1.5m);
        payable.Should().Be(40_000m);
    }

    // ── Material cost resolution ──────────────────────────────────────────────

    [Fact]
    public void ResolveMaterialCost_FixedAmount_ReturnsConfiguredValue()
    {
        var cost = CommissionCalculator.ResolveMaterialCost(
            servicePrice: 100_000m,
            configuredCost: 5_000m,
            costType: MaterialCostType.FixedAmount);

        cost.Should().Be(5_000m);
    }

    [Fact]
    public void ResolveMaterialCost_Percentage_ComputesFromServicePrice()
    {
        var cost = CommissionCalculator.ResolveMaterialCost(
            servicePrice: 100_000m,
            configuredCost: 8m,      // 8%
            costType: MaterialCostType.PercentageOfServicePrice);

        cost.Should().Be(8_000m);
    }

    // ── Rounding ──────────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_RoundsToTwoDecimalPlaces()
    {
        var input = new CommissionCalculator.Input(
            TotalPrice: 10_001m,
            LineDiscountAmount: 0m,
            MaterialCost: 0m,
            LabCost: 0m,
            OtherDirectCost: 0m,
            DoctorCommissionPercentage: 33m,
            BaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        var result = CommissionCalculator.Calculate(input);

        // 10001 × 33% = 3300.33
        result.DoctorCommissionAmount.Should().Be(3_300.33m);
    }
}
