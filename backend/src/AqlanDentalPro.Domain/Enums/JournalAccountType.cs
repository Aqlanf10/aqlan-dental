namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// Account type classification for JournalLine entries in the double-entry bookkeeping model.
/// Each JournalLine must reference one of these account types to indicate which side
/// of the accounting equation is affected.
///
/// Finance V3 uses specific account types for accrual accounting:
/// - PatientReceivable replaces the generic Receivable (tracks per-patient AR)
/// - PatientAdvance tracks unallocated advance payments (liability)
/// - OwnerEquity replaces the generic Equity (for owner capital / opening balances)
/// - OtherReceivable classifies external deposits awaiting explanation
/// </summary>
public enum JournalAccountType
{
    Treasury,          // خزينة/صندوق — Cash or bank account
    PatientReceivable, // ذمم مرضى مدينة — Amounts owed to the clinic by patients (from issued invoices)
    PatientAdvance,    // دفعات مقدمة مرضى — Unallocated advance payments (liability until invoiced)
    Payable,           // ذمم دائنة — Amounts the clinic owes (e.g., supplier bills)
    Revenue,           // إيرادات — Clinic income from services (recognized at invoice issuance)
    Expense,           // مصروفات — Operational costs
    OwnerEquity,       // حقوق الملكية — Owner's equity (capital deposits, opening balances)
    OtherReceivable,   // ذمم مدينة أخرى — Classified external deposits awaiting allocation
    InsuranceReceivable, // ذمم شركات التأمين — Amounts owed to the clinic by insurance companies
    ContraRevenue,     // إيرادات مقابلة — Deductions from revenue (discounts, allowances)
    ContraExpense      // مصروفات مقابلة — Reductions to expenses
}
