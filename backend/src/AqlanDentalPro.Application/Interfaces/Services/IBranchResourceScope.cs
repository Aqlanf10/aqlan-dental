namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>
/// Central, fail-closed branch ownership rules for resources addressed by ID.
/// </summary>
public interface IBranchResourceScope
{
    /// <summary>
    /// Branch applied to list queries. Null means an authenticated global admin.
    /// </summary>
    Guid? EffectiveBranchId { get; }

    bool HasGlobalAccess { get; }

    /// <summary>
    /// Returns true only when the current caller owns the resource branch, or is
    /// an authenticated global admin.
    /// </summary>
    bool CanAccess(Guid? resourceBranchId);

    /// <summary>
    /// Resolves a branch for a new resource. Non-admin users cannot choose a
    /// branch other than the branch in their identity.
    /// </summary>
    Guid? ResolveWriteBranch(Guid? requestedBranchId = null);
}
