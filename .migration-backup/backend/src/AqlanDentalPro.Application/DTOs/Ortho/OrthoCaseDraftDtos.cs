namespace AqlanDentalPro.Application.DTOs.Ortho;

public sealed class OrthoCaseDraftRequestDto
{
    public string Section { get; set; } = string.Empty;
}

public sealed class OrthoCaseDraftResultDto
{
    public string Section { get; set; } = string.Empty;
    public string Draft { get; set; } = string.Empty;
    public List<string> EvidenceUsed { get; set; } = [];
    public List<string> MissingData { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public string Disclaimer { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}
