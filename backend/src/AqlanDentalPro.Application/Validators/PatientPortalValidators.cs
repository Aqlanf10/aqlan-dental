using AqlanDentalPro.Application.DTOs.PatientPortal;
using FluentValidation;

namespace AqlanDentalPro.Application.Validators;

/// <summary>
/// Validates patient portal login requests — public-facing auth endpoint.
/// </summary>
public sealed class PatientLoginRequestValidator : AbstractValidator<PatientLoginRequest>
{
    public PatientLoginRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("اسم المستخدم مطلوب")
            .MaximumLength(100).WithMessage("اسم المستخدم يجب ألا يتجاوز 100 حرف");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("كلمة المرور مطلوبة");
    }
}

/// <summary>
/// Validates forgot-password request — must have a valid phone number.
/// </summary>
public sealed class PatientForgotPasswordRequestValidator : AbstractValidator<PatientForgotPasswordRequest>
{
    public PatientForgotPasswordRequestValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("رقم الهاتف مطلوب")
            .MaximumLength(20).WithMessage("رقم الهاتف يجب ألا يتجاوز 20 رقماً");

        // RecaptchaToken is optional and validated by the service when configured
    }
}

/// <summary>
/// Validates password reset request — phone + code + new password.
/// </summary>
public sealed class PatientResetPasswordRequestValidator : AbstractValidator<PatientResetPasswordRequest>
{
    public PatientResetPasswordRequestValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("رقم الهاتف مطلوب")
            .MaximumLength(20).WithMessage("رقم الهاتف يجب ألا يتجاوز 20 رقماً");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("رمز التحقق مطلوب")
            .MaximumLength(10).WithMessage("رمز التحقق يجب ألا يتجاوز 10 أحرف");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("كلمة المرور الجديدة مطلوبة")
            .MinimumLength(6).WithMessage("كلمة المرور يجب أن تكون 6 أحرف على الأقل")
            .MaximumLength(100).WithMessage("كلمة المرور يجب ألا تتجاوز 100 حرف");
    }
}

/// <summary>
/// Validates password change request for authenticated patients.
/// </summary>
public sealed class PatientChangePasswordRequestValidator : AbstractValidator<PatientChangePasswordRequest>
{
    public PatientChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("كلمة المرور الحالية مطلوبة");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("كلمة المرور الجديدة مطلوبة")
            .MinimumLength(6).WithMessage("كلمة المرور يجب أن تكون 6 أحرف على الأقل")
            .MaximumLength(100).WithMessage("كلمة المرور يجب ألا تتجاوز 100 حرف");
    }
}

/// <summary>
/// Validates patient profile update — only optional contact fields.
/// </summary>
public sealed class PatientProfileUpdateDtoValidator : AbstractValidator<PatientProfileUpdateDto>
{
    public PatientProfileUpdateDtoValidator()
    {
        RuleFor(x => x.Phone)
            .MaximumLength(20).WithMessage("رقم الهاتف يجب ألا يتجاوز 20 رقماً")
            .Matches(@"^[\d+\-\(\)\s]*$").WithMessage("رقم الهاتف يحتوي على أحرف غير صالحة")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.WhatsApp)
            .MaximumLength(20).WithMessage("رقم الواتساب يجب ألا يتجاوز 20 رقماً")
            .Matches(@"^[\d+\-\(\)\s]*$").WithMessage("رقم الواتساب يحتوي على أحرف غير صالحة")
            .When(x => !string.IsNullOrWhiteSpace(x.WhatsApp));

        RuleFor(x => x.Address)
            .MaximumLength(300).WithMessage("العنوان يجب ألا يتجاوز 300 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Address));
    }
}

/// <summary>
/// Validates patient appointment booking request.
/// </summary>
public sealed class PatientAppointmentRequestDtoValidator : AbstractValidator<PatientAppointmentRequestDto>
{
    public PatientAppointmentRequestDtoValidator()
    {
        RuleFor(x => x.AppointmentDate)
            .NotEmpty().WithMessage("تاريخ الموعد مطلوب")
            .Must(BeAValidDate).WithMessage("تاريخ الموعد غير صالح");

        RuleFor(x => x.StartTime)
            .NotEmpty().WithMessage("وقت البداية مطلوب")
            .Must(BeAValidTime).WithMessage("وقت البداية غير صالح");

        RuleFor(x => x.AppointmentType)
            .NotEmpty().WithMessage("نوع الموعد مطلوب")
            .MaximumLength(100).WithMessage("نوع الموعد يجب ألا يتجاوز 100 حرف");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("الملاحظات يجب ألا تتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }

    private static bool BeAValidDate(string? date)
    {
        if (string.IsNullOrWhiteSpace(date)) return false;
        return DateOnly.TryParse(date, out _);
    }

    private static bool BeAValidTime(string? time)
    {
        if (string.IsNullOrWhiteSpace(time)) return false;
        return TimeOnly.TryParse(time, out _);
    }
}
