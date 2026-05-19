using AqlanDentalPro.Application.DTOs.General;
using FluentValidation;

namespace AqlanDentalPro.Application.Validators;

/// <summary>
/// Validates periodontal record creation.
/// </summary>
public sealed class CreatePerioRecordRequestValidator : AbstractValidator<CreatePerioRecordRequest>
{
    public CreatePerioRecordRequestValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty().WithMessage("معرف المريض مطلوب");

        RuleFor(x => x.ToothNumber)
            .InclusiveBetween(1, 48).WithMessage("رقم السن يجب أن يكون بين 1 و 48");

        RuleFor(x => x.ProbingDepth)
            .InclusiveBetween(0m, 15m).WithMessage("عمق الاستكشاف يجب أن يكون بين 0 و 15 مم");

        RuleFor(x => x.ClinicalAttachment)
            .InclusiveBetween(0m, 15m).WithMessage("الارتباط السريري يجب أن يكون بين 0 و 15 مم");

        RuleFor(x => x.PlaqueIndex)
            .InclusiveBetween(0, 3).WithMessage("مؤشر اللويحة يجب أن يكون بين 0 و 3");

        RuleFor(x => x.GingivalIndex)
            .InclusiveBetween(0, 3).WithMessage("مؤشر اللثة يجب أن يكون بين 0 و 3");

        RuleFor(x => x.Furcation)
            .InclusiveBetween(0, 3).WithMessage("درجة التشعب يجب أن تكون بين 0 و 3");

        RuleFor(x => x.Mobility)
            .InclusiveBetween(0, 3).WithMessage("درجة الحركة يجب أن تكون بين 0 و 3");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("الملاحظات يجب ألا تتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}

/// <summary>
/// Validates general treatment creation.
/// </summary>
public sealed class CreateGeneralTreatmentRequestValidator : AbstractValidator<CreateGeneralTreatmentRequest>
{
    public CreateGeneralTreatmentRequestValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty().WithMessage("معرف المريض مطلوب");

        RuleFor(x => x.TreatmentType)
            .NotEmpty().WithMessage("نوع العلاج مطلوب")
            .MaximumLength(100).WithMessage("نوع العلاج يجب ألا يتجاوز 100 حرف");

        RuleFor(x => x.ToothNumber)
            .MaximumLength(10).WithMessage("رقم السن يجب ألا يتجاوز 10 أحرف")
            .When(x => !string.IsNullOrWhiteSpace(x.ToothNumber));

        RuleFor(x => x.MaterialUsed)
            .MaximumLength(100).WithMessage("المادة المستخدمة يجب ألا تتجاوز 100 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.MaterialUsed));

        RuleFor(x => x.Cost)
            .GreaterThanOrEqualTo(0).WithMessage("التكلفة يجب أن تكون قيمة موجبة")
            .When(x => x.Cost.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("الملاحظات يجب ألا تتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}

/// <summary>
/// Validates treatment plan item creation.
/// </summary>
public sealed class CreateTreatmentPlanItemRequestValidator : AbstractValidator<CreateTreatmentPlanItemRequest>
{
    public CreateTreatmentPlanItemRequestValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty().WithMessage("معرف المريض مطلوب");

        RuleFor(x => x.Treatment)
            .NotEmpty().WithMessage("وصف العلاج مطلوب")
            .MaximumLength(200).WithMessage("وصف العلاج يجب ألا يتجاوز 200 حرف");

        RuleFor(x => x.Priority)
            .Must(BeAValidPriority).WithMessage("الأولوية يجب أن تكون: low, medium, high, urgent");

        RuleFor(x => x.ToothNumber)
            .MaximumLength(10).WithMessage("رقم السن يجب ألا يتجاوز 10 أحرف")
            .When(x => !string.IsNullOrWhiteSpace(x.ToothNumber));

        RuleFor(x => x.EstimatedCost)
            .GreaterThanOrEqualTo(0).WithMessage("التكلفة التقديرية يجب أن تكون قيمة موجبة")
            .When(x => x.EstimatedCost.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("الملاحظات يجب ألا تتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }

    private static bool BeAValidPriority(string priority) =>
        priority is "low" or "medium" or "high" or "urgent";
}

/// <summary>
/// Validates treatment plan status update.
/// </summary>
public sealed class UpdateTreatmentPlanStatusRequestValidator : AbstractValidator<UpdateTreatmentPlanStatusRequest>
{
    public UpdateTreatmentPlanStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("الحالة مطلوبة")
            .Must(BeAValidStatus).WithMessage("الحالة يجب أن تكون: planned, in_progress, completed, cancelled");
    }

    private static bool BeAValidStatus(string status) =>
        status is "planned" or "in_progress" or "completed" or "cancelled";
}

/// <summary>
/// Validates tooth condition update in dental chart.
/// </summary>
public sealed class UpdateToothRequestValidator : AbstractValidator<UpdateToothRequest>
{
    public UpdateToothRequestValidator()
    {
        RuleFor(x => x.ToothNumber)
            .NotEmpty().WithMessage("رقم السن مطلوب")
            .MaximumLength(10).WithMessage("رقم السن يجب ألا يتجاوز 10 أحرف");

        RuleFor(x => x.Condition)
            .MaximumLength(100).WithMessage("الحالة يجب ألا تتجاوز 100 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Condition));

        RuleFor(x => x.SurfacesAffected)
            .MaximumLength(50).WithMessage("الأسطح المتأثرة يجب ألا تتجاوز 50 حرفاً")
            .When(x => !string.IsNullOrWhiteSpace(x.SurfacesAffected));

        RuleFor(x => x.TreatmentDone)
            .MaximumLength(200).WithMessage("العلاج المنفذ يجب ألا يتجاوز 200 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.TreatmentDone));

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("الملاحظات يجب ألا تتجاوز 500 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}
