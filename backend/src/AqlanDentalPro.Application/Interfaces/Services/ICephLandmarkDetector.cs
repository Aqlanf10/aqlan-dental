namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>
/// AI-powered cephalometric landmark detection service.
/// Uses ONNX Runtime for local inference with optional VLM fallback.
/// </summary>
public interface ICephLandmarkDetector
{
    /// <summary>
    /// Detect cephalometric landmarks from an image stream.
    /// </summary>
    /// <param name="imageStream">The X-ray image stream (JPEG/PNG)</param>
    /// <param name="imageWidth">Original image width for coordinate mapping</param>
    /// <param name="imageHeight">Original image height for coordinate mapping</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of detected landmarks with confidence scores</returns>
    Task<List<DetectedLandmark>> DetectAsync(Stream imageStream, int imageWidth, int imageHeight, CancellationToken ct = default);

    /// <summary>
    /// Whether the ONNX AI model is available and loaded.
    /// </summary>
    bool IsModelLoaded { get; }

    /// <summary>
    /// Version of the loaded model (null if not loaded).
    /// </summary>
    string? ModelVersion { get; }

    /// <summary>
    /// Type of detection method currently active ("onnx", "template", or "none").
    /// </summary>
    string ModelType { get; }
}

/// <summary>
/// A single detected cephalometric landmark with position and confidence.
/// </summary>
public class DetectedLandmark
{
    /// <summary>Landmark key (e.g., "S", "N", "A", "B").</summary>
    public string Key { get; set; } = "";

    /// <summary>Arabic name of the landmark.</summary>
    public string NameAr { get; set; } = "";

    /// <summary>X coordinate in original image space.</summary>
    public double X { get; set; }

    /// <summary>Y coordinate in original image space.</summary>
    public double Y { get; set; }

    /// <summary>Detection confidence (0.0 to 1.0).</summary>
    public double Confidence { get; set; }

    /// <summary>Whether this landmark was placed by AI (always true for detected landmarks).</summary>
    public bool IsAiPlaced => true;
}
