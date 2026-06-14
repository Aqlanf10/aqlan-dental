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

        RuleFor(x => x.XrayFileUrl)
            .MaximumLength(2048).WithMessage("رابط صورة الأشعة طويل جدًا")
            .Must(BeValidXrayUrl).WithMessage("رابط صورة الأشعة غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.XrayFileUrl));

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("الملاحظات يجب ألا تتجاوز 2000 حرف");
    }

    private static bool BeValidXrayUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;

        var url = value.Trim();
        if (url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            var fileName = url["/uploads/".Length..];
            return fileName.Length > 0
                && !fileName.Contains('/')
                && !fileName.Contains('\\')
                && !fileName.Contains("..", StringComparison.Ordinal);
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

public sealed class SaveLandmarksRequestValidator : AbstractValidator<SaveLandmarksRequest>
{
    public SaveLandmarksRequestValidator()
    {
        RuleFor(x => x.Landmarks)
            .NotEmpty().WithMessage("يجب إضافة نقطة مرجعية واحدة على الأقل");

        // 0 = uncalibrated. Angle-only and ratio analyses (e.g. Jarabak's
        // saddle/articular/gonial angles and the S-Go/N-Me facial-height ratio)
        // are scale-independent and must be savable without ruler calibration;
        // the engine simply skips the mm-based measurements when this is 0.
        RuleFor(x => x.PixelsPerMm)
            .GreaterThanOrEqualTo(0).WithMessage("مقياس البكسل لكل ملم يجب ألا يكون سالبًا");

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

public sealed class SavePhotoAnalysisRequestValidator : AbstractValidator<SavePhotoAnalysisRequest>
{
    public SavePhotoAnalysisRequestValidator()
    {
        RuleFor(x => x.OrthoCaseId)
            .NotEmpty().WithMessage("رقم حالة التقويم مطلوب");

        RuleFor(x => x.ViewType)
            .Must(v => v is "profile" or "frontal")
            .WithMessage("نوع الصورة غير صالح");

        RuleFor(x => x.ImageFileUrl)
            .NotEmpty().WithMessage("صورة التحليل مطلوبة")
            .MaximumLength(1000).WithMessage("رابط الصورة طويل جدًا");

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("الملاحظات يجب ألا تتجاوز 2000 حرف");

        RuleFor(x => x.LandmarksJson)
            .MaximumLength(100_000).WithMessage("بيانات المعالم كبيرة جدًا");

        RuleFor(x => x.MeasurementsJson)
            .MaximumLength(100_000).WithMessage("بيانات القياسات كبيرة جدًا");
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
