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

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("البريد الإلكتروني غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.DateOfBirth)
            .Must(BeAValidDate).WithMessage("تاريخ الميلاد غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.DateOfBirth));

        RuleFor(x => x.Gender)
            .Must(g => g is "Male" or "Female")
            .WithMessage("الجنس يجب أن يكون Male أو Female")
            .When(x => !string.IsNullOrWhiteSpace(x.Gender));

        RuleFor(x => x.MedicalHistory)
            .SetValidator(new MedicalHistoryDtoValidator()!)
            .When(x => x.MedicalHistory != null);

        RuleFor(x => x.DentalHistory)
            .SetValidator(new DentalHistoryDtoValidator()!)
            .When(x => x.DentalHistory != null);
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

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("البريد الإلكتروني غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.MedicalHistory)
            .SetValidator(new MedicalHistoryDtoValidator()!)
            .When(x => x.MedicalHistory != null);

        RuleFor(x => x.DentalHistory)
            .SetValidator(new DentalHistoryDtoValidator()!)
            .When(x => x.DentalHistory != null);
    }
}

/// <summary>
/// Validates medical history nested DTO — length limits on free-text medical fields.
/// </summary>
public sealed class MedicalHistoryDtoValidator : AbstractValidator<MedicalHistoryDto>
{
    private static readonly HashSet<string> ValidPregnancyValues = new(StringComparer.OrdinalIgnoreCase)
        { "Yes", "No", "N/A", "نعم", "لا", "غير محدد", "yes", "no", "na" };

    public MedicalHistoryDtoValidator()
    {
        RuleFor(x => x.ChronicDiseases)
            .MaximumLength(1000).WithMessage("الأمراض المزمنة يجب ألا تتجاوز 1000 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.ChronicDiseases));

        RuleFor(x => x.CurrentMedications)
            .MaximumLength(1000).WithMessage("الأدوية الحالية يجب ألا تتجاوز 1000 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.CurrentMedications));

        RuleFor(x => x.DrugAllergies)
            .MaximumLength(500).WithMessage("حساسية الأدوية يجب ألا تتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.DrugAllergies));

        RuleFor(x => x.IsPregnant)
            .Must(v => string.IsNullOrWhiteSpace(v) || ValidPregnancyValues.Contains(v))
            .WithMessage("حالة الحمل يجب أن تكون Yes أو No أو N/A")
            .When(x => !string.IsNullOrWhiteSpace(x.IsPregnant));

        RuleFor(x => x.PreviousSurgeries)
            .MaximumLength(1000).WithMessage("العمليات السابقة يجب ألا تتجاوز 1000 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.PreviousSurgeries));

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("ملاحظات التاريخ المرضي يجب ألا تتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}

/// <summary>
/// Validates dental history nested DTO — length limits on free-text dental fields.
/// </summary>
public sealed class DentalHistoryDtoValidator : AbstractValidator<DentalHistoryDto>
{
    public DentalHistoryDtoValidator()
    {
        RuleFor(x => x.ChiefComplaint)
            .MaximumLength(500).WithMessage("الشكوى الرئيسية يجب ألا تتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.ChiefComplaint));

        RuleFor(x => x.PreviousTreatments)
            .MaximumLength(1000).WithMessage("العلاجات السابقة يجب ألا تتجاوز 1000 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.PreviousTreatments));

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("ملاحظات التاريخ السني يجب ألا تتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}
