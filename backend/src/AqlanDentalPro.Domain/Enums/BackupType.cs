namespace AqlanDentalPro.Domain.Enums;

public enum BackupType
{
    Database = 0,    // قاعدة البيانات
    Files = 1,       // الملفات (صور/أشعة)
    Full = 2         // كامل
}

public enum BackupStatus
{
    Pending = 0,     // قيد الانتظار
    InProgress = 1,  // جارٍ التنفيذ
    Completed = 2,   // مكتمل
    Failed = 3       // فشل
}
