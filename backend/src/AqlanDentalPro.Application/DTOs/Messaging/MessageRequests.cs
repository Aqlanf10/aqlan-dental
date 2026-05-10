namespace AqlanDentalPro.Application.DTOs.Messaging;

/// <summary>إنشاء محادثة جديدة</summary>
public class CreateConversationRequest
{
    public string? Title { get; set; }
    public bool IsGroup { get; set; } = false;
    public List<Guid> ParticipantIds { get; set; } = [];
    public string? InitialMessage { get; set; }

    /// <summary>نوع المحادثة: StaffToStaff أو StaffToPatient</summary>
    public string? ConversationType { get; set; }

    /// <summary>معرف المريض للمحادثة المرتبطة بمريض</summary>
    public Guid? PatientId { get; set; }
}

/// <summary>إرسال رسالة</summary>
public class SendMessageRequest
{
    public string Content { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public string? AttachmentName { get; set; }
    public string? AttachmentType { get; set; }
    public Guid? ReplyToId { get; set; }
}

/// <summary>عدد الرسائل غير المقروءة</summary>
public class UnreadCountDto
{
    public int TotalUnread { get; set; }
    public int UnreadConversations { get; set; }
}

/// <summary>تعديل رسالة (المرسل فقط، خلال 15 دقيقة)</summary>
public class EditMessageRequest
{
    public string Content { get; set; } = string.Empty;
}

/// <summary>المريض يبدأ محادثة مع المركز</summary>
public class StartConversationRequest
{
    public string? InitialMessage { get; set; }

    /// <summary>نوع المستلم: TreatingDoctor | Reception | Admin</summary>
    public string? RecipientType { get; set; }

    /// <summary>معرف المستخدم المستلم المحدد (مثل معرف الطبيب)</summary>
    public Guid? RecipientUserId { get; set; }
}

/// <summary>مستلم متاح للمريض</summary>
public class PortalRecipientDto
{
    public string Type { get; set; } = string.Empty; // TreatingDoctor | Reception | Admin
    public Guid? UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? AvatarInitials { get; set; }
    public string? Color { get; set; }
}
