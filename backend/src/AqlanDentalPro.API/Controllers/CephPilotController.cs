using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/ceph-pilot")]
[Authorize(Policy = "OrthoAccess")]
public sealed partial class CephPilotController(
    AppDbContext db,
    ICurrentUserService currentUser,
    IAuditService audit,
    IWebHostEnvironment environment) : ControllerBase
{
    private const long MaxImageBytes = 25 * 1024 * 1024;
    private const long MaxArtifactBytes = 10 * 1024 * 1024;
    private const int MinImageDimension = 256;
    private const int MaxImageDimension = 12000;

    [HttpGet("projects")]
    public async Task<IActionResult> ListProjects(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Forbid();

        var query = db.CephPilotProjects.AsNoTracking().Include(item => item.Cases).AsQueryable();
        if (!currentUser.IsAdmin)
        {
            query = query.Where(item =>
                item.ReviewerAUserId == userId ||
                item.ReviewerBUserId == userId ||
                item.AdjudicatorUserId == userId);
        }

        var projects = await query.OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
        return Ok(projects.Select(item => MapProject(item, userId, currentUser.IsAdmin)));
    }

    [HttpPost("projects")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> CreateProject(
        [FromBody] CreateCephPilotProjectRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Forbid();
        var validation = ValidateProjectText(request.Name, request.Code, request.Description, request.DatasetVersion);
        if (validation is not null) return BadRequest(validation);
        if (request.ReviewerAUserId == request.ReviewerBUserId)
            return BadRequest(Error("project.reviewers-must-be-distinct", "Reviewer A and Reviewer B must be different users."));
        if (request.AdjudicatorUserId == request.ReviewerAUserId || request.AdjudicatorUserId == request.ReviewerBUserId)
            return BadRequest(Error("project.adjudicator-must-be-distinct", "The adjudicator must be different from both reviewers."));
        if (!await ReviewersAreEligibleAsync(request.ReviewerAUserId, request.ReviewerBUserId, request.AdjudicatorUserId, cancellationToken))
            return BadRequest(Error("project.reviewer-not-eligible", "All assigned reviewers must be active Admin or Orthodontist users."));

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (await db.CephPilotProjects.AnyAsync(item => item.Code == normalizedCode, cancellationToken))
            return Conflict(Error("project.code-exists", "A Pilot project with this code already exists."));

        var project = new CephPilotProject
        {
            Name = request.Name.Trim(),
            Code = normalizedCode,
            Description = CleanOptional(request.Description),
            DatasetVersion = CleanOptional(request.DatasetVersion),
            CreatedByUserId = userId,
            ReviewerAUserId = request.ReviewerAUserId,
            ReviewerBUserId = request.ReviewerBUserId,
            AdjudicatorUserId = request.AdjudicatorUserId,
        };
        db.CephPilotProjects.Add(project);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Create, "CephPilotProject", project.Id, newData: new
        {
            project.Code,
            project.LandmarkDefinitionVersion,
            project.ReviewerAUserId,
            project.ReviewerBUserId,
            project.AdjudicatorUserId,
            project.IsAiHidden,
            project.IsWebCephHidden,
        });

        return CreatedAtAction(nameof(GetProject), new { id = project.Id }, MapProject(project, userId, true));
    }

    [HttpGet("projects/{id:guid}")]
    public async Task<IActionResult> GetProject(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Forbid();
        var project = await db.CephPilotProjects.AsNoTracking()
            .Include(item => item.Cases)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (project is null) return NotFound(Error("project.not-found", "Pilot project not found."));
        if (!CanAccess(project, userId)) return Forbid();
        return Ok(MapProject(project, userId, currentUser.IsAdmin));
    }

    [HttpPatch("projects/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateProject(
        Guid id,
        [FromBody] UpdateCephPilotProjectRequest request,
        CancellationToken cancellationToken)
    {
        var project = await db.CephPilotProjects.Include(item => item.Cases)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (project is null) return NotFound(Error("project.not-found", "Pilot project not found."));
        if (request.ExpectedRevision <= 0 || request.ExpectedRevision != project.Revision)
            return Conflict(Error("project.revision-conflict", "The project changed. Reload it before saving."));
        if (project.Status is not CephPilotProjectStatus.Draft &&
            (request.ReviewerAUserId.HasValue || request.ReviewerBUserId.HasValue ||
             request.AdjudicatorUserId.HasValue || request.ClearAdjudicator))
            return Conflict(Error("project.assignments-locked", "Reviewer assignments are locked after the project leaves Draft."));

        var reviewerA = request.ReviewerAUserId ?? project.ReviewerAUserId;
        var reviewerB = request.ReviewerBUserId ?? project.ReviewerBUserId;
        var adjudicator = request.ClearAdjudicator ? null : request.AdjudicatorUserId ?? project.AdjudicatorUserId;
        if (reviewerA == reviewerB)
            return BadRequest(Error("project.reviewers-must-be-distinct", "Reviewer A and Reviewer B must be different users."));
        if (adjudicator == reviewerA || adjudicator == reviewerB)
            return BadRequest(Error("project.adjudicator-must-be-distinct", "The adjudicator must be different from both reviewers."));
        if (!await ReviewersAreEligibleAsync(reviewerA, reviewerB, adjudicator, cancellationToken))
            return BadRequest(Error("project.reviewer-not-eligible", "All assigned reviewers must be active Admin or Orthodontist users."));

        if (request.Status.HasValue && !CephPilotStateTransitions.CanTransition(project.Status, request.Status.Value))
            return Conflict(Error("project.invalid-transition", $"Cannot transition from {project.Status} to {request.Status.Value}."));
        if (request.Status == CephPilotProjectStatus.ReadyForAnnotation &&
            !project.Cases.Any(item => item.Status == CephPilotCaseStatus.Ready))
            return Conflict(Error("project.no-ready-case", "At least one calibrated, de-identified case is required."));

        var oldData = new
        {
            project.Name,
            project.Description,
            project.DatasetVersion,
            project.ReviewerAUserId,
            project.ReviewerBUserId,
            project.AdjudicatorUserId,
            Status = project.Status.ToString(),
            project.Revision,
        };
        if (request.Name is not null) project.Name = request.Name.Trim();
        if (request.Description is not null) project.Description = CleanOptional(request.Description);
        if (request.DatasetVersion is not null) project.DatasetVersion = CleanOptional(request.DatasetVersion);
        project.ReviewerAUserId = reviewerA;
        project.ReviewerBUserId = reviewerB;
        project.AdjudicatorUserId = adjudicator;
        if (request.Status.HasValue)
        {
            project.Status = request.Status.Value;
            if (project.Status == CephPilotProjectStatus.ReadyForAnnotation)
                project.LockedAt ??= DateTime.UtcNow;
        }
        project.Revision++;

        var validation = ValidateProjectText(project.Name, project.Code, project.Description, project.DatasetVersion);
        if (validation is not null) return BadRequest(validation);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(Error("project.revision-conflict", "The project changed. Reload it before saving."));
        }
        await audit.LogAsync(AuditAction.Update, "CephPilotProject", project.Id, oldData: oldData, newData: new
        {
            project.Name,
            project.Description,
            project.DatasetVersion,
            project.ReviewerAUserId,
            project.ReviewerBUserId,
            project.AdjudicatorUserId,
            Status = project.Status.ToString(),
            project.Revision,
        });
        return Ok(MapProject(project, currentUser.UserId!.Value, true));
    }

    [HttpGet("projects/{id:guid}/cases")]
    public async Task<IActionResult> ListCases(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Forbid();
        var project = await db.CephPilotProjects.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (project is null) return NotFound(Error("project.not-found", "Pilot project not found."));
        if (!CanAccess(project, userId)) return Forbid();
        var cases = await db.CephPilotCases.AsNoTracking().Include(item => item.ExportArtifacts)
            .Where(item => item.ProjectId == id)
            .OrderBy(item => item.CaseSequence)
            .ToListAsync(cancellationToken);
        return Ok(cases.Select(item => MapCase(item, currentUser.IsAdmin)));
    }

    [HttpPost("projects/{id:guid}/cases")]
    [Authorize(Policy = "AdminOnly")]
    [RequestSizeLimit(MaxImageBytes + 1024 * 1024)]
    public async Task<IActionResult> CreateCase(
        Guid id,
        [FromForm] CreateCephPilotCaseRequest request,
        CancellationToken cancellationToken)
    {
        var unknownField = await FindUnknownFormFieldAsync(CreateCephPilotCaseRequest.AllowedFields);
        if (unknownField is not null)
            return BadRequest(Error("case.unrecognized-field", $"Unrecognized form field: {unknownField}."));
        var project = await db.CephPilotProjects.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (project is null) return NotFound(Error("project.not-found", "Pilot project not found."));
        if (project.Status != CephPilotProjectStatus.Draft)
            return Conflict(Error("project.staging-locked", "Cases can only be staged while the project is Draft."));
        var inputError = ValidateCaseRequest(request);
        if (inputError is not null) return BadRequest(inputError);

        byte[] inputBytes;
        await using (var input = request.Image.OpenReadStream())
        await using (var buffer = new MemoryStream())
        {
            await input.CopyToAsync(buffer, cancellationToken);
            inputBytes = buffer.ToArray();
        }

        StagedImage staged;
        try
        {
            staged = SanitizeImage(inputBytes);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(Error("case.invalid-image", ex.Message));
        }
        if (await db.CephPilotCases.AnyAsync(item => item.ProjectId == id && item.ImageSha256 == staged.Sha256, cancellationToken))
            return Conflict(Error("case.duplicate-image", "This de-identified image already exists in the project."));

        var sequence = (await db.CephPilotCases.Where(item => item.ProjectId == id)
            .MaxAsync(item => (int?)item.CaseSequence, cancellationToken) ?? 0) + 1;
        var caseId = Guid.NewGuid();
        var storageKey = $"{id:N}/{caseId:N}/source.png";
        var storagePath = ResolvePrivateStoragePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(storagePath)!);
        await System.IO.File.WriteAllBytesAsync(storagePath, staged.Bytes, cancellationToken);

        var entity = new CephPilotCase
        {
            Id = caseId,
            ProjectId = id,
            CaseSequence = sequence,
            CaseCode = $"PILOT-{sequence:000}",
            SourceType = request.SourceType,
            SourceReference = CleanOptional(request.SourceReference),
            ImageStorageKey = storageKey,
            ImageSha256 = staged.Sha256,
            ImageWidth = staged.Width,
            ImageHeight = staged.Height,
            MmPerPixel = request.MmPerPixel,
            CalibrationSource = request.CalibrationSource,
            SiteCode = request.SiteCode.Trim().ToUpperInvariant(),
            DeviceCode = request.DeviceCode.Trim().ToUpperInvariant(),
            PatientGroupToken = request.PatientGroupToken.Trim().ToLowerInvariant(),
            Split = request.Split,
            QualityCategory = request.QualityCategory,
            SkeletalCategory = request.SkeletalCategory,
            VerticalCategory = request.VerticalCategory,
            DeIdentificationConfirmed = true,
            PixelInspectionConfirmed = true,
            NoBarcodeOrQrConfirmed = true,
            LegalBasisConfirmed = true,
            MetadataSanitized = true,
            OrientationConfirmed = true,
            Status = request.MmPerPixel.HasValue ? CephPilotCaseStatus.Ready : CephPilotCaseStatus.Uploaded,
        };
        db.CephPilotCases.Add(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            TryDeletePrivateFile(storagePath);
            throw;
        }
        await audit.LogAsync(AuditAction.Create, "CephPilotCase", entity.Id, newData: new
        {
            entity.ProjectId,
            entity.CaseCode,
            entity.ImageWidth,
            entity.ImageHeight,
            entity.SourceType,
            entity.Status,
            entity.MetadataSanitized,
            entity.DeIdentificationConfirmed,
        });
        return CreatedAtAction(nameof(GetCase), new { caseId = entity.Id }, MapCase(entity, true));
    }

    [HttpGet("cases/{caseId:guid}")]
    public async Task<IActionResult> GetCase(Guid caseId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Forbid();
        var entity = await db.CephPilotCases.AsNoTracking().Include(item => item.Project)
            .Include(item => item.ExportArtifacts)
            .FirstOrDefaultAsync(item => item.Id == caseId, cancellationToken);
        if (entity is null) return NotFound(Error("case.not-found", "Pilot case not found."));
        if (!CanAccess(entity.Project, userId)) return Forbid();
        return Ok(MapCase(entity, currentUser.IsAdmin));
    }

    [HttpGet("cases/{caseId:guid}/image")]
    public async Task<IActionResult> GetCaseImage(Guid caseId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Forbid();
        var entity = await db.CephPilotCases.AsNoTracking().Include(item => item.Project)
            .FirstOrDefaultAsync(item => item.Id == caseId, cancellationToken);
        if (entity is null) return NotFound(Error("case.not-found", "Pilot case not found."));
        if (!CanAccess(entity.Project, userId)) return Forbid();
        var path = ResolvePrivateStoragePath(entity.ImageStorageKey);
        if (!System.IO.File.Exists(path)) return NotFound(Error("case.image-not-found", "Pilot image is unavailable."));
        return PhysicalFile(path, "image/png", enableRangeProcessing: true);
    }

    [HttpPatch("cases/{caseId:guid}/calibration")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateCalibration(
        Guid caseId,
        UpdateCephPilotCalibrationRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await db.CephPilotCases.Include(item => item.Project)
            .FirstOrDefaultAsync(item => item.Id == caseId, cancellationToken);
        if (entity is null) return NotFound(Error("case.not-found", "Pilot case not found."));
        if (entity.Project.Status != CephPilotProjectStatus.Draft ||
            entity.Status is not (CephPilotCaseStatus.Uploaded or CephPilotCaseStatus.Ready))
            return Conflict(Error("case.calibration-locked", "Calibration cannot change after reviewer annotation starts."));
        if (request.ExpectedRevision != entity.Revision)
            return Conflict(Error("case.revision-conflict", "The Pilot case changed. Reload it before updating calibration."));
        if (request.MmPerPixel is <= 0 or > 10)
            return BadRequest(Error("case.invalid-calibration", "MmPerPixel must be greater than 0 and no more than 10."));
        if (!Enum.IsDefined(request.CalibrationSource))
            return BadRequest(Error("case.invalid-calibration-source", "CalibrationSource is not supported."));

        var previousMmPerPixel = entity.MmPerPixel;
        var previousSource = entity.CalibrationSource;
        entity.MmPerPixel = request.MmPerPixel;
        entity.CalibrationSource = request.CalibrationSource;
        entity.Status = CephPilotCaseStatus.Ready;
        entity.Revision += 1;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(Error("case.revision-conflict", "The Pilot case changed. Reload it before updating calibration."));
        }

        await audit.LogAsync(AuditAction.Update, "CephPilotCaseCalibration", entity.Id,
            oldData: new { MmPerPixel = previousMmPerPixel, CalibrationSource = previousSource },
            newData: new { entity.MmPerPixel, entity.CalibrationSource, entity.Status, entity.Revision });
        return Ok(MapCase(entity, true));
    }

    [HttpPost("cases/{caseId:guid}/artifacts")]
    [Authorize(Policy = "AdminOnly")]
    [RequestSizeLimit(MaxArtifactBytes + 1024 * 1024)]
    public async Task<IActionResult> StageArtifact(
        Guid caseId,
        [FromForm] StageCephPilotArtifactRequest request,
        CancellationToken cancellationToken)
    {
        var unknownField = await FindUnknownFormFieldAsync(StageCephPilotArtifactRequest.AllowedFields);
        if (unknownField is not null)
            return BadRequest(Error("artifact.unrecognized-field", $"Unrecognized form field: {unknownField}."));
        if (!request.DeIdentificationVisuallyConfirmed)
            return BadRequest(Error("artifact.deidentification-required", "Visual de-identification confirmation is required."));
        if (request.File is null || request.File.Length == 0 || request.File.Length > MaxArtifactBytes)
            return BadRequest(Error("artifact.invalid-file", "A non-empty export up to 10 MB is required."));

        var entity = await db.CephPilotCases.Include(item => item.Project)
            .FirstOrDefaultAsync(item => item.Id == caseId, cancellationToken);
        if (entity is null) return NotFound(Error("case.not-found", "Pilot case not found."));
        if (entity.Project.Status != CephPilotProjectStatus.Draft)
            return Conflict(Error("project.staging-locked", "Exports can only be staged while the project is Draft."));

        byte[] bytes;
        await using (var input = request.File.OpenReadStream())
        await using (var buffer = new MemoryStream())
        {
            await input.CopyToAsync(buffer, cancellationToken);
            bytes = buffer.ToArray();
        }
        var fileContract = ValidateArtifact(bytes, request.ArtifactType);
        if (fileContract is null)
            return BadRequest(Error("artifact.invalid-format", "The file signature does not match the selected official export type."));
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (await db.CephPilotExportArtifacts.AnyAsync(item => item.CaseId == caseId && item.ArtifactType == request.ArtifactType && item.Sha256 == hash, cancellationToken))
            return Conflict(Error("artifact.duplicate", "This export is already staged for the case."));

        var artifactId = Guid.NewGuid();
        var storageKey = $"{entity.ProjectId:N}/{caseId:N}/artifacts/{artifactId:N}{fileContract.Value.Extension}";
        var path = ResolvePrivateStoragePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await System.IO.File.WriteAllBytesAsync(path, bytes, cancellationToken);
        var artifact = new CephPilotExportArtifact
        {
            Id = artifactId,
            CaseId = caseId,
            ArtifactType = request.ArtifactType,
            StorageKey = storageKey,
            Sha256 = hash,
            ContentType = fileContract.Value.ContentType,
            SizeBytes = bytes.LongLength,
        };
        db.CephPilotExportArtifacts.Add(artifact);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            TryDeletePrivateFile(path);
            throw;
        }
        await audit.LogAsync(AuditAction.Create, "CephPilotExportArtifact", artifact.Id, newData: new
        {
            artifact.CaseId,
            artifact.ArtifactType,
            artifact.SizeBytes,
            artifact.IsComparatorOnly,
            artifact.ImportStatus,
        });
        return Ok(MapArtifact(artifact));
    }

    [HttpGet("cases/{caseId:guid}/artifacts/{artifactId:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DownloadArtifact(Guid caseId, Guid artifactId, CancellationToken cancellationToken)
    {
        var artifact = await db.CephPilotExportArtifacts.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == artifactId && item.CaseId == caseId, cancellationToken);
        if (artifact is null) return NotFound(Error("artifact.not-found", "Pilot export artifact not found."));
        var path = ResolvePrivateStoragePath(artifact.StorageKey);
        if (!System.IO.File.Exists(path)) return NotFound(Error("artifact.file-not-found", "Pilot export artifact is unavailable."));
        return PhysicalFile(path, artifact.ContentType, enableRangeProcessing: true);
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = currentUser.UserId ?? Guid.Empty;
        return userId != Guid.Empty;
    }

    private bool CanAccess(CephPilotProject project, Guid userId) =>
        currentUser.IsAdmin || project.ReviewerAUserId == userId || project.ReviewerBUserId == userId || project.AdjudicatorUserId == userId;

    private async Task<bool> ReviewersAreEligibleAsync(Guid reviewerA, Guid reviewerB, Guid? adjudicator, CancellationToken cancellationToken)
    {
        var ids = new[] { reviewerA, reviewerB }.Concat(adjudicator.HasValue ? [adjudicator.Value] : []).Distinct().ToArray();
        var count = await db.Users.CountAsync(item => ids.Contains(item.Id) &&
            (item.Role == UserRole.Admin || item.Role == UserRole.Orthodontist), cancellationToken);
        return count == ids.Length;
    }

    private static object? ValidateProjectText(string name, string code, string? description, string? datasetVersion)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150)
            return Error("project.invalid-name", "Project name is required and must not exceed 150 characters.");
        if (!SafeCodeRegex().IsMatch(code.Trim()))
            return Error("project.invalid-code", "Project code must contain 3-50 letters, numbers, dots, underscores, or hyphens.");
        if (description?.Trim().Length > 1000)
            return Error("project.description-too-long", "Description must not exceed 1000 characters.");
        if (datasetVersion?.Trim().Length > 100)
            return Error("project.dataset-version-too-long", "Dataset version must not exceed 100 characters.");
        return null;
    }

    private static object? ValidateCaseRequest(CreateCephPilotCaseRequest request)
    {
        if (request.Image is null || request.Image.Length == 0 || request.Image.Length > MaxImageBytes)
            return Error("case.invalid-image", "A non-empty JPEG or PNG image up to 25 MB is required.");
        if (!request.DeIdentificationVisuallyConfirmed || !request.PixelInspectionConfirmed ||
            !request.NoBarcodeOrQrConfirmed || !request.LegalBasisConfirmed || !request.OrientationFacingRightConfirmed)
            return Error("case.confirmations-required", "All de-identification, pixel, barcode/QR, legal-basis, and orientation confirmations are required.");
        if (!HashRegex().IsMatch(request.PatientGroupToken?.Trim() ?? string.Empty))
            return Error("case.invalid-patient-group-token", "PatientGroupToken must be a lowercase 64-character salted SHA-256 token.");
        if (!ShortCodeRegex().IsMatch(request.SiteCode?.Trim() ?? string.Empty) || !ShortCodeRegex().IsMatch(request.DeviceCode?.Trim() ?? string.Empty))
            return Error("case.invalid-source-code", "SiteCode and DeviceCode must be opaque 2-40 character codes.");
        if (!string.IsNullOrWhiteSpace(request.SourceReference) && !SourceReferenceRegex().IsMatch(request.SourceReference.Trim()))
            return Error("case.invalid-source-reference", "SourceReference must be a non-sensitive opaque token up to 80 characters.");
        if (request.MmPerPixel is <= 0 or > 10)
            return Error("case.invalid-calibration", "MmPerPixel must be greater than 0 and no more than 10.");
        if (request.MmPerPixel.HasValue != request.CalibrationSource.HasValue)
            return Error("case.calibration-source-required", "MmPerPixel and CalibrationSource must be provided together.");
        if (request.CalibrationSource.HasValue && !Enum.IsDefined(request.CalibrationSource.Value))
            return Error("case.invalid-calibration-source", "CalibrationSource is not supported.");
        return null;
    }

    internal static StagedImage SanitizeImage(byte[] inputBytes)
    {
        using var bitmap = SKBitmap.Decode(inputBytes);
        if (bitmap is null)
            throw new ArgumentException("The file is not a decodable JPEG or PNG image.");
        if (bitmap.Width < MinImageDimension || bitmap.Height < MinImageDimension ||
            bitmap.Width > MaxImageDimension || bitmap.Height > MaxImageDimension)
            throw new ArgumentException($"Image dimensions must be between {MinImageDimension} and {MaxImageDimension} pixels.");
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var sanitized = encoded.ToArray();
        return new StagedImage(
            sanitized,
            bitmap.Width,
            bitmap.Height,
            Convert.ToHexString(SHA256.HashData(sanitized)).ToLowerInvariant());
    }

    private static (string Extension, string ContentType)? ValidateArtifact(byte[] bytes, CephPilotExportArtifactType type)
    {
        if (type == CephPilotExportArtifactType.WebCephMeasurementReport)
            return bytes.Length >= 5 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8)
                ? (".pdf", "application/pdf")
                : null;
        if (bytes.Length == 0 || bytes.AsSpan(0, Math.Min(bytes.Length, 1024)).Contains((byte)0)) return null;
        return (".csv", "text/csv");
    }

    private string ResolvePrivateStoragePath(string storageKey)
    {
        var root = Environment.GetEnvironmentVariable("CEPH_PILOT_STORAGE_PATH");
        if (string.IsNullOrWhiteSpace(root))
        {
            if (environment.IsProduction())
                throw new InvalidOperationException("CEPH_PILOT_STORAGE_PATH is required in Production.");
            root = Path.Combine(Path.GetTempPath(), "aqlan-ceph-pilot-private");
        }
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, storageKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid Pilot storage key.");
        return fullPath;
    }

    private async Task<string?> FindUnknownFormFieldAsync(IReadOnlySet<string> allowed)
    {
        if (!Request.HasFormContentType) return null;
        var form = await Request.ReadFormAsync();
        return form.Keys.FirstOrDefault(key => !allowed.Contains(key)) ??
               form.Files.Select(file => file.Name).FirstOrDefault(key => !allowed.Contains(key));
    }

    private static void TryDeletePrivateFile(string path)
    {
        try
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
        catch
        {
            // The database operation remains the authority; stale private files are cleaned operationally.
        }
    }

    private static object MapProject(CephPilotProject item, Guid userId, bool isAdmin)
    {
        if (!isAdmin)
        {
            return new
            {
                item.Id,
                item.Name,
                item.Code,
                item.Description,
                item.LandmarkDefinitionVersion,
                status = item.Status.ToString(),
                item.DatasetVersion,
                item.Revision,
                item.CreatedAt,
                item.LockedAt,
                myReviewerSlot = item.ReviewerAUserId == userId ? "A" : item.ReviewerBUserId == userId ? "B" : "Adjudicator",
                caseCount = item.Cases.Count,
                readyCaseCount = item.Cases.Count(caseItem => caseItem.Status == CephPilotCaseStatus.Ready),
            };
        }

        return new
        {
            item.Id,
            item.Name,
            item.Code,
            item.Description,
            item.LandmarkDefinitionVersion,
            status = item.Status.ToString(),
            item.DatasetVersion,
            item.IsAiHidden,
            item.IsWebCephHidden,
            item.Revision,
            item.CreatedAt,
            item.LockedAt,
            item.ReviewerAUserId,
            item.ReviewerBUserId,
            item.AdjudicatorUserId,
            myReviewerSlot = "Admin",
            caseCount = item.Cases.Count,
            readyCaseCount = item.Cases.Count(caseItem => caseItem.Status == CephPilotCaseStatus.Ready),
        };
    }

    private static object MapCase(CephPilotCase item, bool isAdmin)
    {
        if (!isAdmin)
        {
            return new
            {
                item.Id,
                item.ProjectId,
                item.CaseCode,
                item.ImageWidth,
                item.ImageHeight,
                item.MmPerPixel,
                calibrationSource = item.CalibrationSource?.ToString(),
                item.Orientation,
                item.SiteCode,
                item.DeviceCode,
                split = item.Split.ToString(),
                qualityCategory = item.QualityCategory.ToString(),
                skeletalCategory = item.SkeletalCategory?.ToString(),
                verticalCategory = item.VerticalCategory?.ToString(),
                status = item.Status.ToString(),
                item.Revision,
                item.CreatedAt,
            };
        }

        return new
        {
            item.Id,
            item.ProjectId,
            item.CaseCode,
            sourceType = item.SourceType.ToString(),
            item.SourceReference,
            item.ImageWidth,
            item.ImageHeight,
            item.MmPerPixel,
            calibrationSource = item.CalibrationSource?.ToString(),
            item.Orientation,
            item.SiteCode,
            item.DeviceCode,
            split = item.Split.ToString(),
            qualityCategory = item.QualityCategory.ToString(),
            skeletalCategory = item.SkeletalCategory?.ToString(),
            verticalCategory = item.VerticalCategory?.ToString(),
            status = item.Status.ToString(),
            migrationDecision = item.MigrationDecision.ToString(),
            item.Revision,
            item.CreatedAt,
            item.DeIdentificationConfirmed,
            item.PixelInspectionConfirmed,
            item.NoBarcodeOrQrConfirmed,
            item.LegalBasisConfirmed,
            item.MetadataSanitized,
            item.OrientationConfirmed,
            hasWebCephLandmarkTable = item.ExportArtifacts.Any(artifact => artifact.ArtifactType == CephPilotExportArtifactType.WebCephLandmarkTable),
            hasWebCephMeasurementReport = item.ExportArtifacts.Any(artifact => artifact.ArtifactType == CephPilotExportArtifactType.WebCephMeasurementReport),
            artifacts = item.ExportArtifacts.Select(MapArtifact),
        };
    }

    private static object MapArtifact(CephPilotExportArtifact item) => new
    {
        item.Id,
        item.CaseId,
        artifactType = item.ArtifactType.ToString(),
        item.SizeBytes,
        item.IsComparatorOnly,
        item.ImportStatus,
        item.CreatedAt,
    };

    private static object Error(string code, string message) => new { code, message };
    private static string? CleanOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal sealed record StagedImage(byte[] Bytes, int Width, int Height, string Sha256);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{2,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeCodeRegex();
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{1,39}$", RegexOptions.CultureInvariant)]
    private static partial Regex ShortCodeRegex();
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex SourceReferenceRegex();
    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex HashRegex();
}

