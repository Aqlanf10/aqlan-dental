namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// YOLO-S5: Patient segment — a named group of patients.
///
/// Two kinds:
///   - Custom (IsDynamic = false): a manually maintained list of
///     PatientSegmentMember rows (admin adds/removes patients explicitly).
///   - Dynamic (IsDynamic = true): a stored definition (QueryJson) whose
///     membership is computed at read time. The pre-built dynamic segments
///     ("ortho overdue", "outstanding balance", "no recent visit",
///     "lab ready for delivery") are NOT stored — they are returned by
///     PatientSegmentsController as virtual segments identified by a stable
///     Key (see <see cref="PatientSegmentBuiltInKeys"/>).
///
/// Inherits BaseEntity (IsActive + soft-delete) so deleting a segment is
/// reversible and the global query filter excludes tombstoned rows.
/// </summary>
public class PatientSegment : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }

    /// <summary>
    /// true → membership is computed from <see cref="QueryJson"/> at read time.
    /// false → membership is the explicit list in <see cref="Members"/>.
    /// </summary>
    public bool IsDynamic { get; set; }

    /// <summary>
    /// Optional JSON definition of the dynamic query (kept for future use —
    /// the pre-built dynamic segments are computed in code and not stored
    /// here, so this column is reserved for custom dynamic segments later).
    /// </summary>
    public string? QueryJson { get; set; }

    public ICollection<PatientSegmentMember> Members { get; set; } = [];
}

/// <summary>
/// Built-in (pre-built, non-stored) dynamic segment keys returned by
/// <c>PatientSegmentsController.GetList</c> alongside custom segments.
/// </summary>
public static class PatientSegmentBuiltInKeys
{
    public const string OrthoOverdue      = "builtin:ortho-overdue";
    public const string OutstandingBalance = "builtin:outstanding-balance";
    public const string NoRecentVisit     = "builtin:no-recent-visit";
    public const string LabReady          = "builtin:lab-ready";
}

/// <summary>
/// Explicit membership row for a custom (non-dynamic) segment.
/// AddedAt is recorded separately from BaseEntity.CreatedAt so the segment
/// owner can sort members by join date without colliding with audit timestamps.
/// </summary>
public class PatientSegmentMember : BaseEntity
{
    public Guid SegmentId { get; set; }
    public Guid PatientId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public PatientSegment? Segment { get; set; }
    public Patient? Patient { get; set; }
}
