namespace AqlanDentalPro.Domain.Enums
{
    /// <summary>
    /// حالة قسط التقسيط - تتبع حالة كل قسط في خطة التقسيط.
    /// النظام سيقوم بإرسال تذكيرات تلقائية للأقساط المتأخرة مستقبلاً.
    /// </summary>
    public enum InstallmentStatus
    {
        /// <summary>مستحق - القسط لم يحين موعد سداده بعد أو حالياً واجب الدفع.</summary>
        Pending = 0,

        /// <summary>مدفوع - تم سداد القسط بالكامل وربطه بسند قبض.</summary>
        Paid = 1,

        /// <summary>متأخر - تجاوز القسط تاريخ الاستحقاق دون سداد (سيتم إرسال تذكير تلقائي).</summary>
        Overdue = 2
    }
}
