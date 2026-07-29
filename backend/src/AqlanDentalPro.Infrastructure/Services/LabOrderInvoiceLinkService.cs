using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// CORE-FIN-LAB: why a lab order could (or could not) be attached to an invoice line.
/// Exposed so the caller can log the reason — an unresolved link is a normal outcome,
/// not an error, and it must be visible instead of silently swallowed.
/// </summary>
public enum LabOrderInvoiceLinkStatus
{
    /// <summary>Exactly one candidate line was found — <c>LineItem</c> carries it.</summary>
    Resolved,

    /// <summary>The lab order carries no <c>VisitId</c>, so there is no signal to resolve on.</summary>
    NoVisit,

    /// <summary>No active, unclaimed invoice line belongs to the order's visit.</summary>
    NoCandidate,

    /// <summary>More than one candidate survived — deliberately left unlinked.</summary>
    Ambiguous,

    /// <summary>
    /// A single candidate was found but it belongs to a different patient (or its
    /// invoice row is unreadable). Data-integrity backstop — never link across patients.
    /// </summary>
    PatientMismatch
}

/// <summary>
/// CORE-FIN-LAB: outcome of resolving the invoice line a lab order belongs to.
/// <para>
/// <c>LineItem</c> is non-null ONLY when <c>Status</c> is
/// <see cref="LabOrderInvoiceLinkStatus.Resolved"/>. <c>CandidateCount</c> is the number
/// of candidates that survived filtering, so an ambiguous result can be logged with the
/// count that made it ambiguous.
/// </para>
/// </summary>
public sealed record LabOrderInvoiceLinkResult(
    LabOrderInvoiceLinkStatus Status,
    InvoiceLineItem? LineItem,
    int CandidateCount)
{
    /// <summary>True only when a single unambiguous line was identified.</summary>
    public bool IsResolved => Status == LabOrderInvoiceLinkStatus.Resolved && LineItem is not null;

    internal static LabOrderInvoiceLinkResult Resolved(InvoiceLineItem lineItem) =>
        new(LabOrderInvoiceLinkStatus.Resolved, lineItem, 1);

    internal static LabOrderInvoiceLinkResult Unresolved(LabOrderInvoiceLinkStatus status, int candidateCount = 0) =>
        new(status, null, candidateCount);
}

