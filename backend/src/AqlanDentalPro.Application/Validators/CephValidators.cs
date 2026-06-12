using AqlanDentalPro.Application.DTOs.Ceph;
using FluentValidation;

namespace AqlanDentalPro.Application.Validators;

public sealed class CreateCephAnalysisRequestValidator : AbstractValidator<CreateCephAnalysisRequest>
{
    public CreateCephAnalysisRequestValidator()
    {
        RuleFor(x => x.OrthoCaseId)
            .NotEmpty().WithMessage("رقم حالة التقويم مطلوب");

        RuleFor(x => x.AnalysisType)
            .NotEmpty().WithMessage("نوع التحليل مطلوب")
            // "full" runs every analysis; "wits" is computed by the engine too.
            // These must match the options offered by the frontend new-analysis form.
            .Must(t => t is "full" or "steiner" or "mcnamara" or "downs" or "tweed"
                or "ricketts" or "jarabak" or "wits")
            .WithMessage("نوع التحليل غير صالح");
    }
}

public sealed class SaveLandmarksRequestValidator : AbstractValidator<SaveLandmarksRequest>
{
    public SaveLandmarksRequestValidator()
    {
        RuleFor(x => x.Landmarks)
            .NotEmpty().WithMessage("يجب إضافة نقطة مرجعية واحدة على الأقل");

        RuleFor(x => x.PixelsPerMm)
            .GreaterThan(0).WithMessage("مقياس البكسل لكل ملم يجب أن يكون أكبر من صفر");

        RuleFor(x => x.ImageWidth)
            .GreaterThan(0).WithMessage("عرض الصورة يجب أن يكون أكبر من صفر");

        RuleFor(x => x.ImageHeight)
            .GreaterThan(0).WithMessage("ارتفاع الصورة يجب أن يكون أكبر من صفر");

        RuleForEach(x => x.Landmarks)
            .ChildRules(landmark =>
            {
                landmark.RuleFor(l => l.Key)
                    .NotEmpty().WithMessage("مفتاح النقطة المرجعية مطلوب");
                landmark.RuleFor(l => l.X)
                    .GreaterThanOrEqualTo(0).WithMessage("الإحداثي الأفقي يجب أن يكون صفر أو أكبر");
                landmark.RuleFor(l => l.Y)
                    .GreaterThanOrEqualTo(0).WithMessage("الإحداثي العمودي يجب أن يكون صفر أو أكبر");
            });
    }
}

public sealed class SaveDiagnosisRequestValidator : AbstractValidator<SaveDiagnosisRequest>
{
    public SaveDiagnosisRequestValidator()
    {
        RuleFor(x => x.SkeletalClass)
            .Must(s => string.IsNullOrEmpty(s) || s is "Class I" or "Class II" or "Class II Division 1"
                or "Class II Division 2" or "Class III")
            .WithMessage("التصنيف الهيكلي غير صالح");

        RuleFor(x => x.FinalDiagnosis)
            .MaximumLength(2000).WithMessage("التشخيص النهائي يجب ألا يتجاوز 2000 حرف");

        RuleFor(x => x.SoftTissueSummary)
            .MaximumLength(2000).WithMessage("ملخص الأنسجة الرخوة يجب ألا يتجاوز 2000 حرف");
    }
}

public sealed class AiSimulateRequestValidator : AbstractValidator<AiSimulateRequest>
{
    public AiSimulateRequestValidator()
    {
        RuleFor(x => x.ImageWidth)
            .GreaterThan(0).WithMessage("عرض الصورة يجب أن يكون أكبر من صفر");

        RuleFor(x => x.ImageHeight)
            .GreaterThan(0).WithMessage("ارتفاع الصورة يجب أن يكون أكبر من صفر");

        RuleFor(x => x.PixelsPerMm)
            .GreaterThan(0).WithMessage("مقياس البكسل لكل ملم يجب أن يكون أكبر من صفر");
    }
}
