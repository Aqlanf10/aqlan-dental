using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize(Policy = "AIAccess")]
public class AiLandmarkController : ControllerBase
{
    private readonly ICephLandmarkDetector _detector;
    private readonly CephService _cephService;
    private readonly ILogger<AiLandmarkController> _logger;

    public AiLandmarkController(
        ICephLandmarkDetector detector,
        CephService cephService,
        ILogger<AiLandmarkController> logger)
    {
        _detector = detector;
        _cephService = cephService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/ai/detect-landmarks
    /// Upload a cephalometric X-ray image and detect landmarks using AI.
    /// Optionally save the results to an existing analysis.
    /// </summary>
    [HttpPost("detect-landmarks")]
    [RequestSizeLimit(20_000_000)] // 20 MB
    public async Task<IActionResult> DetectLandmarks(
        IFormFile image,
        [FromForm] int imageWidth,
        [FromForm] int imageHeight,
        [FromForm] double pixelsPerMm = 0,
        [FromForm] Guid? analysisId = null)
    {
        if (image == null || image.Length == 0)
            return BadRequest(new { message = "لم يتم تحميل صورة الأشعة" });

        if (imageWidth <= 0 || imageHeight <= 0)
            return BadRequest(new { message = "أبعاد الصورة مطلوبة" });

        _logger.LogInformation(
            "AI landmark detection request: {FileName}, {W}x{H}, model={ModelType}",
            image.FileName, imageWidth, imageHeight, _detector.ModelType);

        try
        {
            // Run AI detection
            using var stream = image.OpenReadStream();
            var detected = await _detector.DetectAsync(stream, imageWidth, imageHeight, HttpContext.RequestAborted);

            var result = detected.Select(d => new CephLandmarkDto
            {
                Id = Guid.NewGuid(),
                Key = d.Key,
                Name = d.NameAr,
                X = d.X,
                Y = d.Y,
                IsAiPlaced = true,
                Confidence = d.Confidence
            }).ToList();

            // Optionally save to analysis
            if (analysisId.HasValue)
            {
                var saveReq = new SaveLandmarksRequest
                {
                    Landmarks = result.Select(r => new LandmarkInput
                    {
                        Key = r.Key,
                        Name = r.Name,
                        X = r.X,
                        Y = r.Y,
                        IsAiPlaced = r.IsAiPlaced,
                        Confidence = r.Confidence
                    }).ToList(),
                    PixelsPerMm = pixelsPerMm,
                    ImageWidth = imageWidth,
                    ImageHeight = imageHeight
                };

                var ok = await _cephService.SaveLandmarksAsync(analysisId.Value, saveReq);
                if (!ok)
                    return NotFound(new { message = "تحليل السيفالومتري غير موجود" });

                _logger.LogInformation(
                    "Landmarks saved to analysis {AnalysisId} via AI detection ({Model})",
                    analysisId, _detector.ModelType);
            }

            return Ok(new
            {
                landmarks = result,
                modelType = _detector.ModelType,
                isModelLoaded = _detector.IsModelLoaded,
                modelVersion = _detector.ModelVersion,
                count = result.Count,
                averageConfidence = result.Count > 0
                    ? Math.Round(result.Average(r => r.Confidence ?? 0), 3)
                    : 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI landmark detection failed");
            return StatusCode(500, new { message = "فشل الكشف عن النقاط باستخدام الذكاء الاصطناعي" });
        }
    }

    /// <summary>
    /// GET /api/ai/model-status
    /// Returns the current status of the AI model.
    /// </summary>
    [HttpGet("model-status")]
    [AllowAnonymous] // Allow unauthenticated access for status checks
    public IActionResult ModelStatus()
    {
        return Ok(new
        {
            isModelLoaded = _detector.IsModelLoaded,
            modelVersion = _detector.ModelVersion,
            modelType = _detector.ModelType,
            description = _detector.ModelType switch
            {
                "onnx" => "ONNX Runtime — نموذج ذكاء اصطناعي محلي",
                "template" => "قالب تشريحي — كشف تقريبي بدون نموذج",
                _ => "غير متوفر"
            }
        });
    }
}
