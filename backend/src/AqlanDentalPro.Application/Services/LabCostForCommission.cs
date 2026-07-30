namespace AqlanDentalPro.Application.Services;

/// <summary>
/// CORE-FIN-LAB-ADJ: the one place that answers "how much lab cost may be deducted before this
/// doctor's commission, expressed in the invoice's own currency".
/// <para>
/// Three rules live here because all three were previously implicit and each one cost the doctor
/// money when it was wrong:
/// </para>
/// <list type="number">
/// <item>
/// A DRAFT is not a decision. The clinic has not committed to the cost, so nothing here overrides
/// whatever estimate the line already carries — and because a lab order can never transition back
/// to draft (draft → sent | cancelled is the only way out), a draft is always the initial state
/// and never a retreat from a committed one.
/// </item>
/// <item>
/// A CANCELLED or deleted order is a decision, in the other direction: the cost is definitively
/// zero and the deduction must be released, or the doctor stays short for a bill nobody owes.
/// </item>
/// <item>
/// A lab order carries its own currency and rate; an invoice carries its own currency. Assigning
/// a SAR lab cost straight into a YER invoice line silently subtracts roughly one riyal per
/// hundred owed. Conversion goes through YER, which is the only rate either side stores — and
/// when neither side is YER there is no rate that relates them, so this refuses to guess.
/// </item>
/// </list>
/// </summary>
public static class LabCostForCommission
{
    public record Input(
        string? LabOrderStatus,
        bool LabOrderIsActive,
        decimal? TotalCost,
        decimal? Cost,
        string? LabOrderCurrency,
        decimal LabOrderExchangeRateToYer,
        string? InvoiceCurrency);

    public enum LabCostAction
    {
        /// <summary>The line's LabCost should be set to <c>Amount</c>.</summary>
        Write,

        /// <summary>The line's LabCost must be left exactly as it is.</summary>
        Leave,
    }

    /// <param name="Reason">
    /// Null when leaving the line alone is the ordinary answer (a draft). Non-null when something
    /// genuinely could not be resolved and a human needs to see it in the fix-up list.
    /// </param>
    public record Resolution(LabCostAction Action, decimal Amount, string? Reason)
    {
        public bool ShouldWrite => Action == LabCostAction.Write;

        /// <summary>True only for a <see cref="LabCostAction.Leave"/> that needs reporting.</summary>
        public bool NeedsAttention => Action == LabCostAction.Leave && Reason is not null;
    }

    /// <summary>
    /// The two statuses that are NOT a cost the clinic owes. Exposed as data rather than kept
    /// inside <see cref="IsCommitted"/> so an EF query can use it directly — the lab cost
    /// reports need exactly this boundary in SQL, and a second hand-written copy of the list
    /// would eventually disagree with the commission side.
    /// <para>
    /// Matching in SQL is case-sensitive, which is safe here: <c>LabOrdersController</c> writes
    /// statuses only from its own lower-case <c>ValidStatuses</c> list.
    /// </para>
    /// </summary>
    public static readonly string[] UncommittedStatuses = ["draft", "cancelled"];

    /// <summary>A committed cost is anything past draft that has not been cancelled.</summary>
    public static bool IsCommitted(string? status, bool isActive)
    {
        if (!isActive) return false;

        var s = (status ?? string.Empty).Trim();
        return s.Length > 0
            && !UncommittedStatuses.Contains(s, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>True when the order has been positively decided against — cancelled or deleted.</summary>
    public static bool IsRevoked(string? status, bool isActive)
        => !isActive || (status ?? string.Empty).Trim().Equals("cancelled", StringComparison.OrdinalIgnoreCase);

    public static Resolution Resolve(Input i)
    {
        if (IsRevoked(i.LabOrderStatus, i.LabOrderIsActive))
            return new Resolution(LabCostAction.Write, 0m, null);

        if (!IsCommitted(i.LabOrderStatus, i.LabOrderIsActive))
            return new Resolution(LabCostAction.Leave, 0m, null);   // still a draft

        var raw = i.TotalCost ?? i.Cost ?? 0m;
        if (raw <= 0m)
            return new Resolution(LabCostAction.Write, 0m, null);

        var labCurrency = Normalise(i.LabOrderCurrency);
        var invoiceCurrency = Normalise(i.InvoiceCurrency);

        if (string.Equals(labCurrency, invoiceCurrency, StringComparison.Ordinal))
            return new Resolution(LabCostAction.Write, Math.Round(raw, 2), null);

        if (invoiceCurrency == "YER")
        {
            var rate = i.LabOrderExchangeRateToYer;
            if (rate <= 0m)
                return new Resolution(LabCostAction.Leave, 0m,
                    $"طلب المعمل بعملة {labCurrency} بلا سعر صرف صالح إلى الريال اليمني — لم تُخصم تكلفة المعمل");

            return new Resolution(LabCostAction.Write, Math.Round(raw * rate, 2), null);
        }

        // Invoice in a foreign currency and the lab order in a different one: the only rate
        // either side stores is to YER, and nothing here relates the two. Refuse rather than
        // subtract a number that is wrong by the whole exchange rate.
        return new Resolution(LabCostAction.Leave, 0m,
            $"عملة الفاتورة ({invoiceCurrency}) تختلف عن عملة طلب المعمل ({labCurrency}) ولا يوجد سعر صرف بينهما — يلزم تحديد التكلفة يدويًا");
    }

    private static string Normalise(string? currency)
        => string.IsNullOrWhiteSpace(currency) ? "YER" : currency.Trim().ToUpperInvariant();
}
