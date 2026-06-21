using System.Globalization;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AqlanDentalPro.API.Services;

/// <summary>
/// Unified one-page Arabic orthodontic case summary PDF (QuestPDF).
/// Aggregates ONLY data that already exists for the case — patient info,
/// clinical exam, latest ceph, latest model/cast analysis, diagnosis &amp;
/// treatment-plan status, visits, retention, and the finance figures shown in
/// the case overview. No new computation; mirrors <see cref="CephReportPdfGenerator"/>
/// for layout, font and clinic identity (Settings, no hardcoding).
/// </summary>
public class OrthoCaseSummaryReportPdfGenerator(AppDbContext db)
{
    private const string FontName = AqlanDentalPro.Infrastructure.Services.PdfService.ArabicFontName;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public async Task<byte[]> GenerateAsync(Guid orthoCaseId)
    {
        var orthoCase = await db.OrthoCases
            .AsNoTracking()
            .Include(c => c.Patient)
            .Include(c => c.ClinicalExam)
            .Include(c => c.Diagnosis)
            .Include(c => c.TreatmentPlans)
            .Include(c => c.Visits)
            .Include(c => c.RetentionRecord)
            .Include(c => c.CephAnalyses)
            .Include(c => c.ModelAnalyses)
            .FirstOrDefaultAsync(c => c.Id == orthoCaseId && c.IsActive);

        if (orthoCase is null)
            throw new ArgumentException("Ortho case not found.", nameof(orthoCaseId));

        var identity = await CephReportPdfGenerator.ResolveClinicIdentityAsync(db);
        var finance = await ResolveFinanceAsync(orthoCase);

        // CLIN-12: CPU-bound QuestPDF generation is offloaded to the thread pool so the
        // request thread is released. The AsNoTracking entity graph is fully materialized
        // by this point, so reading it from another thread is safe.
        return await Task.Run(() => Generate(orthoCase, identity, finance));
    }

    private sealed record FinanceSummary(decimal? Total, decimal? Paid, decimal? Remaining);

    // Replicates the contract-selection rule of OrthoCasesController.GetOverview:
    // prefer the contract linked to this case; else the single unlinked ortho contract.
    private async Task<FinanceSummary> ResolveFinanceAsync(OrthoCase orthoCase)
    {
        var contract = await db.Contracts
            .AsNoTracking()
            .Include(c => c.Payments)
            .Where(c => c.PatientId == orthoCase.PatientId && c.RelatedCaseId == orthoCase.Id)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (contract is null)
        {
            var unlinked = await db.Contracts
                .AsNoTracking()
                .Include(c => c.Payments)
                .Where(c => c.PatientId == orthoCase.PatientId && c.RelatedCaseId == null &&
                    (c.Specialty == "orthodontics" || c.Specialty == "ortho"))
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
            contract = unlinked.Count == 1 ? unlinked[0] : null;
        }

        if (contract is null) return new FinanceSummary(null, null, null);

        var total = contract.TotalAmount - contract.DiscountAmount;
        var paid = contract.Payments.Where(p => p.IsActive).Sum(p => p.Amount);
        return new FinanceSummary(total, paid, Math.Max(0, total - paid));
    }

    private static byte[] Generate(OrthoCase orthoCase, CephReportClinicIdentity identity, FinanceSummary finance)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        AqlanDentalPro.Infrastructure.Services.PdfService.EnsureFontsRegistered();

