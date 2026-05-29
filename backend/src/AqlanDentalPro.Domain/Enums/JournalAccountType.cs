namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// Account type classification for JournalLine entries in the double-entry bookkeeping model.
/// Each JournalLine must reference one of these account types to indicate which side
/// of the accounting equation is affected.
///
/// IMPORTANT: Values are stored as strings in PostgreSQL (HasConversion&lt;string&gt;),
/// so adding new members anywhere is safe. However, explicit integer values are
/// assigned as a defensive measure — if any future code path reads these as integers,
/// the values will remain stable and never shift.
///
/// Finance V3 uses specific account types for accrual accounting:
/// - PatientReceivable replaces the generic Receivable (tracks per-patient AR)
/// - PatientAdvance tracks unallocated advance payments (liability)
/// - OwnerEquity replaces the generic Equity (for owner capital / opening balances)
/// - OtherReceivable classifies external deposits awaiting explanation
/// - InsuranceReceivable tracks amounts owed by insurance companies (V4)
/// </summary>
public enum JournalAccountType
{
    Treasury = 1,            // خزينة/صندوق — Cash or bank account
    PatientReceivable = 2,   // ذمم مرضى مدينة — Amounts owed to the clinic by patients (from issued invoices)
    PatientAdvance = 3,      // دفعات مقدمة مرضى — Unallocated advance payments (liability until invoiced)
    Payable = 4,             // ذمم دائنة — Amounts the clinic owes (e.g., supplier bills)
    Revenue = 5,             // إيرادات — Clinic income from services (recognized at invoice issuance)
    Expense = 6,             // مصروفات — Operational costs
    OwnerEquity = 7,         // حقوق الملكية — Owner's equity (capital deposits, opening balances)
    OtherReceivable = 8,     // ذمم مدينة أخرى — Classified external deposits awaiting allocation
    ContraRevenue = 9,       // إيرادات مقابلة — Deductions from revenue (discounts, allowances)
    ContraExpense = 10,      // مصروفات مقابلة — Reductions to expenses
    InsuranceReceivable = 90 // ذمم شركات التأمين — Amounts owed by insurance companies (V4 — high value to avoid future conflicts)
}
