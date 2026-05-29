namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// Discriminator indicating the type of source document for a JournalEntry.
/// Used to trace back from the journal entry to the originating financial event.
///
/// IMPORTANT: Values are stored as strings in PostgreSQL (HasConversion&lt;string&gt;),
/// so adding new members anywhere is safe. However, explicit integer values are
/// assigned as a defensive measure — if any future code path reads these as integers,
/// the values will remain stable and never shift.
/// </summary>
public enum FinancialDocumentType
{
    Payment = 1,              // دفعة مريض — Patient payment
    Refund = 2,               // استرداد — Refund
    Invoice = 3,              // فاتورة — Invoice issuance
    Expense = 4,              // مصروف تشغيلي — Operational expense
    SalaryPayment = 5,        // صرف راتب — Salary payment
    AdvancePayment = 6,       // سلفة موظف — Employee advance payment
    CommissionPayment = 7,    // صرف عمولة — Doctor commission payment
    SupplierPayment = 8,      // دفع مورد — Supplier bill payment
    VaultTransfer = 9,        // ترحيل سيولة — Vault transfer / external deposit
    ContractCancellation = 10, // إلغاء عقد — Contract cancellation reversal
    PaymentDeletion = 11,     // حذف دفعة — Payment deletion reversal
    InstallmentPayment = 90,  // سداد قسط تقسيط — Installment payment (V4 — high value to avoid future conflicts)
    Other = 99                // أخرى — Unclassified
}
