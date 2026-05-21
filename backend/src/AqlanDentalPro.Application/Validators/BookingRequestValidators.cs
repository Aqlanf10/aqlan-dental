using AqlanDentalPro.Application.DTOs.BookingRequests;
using FluentValidation;

namespace AqlanDentalPro.Application.Validators;

/// <summary>
/// Validates public booking request — PatientName and PhoneNumber are required.
/// </summary>
public sealed class CreateBookingRequestDtoValidator : AbstractValidator<CreateBookingRequestDto>
{
    public CreateBookingRequestDtoValidator()
    {
        RuleFor(x => x.PatientName)
            .NotEmpty().WithMessage("اسم المريض مطلوب")
            .MaximumLength(100).WithMessage("اسم المريض يجب ألا يتجاوز 100 حرف");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("رقم الهاتف مطلوب")
            .MaximumLength(20).WithMessage("رقم الهاتف يجب ألا يتجاوز 20 رقماً");

        RuleFor(x => x.Email)
            .MaximumLength(150).WithMessage("البريد الإلكتروني يجب ألا يتجاوز 150 حرفاً")
            .EmailAddress().WithMessage("صيغة البريد الإلكتروني غير صالحة")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.ServiceType)
            .MaximumLength(100).WithMessage("نوع الخدمة يجب ألا يتجاوز 100 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.ServiceType));

        RuleFor(x => x.PreferredDate)
            .MaximumLength(50).WithMessage("التاريخ المفضل يجب ألا يتجاوز 50 حرفاً")
            .When(x => !string.IsNullOrWhiteSpace(x.PreferredDate));

        RuleFor(x => x.PreferredTime)
            .MaximumLength(50).WithMessage("الوقت المفضل يجب ألا يتجاوز 50 حرفاً")
            .When(x => !string.IsNullOrWhiteSpace(x.PreferredTime));

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("الملاحظات يجب ألا تتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}

/// <summary>
/// Validates booking status update — status must be a known value.
/// </summary>
public sealed class UpdateBookingRequestStatusDtoValidator : AbstractValidator<UpdateBookingRequestStatusDto>
{
    public UpdateBookingRequestStatusDtoValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("الحالة مطلوبة")
            .Must(BeAValidStatus).WithMessage("الحالة يجب أن تكون: Pending, Reviewed, Confirmed, Rejected");

        RuleFor(x => x.StaffNotes)
            .MaximumLength(500).WithMessage("ملاحظات الموظف يجب ألا تتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.StaffNotes));
    }

    private static bool BeAValidStatus(string status) =>
        status is "Pending" or "Reviewed" or "Confirmed" or "Rejected";
}

/// <summary>
/// Validates conversion of booking request to appointment.
/// </summary>
public sealed class ConvertBookingRequestToAppointmentDtoValidator : AbstractValidator<ConvertBookingRequestToAppointmentDto>
{
    public ConvertBookingRequestToAppointmentDtoValidator()
    {
        // PatientId may be Guid.Empty — backend auto-resolves patient by phone
        RuleFor(x => x.DoctorId)
            .NotEmpty().WithMessage("معرف الطبيب مطلوب");

        RuleFor(x => x.AppointmentDate)
            .GreaterThan(default(DateOnly)).WithMessage("تاريخ الموعد غير صالح");

        RuleFor(x => x.StartTime)
            .GreaterThan(default(TimeOnly)).WithMessage("وقت البداية غير صالح");

        RuleFor(x => x.EndTime)
            .GreaterThan(default(TimeOnly)).WithMessage("وقت النهاية غير صالح");

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(5, 480).WithMessage("مدة الموعد يجب أن تكون بين 5 و 480 دقيقة");

        RuleFor(x => x.AppointmentType)
            .MaximumLength(100).WithMessage("نوع الموعد يجب ألا يتجاوز 100 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.AppointmentType));
    }
}

/// <summary>
/// Validates public booking cancellation — PhoneNumber is required.
/// </summary>
public sealed class CancelPublicBookingRequestValidator : AbstractValidator<CancelPublicBookingRequest>
{
    public CancelPublicBookingRequestValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("رقم الهاتف مطلوب")
            .MaximumLength(20).WithMessage("رقم الهاتف يجب ألا يتجاوز 20 رقماً");
    }
}
