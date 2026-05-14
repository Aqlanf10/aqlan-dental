using AqlanDentalPro.Application.DTOs.WhatsApp;
using FluentValidation;

namespace AqlanDentalPro.Application.Validators;

/// <summary>
/// Validates single WhatsApp message send request.
/// </summary>
public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty().WithMessage("معرف المريض مطلوب");

        RuleFor(x => x.TemplateType)
            .NotEmpty().WithMessage("نوع القالب مطلوب")
            .MaximumLength(100).WithMessage("نوع القالب يجب ألا يتجاوز 100 حرف");

        RuleFor(x => x.CustomMessage)
            .MaximumLength(1000).WithMessage("الرسالة المخصصة يجب ألا تتجاوز 1000 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.CustomMessage));
    }
}

/// <summary>
/// Validates bulk WhatsApp message request.
/// </summary>
public sealed class BulkSendMessageRequestValidator : AbstractValidator<BulkSendMessageRequest>
{
    public BulkSendMessageRequestValidator()
    {
        RuleFor(x => x.PatientIds)
            .NotEmpty().WithMessage("قائمة المرضى مطلوبة")
            .Must(ids => ids.Count <= 200).WithMessage("لا يمكن إرسال رسائل لأكثر من 200 مريض في المرة الواحدة");

        RuleFor(x => x.TemplateType)
            .NotEmpty().WithMessage("نوع القالب مطلوب")
            .MaximumLength(100).WithMessage("نوع القالب يجب ألا يتجاوز 100 حرف");
    }
}

/// <summary>
/// Validates appointment reminder scheduling.
/// </summary>
public sealed class SendAppointmentReminderRequestValidator : AbstractValidator<SendAppointmentReminderRequest>
{
    public SendAppointmentReminderRequestValidator()
    {
        RuleFor(x => x.AppointmentId)
            .NotEmpty().WithMessage("معرف الموعد مطلوب");

        RuleFor(x => x.HoursBefore)
            .InclusiveBetween(1, 168).WithMessage("يجب أن يكون التذكير بين 1 و 168 ساعة قبل الموعد");
    }
}

/// <summary>
/// Validates WhatsApp template update.
/// </summary>
public sealed class UpdateWhatsAppTemplateRequestValidator : AbstractValidator<UpdateWhatsAppTemplateRequest>
{
    public UpdateWhatsAppTemplateRequestValidator()
    {
        RuleFor(x => x.ContentTemplate)
            .NotEmpty().WithMessage("محتوى القالب مطلوب")
            .MaximumLength(2000).WithMessage("محتوى القالب يجب ألا يتجاوز 2000 حرف");
    }
}
