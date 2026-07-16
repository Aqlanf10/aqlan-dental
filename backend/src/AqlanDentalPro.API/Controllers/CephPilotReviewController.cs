using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/ceph-pilot")]
[Authorize(Policy = "OrthoAccess")]
public sealed class CephPilotReviewController(
    AppDbContext db,
    ICurrentUserService currentUser,
    IAuditService audit) : ControllerBase
{
    [HttpGet("cases/{caseId:guid}/review")]
    public async Task<IActionResult> GetMyReview(Guid caseId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Forbid();
        var pilotCase = await db.CephPilotCases.AsNoTracking().Include(item => item.Project)
            .FirstOrDefaultAsync(item => item.Id == caseId, cancellationToken);
        if (pilotCase is null) return NotFound(Error("case.not-found", "Pilot case not found."));
        if (!TryGetReviewerSlot(pilotCase.Project, userId, out _)) return Forbid();

        var session = await db.CephPilotReviewSessions.AsNoTracking().Include(item => item.Points)
            .FirstOrDefaultAsync(item => item.CaseId == caseId && item.ReviewerUserId == userId, cancellationToken);
        if (session is null) return NotFound(Error("review.not-started", "Your blinded review has not started."));
        return Ok(MapSession(session, pilotCase));
    }

    [HttpPost("cases/{caseId:guid}/review/start")]
    public async Task<IActionResult> StartMyReview(Guid caseId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Forbid();
        var pilotCase = await db.CephPilotCases.Include(item => item.Project)
            .FirstOrDefaultAsync(item => item.Id == caseId, cancellationToken);
        if (pilotCase is null) return NotFound(Error("case.not-found", "Pilot case not found."));
        if (!TryGetReviewerSlot(pilotCase.Project, userId, out var slot)) return Forbid();

        var existing = await db.CephPilotReviewSessions.Include(item => item.Points)
            .FirstOrDefaultAsync(item => item.CaseId == caseId && item.ReviewerUserId == userId, cancellationToken);
        if (existing is not null) return Ok(MapSession(existing, pilotCase));
        if (pilotCase.Project.Status is not (CephPilotProjectStatus.ReadyForAnnotation or CephPilotProjectStatus.AnnotationInProgress))
            return Conflict(Error("review.project-not-open", "The Pilot project is not open for blinded annotation."));
        if (pilotCase.Status is CephPilotCaseStatus.Uploaded or CephPilotCaseStatus.ComparisonReady or
            CephPilotCaseStatus.NeedsAdjudication or CephPilotCaseStatus.Adjudicated or CephPilotCaseStatus.ReleaseReady)
            return Conflict(Error("review.case-not-open", "The Pilot case is not available for reviewer annotation."));

        var session = new CephPilotReviewSession
        {
            CaseId = caseId,
            ReviewerUserId = userId,
            ReviewerSlot = slot,
            LandmarkDefinitionVersion = pilotCase.Project.LandmarkDefinitionVersion,
        };
        db.CephPilotReviewSessions.Add(session);
        if (pilotCase.Project.Status == CephPilotProjectStatus.ReadyForAnnotation)
        {
            pilotCase.Project.Status = CephPilotProjectStatus.AnnotationInProgress;
            pilotCase.Project.Revision++;
        }
        if (pilotCase.Status == CephPilotCaseStatus.Ready)
        {
            pilotCase.Status = slot == CephPilotReviewerSlot.A
                ? CephPilotCaseStatus.ReviewerAInProgress
                : CephPilotCaseStatus.ReviewerBInProgress;
            pilotCase.Revision++;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(Error("review.start-conflict", "The review was started elsewhere. Reload the case."));
        }
        await audit.LogAsync(AuditAction.Create, "CephPilotReviewSession", session.Id, newData: new
        {
            session.CaseId,
            ReviewerSlot = session.ReviewerSlot.ToString(),
            Status = session.Status.ToString(),
            session.LandmarkDefinitionVersion,
            session.ToolVersion,
        });
        return CreatedAtAction(nameof(GetMyReview), new { caseId }, MapSession(session, pilotCase));
    }

    [HttpPut("cases/{caseId:guid}/review")]
    public async Task<IActionResult> SaveMyReview(
        Guid caseId,
        SaveCephPilotReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Forbid();
        var session = await db.CephPilotReviewSessions.Include(item => item.Case).ThenInclude(item => item.Project)
            .Include(item => item.Points)
            .FirstOrDefaultAsync(item => item.CaseId == caseId && item.ReviewerUserId == userId, cancellationToken);
        if (session is null) return NotFound(Error("review.not-started", "Start your blinded review before saving."));
        if (!TryGetReviewerSlot(session.Case.Project, userId, out var slot) || slot != session.ReviewerSlot) return Forbid();
        if (session.Case.Project.Status != CephPilotProjectStatus.AnnotationInProgress)
            return Conflict(Error("review.project-not-open", "The Pilot project is not open for blinded annotation."));
        if (session.Status != CephPilotReviewStatus.Draft)
            return Conflict(Error("review.submitted", "A submitted blinded review is immutable."));
        if (request.ExpectedRevision <= 0 || request.ExpectedRevision != session.Revision)
            return Conflict(Error("review.revision-conflict", "The review changed. Reload it before saving."));

        var validation = ValidatePoints(request.Points, session.Case);
        if (validation is not null) return BadRequest(validation);

        var now = DateTime.UtcNow;
        var requestedKeys = request.Points.Select(item => item.LandmarkKey).ToHashSet(StringComparer.Ordinal);
        var removed = session.Points.Where(item => !requestedKeys.Contains(item.LandmarkKey)).ToArray();
        db.CephPilotReviewPoints.RemoveRange(removed);
        var existingByKey = session.Points.Except(removed).ToDictionary(item => item.LandmarkKey, StringComparer.Ordinal);
        foreach (var point in request.Points)
        {
            if (!existingByKey.TryGetValue(point.LandmarkKey, out var entity))
            {
                entity = new CephPilotReviewPoint
                {
                    ReviewSessionId = session.Id,
                    LandmarkKey = point.LandmarkKey,
                    IsCore = CephPilotLandmarkCatalog.CoreKeySet.Contains(point.LandmarkKey),
                };
                db.CephPilotReviewPoints.Add(entity);
            }
            entity.Visibility = point.Visibility;
            entity.XCoordPx = point.XCoordPx;
            entity.YCoordPx = point.YCoordPx;
            entity.ContourDecision = point.ContourDecision;
            entity.AnnotatedAt = now;
        }
        session.LastSavedAt = now;
        session.Revision++;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(Error("review.revision-conflict", "The review changed. Reload it before saving."));
        }
        await audit.LogAsync(AuditAction.Update, "CephPilotReviewSession", session.Id, newData: new
        {
            session.CaseId,
            session.Revision,
            pointCount = request.Points.Count,
            coreDecisionCount = request.Points.Count(item => CephPilotLandmarkCatalog.CoreKeySet.Contains(item.LandmarkKey)),
        });
        return Ok(MapSession(session, session.Case));
    }

    [HttpPost("cases/{caseId:guid}/review/submit")]
    public async Task<IActionResult> SubmitMyReview(
        Guid caseId,
        SubmitCephPilotReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Forbid();
        var session = await db.CephPilotReviewSessions.Include(item => item.Case).ThenInclude(item => item.Project)
            .Include(item => item.Points)
            .FirstOrDefaultAsync(item => item.CaseId == caseId && item.ReviewerUserId == userId, cancellationToken);
        if (session is null) return NotFound(Error("review.not-started", "Start your blinded review before submitting."));
        if (!TryGetReviewerSlot(session.Case.Project, userId, out var slot) || slot != session.ReviewerSlot) return Forbid();
        if (session.Case.Project.Status != CephPilotProjectStatus.AnnotationInProgress)
            return Conflict(Error("review.project-not-open", "The Pilot project is not open for blinded annotation."));
        if (session.Status != CephPilotReviewStatus.Draft)
            return Conflict(Error("review.submitted", "This blinded review was already submitted."));
        if (request.ExpectedRevision <= 0 || request.ExpectedRevision != session.Revision)
            return Conflict(Error("review.revision-conflict", "The review changed. Reload it before submitting."));

        var decidedCoreKeys = session.Points.Where(item => item.IsCore)
            .Select(item => item.LandmarkKey).ToHashSet(StringComparer.Ordinal);
        var missing = CephPilotLandmarkCatalog.CoreKeys.Where(key => !decidedCoreKeys.Contains(key)).ToArray();
        if (missing.Length > 0)
            return Conflict(Error("review.core-incomplete", $"All 24 core landmarks require a visible or not-visible decision. Missing: {string.Join(", ", missing)}."));

        session.Status = CephPilotReviewStatus.Submitted;
        session.SubmittedAt = DateTime.UtcNow;
        session.Revision++;
        var peerSubmitted = await db.CephPilotReviewSessions.AsNoTracking().AnyAsync(item =>
            item.CaseId == caseId && item.ReviewerUserId != userId && item.Status == CephPilotReviewStatus.Submitted,
            cancellationToken);
        session.Case.Status = peerSubmitted
            ? CephPilotCaseStatus.ComparisonReady
            : slot == CephPilotReviewerSlot.A
                ? CephPilotCaseStatus.ReviewerASubmitted
                : CephPilotCaseStatus.ReviewerBSubmitted;
        session.Case.Revision++;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(Error("review.revision-conflict", "The review changed. Reload it before submitting."));
        }
        await audit.LogAsync(AuditAction.Approve, "CephPilotReviewSession", session.Id, newData: new
        {
            session.CaseId,
            ReviewerSlot = session.ReviewerSlot.ToString(),
            Status = session.Status.ToString(),
            coreDecisionCount = CephPilotLandmarkCatalog.CoreKeys.Count,
            optionalDecisionCount = session.Points.Count(item => !item.IsCore),
            comparisonReady = peerSubmitted,
        });
        return Ok(MapSession(session, session.Case));
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = currentUser.UserId ?? Guid.Empty;
        return userId != Guid.Empty;
    }

    private static bool TryGetReviewerSlot(CephPilotProject project, Guid userId, out CephPilotReviewerSlot slot)
    {
        if (project.ReviewerAUserId == userId)
        {
            slot = CephPilotReviewerSlot.A;
            return true;
        }
        if (project.ReviewerBUserId == userId)
        {
            slot = CephPilotReviewerSlot.B;
            return true;
        }
        slot = default;
        return false;
    }

    private static object? ValidatePoints(IReadOnlyList<CephPilotReviewPointRequest> points, CephPilotCase pilotCase)
    {
        if (points.Count > CephPilotLandmarkCatalog.AllKeys.Count)
            return Error("review.too-many-points", "A review may contain at most 27 ratified landmark decisions.");
        var duplicate = points.GroupBy(item => item.LandmarkKey, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicate is not null)
            return Error("review.duplicate-key", $"Landmark {duplicate} appears more than once.");

        foreach (var point in points)
        {
            if (!CephPilotLandmarkCatalog.AllKeys.Contains(point.LandmarkKey))
                return Error("review.unknown-key", $"Landmark {point.LandmarkKey} is not part of ADP-LM-LAT-v1.0.");
            if (!Enum.IsDefined(point.Visibility) || !Enum.IsDefined(point.ContourDecision))
                return Error("review.invalid-decision", $"Landmark {point.LandmarkKey} has an unsupported review decision.");
            if (point.Visibility == CephPilotLandmarkVisibility.Visible)
            {
                if (!point.XCoordPx.HasValue || !point.YCoordPx.HasValue ||
                    point.XCoordPx < 0 || point.YCoordPx < 0 ||
                    point.XCoordPx > pilotCase.ImageWidth || point.YCoordPx > pilotCase.ImageHeight)
                    return Error("review.coordinate-out-of-bounds", $"Landmark {point.LandmarkKey} must have coordinates inside the original image.");
                if (point.ContourDecision == CephPilotContourDecision.Unresolvable)
                    return Error("review.invalid-contour-decision", $"Visible landmark {point.LandmarkKey} cannot be marked Unresolvable.");
            }
            else if (point.XCoordPx.HasValue || point.YCoordPx.HasValue || point.ContourDecision != CephPilotContourDecision.Unresolvable)
            {
                return Error("review.not-visible-contract", $"Not-visible landmark {point.LandmarkKey} must have no coordinates and use Unresolvable.");
            }
        }
        return null;
    }

    private static object MapSession(CephPilotReviewSession session, CephPilotCase pilotCase) => new
    {
        session.Id,
        session.CaseId,
        reviewerSlot = session.ReviewerSlot.ToString(),
        status = session.Status.ToString(),
        session.LandmarkDefinitionVersion,
        session.ToolVersion,
        session.StartedAt,
        session.LastSavedAt,
        session.SubmittedAt,
        session.Revision,
        pilotCase.CaseCode,
        pilotCase.ImageWidth,
        pilotCase.ImageHeight,
        pilotCase.MmPerPixel,
        coreLandmarkCount = CephPilotLandmarkCatalog.CoreKeys.Count,
        points = session.Points.OrderBy(item => Array.IndexOf(CephPilotLandmarkCatalog.AllOrderedKeys, item.LandmarkKey)).Select(item => new
        {
            item.LandmarkKey,
            item.IsCore,
            visibility = item.Visibility.ToString(),
            item.XCoordPx,
            item.YCoordPx,
            contourDecision = item.ContourDecision.ToString(),
            item.AnnotatedAt,
        }),
    };

    private static object Error(string code, string message) => new { code, message };
}
