namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// Discriminator indicating the source document for a JournalEntry.
/// </summary>
public enum FinancialDocumentType
{
    Payment,
    Refund,
    Invoice,
    Expense,
    SalaryPayment,
    AdvancePayment,
    CommissionPayment,
    SupplierPayment,
    CreditNoteRefund,
    VaultTransfer,
    ContractCancellation,
    PaymentDeletion,
    PaymentAllocation,
    Other,
    YearEndClosing,
    OpeningBalance,
    SupplierBill
}
