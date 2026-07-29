using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

// ─── DTOs ────────────────────────────────────────────────────────────────────

public sealed class PatientSegmentDto
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Color { get; init; }
    public bool IsDynamic { get; init; }
    public bool IsBuiltIn { get; init; }
    public int MemberCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class PatientSegmentMemberDto
{
    public Guid Id { get; init; }
    public Guid PatientId { get; init; }
    public string PatientNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public DateTime AddedAt { get; init; }
    /// <summary>Context for built-in dynamic segments (e.g. overdue amount, days since visit).</summary>
    public string? Reason { get; init; }
}

public sealed class CreatePatientSegmentRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Color { get; init; }
}

public sealed class CreatePatientSegmentRequestValidator : AbstractValidator<CreatePatientSegmentRequest>
{
    public CreatePatientSegmentRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم المجموعة مطلوب")
            .MaximumLength(200).WithMessage("اسم المجموعة يجب ألا يتجاوز 200 حرف");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("الوصف يجب ألا يتجاوز 1000 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.Color)
            .MaximumLength(20).WithMessage("اللون يجب ألا يتجاوز 20 حرفاً")
            .When(x => !string.IsNullOrWhiteSpace(x.Color));
    }
}

public sealed class AddSegmentMemberRequest
{
    public Guid PatientId { get; init; }
}

// ─── Controller ──────────────────────────────────────────────────────────────

/// <summary>
/// YOLO-S5: Patient segments — pre-built dynamic segments (computed, not stored)
/// plus custom manual segments (CRUD).
///
/// Pre-built dynamic segments (returned in GetList with IsBuiltIn = true):
///   - "مرضى تقويم متأخرون"    — OrthoCase patients whose latest OrthoVisit
///                                 has NextAppointmentDate in the past.
///   - "مرضى عليهم مبالغ"      — patients with outstanding balance > 0
///                                 (contracts + non-draft invoices − payments).
///   - "مرضى لم يحضروا"        — patients with no Visit in the last 90 days.
///   - "مرضى المختبر الجاهز"   — patients with a LabOrder in "Ready" status
///                                 (appliance ready for delivery).
///
/// Custom segments are stored in PatientSegments + PatientSegmentMembers.
/// All endpoints require Admin role (mirrors InventoryController).
/// </summary>
[ApiController]
[Route("api/patient-segments")]
[Authorize(Policy = "AdminOnly")]
public class PatientSegmentsController(AppDbContext db) : ControllerBase
{
    /// <summary>Returns both built-in (computed) and custom (stored) segments.</summary>
    [HttpGet]
    public async Task<IActionResult> GetList()
    {
        // ── Built-in dynamic segments (always 4, computed in code) ──────────
        var today = ClinicTimeProvider.ClinicToday();
        var ninetyDaysAgo = today.AddDays(-90);

        var orthoOverdueCount = await (
            from oc in db.OrthoCases
            where oc.IsActive && oc.Status == OrthoCaseStatus.Active
            let latestVisit = oc.Visits
                .Where(v => v.IsActive)
                .OrderByDescending(v => v.VisitDate)
                .FirstOrDefault()
            where latestVisit != null
               && latestVisit.NextAppointmentDate != null
               && latestVisit.NextAppointmentDate < today
            select oc.PatientId
        ).Distinct().CountAsync();

        // Patients with outstanding balance > 0 (contracts + non-draft invoices + unbilled visits − payments).
        // Computed server-side; mirrors FinanceService.GetPatientFinanceSummaryAsync math
        // but aggregated across all patients in one query. QA-596: now includes
        // Visit.AmountDueReference for sessions with no linked invoice, so patients
        // with unbilled sessions appear in the "مرضى عليهم مبالغ" segment.
        var contractSpend = await db.Contracts
            .Where(c => c.IsActive && c.PatientId != Guid.Empty && c.Status != ContractStatus.Cancelled)
            .GroupBy(c => c.PatientId)
            .Select(g => new { PatientId = g.Key, Total = g.Sum(c => c.TotalAmount - c.DiscountAmount) })
            .ToListAsync();
        var invoiceSpend = await db.Invoices
            .Where(i => i.IsActive
                     && i.PatientId != Guid.Empty
                     && i.Status != InvoiceStatus.Cancelled
                     && i.Status != InvoiceStatus.Draft)
            .GroupBy(i => i.PatientId)
            .Select(g => new { PatientId = g.Key, Total = g.Sum(i => i.TotalAmount) })
            .ToListAsync();
        var paymentsByPatient = await db.Payments
            .Where(p => p.IsActive && p.PatientId != Guid.Empty)
            .GroupBy(p => p.PatientId)
            .Select(g => new { PatientId = g.Key, Total = g.Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount) })
            .ToListAsync();
        // QA-596: unbilled visits — sessions with AmountDueReference and no linked invoice.
        // Build the set of billed visit IDs (visits that already have an invoice via Invoice.VisitId)
        // and exclude them to avoid double-counting with invoiceSpend above.
        var billedVisitIds = await db.Invoices
            .Where(i => i.VisitId.HasValue && i.IsActive && i.PatientId != Guid.Empty)
            .Select(i => i.VisitId!.Value)
            .ToListAsync();
        var billedVisitSet = billedVisitIds.ToHashSet();
        var unbilledVisitRows = await db.Visits
            .Where(v => v.IsActive && v.PatientId != Guid.Empty
                     && v.AmountDueReference.HasValue && v.AmountDueReference > 0)
            .Select(v => new { v.PatientId, v.Id, Amount = v.AmountDueReference ?? 0m })
            .ToListAsync();
        var unbilledVisitsByPatient = unbilledVisitRows
            .Where(v => !billedVisitSet.Contains(v.Id))
            .GroupBy(v => v.PatientId)
            .Select(g => new { PatientId = g.Key, Total = g.Sum(v => v.Amount) })
            .ToList();