        var doc = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(FontName));

                page.Header().Element(c => ComposeHeader(c, orthoCase, identity));
                page.Content().Element(c => ComposeContent(c, orthoCase, identity, finance));
                page.Footer().Element(c => ComposeFooter(c, identity));
            });
        });

        return doc.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, OrthoCase orthoCase, CephReportClinicIdentity identity)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(identity.ClinicName).Bold().FontSize(15).FontFamily(FontName);
                    col.Item().Text(identity.LeadDoctor).Bold().FontSize(11).FontFamily(FontName);
                    col.Item().Text(identity.LeadDoctorTitle).FontSize(9).FontFamily(FontName);
                    col.Item().Text(identity.LeadDoctorCredentials)
                        .FontSize(8).FontColor(Colors.Grey.Darken1).FontFamily(FontName);
                });

                row.RelativeItem().AlignLeft().Column(col =>
                {
                    col.Item().AlignLeft().Text("ملخّص الحالة التقويمية").Bold().FontSize(14).FontFamily(FontName);
                    col.Item().AlignLeft().Text($"رقم الحالة: {orthoCase.CaseNumber}").FontSize(9).FontFamily(FontName);
                    col.Item().AlignLeft().Text($"تاريخ التقرير: {CephReportPdfGenerator.FormatArabicDate(DateOnly.FromDateTime(DateTime.UtcNow))}")
                        .FontSize(9).FontFamily(FontName);
                });
            });
            column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposeContent(
        IContainer container, OrthoCase orthoCase, CephReportClinicIdentity identity, FinanceSummary finance)
    {
        container.Column(column =>
        {
            column.Spacing(8);

            // 1. Patient + case info
            var p = orthoCase.Patient;
            Section(column, "بيانات المريض والحالة");
            Pairs(column,
            [
                ("الاسم", PatientName(orthoCase)),
                ("رقم الملف", string.IsNullOrWhiteSpace(p?.PatientNumber) ? "—" : p!.PatientNumber),
                ("العمر", AgeText(p?.DateOfBirth)),
                ("الجنس", GenderAr(p?.Gender)),
                ("الهاتف", string.IsNullOrWhiteSpace(p?.Phone) ? "—" : p!.Phone!),
                ("حالة العلاج", StatusAr(orthoCase.Status)),
                ("نوع الجهاز", string.IsNullOrWhiteSpace(orthoCase.ApplianceType) ? "—" : orthoCase.ApplianceType!),
                ("المرحلة الحالية", string.IsNullOrWhiteSpace(orthoCase.CurrentStage) ? "—" : orthoCase.CurrentStage!),
                ("تاريخ البدء", orthoCase.StartDate.HasValue ? CephReportPdfGenerator.FormatArabicDate(orthoCase.StartDate.Value) : "—"),
                ("نسبة التقدّم", $"{orthoCase.StagePercentage}%"),
            ]);

            // 2. Clinical exam summary
            Section(column, "ملخّص الفحص السريري");
            var ex = orthoCase.ClinicalExam;
            if (ex is null) NoData(column, "لا يوجد فحص سريري مسجّل.");
            else Pairs(column,
            [
                ("علاقة الأرحاء", FirstNonEmpty(ex.MolarRelation, ex.MolarRelationRight, ex.MolarRelationLeft)),
                ("علاقة الأنياب", FirstNonEmpty(ex.CanineRelation, ex.CanineRelationRight, ex.CanineRelationLeft)),
                ("البزوغ (Overjet)", Mm(ex.Overjet)),
                ("التطابق (Overbite)", Mm(ex.Overbite)),
                ("ازدحام علوي", FirstNonEmpty(ex.UpperCrowding, Mm(ex.UpperCrowdingMm))),
                ("ازدحام سفلي", FirstNonEmpty(ex.LowerCrowding, Mm(ex.LowerCrowdingMm))),
                ("عضّة معكوسة", ex.Crossbite ? "نعم" : "لا"),
                ("عضّة مفتوحة", ex.OpenBite ? "نعم" : "لا"),
            ]);

            // 3. Latest ceph summary
            Section(column, "آخر تحليل سيفالومتري");
            var ceph = orthoCase.CephAnalyses
                .OrderByDescending(a => a.AnalysisDate).ThenByDescending(a => a.UpdatedAt).FirstOrDefault();
            if (ceph is null) NoData(column, "لا يوجد تحليل سيفالومتري.");
            else Pairs(column,
            [
                ("نوع التحليل", CephTypeAr(ceph.AnalysisType)),
                ("تاريخ التحليل", CephReportPdfGenerator.FormatArabicDate(ceph.AnalysisDate)),
                ("إجمالي التحاليل", orthoCase.CephAnalyses.Count.ToString(Inv)),
            ]);

            // 4. Latest model / cast analysis summary
            Section(column, "آخر تحليل نماذج (Cast)");
            var model = orthoCase.ModelAnalyses
                .OrderByDescending(m => m.AnalysisDate).ThenByDescending(m => m.CreatedAt).FirstOrDefault();
            if (model is null) NoData(column, "لا يوجد تحليل نماذج.");
            else Pairs(column,
            [
                ("بولتون الكلي", Pct(model.BoltonOverall)),
                ("بولتون الأمامي", Pct(model.BoltonAnterior)),
                ("ALD علوي", Mm(model.UpperAld)),
                ("ALD سفلي", Mm(model.LowerAld)),
                ("مؤشر بونت", model.PontIndex.HasValue ? Mm(model.PontIndex) : "—"),
                ("الاعتماد", model.ApprovedAt is not null ? "معتمد" : "غير معتمد"),
            ]);

            // 5. Diagnosis status
            Section(column, "التشخيص");
            var dx = orthoCase.Diagnosis;
            if (dx is null) NoData(column, "لا يوجد تشخيص مسجّل.");
            else
            {
                Pairs(column,
                [
                    ("الحالة", dx.ApprovedAt is not null ? "معتمد" : "مسودّة"),
                    ("التصنيف الهيكلي", string.IsNullOrWhiteSpace(dx.SkeletalClassification) ? "—" : dx.SkeletalClassification!),
                    ("التصنيف السنّي", string.IsNullOrWhiteSpace(dx.DentalClassification) ? "—" : dx.DentalClassification!),
                ]);
                if (!string.IsNullOrWhiteSpace(dx.Summary))
                    column.Item().Text(dx.Summary).FontSize(9).FontFamily(FontName);
            }

            // 6. Treatment plan status — prefer the approved plan; fall back to the
            // newest draft only when no approved plan exists (so a later draft
            // alternative never hides an existing approved plan).
            Section(column, "خطة العلاج");
            var plan = orthoCase.TreatmentPlans.Where(t => t.IsApproved)
                    .OrderByDescending(t => t.ApprovedAt ?? t.CreatedAt).FirstOrDefault()
                ?? orthoCase.TreatmentPlans.OrderByDescending(t => t.CreatedAt).FirstOrDefault();
            if (plan is null) NoData(column, "لا توجد خطة علاج.");
            else
            {
                Pairs(column,
                [
                    ("الخطة", $"{plan.PlanLabel} (v{plan.PlanVersion})"),
                    ("الحالة", plan.IsApproved ? "معتمدة" : "مسودّة"),
                    ("نوع الجهاز", string.IsNullOrWhiteSpace(plan.ApplianceType) ? "—" : plan.ApplianceType!),
                    ("خطة القلع", string.IsNullOrWhiteSpace(plan.ExtractionPlan) ? "—" : plan.ExtractionPlan!),
                    ("المدة المتوقعة", plan.ExpectedDurationMonths.HasValue ? $"{plan.ExpectedDurationMonths} شهر" : "—"),
                ]);
                if (!string.IsNullOrWhiteSpace(plan.TreatmentGoals))
                    column.Item().Text($"الأهداف: {plan.TreatmentGoals}").FontSize(9).FontFamily(FontName);
            }

            // 7. Visits
            Section(column, "الزيارات");
            var latestVisit = orthoCase.Visits.OrderByDescending(v => v.VisitDate).FirstOrDefault();
            Pairs(column,
            [
                ("إجمالي الزيارات", orthoCase.Visits.Count.ToString(Inv)),
                ("آخر زيارة", latestVisit is not null ? CephReportPdfGenerator.FormatArabicDate(latestVisit.VisitDate) : "—"),
                ("الموعد القادم", latestVisit?.NextAppointmentDate is not null
                    ? CephReportPdfGenerator.FormatArabicDate(latestVisit.NextAppointmentDate.Value) : "—"),
            ]);

            // 8. Retention
            Section(column, "الاحتفاظ (Retention)");
            var ret = orthoCase.RetentionRecord;
            if (ret is null) NoData(column, "لا يوجد سجل احتفاظ.");
            else Pairs(column,
            [
                ("الحالة", RetentionStatusAr(ret.Status)),
                ("تاريخ فك التقويم", ret.DebondDate.HasValue ? CephReportPdfGenerator.FormatArabicDate(ret.DebondDate.Value) : "—"),
                ("المثبّت العلوي", string.IsNullOrWhiteSpace(ret.UpperRetainer) ? "—" : ret.UpperRetainer!),
                ("المثبّت السفلي", string.IsNullOrWhiteSpace(ret.LowerRetainer) ? "—" : ret.LowerRetainer!),
            ]);

            // 9. Finance summary
            Section(column, "الملخّص المالي");
            if (finance.Total is null) NoData(column, "لا يوجد عقد مالي مرتبط بالحالة.");
            else Pairs(column,
            [
                ("إجمالي العقد", Money(finance.Total)),
                ("المدفوع", Money(finance.Paid)),
                ("المتبقّي", Money(finance.Remaining)),
            ]);

            // Signature
            column.Item().PaddingTop(12).Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(230).Column(col =>
                {
                    col.Item().PaddingBottom(22).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                    col.Item().AlignCenter().Text($"{identity.LeadDoctor} — {identity.LeadDoctorTitle}")
                        .Bold().FontSize(10).FontFamily(FontName);
                    col.Item().AlignCenter().Text(identity.LeadDoctorCredentials)
                        .FontSize(8).FontColor(Colors.Grey.Darken1).FontFamily(FontName);
                });
            });
        });
    }

    private static void ComposeFooter(IContainer container, CephReportClinicIdentity identity)
    {
        container.Column(column =>
        {
            var contact = string.Join(" — ",
                new[] { identity.Phones, identity.Location }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (contact.Length > 0)
                column.Item().AlignCenter().Text(contact)
                    .FontSize(8).FontColor(Colors.Grey.Darken1).FontFamily(FontName);

            column.Item().AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Medium).FontFamily(FontName));
                text.Span("صفحة ");
                text.CurrentPageNumber();
                text.Span(" من ");
                text.TotalPages();
            });
        });
    }

    // ── Layout helpers ──
    private static void Section(ColumnDescriptor column, string title) =>
        column.Item().PaddingTop(2).Background(Colors.Grey.Lighten4).Padding(4)
            .Text(title).Bold().FontSize(11).FontFamily(FontName);

    private static void Pairs(ColumnDescriptor column, (string Label, string Value)[] pairs) =>
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1); columns.RelativeColumn(1.4f);
                columns.RelativeColumn(1); columns.RelativeColumn(1.4f);
            });
            foreach (var (label, value) in pairs)
            {
                table.Cell().Element(LabelCell).Text(label).FontFamily(FontName);
                table.Cell().Element(ValueCell).Text(value).FontFamily(FontName);
            }
        });

    private static IContainer LabelCell(IContainer c) =>
        c.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(2).PaddingHorizontal(4)
            .DefaultTextStyle(s => s.FontColor(Colors.Grey.Darken1).FontSize(9));

    private static IContainer ValueCell(IContainer c) =>
        c.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(2).PaddingHorizontal(4)
            .DefaultTextStyle(s => s.FontSize(9.5f));

    private static void NoData(ColumnDescriptor column, string message) =>
        column.Item().Text(message).FontSize(9).FontColor(Colors.Grey.Darken1).FontFamily(FontName);

    // ── Formatting ──
    private static string Mm(decimal? v) => v.HasValue ? string.Create(Inv, $"{v.Value:0.0} مم") : "—";
    private static string Pct(decimal? v) => v.HasValue ? string.Create(Inv, $"{v.Value:0.0}%") : "—";
    private static string Money(decimal? v) => v.HasValue ? string.Create(Inv, $"{v.Value:#,0} ريال") : "—";

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v) && v != "—") ?? "—";

    private static string PatientName(OrthoCase orthoCase)
    {
        var p = orthoCase.Patient;
        if (p is null) return "غير محدد";
        var name = $"{p.FirstName} {p.MiddleName} {p.LastName}".Replace("  ", " ").Trim();
        return name.Length > 0 ? name : "غير محدد";
    }

    private static string AgeText(DateOnly? dob)
    {
        if (!dob.HasValue) return "—";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - dob.Value.Year;
        if (dob.Value > today.AddYears(-age)) age--;
        return age >= 0 ? $"{age} سنة" : "—";
    }

    private static string GenderAr(Gender? g) => g switch
    {
        Gender.Male => "ذكر",
        Gender.Female => "أنثى",
        _ => "—",
    };

    private static string StatusAr(OrthoCaseStatus status) => status switch
    {
        OrthoCaseStatus.Active => "نشطة",
        OrthoCaseStatus.Completed => "مكتملة",
        OrthoCaseStatus.OnHold => "معلّقة",
        OrthoCaseStatus.Cancelled => "ملغاة",
        _ => status.ToString(),
    };

    private static string RetentionStatusAr(string? status) => status switch
    {
        "active" => "نشط",
        "completed" => "مكتمل",
        "discontinued" => "متوقف",
        _ => status ?? "—",
    };

    private static string CephTypeAr(string? type) => type?.ToLowerInvariant() switch
    {
        "steiner" => "ستاينر",
        "tweed" => "تويد",
        "mcnamara" => "ماكنامارا",
        "ricketts" => "ريكتس",
        "downs" => "داونز",
        "jarabak" => "جاراباك",
        "wits" => "وتس",
        "full" => "شامل",
        _ => string.IsNullOrWhiteSpace(type) ? "—" : type!,
    };
}
