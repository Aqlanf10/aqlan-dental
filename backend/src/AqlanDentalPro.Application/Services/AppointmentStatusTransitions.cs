using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Application.Services;

/// <summary>
/// Defines valid status transitions for appointments.
/// Prevents invalid state changes (e.g., jumping from Scheduled directly to InProgress).
/// </summary>
public static class AppointmentStatusTransitions
{
    // Each status maps to the set of statuses it can transition TO.
    private static readonly Dictionary<AppointmentStatus, HashSet<AppointmentStatus>> ValidTransitions = new()
    {
        // New appointment can be confirmed, cancelled, or marked no-show
        [AppointmentStatus.Scheduled] = new()
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.Arrived,
            AppointmentStatus.Cancelled,
            AppointmentStatus.NoShow
        },
        // Confirmed can move to arrived, cancelled, or no-show
        [AppointmentStatus.Confirmed] = new()
        {
            AppointmentStatus.Scheduled,  // Un-confirm
            AppointmentStatus.Arrived,
            AppointmentStatus.Waiting,
            AppointmentStatus.Cancelled,
            AppointmentStatus.NoShow
        },
        // Arrived can move to waiting, called (direct call), in-room (direct entry), cancelled, or no-show
        [AppointmentStatus.Arrived] = new()
        {
            AppointmentStatus.Scheduled,  // Re-schedule
            AppointmentStatus.Confirmed,  // Back to confirmed
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,     // Direct call from arrived (skip waiting)
            AppointmentStatus.InRoom,     // Direct entry to room (skip waiting/called)
            AppointmentStatus.Cancelled,
            AppointmentStatus.NoShow
        },
        // Waiting can be called back to confirmed/arrived, cancelled, or no-show
        [AppointmentStatus.Waiting] = new()
        {
            AppointmentStatus.Called,
            AppointmentStatus.Confirmed,
            AppointmentStatus.Arrived,
            AppointmentStatus.Cancelled,
            AppointmentStatus.NoShow
        },
        // Called can go to in-room or back to waiting
        [AppointmentStatus.Called] = new()
        {
            AppointmentStatus.InRoom,
            AppointmentStatus.Waiting,
            AppointmentStatus.Cancelled
        },
        // In room can start treatment or go back
        [AppointmentStatus.InRoom] = new()
        {
            AppointmentStatus.InProgress,
            AppointmentStatus.Called,
            AppointmentStatus.Cancelled
        },
        // In progress can only be completed or cancelled
        [AppointmentStatus.InProgress] = new()
        {
            AppointmentStatus.Completed,
            AppointmentStatus.Cancelled
        },
        // Terminal states — no transitions out
        [AppointmentStatus.Completed] = new(),
        [AppointmentStatus.Cancelled] = new(),
        [AppointmentStatus.NoShow] = new()
    };

    /// <summary>
    /// Checks if a transition from currentStatus to newStatus is valid.
    /// </summary>
    public static bool IsValidTransition(AppointmentStatus currentStatus, AppointmentStatus newStatus)
    {
        // Same status is always valid (idempotent)
        if (currentStatus == newStatus) return true;

        // Check if the current status has any defined transitions
        if (!ValidTransitions.TryGetValue(currentStatus, out var allowedStatuses))
            return false;

        return allowedStatuses.Contains(newStatus);
    }

    /// <summary>
    /// CORE-APPT-005: the one sanctioned way out of a terminal state — putting a
    /// Cancelled/NoShow appointment back to Scheduled (re-booking the patient).
    /// <para>
    /// This is deliberately NOT an entry in <see cref="ValidTransitions"/>: the
    /// clinical flows that consume that table (checkout, queue, visit) must keep
    /// treating Cancelled/NoShow as strictly terminal, and existing tests pin that.
    /// Only an explicit re-scheduling intent may reopen them.
    /// </para>
    /// </summary>
    public static bool IsReschedulingFromTerminal(AppointmentStatus currentStatus, AppointmentStatus newStatus) =>
        (currentStatus is AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
        && newStatus == AppointmentStatus.Scheduled;

    /// <summary>
    /// CORE-APPT-005: the full rule for a user-initiated appointment status change —
    /// a normal transition OR the re-scheduling exception above. Single-appointment
    /// and batch status updates must both use this, otherwise the same change is
    /// accepted individually and rejected in bulk (which is exactly the bug this
    /// closes: the batch path called <see cref="IsValidTransition"/> directly and
    /// reported a genuine re-schedule as a "status conflict").
    /// </summary>
    public static bool IsValidStatusChange(AppointmentStatus currentStatus, AppointmentStatus newStatus) =>
        IsReschedulingFromTerminal(currentStatus, newStatus)
        || IsValidTransition(currentStatus, newStatus);

    /// <summary>
    /// Gets all valid target statuses for a given current status.
    /// </summary>
    public static IEnumerable<AppointmentStatus> GetAllowedTransitions(AppointmentStatus currentStatus)
    {
        if (!ValidTransitions.TryGetValue(currentStatus, out var allowedStatuses))
            return [];

        // Always include current status (idempotent)
        return allowedStatuses.Prepend(currentStatus);
    }
}
