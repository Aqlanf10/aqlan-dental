using AqlanDentalPro.Application.DTOs.Appointments;
using AqlanDentalPro.Domain.Enums;
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

public sealed class BatchUpdateStatusRequestValidator : AbstractValidator<BatchUpdateStatusRequest>
{
    public BatchUpdateStatusRequestValidator()
    {
        RuleFor(x => x.AppointmentIds)
            .NotEmpty().WithMessage("قائمة المواعيد مطلوبة");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("الحالة مطلوبة")
            .Must(BeAValidStatus).WithMessage("الحالة يجب أن تكون: Scheduled, Confirmed, Arrived, Waiting, Called, InRoom, InProgress, Completed, Cancelled, NoShow");
    }

    private static bool BeAValidStatus(string status) =>
        Enum.TryParse<AppointmentStatus>(status, ignoreCase: true, out _);
}
