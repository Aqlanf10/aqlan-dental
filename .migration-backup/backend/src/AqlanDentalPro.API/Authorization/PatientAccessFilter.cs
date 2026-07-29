using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AqlanDentalPro.API.Authorization;

/// <summary>
/// CLIN-01 / C-02 FIX: Action filter that enforces per-patient access control on endpoints
/// that accept a patientId route/query parameter. Mirrors the proven DenyIfDoctorCannotAccess
/// pattern in PatientsController, but as a reusable [ServiceFilter] so every patient-data
/// controller can apply it without duplicating the guard.
///
/// Usage:
///   [ServiceFilter(typeof(PatientAccessFilter))]
///   public class VisitsController : ControllerBase { ... }
///
/// The filter looks for a route value named "patientId" by default. If the endpoint uses a
/// different route parameter name (e.g. "id" on a controller where id IS the patient id),
/// set <see cref="PatientIdRouteName"/> via the <see cref="RequirePatientAccessAttribute"/>.
///
/// Non-doctor roles (Admin, Reception, Accountant) bypass the check (PatientAccessService
/// returns true for them). Only doctor roles (Orthodontist, GeneralDentist, OralSurgeon) are
/// restricted to patients they are linked to.
/// </summary>
public class PatientAccessFilter : IAsyncActionFilter
{
    private readonly IPatientAccessService _patientAccess;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public PatientAccessFilter(
        IPatientAccessService patientAccess,
        ICurrentUserService currentUser,
        IAuditService audit)
    {
        _patientAccess = patientAccess;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Determine the patientId route name to look for. Default: "patientId".
        // Can be overridden via RequirePatientAccessAttribute.PatientIdRouteName.
        var routeName = "patientId";
        var attr = context.ActionDescriptor.EndpointMetadata
            .OfType<RequirePatientAccessAttribute>()
            .FirstOrDefault();
        if (attr is not null && !string.IsNullOrWhiteSpace(attr.PatientIdRouteName))
            routeName = attr.PatientIdRouteName;

        // Try route values first, then query string.
        Guid patientId = Guid.Empty;
        if (context.RouteData.Values.TryGetValue(routeName, out var routeVal) && routeVal is string routeStr)
            Guid.TryParse(routeStr, out patientId);
        if (patientId == Guid.Empty)
        {
            var queryVal = context.HttpContext.Request.Query[routeName].ToString();
            Guid.TryParse(queryVal, out patientId);
        }

        // If no patientId found, let the action proceed (the endpoint may not take a patientId,
        // or may list-entities — list endpoints should use GetAccessiblePatientIdsAsync instead).
        if (patientId == Guid.Empty)
        {
            await next();
            return;
        }

        // Non-doctor roles bypass the check (PatientAccessService handles this internally,
        // but we short-circuit to avoid the DB query for admins/reception/accountants).
        if (!_patientAccess.IsDoctor)
        {
            await next();
            return;
        }

        if (!await _patientAccess.CanAccessPatientAsync(patientId))
        {
            // Log the denial for the audit trail (same pattern as PatientsController).
            // Best-effort — don't fail the 403 if audit logging throws.
            try
            {
                await _audit.LogAsync(
                    AuditAction.View,
                    "Patient",
                    patientId,
                    newData: new { status = "denied", route = routeName, role = _currentUser.Role?.ToString(), userId = _currentUser.UserId });
            }
            catch { /* audit failure should not mask the 403 */ }

            context.Result = new ObjectResult(new { message = "غير مصرح لك بعرض بيانات هذا المريض" })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }
}

/// <summary>
/// Marker attribute for endpoints/controllers that should enforce per-patient access control.
/// Apply together with [ServiceFilter(typeof(PatientAccessFilter))].
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequirePatientAccessAttribute : Attribute
{
    /// <summary>
    /// The route parameter name that carries the patient id. Default: "patientId".
    /// Set to "id" for controllers where the route is /api/{controller}/{id:guid} and id IS the patient.
    /// </summary>
    public string PatientIdRouteName { get; set; } = "patientId";
}
