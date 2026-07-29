namespace AqlanDentalPro.Domain.Constants;

/// <summary>
/// M6 FIX: Shared pagination limits to prevent unbounded queries.
/// All paginated endpoints MUST cap pageSize/limit to MaxPageSize.
/// </summary>
public static class PaginationDefaults
{
    /// <summary>Maximum allowed page size for any endpoint.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Default page size when not specified.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// Caps the given page size to MaxPageSize and ensures it's at least 1.
    /// Use this at the entry point of every paginated controller method.
    /// </summary>
    public static int Clamp(int? pageSize) =>
        Math.Max(1, Math.Min(pageSize ?? DefaultPageSize, MaxPageSize));
}
