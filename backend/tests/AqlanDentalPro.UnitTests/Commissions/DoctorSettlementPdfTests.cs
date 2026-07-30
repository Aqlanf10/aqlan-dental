using AqlanDentalPro.API.Services;
using AqlanDentalPro.Application.DTOs.Commission;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using Xunit;

namespace AqlanDentalPro.UnitTests.Commissions;

/// <summary>
/// The settlement statement must actually RENDER, not merely compile. QuestPDF fails at
/// generation time — a bad layout, an unresolvable font, a table whose columns do not add up
/// all throw only when the document is produced. A test that builds the model and stops would
/// pass while the endpoint returned a 500 in the clinic.
/// </summary>
public class DoctorSettlementPdfTests
{
    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"settlement-pdf-{Guid.NewGuid()}")
            .Options);

    private static CommissionAdjustmentDto Adjustment(decimal amount, string status = "Pending") => new(
        Id: Guid.NewGuid(),
        DoctorId: Guid.NewGuid(),
        DoctorName: "د. عقلان",
        InvoiceLineItemId: Guid.NewGuid(),
        InvoiceId: Guid.NewGuid(),
        InvoiceNumber: "INV-2026-001",
        PatientName: "سالم المريض",
        ServiceName: "تركيب تاج زيركون",
        LabOrderId: Guid.NewGuid(),
        LabOrderNumber: "LAB-2026-003",
        PaidCommissionAmount: 32_000m,
        RecalculatedCommissionAmount: 32_000m + amount,
        AdjustmentAmount: amount,
        PreviousLabCost: 20_000m,
        CurrentLabCost: 35_000m,
        Reason: "تغيّرت تكلفة المعمل الفعلية بعد صرف العمولة",
        Status: status,
        SettledByPaymentId: null,
        SettledOn: null,
        CreatedAt: new DateTime(2026, 7, 29, 10, 0, 0, DateTimeKind.Utc));

    private static DoctorSettlementSummaryDto Summary(decimal adjustments, decimal paid) => new(
        DoctorId: Guid.NewGuid(),
        DoctorName: "د. عقلان الكامل",
        EarnedFromLineItems: 32_000m,
        AdjustmentsTotal: adjustments,
        TotalDue: 32_000m + adjustments,
        AlreadyPaid: paid,
        Remaining: 32_000m + adjustments - paid,
        PendingAdjustmentCount: 1);

    private static async Task<byte[]> RenderAsync(
        DoctorSettlementSummaryDto summary, params CommissionAdjustmentDto[] adjustments)
    {
        await using var db = CreateDb();
        var identity = await FinanceClinicIdentity.ResolveAsync(db);

        return new DoctorSettlementPdfGenerator(
            identity,
            new DoctorSettlementStatement(summary, adjustments, new DateOnly(2026, 7, 30)))
            .GeneratePdf();
    }

    private static void ShouldBeARealPdf(byte[] bytes)
    {
        bytes.Should().NotBeNullOrEmpty();
        bytes.Length.Should().BeGreaterThan(1_000, "a statement with content is not a few hundred bytes");
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task AStatementWithCorrections_Renders()
    {
        ShouldBeARealPdf(await RenderAsync(Summary(-6_000m, 32_000m), Adjustment(-6_000m)));
    }

    [Fact]
    public async Task AStatementWithNoCorrections_Renders()
    {
        // The empty branch takes a different path through the layout and has its own way to
        // throw — a table with no rows is a classic QuestPDF failure.
        ShouldBeARealPdf(await RenderAsync(Summary(0m, 0m)));
    }

    [Fact]
    public async Task AStatementWhereTheDoctorOwesTheClinic_Renders()
    {
        // The negative-remaining branch swaps the label and the colour; it must not throw.
        ShouldBeARealPdf(await RenderAsync(Summary(-6_000m, 32_000m), Adjustment(-6_000m)));
    }

    [Fact]
    public async Task AStatementWithCancelledAndPositiveLines_Renders()
    {
        // Cancelled rows take the strikethrough branch, positive ones the "+" prefix.
        ShouldBeARealPdf(await RenderAsync(
            Summary(3_200m, 0m),
            Adjustment(3_200m),
            Adjustment(-1_000m, status: "Cancelled"),
            Adjustment(500m, status: "Settled")));
    }

    [Fact]
    public async Task AStatementWithAMissingLabOrderNumber_StillRenders()
    {
        // LabOrderNumber is nullable — a correction can outlive the order that caused it.
        var orphan = Adjustment(-2_000m) with { LabOrderNumber = null, PatientName = "", ServiceName = "" };
        ShouldBeARealPdf(await RenderAsync(Summary(-2_000m, 0m), orphan));
    }

    [Fact]
    public async Task ManyCorrections_PaginateWithoutThrowing()
    {
        // A doctor with a long history pushes the table past one page; QuestPDF throws on
        // layouts that cannot paginate rather than silently truncating.
        var many = Enumerable.Range(0, 120).Select(i => Adjustment(i % 2 == 0 ? 250m : -250m)).ToArray();
        ShouldBeARealPdf(await RenderAsync(Summary(0m, 0m), many));
    }
}
