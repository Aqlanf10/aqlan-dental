namespace AqlanDentalPro.Domain.Entities;

using AqlanDentalPro.Domain.Enums;

/// <summary>
/// Sprint 18 — Backup record.
/// Tracks database and file backup operations.
/// </summary>
public class BackupRecord : BaseEntity
{
    public BackupType Type { get; set; }
    public BackupStatus Status { get; set; } = BackupStatus.Pending;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? SizeBytes { get; set; }
    public string? FilePath { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? TriggeredBy { get; set; }
    public bool IsAutomatic { get; set; }
}
