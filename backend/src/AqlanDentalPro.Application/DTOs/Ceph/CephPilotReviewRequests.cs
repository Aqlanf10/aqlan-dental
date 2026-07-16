using AqlanDentalPro.Domain.Enums;
using System.Text.Json.Serialization;

namespace AqlanDentalPro.Application.DTOs.Ceph;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SaveCephPilotReviewRequest
{
    public int ExpectedRevision { get; init; }
    public IReadOnlyList<CephPilotReviewPointRequest> Points { get; init; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SubmitCephPilotReviewRequest
{
    public int ExpectedRevision { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CephPilotReviewPointRequest
{
    public string LandmarkKey { get; init; } = string.Empty;
    public CephPilotLandmarkVisibility Visibility { get; init; }
    public decimal? XCoordPx { get; init; }
    public decimal? YCoordPx { get; init; }
    public CephPilotContourDecision ContourDecision { get; init; }
}
