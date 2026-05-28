namespace AqlanDentalPro.Domain.Enums
{
    /// <summary>
    /// حالة مطالبة التأمين - تتبع دورة حياة المطالبة من التقديم حتى السداد.
    /// </summary>
    public enum ClaimStatus
    {
        /// <summary>قيد الانتظار - تم تقديم المطالبة ولم يتم البت فيها بعد.</summary>
        Pending = 0,

        /// <summary>تمت الموافقة - وافقت شركة التأمين على المطالبة.</summary>
        Approved = 1,

        /// <summary>مرفوضة - رفضت شركة التأمين المطالبة مع ذكر السبب.</summary>
        Rejected = 2,

        /// <summary>تم السداد - قامت شركة التأمين بسداد المبلغ المغطى.</summary>
        Paid = 3
    }
}
