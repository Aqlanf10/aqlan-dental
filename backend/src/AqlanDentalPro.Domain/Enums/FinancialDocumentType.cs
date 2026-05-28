namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// Discriminator indicating the type of source document for a JournalEntry.
/// Used to trace back from the journal entry to the originating financial event.
/// </summary>
public enum FinancialDocumentType
{
    Payment,           // دفعة مريض — Patient payment
    Refund,            // استرداد — Refund
    Invoice,           // فاتورة — Invoice issuance
    Expense,           // مصروف تشغيلي — Operational expense
    SalaryPayment,     // صرف راتب — Salary payment
    AdvancePayment,    // سلفة موظف — Employee advance payment
    CommissionPayment, // صرف عمولة — Doctor commission payment
    SupplierPayment,   // دفع مورد — Supplier bill payment
    VaultTransfer,     // ترحيل سيولة — Vault transfer / external deposit
    ContractCancellation, // إلغاء عقد — Contract cancellation reversal
    PaymentDeletion,   // حذف دفعة — Payment deletion reversal
    InstallmentPayment, // سداد قسط تقسيط — Installment payment
    Other              // أخرى — Unclassified
}