        var spendByPatient = new Dictionary<Guid, decimal>();
        foreach (var c in contractSpend)
            spendByPatient[c.PatientId] = c.Total;
        foreach (var i in invoiceSpend)
            spendByPatient[i.PatientId] = (spendByPatient.TryGetValue(i.PatientId, out var v) ? v : 0m) + i.Total;
        // QA-596: subtract billed visit amounts from unbilled aggregate to avoid double-count,
        // then add the per-patient unbilled total to the outstanding calculation.
        var unbilledNetByPatient = new Dictionary<Guid, decimal>();
        foreach (var u in unbilledVisitsByPatient)
            unbilledNetByPatient[u.PatientId] = u.Total;
        var outstandingPatientIds = spendByPatient
            .Where(kv =>
            {
                var paid = paymentsByPatient.FirstOrDefault(p => p.PatientId == kv.Key)?.Total ?? 0m;
                var unbilled = unbilledNetByPatient.TryGetValue(kv.Key, out var u) ? u : 0m;
                return Math.Max(0m, kv.Value + unbilled - paid) > 0m;
            })
            .Select(kv => kv.Key)
            .ToHashSet();
        // Also include patients who have ONLY unbilled visits (no contract/invoice) —
        // they wouldn't be in spendByPatient but still owe money.
        var onlyUnbilledPatientIds = unbilledNetByPatient
            .Where(kv =>
            {
                if (spendByPatient.ContainsKey(kv.Key)) return false;
                var paid = paymentsByPatient.FirstOrDefault(p => p.PatientId == kv.Key)?.Total ?? 0m;
                return Math.Max(0m, kv.Value - paid) > 0m;
            })
            .Select(kv => kv.Key);
        outstandingPatientIds = [..outstandingPatientIds, ..onlyUnbilledPatientIds];
        var outstandingCount = outstandingPatientIds.Count;

        // Patients with no visit in last 90 days (must have at least one visit ever
        // — brand-new patients without any visit history are excluded to avoid
        // listing walk-ins who simply haven't been seen yet).
        var recentVisitPatientIds = await db.Visits
            .Where(v => v.IsActive && v.VisitDate >= ninetyDaysAgo)
            .Select(v => v.PatientId)
            .Distinct()
            .ToListAsync();
        var anyVisitPatientIds = await db.Visits
            .Where(v => v.IsActive)
            .Select(v => v.PatientId)
            .Distinct()
            .ToListAsync();
        var noRecentVisitCount = anyVisitPatientIds.Except(recentVisitPatientIds).Count();

        // Patients with at least one lab order in "Ready" status (Ready = 4).
        var labReadyCount = await db.LabOrders
            .Where(l => l.IsActive && l.Status == "Ready")
            .Select(l => l.PatientId)
            .Distinct()
            .CountAsync();

