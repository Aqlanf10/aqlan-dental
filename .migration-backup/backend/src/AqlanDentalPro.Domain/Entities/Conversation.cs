using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// محادثة بين مستخدمين — يمكن أن تكون مباشرة (شخصين) أو جماعية.
/// StaffToPatient: محادثة داخلية حول مريض (لا تحتاج حساب مريض).
/// </summary>
public class Conversation : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public bool IsGroup { get; set; } = false;
    /// <summary>StaffToStaff | StaffToPatient</summary>
    public string ConversationType { get; set; } = "StaffToStaff";
    public Guid? CreatedBy { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public string? LastMessagePreview { get; set; }

    /// <summary>للمحادثات المتعلقة بمريض — لا يحتاج المريض حساب مستخدم</summary>
    public Guid? PatientId { get; set; }
    public Guid? BranchId { get; set; }

    /// <summary>
    /// نوع المستلم للمحادثات الموجهة من المريض: TreatingDoctor | Reception | Admin
    /// يُستخدم فقط مع المحادثات من نوع PatientFacing
    /// </summary>
    public string? RecipientType { get; set; }

    /// <summary>
    /// معرف المستخدم المستلم المحدد (مثل معرف الطبيب المعالج)
    /// يُستخدم فقط عندما يكون RecipientType = TreatingDoctor ويكون الطبيب معروفاً
    /// </summary>
    public Guid? RecipientUserId { get; set; }

    // Navigation
    public User? Creator { get; set; }
    public Patient? Patient { get; set; }
    public Branch? Branch { get; set; }
    public ICollection<ConversationParticipant> Participants { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];
}
