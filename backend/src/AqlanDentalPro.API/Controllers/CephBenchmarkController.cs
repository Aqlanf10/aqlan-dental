using System.Text.Json;
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
    private static readonly JsonSerializerOptions StrictJsonOptions =
        new(JsonSerializerDefaults.Web);

    [HttpGet("contract")]
    public IActionResult Contract() => Ok(new
    {
        schemaVersion = CephBenchmarkManifestValidator.SchemaVersion,
        landmarkDefinitionVersion = CephBenchmarkManifestValidator.LandmarkDefinitionVersion,
        adjudicationThresholdMm = CephBenchmarkManifestValidator.AdjudicationThresholdMm,
        evaluationProtocolVersion = CephLandmarkEvaluationEngine.ProtocolVersion,
        sdrThresholdsMm = new[] { 1d, 1.5d, 2d, 2.5d, 3d, 4d },
        bootstrapReplicates = new
        {
            minimum = CephLandmarkEvaluationEngine.MinimumBootstrapReplicates,
            maximum = CephLandmarkEvaluationEngine.MaximumBootstrapReplicates,
        },
        evaluationScope = "one-selected-non-training-split-core-landmarks",
        coreLandmarkKeys = CephBenchmarkManifestValidator.CoreLandmarkKeys.OrderBy(key => key),
        optionalLandmarkKeys = CephBenchmarkManifestValidator.AllowedLandmarkKeys
            .Except(CephBenchmarkManifestValidator.CoreLandmarkKeys)
            .OrderBy(key => key),
        persistence = "stateless-validation-only",
        prohibitedFields = new[] { "patientName", "patientId", "dateOfBirth", "filePath", "imageUrl" },
    });

    [HttpPost("validate")]
    [Consumes("application/json")]
    public IActionResult Validate([FromBody] JsonElement manifestJson)
    {
        if (manifestJson.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return BadRequest(new { code = "manifest.required" });

        try
        {
            var manifest = manifestJson.Deserialize<CephBenchmarkManifestDto>(StrictJsonOptions);
            if (manifest is null)
                return BadRequest(new { code = "manifest.required" });

            return Ok(CephBenchmarkManifestValidator.Validate(manifest));
        }
        catch (JsonException)
        {
            return BadRequest(new { code = "manifest.invalid-json-contract" });
        }
    }

    [HttpPost("evaluate-landmarks")]
    [Consumes("application/json")]
    public IActionResult EvaluateLandmarks([FromBody] JsonElement requestJson)
    {
        if (requestJson.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return BadRequest(new { code = "request.required" });

        try
        {
            var request = requestJson.Deserialize<CephLandmarkEvaluationRequestDto>(StrictJsonOptions);
            if (request is null)
                return BadRequest(new { code = "request.required" });

            return Ok(CephLandmarkEvaluationEngine.Evaluate(request));
        }
        catch (JsonException)
        {
            return BadRequest(new { code = "request.invalid-json-contract" });
        }
    }
}
