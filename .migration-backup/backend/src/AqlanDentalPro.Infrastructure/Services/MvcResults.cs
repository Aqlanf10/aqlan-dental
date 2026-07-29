using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Static helpers that mirror <see cref="ControllerBase"/>'s Ok / BadRequest /
/// Conflict / NotFound / StatusCode factory methods so that services which are
/// not controllers can still produce <see cref="IActionResult"/> instances with
/// the same HTTP semantics. Introduced for the CLIN-22 PatientJourneyController
/// extraction — the moved methods returned IActionResult directly from the
/// controller, and reshaping them to return DTOs would have changed the
/// role-filtered response contract of <c>GetDailySummary</c>.
/// </summary>
internal static class MvcResults
{
    public static OkObjectResult Ok(object? value) => new(value);
    public static BadRequestObjectResult BadRequest(object? value) => new(value);
    public static ConflictObjectResult Conflict(object? value) => new(value);
    public static NotFoundObjectResult NotFound(object? value) => new(value);
    public static ObjectResult StatusCode(int statusCode, object? value)
        => new(value) { StatusCode = statusCode };
}
