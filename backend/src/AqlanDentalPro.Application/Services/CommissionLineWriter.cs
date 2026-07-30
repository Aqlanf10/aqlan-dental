using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Application.Services;

/// <summary>
/// The single writer that stamps a computed commission back onto an <see cref="InvoiceLineItem"/>.
/// <para>
/// Extracted so the resync service and <c>CommissionService</c> cannot drift apart: the
/// OnPaymentCollection carve-out below is subtle enough that a second copy of it would
/// eventually be written without the carve-out and would pay doctors commission on money the
/// clinic has not collected.
/// </para>
/// </summary>
public static class CommissionLineWriter
{
    /// <summary>Recomputes and writes NetCommissionableAmount / DoctorCommissionAmount / CenterShareAmount.</summary>
    /// <param name="recognitionMode">
    /// The owning service's recognition mode. Pass null when the service is unknown — that is
    /// treated as accrual, matching the previous behaviour of a null <c>item.Service</c>.
    /// </param>
    public static void ApplyCalculation(InvoiceLineItem item, CommissionRecognitionMode? recognitionMode)
    {
        var result = Compute(item);

        item.NetCommissionableAmount = result.NetCommissionableAmount;

        // FIN-09: under OnPaymentCollection, DoctorCommissionAmount is a PROPORTION of the
        // accrual set by TriggerOnPaymentCommissionsAsync from what has actually been collected.
        // Overwriting it with the full amount here would pay commission on uncollected revenue.
        if (recognitionMode != CommissionRecognitionMode.OnPaymentCollection)
        {
            item.DoctorCommissionAmount = result.DoctorCommissionAmount;
            item.CenterShareAmount      = result.CenterShareAmount;
        }
        else
        {
            item.CenterShareAmount = result.NetCommissionableAmount - item.DoctorCommissionAmount;
        }

        if (item.CommissionStatus == CommissionStatus.Pending && item.DoctorCommissionPercentage > 0)
            item.CommissionStatus = CommissionStatus.Calculated;
    }

    /// <summary>Pure calculation against the line's current fields — writes nothing.</summary>
    public static CommissionCalculator.Result Compute(InvoiceLineItem item)
        => CommissionCalculator.Calculate(new CommissionCalculator.Input(
            TotalPrice:                 item.TotalPrice,
            LineDiscountAmount:         item.LineDiscountAmount,
            MaterialCost:               item.MaterialCost,
            LabCost:                    item.LabCost,
            OtherDirectCost:            item.OtherDirectCost,
            DoctorCommissionPercentage: item.DoctorCommissionPercentage,
            BaseRule:                   item.CommissionBaseRule));

    /// <summary>Same calculation with the lab cost swapped for a hypothetical one.</summary>
    public static CommissionCalculator.Result ComputeWithLabCost(InvoiceLineItem item, decimal labCost)
        => CommissionCalculator.Calculate(new CommissionCalculator.Input(
            TotalPrice:                 item.TotalPrice,
            LineDiscountAmount:         item.LineDiscountAmount,
            MaterialCost:               item.MaterialCost,
            LabCost:                    labCost,
            OtherDirectCost:            item.OtherDirectCost,
            DoctorCommissionPercentage: item.DoctorCommissionPercentage,
            BaseRule:                   item.CommissionBaseRule));
}
