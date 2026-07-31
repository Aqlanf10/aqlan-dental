using AqlanDentalPro.Application.Interfaces.Services;

namespace AqlanDentalPro.Application.Services;

public sealed class BranchResourceScope(ICurrentUserService currentUser) : IBranchResourceScope
{
    private static bool IsValid(Guid? value) => value.HasValue && value.Value != Guid.Empty;

    public bool HasGlobalAccess =>
        currentUser.IsAuthenticated && currentUser.IsAdmin;

    public Guid? EffectiveBranchId =>
        HasGlobalAccess ? null : IsValid(currentUser.BranchId) ? currentUser.BranchId : null;

    public bool CanAccess(Guid? resourceBranchId)
    {
        if (!currentUser.IsAuthenticated)
            return false;

        if (HasGlobalAccess)
            return true;

        return IsValid(currentUser.BranchId)
            && IsValid(resourceBranchId)
            && currentUser.BranchId == resourceBranchId;
    }

    public Guid? ResolveWriteBranch(Guid? requestedBranchId = null)
    {
        if (!currentUser.IsAuthenticated)
            return null;

        if (HasGlobalAccess)
            return IsValid(requestedBranchId)
                ? requestedBranchId
                : IsValid(currentUser.BranchId) ? currentUser.BranchId : null;

        if (!IsValid(currentUser.BranchId))
            return null;

        if (IsValid(requestedBranchId) && requestedBranchId != currentUser.BranchId)
            return null;

        return currentUser.BranchId;
    }
}