        var builtIn = new List<PatientSegmentDto>
        {
            new()
            {
                Id = Guid.Empty,
                Key = PatientSegmentBuiltInKeys.OrthoOverdue,
                Name = "مرضى تقويم متأخرون",
                Description = "حالات التقويم النشطة التي تأخر موعد الزيارة القادمة لها",
                Color = "#dc2626",
                IsDynamic = true,
                IsBuiltIn = true,
                MemberCount = orthoOverdueCount,
                CreatedAt = DateTime.UtcNow,
            },
            new()
            {
                Id = Guid.Empty,
                Key = PatientSegmentBuiltInKeys.OutstandingBalance,
                Name = "مرضى عليهم مبالغ",
                Description = "المرضى الذين لديهم رصيد مستحق أكبر من صفر",
                Color = "#f5922e",
                IsDynamic = true,
                IsBuiltIn = true,
                MemberCount = outstandingCount,
                CreatedAt = DateTime.UtcNow,
            },
            new()
            {
                Id = Guid.Empty,
                Key = PatientSegmentBuiltInKeys.NoRecentVisit,
                Name = "مرضى لم يحضروا",
                Description = "المرضى الذين لم تكن لهم زيارة خلال آخر 90 يوماً",
                Color = "#6366f1",
                IsDynamic = true,
                IsBuiltIn = true,
                MemberCount = noRecentVisitCount,
                CreatedAt = DateTime.UtcNow,
            },
            new()
            {
                Id = Guid.Empty,
                Key = PatientSegmentBuiltInKeys.LabReady,
                Name = "مرضى المختبر الجاهز",
                Description = "المرضى الذين لديهم طلبات مختبر جاهزة للتسليم",
                Color = "#16a34a",
                IsDynamic = true,
                IsBuiltIn = true,
                MemberCount = labReadyCount,
                CreatedAt = DateTime.UtcNow,
            },
        };

