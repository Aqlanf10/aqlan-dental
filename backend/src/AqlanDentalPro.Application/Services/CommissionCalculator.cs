using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Application.Services;

/// <summary>
/// Pure static commission formula — no DI, fully unit-testable.
///
/// Formula:
///   Base = TotalPrice (gross)          [GrossAmount rule]
///        | TotalPrice − LineDiscount   [AfterDiscount rule]
///        | TotalPrice − LineDiscount   [AfterDiscountAndCosts rule — discount applied to base]
///
///   NetCommissionableAmount = Base − MaterialCost − LabCost − OtherDirectCost
///   DoctorCommissionAmount  = NetCommissionableAmount × Percentage / 100
///   CenterShareAmount       = NetCommissionableAmount − DoctorCommissionAmount
///
/// NetCommissionableAmount is floored at 0 (cannot be negative for commission purposes).
/// </summary>
public static class CommissionCalculator
{
    public record Input(
        decimal TotalPrice,
        decimal LineDiscountAmount,
        decimal MaterialCost,
        decimal LabCost,
        decimal OtherDirectCost,
        decimal DoctorCommissionPercentage,
        CommissionBaseRule BaseRule);

    public record Result(
        decimal CommissionBase,
        decimal NetCommissionableAmount,
        decimal DoctorCommissionAmount,
        decimal CenterShareAmount);

    public static Result Calculate(Input i)
    {
        // 1. Determine the commission base
        // FIN-23: AfterDiscount and AfterDiscountAndCosts produce the same commissionBase
        // (TotalPrice - LineDiscount). The difference is in step 2 where AfterDiscountAndCosts
        // also deducts direct costs. The naming is misleading but the logic is correct.
        // Added this comment to prevent a future developer from "fixing" the duplicate case
        // by merging them — the cost deduction in step 2 depends on the distinction.
        var commissionBase = i.BaseRule switch
        {
            CommissionBaseRule.GrossAmount           => i.TotalPrice,
            CommissionBaseRule.AfterDiscount         => i.TotalPrice - i.LineDiscountAmount,
            CommissionBaseRule.AfterDiscountAndCosts => i.TotalPrice - i.LineDiscountAmount, // costs deducted in step 2
            _                                        => i.TotalPrice - i.LineDiscountAmount,
        };
        commissionBase = Math.Max(0, commissionBase);

        // 2. Deduct direct costs (only for AfterDiscountAndCosts)
        decimal net;
        if (i.BaseRule == CommissionBaseRule.AfterDiscountAndCosts)
            net = commissionBase - i.MaterialCost - i.LabCost - i.OtherDirectCost;
        else
            net = commissionBase; // costs not deducted for other rules

        net = Math.Max(0, net); // floor at 0

        // 3. Doctor commission
        var pct = Math.Clamp(i.DoctorCommissionPercentage, 0, 100);
        var doctorAmount = Math.Round(net * pct / 100m, 2);
        var centerAmount = Math.Round(net - doctorAmount, 2);

        return new Result(commissionBase, net, doctorAmount, centerAmount);
    }

    /// <summary>
    /// Proportional commission when payment mode is OnPaymentCollection.
    /// Example: net = 100,000 | doctorCommission = 40,000 | paidRatio = 0.5
    ///          → payable = 20,000
    /// </summary>
    public static decimal ProportionalCommission(decimal doctorCommissionAmount, decimal paidRatio)
    {
        var ratio = Math.Clamp(paidRatio, 0, 1);
        return Math.Round(doctorCommissionAmount * ratio, 2);
    }

    /// <summary>
    /// Effective material cost: resolves PercentageOfServicePrice to an amount.
    /// </summary>
    public static decimal ResolveMaterialCost(
        decimal servicePrice,
        decimal configuredCost,
        MaterialCostType costType)
    {
        return costType switch
        {
            MaterialCostType.FixedAmount              => configuredCost,
            MaterialCostType.PercentageOfServicePrice => Math.Round(servicePrice * configuredCost / 100m, 2),
            _                                         => configuredCost,
        };
    }
}
