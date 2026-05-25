namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// Broad category of a financial transaction for bookkeeping and reporting.
/// </summary>
public enum FinancialCategory
{
    PatientPayment,     // دفعة مريض
    SupplierPayment,    // دفعة مورد (مواد وأدوات)
    SalaryPayment,      // صرف راتب موظف
    DoctorCommission,   // صرف عمولة طبيب
    OperationalExpense, // مصروف تشغيلي (إيجار، منافع، إلخ)
    Refund,             // استرداد دفعة مريض
    GeneralCost,        // تكاليف عامة
    InternalTransfer,   // تحويل سيولة داخلي بين الخزائن
    SalaryAdvance       // سلفة موظف
}
