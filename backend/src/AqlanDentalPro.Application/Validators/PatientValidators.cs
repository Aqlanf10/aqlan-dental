using AqlanDentalPro.Application.DTOs.Patients;
using FluentValidation;

namespace AqlanDentalPro.Application.Validators;

public sealed class CreatePatientRequestValidator : AbstractValidator<CreatePatientRequest>
{
    public CreatePatientRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("الاسم الأول مطلوب")
            .MaximumLength(100).WithMessage("الاسم الأول يجب ألا يتجاوز 100 حرف");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("اسم العائلة مطلوب")
            .MaximumLength(100).WithMessage("اسم العائلة يجب ألا يتجاوز 100 حرف");

        RuleFor(x => x.MiddleName)
            .MaximumLength(100).WithMessage("الاسم الأوسط يجب ألا يتجاوز 100 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.MiddleName));

        RuleFor(x => x.Phone)
            .MaximumLength(20).WithMessage("رقم الهاتف يجب ألا يتجاوز 20 رقماً")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.DateOfBirth)
            .Must(BeAValidDate).WithMessage("تاريخ الميلاد غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.DateOfBirth));

        RuleFor(x => x.Gender)
            .Must(g => g is "Male" or "Female")
            .WithMessage("الجنس يجب أن يكون Male أو Female")
            .When(x => !string.IsNullOrWhiteSpace(x.Gender));
    }

    private static bool BeAValidDate(string? date)
    {
        if (string.IsNullOrWhiteSpace(date)) return true;
        return DateOnly.TryParse(date, out _);
    }
}

public sealed class UpdatePatientRequestValidator : AbstractValidator<UpdatePatientRequest>
{
    public UpdatePatientRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("الاسم الأول مطلوب")
            .MaximumLength(100).WithMessage("الاسم الأول يجب ألا يتجاوز 100 حرف");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("اسم العائلة مطلوب")
            .MaximumLength(100).WithMessage("اسم العائلة يجب ألا يتجاوز 100 حرف");

        RuleFor(x => x.Phone)
            .MaximumLength(20).WithMessage("رقم الهاتف يجب ألا يتجاوز 20 رقماً")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
    }
}
