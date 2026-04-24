using AqlanDentalPro.Application.DTOs.Ortho;
using FluentValidation;

namespace AqlanDentalPro.Application.Validators;

public sealed class CreateOrthoCaseRequestValidator : AbstractValidator<CreateOrthoCaseRequest>
{
    public CreateOrthoCaseRequestValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty().WithMessage("المريض مطلوب");

        RuleFor(x => x.ApplianceType)
            .NotEmpty().WithMessage("نوع الجهاز مطلوب")
            .MaximumLength(100).WithMessage("نوع الجهاز يجب ألا يتجاوز 100 حرف");

        RuleFor(x => x.ExpectedDurationMonths)
            .InclusiveBetween(1, 120).WithMessage("المدة المتوقعة يجب أن تكون بين شهر و120 شهراً")
            .When(x => x.ExpectedDurationMonths.HasValue);

        RuleFor(x => x.TotalFee)
            .GreaterThanOrEqualTo(0).WithMessage("الرسوم الإجمالية يجب أن تكون صفراً أو أكثر")
            .When(x => x.TotalFee.HasValue);

        RuleFor(x => x.StartDate)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage("تنسيق تاريخ البداية غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.StartDate));
    }
}

public sealed class CreateOrthoVisitRequestValidator : AbstractValidator<CreateOrthoVisitRequest>
{
    public CreateOrthoVisitRequestValidator()
    {
        RuleFor(x => x.VisitDate)
            .NotEmpty().WithMessage("تاريخ الزيارة مطلوب")
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage("تنسيق تاريخ الزيارة غير صالح");

        RuleFor(x => x.VisitType)
            .NotEmpty().WithMessage("نوع الزيارة مطلوب")
            .MaximumLength(100).WithMessage("نوع الزيارة يجب ألا يتجاوز 100 حرف");
    }
}