public sealed class CreateCephPilotCaseRequest
{
    internal static readonly IReadOnlySet<string> AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        nameof(Image), nameof(SourceType), nameof(SourceReference), nameof(MmPerPixel), nameof(CalibrationSource),
        nameof(SiteCode), nameof(DeviceCode), nameof(PatientGroupToken), nameof(Split), nameof(QualityCategory),
        nameof(SkeletalCategory), nameof(VerticalCategory), nameof(DeIdentificationVisuallyConfirmed),
        nameof(PixelInspectionConfirmed), nameof(NoBarcodeOrQrConfirmed), nameof(LegalBasisConfirmed),
        nameof(OrientationFacingRightConfirmed),
    };

    public IFormFile Image { get; init; } = null!;
    public CephPilotSourceType SourceType { get; init; }
    public string? SourceReference { get; init; }
    public decimal? MmPerPixel { get; init; }
    public CephPilotCalibrationSource? CalibrationSource { get; init; }
    public string SiteCode { get; init; } = string.Empty;
    public string DeviceCode { get; init; } = string.Empty;
    public string PatientGroupToken { get; init; } = string.Empty;
    public CephPilotSplit Split { get; init; } = CephPilotSplit.Pilot;
    public CephPilotQualityCategory QualityCategory { get; init; }
    public CephPilotSkeletalCategory? SkeletalCategory { get; init; }
    public CephPilotVerticalCategory? VerticalCategory { get; init; }
    public bool DeIdentificationVisuallyConfirmed { get; init; }
    public bool PixelInspectionConfirmed { get; init; }
    public bool NoBarcodeOrQrConfirmed { get; init; }
    public bool LegalBasisConfirmed { get; init; }
    public bool OrientationFacingRightConfirmed { get; init; }
}

public sealed class StageCephPilotArtifactRequest
{
    internal static readonly IReadOnlySet<string> AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        nameof(File), nameof(ArtifactType), nameof(DeIdentificationVisuallyConfirmed),
    };

    public IFormFile File { get; init; } = null!;
    public CephPilotExportArtifactType ArtifactType { get; init; }
    public bool DeIdentificationVisuallyConfirmed { get; init; }
}
