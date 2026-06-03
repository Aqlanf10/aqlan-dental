using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Enums;
using Xunit;

namespace AqlanDentalPro.UnitTests.Lab;

/// <summary>
/// Lab Sprint 5 — Commission calculator tests verifying lab cost deduction.
/// When CommissionBaseRule is AfterDiscountAndCosts, lab cost is deducted from
/// the commissionable base before the doctor's commission is calculated.
/// </summary>
public class LabCommissionDeductionTests
{
    [Fact]
    public void LabCost_Deducted_From_Commission_When_AfterDiscountAndCosts()
    {
        var input = new CommissionCalculator.Input(
            TotalPrice: 1000, LineDiscountAmount: 0, MaterialCost: 0,
            LabCost: 300, OtherDirectCost: 0, DoctorCommissionPercentage: 40,
            BaseRule: CommissionBaseRule.AfterDiscountAndCosts
        );
        var result = CommissionCalculator.Calculate(input);
        Assert.Equal(700, result.NetCommissionableAmount);
        Assert.Equal(280, result.DoctorCommissionAmount);
        Assert.Equal(420, result.CenterShareAmount);
    }

    [Fact]
    public void LabCost_Not_Deducted_When_GrossAmount_Rule()
    {
        var input = new CommissionCalculator.Input(
            TotalPrice: 1000, LineDiscountAmount: 0, MaterialCost: 0,
            LabCost: 300, OtherDirectCost: 0, DoctorCommissionPercentage: 40,
            BaseRule: CommissionBaseRule.GrossAmount
        );
        var result = CommissionCalculator.Calculate(input);
        Assert.Equal(1000, result.NetCommissionableAmount);
        Assert.Equal(400, result.DoctorCommissionAmount);
    }

    [Fact]
    public void LabCost_Floors_Net_At_Zero()
    {
        var input = new CommissionCalculator.Input(
            TotalPrice: 500, LineDiscountAmount: 0, MaterialCost: 0,
            LabCost: 600, OtherDirectCost: 0, DoctorCommissionPercentage: 40,
            BaseRule: CommissionBaseRule.AfterDiscountAndCosts
        );
        var result = CommissionCalculator.Calculate(input);
        Assert.Equal(0, result.NetCommissionableAmount);
        Assert.Equal(0, result.DoctorCommissionAmount);
    }

    [Fact]
    public void LabCost_With_MaterialCost_Both_Deducted()
    {
        var input = new CommissionCalculator.Input(
            TotalPrice: 2000, LineDiscountAmount: 0, MaterialCost: 200,
            LabCost: 500, OtherDirectCost: 100, DoctorCommissionPercentage: 30,
            BaseRule: CommissionBaseRule.AfterDiscountAndCosts
        );
        var result = CommissionCalculator.Calculate(input);
        Assert.Equal(1200, result.NetCommissionableAmount);
        Assert.Equal(360, result.DoctorCommissionAmount);
    }

    [Fact]
    public void Proportional_Commission_With_Lab_Cost()
    {
        var payable = CommissionCalculator.ProportionalCommission(280, 0.5m);
        Assert.Equal(140, payable);
    }
}