        // ── Custom (stored) segments ────────────────────────────────────────
        var custom = await db.PatientSegments
            .Select(s => new PatientSegmentDto
            {
                Id = s.Id,
                Key = s.Id.ToString(),
                Name = s.Name,
                Description = s.Description,
                Color = s.Color,
                IsDynamic = s.IsDynamic,
                IsBuiltIn = false,
                MemberCount = s.Members.Count(m => m.IsActive),
                CreatedAt = s.CreatedAt,
            })
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return Ok(new { builtIn, custom });
    }

    /// <summary>Returns the patient list for a segment (built-in by Key, custom by Id).</summary>
    [HttpGet("{key}/members")]
    public async Task<IActionResult> GetMembers(string key)
    {
        // ── Built-in dynamic segments (computed) ────────────────────────────
        if (key.StartsWith("builtin:", StringComparison.Ordinal))
        {
            var members = await GetBuiltInMembersAsync(key);
            return Ok(members);
        }

        // ── Custom segment (stored) ─────────────────────────────────────────
        if (!Guid.TryParse(key, out var segmentId))
            return BadRequest(new { message = "مفتاح المجموعة غير صالح" });

        var segmentExists = await db.PatientSegments.AnyAsync(s => s.Id == segmentId);
        if (!segmentExists)
            return NotFound(new { message = "المجموعة غير موجودة" });

        var list = await db.PatientSegmentMembers
            .Where(m => m.SegmentId == segmentId && m.IsActive)
            .OrderByDescending(m => m.AddedAt)
            .Select(m => new
            {
                m.Id,
                m.PatientId,
                PatientNumber = m.Patient != null ? m.Patient.PatientNumber : "",
                FullName = m.Patient != null
                    ? (m.Patient.FirstName + " " + (m.Patient.MiddleName ?? "") + " " + m.Patient.LastName).Trim()
                    : "",
                Phone = m.Patient != null ? m.Patient.Phone : null,
                m.AddedAt,
                Reason = (string?)null,
            })
            .ToListAsync();

        return Ok(list);
    }

    private async Task<List<object>> GetBuiltInMembersAsync(string key)
    {
        var today = ClinicTimeProvider.ClinicToday();
        var ninetyDaysAgo = today.AddDays(-90);

        if (key == PatientSegmentBuiltInKeys.OrthoOverdue)
        {
            // Active OrthoCase patients whose latest OrthoVisit's NextAppointmentDate < today.
            var orthoCases = await db.OrthoCases
                .Include(oc => oc.Patient)
                .Where(oc => oc.IsActive && oc.Status == OrthoCaseStatus.Active)
                .ToListAsync();
            var result = new List<object>();
            foreach (var oc in orthoCases)
            {
                var latestVisit = oc.Visits?
                    .Where(v => v.IsActive)
                    .OrderByDescending(v => v.VisitDate)
                    .FirstOrDefault();
                if (latestVisit?.NextAppointmentDate is { } next && next < today)
                {
                    result.Add(new
                    {
                        Id = Guid.Empty,
                        PatientId = oc.PatientId,
                        PatientNumber = oc.Patient?.PatientNumber ?? "",
                        FullName = oc.Patient != null
                            ? $"{oc.Patient.FirstName} {oc.Patient.MiddleName ?? ""} {oc.Patient.LastName}".Trim()
                            : "",
                        Phone = oc.Patient?.Phone,
                        AddedAt = latestVisit.VisitDate.ToDateTime(TimeOnly.MinValue),
                        Reason = $"موعد الزيارة القادمة: {next:yyyy-MM-dd}",
                    });
                }
            }
            return result;
        }

        if (key == PatientSegmentBuiltInKeys.OutstandingBalance)
        {
            // Server-side aggregations + in-memory balance calculation per patient.
            var contractSpend = await db.Contracts
                .Where(c => c.IsActive && c.Status != ContractStatus.Cancelled)
                .GroupBy(c => c.PatientId)
                .Select(g => new { PatientId = g.Key, Total = g.Sum(c => c.TotalAmount - c.DiscountAmount) })
                .ToListAsync();
            var invoiceSpend = await db.Invoices
                .Where(i => i.IsActive && i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Draft)
                .GroupBy(i => i.PatientId)
                .Select(g => new { PatientId = g.Key, Total = g.Sum(i => i.TotalAmount) })
                .ToListAsync();
            var payments = await db.Payments
                .Where(p => p.IsActive)
                .GroupBy(p => p.PatientId)
                .Select(g => new { PatientId = g.Key, Total = g.Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount) })
                .ToListAsync();
            // QA-596: unbilled visits (sessions with AmountDueReference, no linked invoice)
            var billedVisitIds = await db.Invoices
                .Where(i => i.VisitId.HasValue && i.IsActive)
                .Select(i => i.VisitId!.Value)
                .ToListAsync();
            var billedVisitSet = billedVisitIds.ToHashSet();
            var unbilledVisitRows = await db.Visits
                .Where(v => v.IsActive && v.AmountDueReference.HasValue && v.AmountDueReference > 0)
                .Select(v => new { v.PatientId, v.Id, Amount = v.AmountDueReference ?? 0m })
                .ToListAsync();
            var unbilledByPatient = unbilledVisitRows
                .Where(v => !billedVisitSet.Contains(v.Id))
                .GroupBy(v => v.PatientId)
                .ToDictionary(g => g.Key, g => g.Sum(v => v.Amount));

            var patientIds = contractSpend.Select(c => c.PatientId)
                .Concat(invoiceSpend.Select(i => i.PatientId))
                .Concat(unbilledByPatient.Keys)
                .Distinct()
                .ToList();
            var patients = await db.Patients
                .Where(p => patientIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            var result = new List<object>();
            foreach (var pid in patientIds)
            {
                var billed = (contractSpend.FirstOrDefault(c => c.PatientId == pid)?.Total ?? 0m)
                           + (invoiceSpend.FirstOrDefault(i => i.PatientId == pid)?.Total ?? 0m);
                var unbilled = unbilledByPatient.TryGetValue(pid, out var u) ? u : 0m;
                var paid = payments.FirstOrDefault(p => p.PatientId == pid)?.Total ?? 0m;
                var outstanding = Math.Max(0m, billed + unbilled - paid);
                if (outstanding <= 0m) continue;
                patients.TryGetValue(pid, out var p);
                result.Add(new
                {
                    Id = Guid.Empty,
                    PatientId = pid,
                    PatientNumber = p?.PatientNumber ?? "",
                    FullName = p != null
                        ? $"{p.FirstName} {p.MiddleName ?? ""} {p.LastName}".Trim()
                        : "",
                    Phone = p?.Phone,
                    AddedAt = DateTime.UtcNow,
                    Reason = $"الرصيد المستحق: {outstanding:0.##}",
                });
            }
            return result;
        }

        if (key == PatientSegmentBuiltInKeys.NoRecentVisit)
        {
            var recentVisitPatientIds = await db.Visits
                .Where(v => v.IsActive && v.VisitDate >= ninetyDaysAgo)
                .Select(v => v.PatientId)
                .Distinct()
                .ToListAsync();
            var lastVisitByPatient = await db.Visits
                .Where(v => v.IsActive)
                .GroupBy(v => v.PatientId)
                .Select(g => new { PatientId = g.Key, LastVisit = g.Max(v => v.VisitDate) })
                .ToListAsync();
            var noRecent = lastVisitByPatient.Where(x => !recentVisitPatientIds.Contains(x.PatientId)).ToList();
            var patientIds = noRecent.Select(x => x.PatientId).ToList();
            var patients = await db.Patients
                .Where(p => patientIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            return noRecent.Select(x =>
            {
                patients.TryGetValue(x.PatientId, out var p);
                return (object)new
                {
                    Id = Guid.Empty,
                    PatientId = x.PatientId,
                    PatientNumber = p?.PatientNumber ?? "",
                    FullName = p != null
                        ? $"{p.FirstName} {p.MiddleName ?? ""} {p.LastName}".Trim()
                        : "",
                    Phone = p?.Phone,
                    AddedAt = x.LastVisit.ToDateTime(TimeOnly.MinValue),
                    Reason = $"آخر زيارة: {x.LastVisit:yyyy-MM-dd}",
                };
            }).ToList();
        }

        if (key == PatientSegmentBuiltInKeys.LabReady)
        {
            var labReadyPatientIds = await db.LabOrders
                .Where(l => l.IsActive && l.Status == "Ready")
                .Select(l => l.PatientId)
                .Distinct()
                .ToListAsync();
            var patients = await db.Patients
                .Where(p => labReadyPatientIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);
            var labOrders = await db.LabOrders
                .Where(l => l.IsActive && l.Status == "Ready")
                .ToListAsync();

            return labReadyPatientIds.Select(pid =>
            {
                patients.TryGetValue(pid, out var p);
                var lab = labOrders.FirstOrDefault(l => l.PatientId == pid);
                return (object)new
                {
                    Id = Guid.Empty,
                    PatientId = pid,
                    PatientNumber = p?.PatientNumber ?? "",
                    FullName = p != null
                        ? $"{p.FirstName} {p.MiddleName ?? ""} {p.LastName}".Trim()
                        : "",
                    Phone = p?.Phone,
                    AddedAt = lab?.ReceivedDate?.ToDateTime(TimeOnly.MinValue) ?? DateTime.UtcNow,
                    Reason = lab?.ApplianceType ?? "تركيب جاهز للتسليم",
                };
            }).ToList();
        }

        return new List<object>();
    }

    /// <summary>Creates a custom (non-dynamic) segment.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePatientSegmentRequest req)
    {
        var segment = new PatientSegment
        {
            Name = req.Name,
            Description = req.Description,
            Color = req.Color,
            IsDynamic = false,
            QueryJson = null,
        };
        db.PatientSegments.Add(segment);
        await db.SaveChangesAsync();
        return CreatedAtAction(null, new { id = segment.Id }, new { segment.Id, segment.Name });
    }

    /// <summary>Adds a patient to a custom segment. Built-in segments are read-only.</summary>
    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddSegmentMemberRequest req)
    {
        var segment = await db.PatientSegments.FindAsync(id);
        if (segment is null) return NotFound(new { message = "المجموعة غير موجودة" });
        if (segment.IsDynamic)
            return BadRequest(new { message = "لا يمكن إضافة أعضاء يدوياً إلى مجموعة ديناميكية" });

        var patientExists = await db.Patients.AnyAsync(p => p.Id == req.PatientId);
        if (!patientExists) return NotFound(new { message = "المريض غير موجود" });

        var already = await db.PatientSegmentMembers
            .AnyAsync(m => m.SegmentId == id && m.PatientId == req.PatientId && m.IsActive);
        if (already)
            return BadRequest(new { message = "المريض موجود مسبقاً في هذه المجموعة" });

        var member = new PatientSegmentMember
        {
            SegmentId = id,
            PatientId = req.PatientId,
            AddedAt = DateTime.UtcNow,
        };
        db.PatientSegmentMembers.Add(member);
        await db.SaveChangesAsync();
        return Ok(new { member.Id });
    }

    /// <summary>Removes a patient from a custom segment.</summary>
    [HttpDelete("{id:guid}/members/{patientId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid patientId)
    {
        var member = await db.PatientSegmentMembers
            .FirstOrDefaultAsync(m => m.SegmentId == id && m.PatientId == patientId && m.IsActive);
        if (member is null) return NotFound(new { message = "العضو غير موجود في المجموعة" });

        // Soft-delete (BaseEntity filter will exclude it from subsequent reads).
        member.IsActive = false;
        member.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف المريض من المجموعة" });
    }

    /// <summary>Soft-deletes a custom segment (and its members via cascade).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var segment = await db.PatientSegments.FindAsync(id);
        if (segment is null) return NotFound(new { message = "المجموعة غير موجودة" });

        segment.IsActive = false;
        segment.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف المجموعة" });
    }
}
