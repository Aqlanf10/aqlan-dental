using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Domain.Entities;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Shared static mappers + currency normalizer for the finance module.
///
/// Extracted from <c>FinanceService</c> as part of TD-021 PR A2 so that both
/// <c>FinanceService</c> (write side) and <c>FinanceReadService</c> (read side)
/// can use the same mapping logic without duplicating it. This is the
/// "split" approach recommended in the TD-021 plan for shared private helpers.
///
/// Marked <c>internal static</c> because these are pure functions with no
/// state — they belong to the Infrastructure layer and are not part of any
/// public contract.
/// </summary>
internal static class FinanceMappers
{
    /// <summary>
    /// Base currency for the system. All monetary amounts have a corresponding
    /// AccountCurrency that is normalized to one of <see cref="SupportedCurrencies"/>.
    /// </summary>
    public const string BaseCurrency = "YER";

    /// <summary>
    /// Currencies the system supports for multi-currency payments.
    /// Used by <see cref="NormalizeCurrency"/> to reject unknown codes early.
    /// </summary>
    public static readonly HashSet<string> SupportedCurrencies = ["YER", "SAR", "USD"];

    /// <summary>
    /// Maps a <see cref="Payment"/> entity to its DTO representation.
    /// Handles null patient/doctor/invoice gracefully (they may not be loaded).
    /// Uses <see cref="NormalizeCurrency"/> to ensure AccountCurrency is always
    /// a valid uppercase code.
    /// </summary>
    public static PaymentDto MapPayment(Payment p) => new()
    {
        Id = p.Id,
        PatientId = p.PatientId,
        PatientName = string.Join(" ", new[] { p.Patient?.FirstName, p.Patient?.LastName }.Where(n => !string.IsNullOrEmpty(n))),
        ContractId = p.ContractId,
        InvoiceId = p.InvoiceId,
        InvoiceNumber = p.Invoice?.InvoiceNumber,
        Amount = p.Amount,
        Currency = p.Currency,
        AccountCurrency = NormalizeCurrency(p.AccountCurrency),
        ExchangeRateToAccountCurrency = p.ExchangeRateToAccountCurrency == 0 ? 1m : p.ExchangeRateToAccountCurrency,
        AppliedAmount = p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount,
        ExchangeRateSource = p.ExchangeRateSource,
        PaymentDate = p.PaymentDate.ToString("yyyy-MM-dd"),
        PaymentMethod = p.PaymentMethod,
        ServiceDescription = p.ServiceDescription,
        Specialty = p.Specialty,
        DoctorName = p.Doctor?.Name,
        ReceiptNumber = p.ReceiptNumber,
        Notes = p.Notes
    };

    /// <summary>
    /// Normalizes a currency code to uppercase and validates it against
    /// <see cref="SupportedCurrencies"/>. Empty/null input defaults to
    /// <see cref="BaseCurrency"/> (YER).
    /// Throws <see cref="ArgumentException"/> with an Arabic message for
    /// unsupported codes — this surfaces as a 400 to the user.
    /// </summary>
    public static string NormalizeCurrency(string? currency)
    {
        var code = string.IsNullOrWhiteSpace(currency) ? BaseCurrency : currency.Trim().ToUpperInvariant();
        if (!SupportedCurrencies.Contains(code))
            throw new ArgumentException("العملة يجب أن تكون YER أو SAR أو USD");
        return code;
    }
}
