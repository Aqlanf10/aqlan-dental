namespace AqlanDentalPro.API.Services;

public enum OrthoPresentationSlideType
{
    Title,
    PatientInformation,
    ChiefComplaint,
    ExtraoralPhotos,
    FacialAnalysis,
    IntraoralPhotos,
    OcclusionAssessment,
    PanoramicXray,
    CephalometricSummary,
    CephalometricMeasurements,
    CastAnalysis,
    Bolton,
    Diagnosis,
    TreatmentObjectives,
    ProblemList,
    TreatmentPlan,
    Mechanotherapy,
    VisitProgress,
    ProgressPhotos,
    Retention,
    ThankYou,
}

public sealed record OrthoPresentationSlideDefinition(
    int Order,
    OrthoPresentationSlideType Type,
    string Title,
    bool Required,
    IReadOnlyList<string> TextPlaceholders,
    IReadOnlyList<string> ImagePlaceholders,
    IReadOnlyList<string> TablePlaceholders);

public sealed class GenerateOrthoCasePresentationRequest
{
    public bool IncludeEmptyOptionalSlides { get; init; } = true;
    public IReadOnlyList<OrthoPresentationSlideType>? IncludedSlides { get; init; }
}

public sealed record OrthoPresentationDefinitionItem(
    int Order,
    OrthoPresentationSlideType Type,
    string Title,
    bool Required,
    bool HasData,
    IReadOnlyList<string> TextPlaceholders,
    IReadOnlyList<string> ImagePlaceholders,
    IReadOnlyList<string> TablePlaceholders);

public sealed record OrthoPresentationDefinitionResponse(
    string Template,
    int TotalSlides,
    int ReadySlides,
    IReadOnlyList<OrthoPresentationDefinitionItem> Slides);

internal sealed record PresentationImage(
    string Label,
    byte[] Bytes,
    int PixelWidth,
    int PixelHeight,
    double CropX = 0,
    double CropY = 0,
    double CropWidth = 1,
    double CropHeight = 1,
    bool FlipHorizontal = false,
    bool FlipVertical = false);

internal sealed record PresentationTable(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows);

internal sealed record PresentationSlideContent(
    OrthoPresentationSlideDefinition Definition,
    IReadOnlyList<string> Lines,
    IReadOnlyList<PresentationImage> Images,
    PresentationTable? Table,
    bool HasData);

internal sealed record OrthoCasePresentationDocument(
    string ClinicName,
    string LeadDoctor,
    string CaseNumber,
    string PatientName,
    IReadOnlyList<PresentationSlideContent> Slides);
