using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Infrastructure.Data;

/// <summary>
/// Canonical definition of a clinically active orthodontic case.
/// IsActive is the soft-delete flag; Status is the clinical lifecycle.
/// Both must be true so completed/cancelled cases never appear as active.
/// </summary>
public static class OrthoCaseQueryExtensions
{
    public static IQueryable<OrthoCase> ActiveCases(this IQueryable<OrthoCase> query) =>
        query.Where(c => c.IsActive && c.Status == OrthoCaseStatus.Active);
}
