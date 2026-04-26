using AqlanDentalPro.Application.DTOs.Appointments;
using FluentValidation;

namespace AqlanDentalPro.Application.Validators;

public sealed class CreateAppointmentRequestValidator : AbstractValidator<CreateAppointmentRequest>
{
    public CreateAppointmentRequestValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty().WithMessage("المريض مطلوب");

        RuleFor(x => x.DoctorId)
            .NotEmpty().WithMessage("الطبيب مطلوب");

        RuleFor(x => x.AppointmentDate)
            .NotEmpty().WithMessage("تاريخ الموعد مطلوب")
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage("تنسيق تاريخ الموعد غير صالح (yyyy-MM-dd)");

        RuleFor(x => x.StartTime)
            .NotEmpty().WithMessage("وقت البداية مطلوب")
            .Must(t => TimeOnly.TryParse(t, out _)).WithMessage("تنسيق وقت البداية غير صالح (HH:mm)");

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(5, 480).WithMessage("مدة الموعد يجب أن تكون بين 5 و480 دقيقة");

        RuleFor(x => x.AppointmentType)
            .NotEmpty().WithMessage("نوع الموعد مطلوب")
            .MaximumLength(100).WithMessage("نوع الموعد يجب ألا يتجاوز 100 حرف");
    }
}
