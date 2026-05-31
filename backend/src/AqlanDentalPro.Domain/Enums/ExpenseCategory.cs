namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// Specific category of an operational expense.
/// </summary>
public enum ExpenseCategory
{
    Rent,            // إيجار المركز/الفروع
    Utilities,       // كهرباء، مياه، اتصالات، إنترنت
    LabFees,         // تكاليف معامل/مختبرات الأسنان الخارجية
    Marketing,       // إعلانات وتسويق
    ClinicSupplies,  // مستلزمات ومواد طبية وعيادية
    Maintenance,     // صيانة أجهزة وعيادات
    Salaries,        // رواتب وأجور موظفين
    Commissions,     // عمولات ومستحقات أطباء
    Taxes,           // ضرائب ورسوم حكومية
    Miscellaneous    // نثريات ومصاريف أخرى
}
