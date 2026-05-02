namespace AqlanDentalPro.Domain.Enums;

public enum ConversationType
{
    StaffToStaff = 0,
    /// <summary>محادثة داخلية حول مريض — يراها الطاقم فقط، المريض لا يراها أبداً</summary>
    StaffToPatient = 1,
    /// <summary>محادثة مرئية للمريض — يراها المريض والطاقم، تظهر في بوابة المريض</summary>
    PatientFacing = 2
}