/// <summary>
/// CORE-FIN-LAB: resolves which invoice line a lab order's cost belongs to, so the
/// doctor's commission is computed after deducting the REAL lab cost instead of
/// <c>ClinicService.DefaultLabCost</c> (an estimate).
/// <para>
/// <b>Source of truth is <c>InvoiceLineItem.LabOrderId</c>.</b> It is the only side of
/// this relationship configured in EF (HasOne/WithMany/HasForeignKey + index +
/// OnDelete SetNull) and the only side <c>CommissionService</c> reads.
/// <c>LabOrder.InvoiceLineItemId</c> is an abandoned bare column with no EF
/// configuration and zero readers — this service never touches it.
/// </para>
/// <para>
/// <b>Why "unambiguous or nothing".</b> The only signal available today is
/// <c>LabOrder.VisitId</c> ↔ <c>InvoiceLineItem.RelatedVisitId</c>; there is no
/// "this service requires lab work" flag, and one visit routinely carries several
/// invoice lines. Picking "the first" or "the nearest" line would put one service's
/// lab cost against another — silently corrupting a DIFFERENT doctor's commission,
/// which is exactly the damage this work exists to prevent. So when certainty is not
/// available the order is left UNLINKED and surfaces in an admin fix-up list, where a
/// human decides. An unlinked order simply keeps today's behavior (the estimated
/// default lab cost); a wrongly linked one corrupts money.
/// </para>
/// <para>
/// This service performs no writes and never calls SaveChanges — the caller owns the
/// transaction and assigns <c>LineItem.LabOrderId</c> itself, so the order and the link
/// commit together.
/// </para>
/// </summary>
public sealed class LabOrderInvoiceLinkService(AppDbContext db)
{
    /// <summary>
    /// Resolves the invoice line <paramref name="order"/> belongs to, or reports why it
    /// could not be determined with certainty.
    /// </summary>
    /// <remarks>
    /// The resolution rule, in order:
    /// <list type="number">
    /// <item>No <c>VisitId</c> on the order → <see cref="LabOrderInvoiceLinkStatus.NoVisit"/>.
    /// The visit is the only join available; without it nothing can be inferred.</item>
    /// <item>Load the ACTIVE invoice lines whose <c>RelatedVisitId</c> equals the order's
    /// <c>VisitId</c>.</item>
    /// <item>Drop any line already linked to a DIFFERENT lab order (<c>LabOrderId</c> set
    /// and not this order). A line already carries another order's cost — stealing it
    /// would both misprice this order and strip the other one.</item>
    /// <item>Among the survivors, PREFER lines whose <c>ClinicService.DefaultLabCost &gt; 0</c>
    /// — the only (weak) hint that a service normally involves lab work. The preference is
    /// applied only when at least one such line exists; otherwise the full set is kept, so
    /// a service with no configured default is not silently excluded.</item>
    /// <item>Exactly one survivor → return it. Zero → <c>NoCandidate</c>. Two or more →
    /// <c>Ambiguous</c>. NEVER pick "the first" or "the nearest".</item>
    /// </list>
    /// A final backstop rejects a winner whose invoice belongs to another patient; that
    /// can only remove a link, never add one.
    /// </remarks>
    /// <param name="order">
    /// The lab order to resolve. May be a not-yet-saved entity: only its
    /// <c>Id</c>, <c>VisitId</c> and <c>PatientId</c> are read.
    /// </param>
    public async Task<LabOrderInvoiceLinkResult> ResolveAsync(LabOrder order, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        // (a) No visit → no signal at all. Not an error: plenty of lab orders are
        // created outside a visit (walk-in remakes, ortho appliances ordered ahead).
        if (!order.VisitId.HasValue || order.VisitId.Value == Guid.Empty)
            return LabOrderInvoiceLinkResult.Unresolved(LabOrderInvoiceLinkStatus.NoVisit);

        var visitId = order.VisitId.Value;

        // (b) + (c) Active lines on this visit, excluding lines already claimed by a
        // DIFFERENT lab order. `IsActive` is also enforced by the global soft-delete
        // query filter; it is repeated here so the rule is readable at the call site.
        // `Service` is included (not projected) so the returned lines stay tracked by
        // this same scoped DbContext — the caller assigns LabOrderId on them directly.
        var candidates = await db.InvoiceLineItems
            .Include(line => line.Service)
            .Where(line => line.IsActive
                && line.RelatedVisitId == visitId
                && (line.LabOrderId == null || line.LabOrderId == order.Id))
            .ToListAsync(ct);

        if (candidates.Count == 0)
            return LabOrderInvoiceLinkResult.Unresolved(LabOrderInvoiceLinkStatus.NoCandidate);

        // (d) Prefer lines whose service normally involves lab work. This narrows an
        // otherwise ambiguous visit down to the plausible line — but only when such a
        // line exists, so services with no configured default lab cost are still
        // eligible when they are the only thing on the visit.
        var labLikely = candidates
            .Where(line => line.Service is not null && line.Service.DefaultLabCost > 0m)
            .ToList();

        var shortlist = labLikely.Count > 0 ? labLikely : candidates;

        // (e) Certainty or nothing. Two plausible lines mean we do not know which
        // service the appliance was made for, and a coin flip here is indistinguishable
        // from data corruption once the commission is paid out.
        if (shortlist.Count > 1)
            return LabOrderInvoiceLinkResult.Unresolved(LabOrderInvoiceLinkStatus.Ambiguous, shortlist.Count);

        var winner = shortlist[0];

        // Backstop: a line reached through the visit should always belong to the same
        // patient. Read as a separate scalar so the candidate set above stays exactly
        // the set the rule describes (an Include of the required Invoice navigation
        // could quietly drop candidates whose invoice row is soft-deleted, and a
        // narrower set would mean MORE auto-linking, not less).
        // A missing/soft-deleted invoice resolves to null here and blocks the link.
        var invoicePatientId = await db.Invoices
            .Where(invoice => invoice.Id == winner.InvoiceId)
            .Select(invoice => (Guid?)invoice.PatientId)
            .FirstOrDefaultAsync(ct);

        if (invoicePatientId is null || invoicePatientId.Value != order.PatientId)
            return LabOrderInvoiceLinkResult.Unresolved(LabOrderInvoiceLinkStatus.PatientMismatch, 1);

        return LabOrderInvoiceLinkResult.Resolved(winner);
    }
}
