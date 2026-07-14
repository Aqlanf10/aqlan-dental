using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/ceph-benchmark")]
[Authorize(Policy = "AdminOnly")]
[RequestSizeLimit(5 * 1024 * 1024)]
public sealed class CephBenchmarkController : ControllerBase
{
    [HttpGet("contract")]
    public IActionResult Contract() => Ok(new
    {
        schemaVersion = CephBenchmarkManifestValidator.SchemaVersion,
        landmarkDefinitionVersion = CephBenchmarkManifestValidator.LandmarkDefinitionVersion,
        adjudicationThresholdMm = CephBenchmarkManifestValidator.AdjudicationThresholdMm,
        coreLandmarkKeys = CephBenchmarkManifestValidator.CoreLandmarkKeys.OrderBy(key => key),
        optionalLandmarkKeys = CephBenchmarkManifestValidator.AllowedLandmarkKeys
            .Except(CephBenchmarkManifestValidator.CoreLandmarkKeys)
            .OrderBy(key => key),
        persistence = "stateless-validation-only",
        prohibitedFields = new[] { "patientName", "patientId", "dateOfBirth", "filePath", "imageUrl" },
    });

    [HttpPost("validate")]
    [Consumes("application/json")]
    public IActionResult Validate([FromBody] CephBenchmarkManifestDto? manifest)
    {
        if (manifest is null)
            return BadRequest(new { code = "manifest.required" });

        return Ok(CephBenchmarkManifestValidator.Validate(manifest));
    }
}
