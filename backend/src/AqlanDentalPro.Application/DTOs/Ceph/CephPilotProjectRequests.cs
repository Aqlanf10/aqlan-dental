using AqlanDentalPro.Domain.Enums;
using System.Text.Json.Serialization;

namespace AqlanDentalPro.Application.DTOs.Ceph;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateCephPilotProjectRequest
{
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? DatasetVersion { get; init; }
    public Guid ReviewerAUserId { get; init; }
    public Guid ReviewerBUserId { get; init; }
    public Guid? AdjudicatorUserId { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateCephPilotProjectRequest
{
    public int ExpectedRevision { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? DatasetVersion { get; init; }
    public Guid? ReviewerAUserId { get; init; }
    public Guid? ReviewerBUserId { get; init; }
    public Guid? AdjudicatorUserId { get; init; }
    public bool ClearAdjudicator { get; init; }
    public CephPilotProjectStatus? Status { get; init; }
}
