using AqlanDentalPro.Application.DTOs.Appointments;
using AqlanDentalPro.Application.DTOs.Messaging;
using FluentValidation;

namespace AqlanDentalPro.Application.Validators;

/// <summary>
/// Validates appointment status update.
/// </summary>
public sealed class UpdateAppointmentStatusRequestValidator : AbstractValidator<UpdateAppointmentStatusRequest>
{
    public UpdateAppointmentStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("الحالة مطلوبة")
            .Must(BeAValidStatus).WithMessage("الحالة يجب أن تكون: scheduled, confirmed, completed, cancelled, no_show");

    }

    private static bool BeAValidStatus(string status) =>
        status is "scheduled" or "confirmed" or "completed" or "cancelled" or "no_show";
}

/// <summary>
/// Validates call patient to room request.
/// </summary>
public sealed class CallPatientRequestValidator : AbstractValidator<CallPatientRequest>
{
    public CallPatientRequestValidator()
    {
        RuleFor(x => x.RoomName)
            .NotEmpty().WithMessage("اسم الغرفة مطلوب")
            .MaximumLength(50).WithMessage("اسم الغرفة يجب ألا يتجاوز 50 حرفاً");
    }
}

/// <summary>
/// Validates appointment conflict check.
/// </summary>
public sealed class CheckConflictRequestValidator : AbstractValidator<CheckConflictRequest>
{
    public CheckConflictRequestValidator()
    {
        RuleFor(x => x.DoctorId)
            .NotEmpty().WithMessage("معرف الطبيب مطلوب");

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("التاريخ مطلوب")
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage("تنسيق التاريخ غير صالح (yyyy-MM-dd)");

        RuleFor(x => x.StartTime)
            .NotEmpty().WithMessage("وقت البداية مطلوب")
            .Must(t => TimeOnly.TryParse(t, out _)).WithMessage("تنسيق وقت البداية غير صالح (HH:mm)");

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(5, 480).WithMessage("المدة يجب أن تكون بين 5 و 480 دقيقة");
    }
}

/// <summary>
/// Validates conversation creation request.
/// </summary>
public sealed class CreateConversationRequestValidator : AbstractValidator<CreateConversationRequest>
{
    public CreateConversationRequestValidator()
    {
        RuleFor(x => x.ParticipantIds)
            .NotEmpty().WithMessage("يجب تحديد مشارك واحد على الأقل")
            .Must(ids => ids.Count <= 50).WithMessage("لا يمكن إضافة أكثر من 50 مشارك");

        RuleFor(x => x.Title)
            .MaximumLength(200).WithMessage("العنوان يجب ألا يتجاوز 200 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Title));

        RuleFor(x => x.InitialMessage)
            .MaximumLength(2000).WithMessage("الرسالة الأولية يجب ألا تتجاوز 2000 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.InitialMessage));

        RuleFor(x => x.ConversationType)
            .Must(BeAValidType).WithMessage("نوع المحادثة يجب أن يكون: StaffToStaff أو StaffToPatient")
            .When(x => !string.IsNullOrWhiteSpace(x.ConversationType));
    }

    private static bool BeAValidType(string? type) =>
        type is "StaffToStaff" or "StaffToPatient";
}

/// <summary>
/// Validates send message request.
/// Allows attachment-only messages (no Content required when attachments are present).
/// </summary>
public sealed class MessagingSendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public MessagingSendMessageRequestValidator()
    {
        // Content is optional when attachments are present, but must respect max length when provided
        RuleFor(x => x.Content)
            .MaximumLength(2000).WithMessage("محتوى الرسالة يجب ألا يتجاوز 2000 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Content));

        // Cross-field validation: at least one of Content, AttachmentUrl, or Attachments must be present
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Content)
                     || !string.IsNullOrWhiteSpace(x.AttachmentUrl)
                     || (x.Attachments != null && x.Attachments.Count > 0))
            .WithMessage("يجب أن تحتوي الرسالة على نص أو مرفق");

        RuleFor(x => x.AttachmentUrl)
            .MaximumLength(500).WithMessage("رابط المرفق يجب ألا يتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.AttachmentUrl));

        RuleFor(x => x.AttachmentName)
            .MaximumLength(200).WithMessage("اسم المرفق يجب ألا يتجاوز 200 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.AttachmentName));

        // Validate individual attachments when present
        RuleForEach(x => x.Attachments ?? new List<AttachmentItem>())
            .ChildRules(att =>
            {
                att.RuleFor(a => a.FileUrl)
                    .NotEmpty().WithMessage("رابط المرفق مطلوب")
                    .MaximumLength(1000).WithMessage("رابط المرفق يجب ألا يتجاوز 1000 حرف");
                att.RuleFor(a => a.FileName)
                    .NotEmpty().WithMessage("اسم المرفق مطلوب")
                    .MaximumLength(255).WithMessage("اسم المرفق يجب ألا يتجاوز 255 حرف");
                att.RuleFor(a => a.MimeType)
                    .NotEmpty().WithMessage("نوع المرفق مطلوب")
                    .MaximumLength(100).WithMessage("نوع المرفق يجب ألا يتجاوز 100 حرف");
                att.RuleFor(a => a.FileSize)
                    .LessThanOrEqualTo(10 * 1024 * 1024).WithMessage("حجم المرفق يجب ألا يتجاوز 10 ميجابايت");
            });
    }
}

/// <summary>
/// Validates message edit request.
/// </summary>
public sealed class EditMessageRequestValidator : AbstractValidator<EditMessageRequest>
{
    public EditMessageRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("محتوى الرسالة مطلوب")
            .MaximumLength(2000).WithMessage("محتوى الرسالة يجب ألا يتجاوز 2000 حرف");
    }
}

/// <summary>
/// Validates patient starting a conversation with clinic.
/// </summary>
public sealed class StartConversationRequestValidator : AbstractValidator<StartConversationRequest>
{
    public StartConversationRequestValidator()
    {
        RuleFor(x => x.InitialMessage)
            .MaximumLength(2000).WithMessage("الرسالة يجب ألا تتجاوز 2000 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.InitialMessage));

        RuleFor(x => x.RecipientType)
            .Must(BeAValidRecipientType).WithMessage("نوع المستلم يجب أن يكون: TreatingDoctor أو Reception أو Admin")
            .When(x => !string.IsNullOrWhiteSpace(x.RecipientType));
    }

    private static bool BeAValidRecipientType(string? type) =>
        type is "TreatingDoctor" or "Reception" or "Admin";
}
