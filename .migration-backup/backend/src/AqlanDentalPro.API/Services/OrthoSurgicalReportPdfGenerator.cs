using System.Globalization;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using AqlanDentalPro.Infrastructure.Services;

namespace AqlanDentalPro.API.Services;

/// <summary>
/// Sprint A7 — Arabic PDF reports for the shared Ortho-Surgical planning case (QuestPDF).
/// Aggregates ONLY data that already exists — nothing is computed or duplicated. Follows
/// <see cref="OrthoCaseSummaryReportPdfGenerator"/> for layout, font and clinic identity
/// (Settings, no hardcoding). Two documents:
///   - GenerateDoctorReportAsync: comprehensive clinical report for the two specialists.
///   - GeneratePatientExplanationAsync: simplified Arabic explanation for the patient,
///     with the mandatory "this is a planning simulation, not a final surgical decision"
///     disclaimer per docs/ortho-module/ORTHO_SURGICAL_AI_VISION_EXPANSION.md §9.
/// </summary>
public class OrthoSurgicalReportPdfGenerator(AppDbContext db)
{
    private const string FontName = AqlanDentalPro.Infrastructure.Services.PdfService.ArabicFontName;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private const string SimulationDisclaimer =
        "هذه المحاكاة والخطة تخطيطية تعليمية تقريبية ولا تُعد قرارًا جراحيًا نهائيًا. " +
        "القرار النهائي يعتمد على فحص الطبيب، مراجعة أخصائي جراحة الفم والفكين، الأشعة، القياسات، السجلات، وموافقة المريض.";

    private sealed record ReportData(
        OrthoSurgicalCase Case, OrthoDiagnosis? Diagnosis, CephAnalysis? Ceph,
        List<CephMeasurement> Measurements, RecordsChecklist? Checklist);

    private async Task<ReportData> LoadAsync(Guid orthoSurgicalCaseId)
    {
        var c = await db.OrthoSurgicalCases
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.OrthoCase)
            .Include(x => x.Orthodontist)
            .Include(x => x.Surgeon)
            .Include(x => x.SurgeonReview)
            .Include(x => x.JointPlan)
            .FirstOrDefaultAsync(x => x.Id == orthoSurgicalCaseId && x.IsActive)
            ?? throw new ArgumentException("Ortho-surgical case not found.", nameof(orthoSurgicalCaseId));

        var diagnosis = await db.OrthoDiagnoses.AsNoTracking()
            .FirstOrDefaultAsync(d => d.OrthoCaseId == c.OrthoCaseId);

        var ceph = c.CephAnalysisId.HasValue
            ? await db.CephAnalyses.AsNoTracking().FirstOrDefaultAsync(a => a.Id == c.CephAnalysisId)
            : await db.CephAnalyses.AsNoTracking()
                .Where(a => a.OrthoCaseId == c.OrthoCaseId && a.IsActive)
                .OrderByDescending(a => a.AnalysisDate)
                .FirstOrDefaultAsync();

        var measurements = ceph is null
            ? []
            : await db.CephMeasurements.AsNoTracking()
                .Where(m => m.AnalysisId == ceph.Id)
                .ToListAsync();

        var checklist = await db.RecordsChecklists.AsNoTracking()
            .FirstOrDefaultAsync(r => r.OrthoCaseId == c.OrthoCaseId);

        return new ReportData(c, diagnosis, ceph, measurements, checklist);
    }

    public async Task<byte[]> GenerateDoctorReportAsync(Guid orthoSurgicalCaseId)
    {
        var data = await LoadAsync(orthoSurgicalCaseId);
        var identity = await CephReportPdfGenerator.ResolveClinicIdentityAsync(db);
        return await Task.Run(() => GenerateDoctorReport(data, identity));
    }

    public async Task<byte[]> GeneratePatientExplanationAsync(Guid orthoSurgicalCaseId)
    {
        var data = await LoadAsync(orthoSurgicalCaseId);
        var identity = await CephReportPdfGenerator.ResolveClinicIdentityAsync(db);
        return await Task.Run(() => GeneratePatientExplanation(data, identity));
    }

    // ── Doctor report ──────────────────────────────────────────────────────────────
    private static byte[] GenerateDoctorReport(ReportData data, CephReportClinicIdentity identity)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        AqlanDentalPro.Infrastructure.Services.PdfService.EnsureFontsRegistered();
        var c = data.Case;

        var doc = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(FontName));

                page.Header().Element(el => ComposeHeader(el, c, identity, "التقرير المشترك — تخطيط جراحة الفكين"));
                page.Content().Element(el => ComposeDoctorContent(el, data, identity));
                page.Footer().Element(el => ComposeFooter(el, identity));
            });
        });

        return doc.GeneratePdf();
    }

    private static void ComposeDoctorContent(IContainer container, ReportData data, CephReportClinicIdentity identity)
    {
        var c = data.Case;
        container.Column(column =>
        {
            column.Spacing(8);

            Section(column, "بيانات المريض والحالة");
            Pairs(column,
            [
                ("الاسم", PatientName(c.Patient)),
                ("رقم الملف", string.IsNullOrWhiteSpace(c.Patient?.PatientNumber) ? "—" : c.Patient!.PatientNumber),
                ("العمر", AgeText(c.Patient?.DateOfBirth)),
                ("رقم حالة التخطيط", c.CaseNumber),
                ("الحالة الحالية", OrthoSurgicalStatusTransitions.GetArabicLabel(c.Status)),
                ("أخصائي التقويم", c.Orthodontist?.Name ?? "—"),
                ("أخصائي الجراحة", c.Surgeon?.Name ?? "—"),
            ]);

            Section(column, "التشخيص التقويمي");
            if (data.Diagnosis is null) NoData(column, "لا يوجد تشخيص تقويمي محفوظ.");
            else
            {
                Pairs(column,
                [
                    ("التصنيف الهيكلي", data.Diagnosis.SkeletalClassification ?? "—"),
                    ("التصنيف السنّي", data.Diagnosis.DentalClassification ?? "—"),
                    ("النمط الوجهي", data.Diagnosis.FacialPattern ?? "—"),
                    ("الاعتماد", data.Diagnosis.ApprovedAt is not null ? "معتمد" : "مسودّة"),
                ]);
                if (!string.IsNullOrWhiteSpace(data.Diagnosis.Summary))
                    column.Item().Text(data.Diagnosis.Summary).FontSize(9).FontFamily(FontName);
            }

            Section(column, "القياسات السيفالومترية");
            if (data.Ceph is null) NoData(column, "لا يوجد تحليل سيفالو مرتبط.");
            else
            {
                Pairs(column, [("تاريخ التحليل", CephReportPdfGenerator.FormatArabicDate(data.Ceph.AnalysisDate)),
                    ("الاعتماد", data.Ceph.IsApproved ? "معتمد" : "غير معتمد")]);
                if (data.Measurements.Count > 0)
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.2f); cols.RelativeColumn(1); cols.RelativeColumn(1); cols.RelativeColumn(1);
                        });
                        table.Cell().Element(HeaderCell).Text("القياس").FontFamily(FontName);
                        table.Cell().Element(HeaderCell).Text("القيمة").FontFamily(FontName);
                        table.Cell().Element(HeaderCell).Text("المعياري").FontFamily(FontName);
                        table.Cell().Element(HeaderCell).Text("التصنيف").FontFamily(FontName);
                        foreach (var m in data.Measurements.OrderBy(m => m.MeasurementName))
                        {
                            table.Cell().Element(ValueCell).Text(m.MeasurementName).FontFamily(FontName);
                            table.Cell().Element(ValueCell).Text(NumUnit(m.MeasurementValue, m.Unit)).FontFamily(FontName);
                            table.Cell().Element(ValueCell).Text(NumUnit(m.NormalValue, m.Unit)).FontFamily(FontName);
                            table.Cell().Element(ValueCell).Text(m.Classification ?? "—").FontFamily(FontName);
                        }
                    });
            }

            Section(column, "جاهزية السجلات");
            if (data.Checklist is null) NoData(column, "لم تُحفظ قائمة السجلات بعد.");
            else Pairs(column,
            [
                ("صور خارج الفم", data.Checklist.ExtraoralFrontal && data.Checklist.ExtraoralProfile ? "مكتملة" : "ناقصة"),
                ("صور داخل الفم", data.Checklist.IntraoralFrontal && data.Checklist.IntraoralRight && data.Checklist.IntraoralLeft ? "مكتملة" : "ناقصة"),
                ("بانوراما OPG", data.Checklist.Opg ? "متوفرة" : "غير متوفرة"),
                ("سيفالو جانبي", data.Checklist.LateralCeph ? "متوفر" : "غير متوفر"),
                ("نماذج دراسية", data.Checklist.StudyModels ? "متوفرة" : "غير متوفرة"),
            ]);

            Section(column, "مراجعة الجراح");
            if (c.SurgeonReview is null) NoData(column, "لم تُسجَّل مراجعة الجراح بعد.");
            else
            {
                Pairs(column,
                [
                    ("القرار", DecisionAr(c.SurgeonReview.Decision)),
                    ("الإجراء المقترح", c.SurgeonReview.ProposedProcedure ?? "—"),
                    ("تاريخ المراجعة", c.SurgeonReview.ReviewedAt is not null
                        ? CephReportPdfGenerator.FormatArabicDate(DateOnly.FromDateTime(c.SurgeonReview.ReviewedAt.Value)) : "—"),
                ]);
                if (!string.IsNullOrWhiteSpace(c.SurgeonReview.Notes))
                    column.Item().Text($"ملاحظات: {c.SurgeonReview.Notes}").FontSize(9).FontFamily(FontName);
            }

            Section(column, "الخطة المشتركة والاعتمادات");
            if (c.JointPlan is null) NoData(column, "لم تُوضع الخطة المشتركة بعد.");
            else
            {
                Pairs(column,
                [
                    ("نوع الإجراء", c.JointPlan.ProcedureType ?? "—"),
                    ("التوقيت", c.JointPlan.Timing ?? "—"),
                    ("أهداف التقويم", c.JointPlan.OrthodonticObjectives ?? "—"),
                    ("أهداف الجراحة", c.JointPlan.SurgicalObjectives ?? "—"),
                    ("متطلبات ما قبل الجراحة", c.JointPlan.PreSurgicalRequirements ?? "—"),
                    ("خطة ما بعد الجراحة", c.JointPlan.PostSurgicalPlan ?? "—"),
                    ("المخاطر والحدود", c.JointPlan.Risks ?? "—"),
                    ("اعتماد التقويم", c.OrthodontistApprovedAt is not null
                        ? CephReportPdfGenerator.FormatArabicDate(DateOnly.FromDateTime(c.OrthodontistApprovedAt.Value)) : "لم يُعتمد بعد"),
                    ("اعتماد الجراحة", c.SurgeonApprovedAt is not null
                        ? CephReportPdfGenerator.FormatArabicDate(DateOnly.FromDateTime(c.SurgeonApprovedAt.Value)) : "لم يُعتمد بعد"),
                    ("حالة القفل", c.JointPlan.LockedAt is not null ? "معتمدة ومقفلة" : "قابلة للتعديل"),
                ]);
            }

            column.Item().PaddingTop(6).Background(Colors.Amber.Lighten4).Padding(6)
                .Text(SimulationDisclaimer).FontSize(8.5f).FontColor(Colors.Grey.Darken2).FontFamily(FontName);

            SignatureBlock(column, identity);
        });
    }

    // ── Patient explanation ────────────────────────────────────────────────────────
    private static byte[] GeneratePatientExplanation(ReportData data, CephReportClinicIdentity identity)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        AqlanDentalPro.Infrastructure.Services.PdfService.EnsureFontsRegistered();
        var c = data.Case;

        var doc = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(x => x.FontSize(10.5f).FontFamily(FontName));

                page.Header().Element(el => ComposeHeader(el, c, identity, "شرح مبسّط للمريض — خطة تقويم وجراحة الفكين"));
                page.Content().Element(el => ComposePatientContent(el, data, identity));
                page.Footer().Element(el => ComposeFooter(el, identity));
            });
        });

        return doc.GeneratePdf();
    }

    private static void ComposePatientContent(IContainer container, ReportData data, CephReportClinicIdentity identity)
    {
        var c = data.Case;
        var plan = c.JointPlan;

        container.Column(column =>
        {
            column.Spacing(10);

            column.Item().Text($"عزيزي/عزيزتي {PatientName(c.Patient)}،").FontSize(11).Bold().FontFamily(FontName);
            column.Item().Text("نشارككم فيما يلي شرحًا مبسّطًا لخطة العلاج المقترحة لحالتكم:").FontFamily(FontName);

            Question(column, "ما المشكلة؟",
                !string.IsNullOrWhiteSpace(c.DiagnosisSummary) ? c.DiagnosisSummary!
                : (data.Diagnosis?.SkeletalClassification ?? "سيوضّح لكم الطبيب طبيعة الحالة بالتفصيل خلال الزيارة."));

            Question(column, "لماذا قد نحتاج جراحة؟",
                plan?.SurgicalObjectives ?? "قد تحتاج بعض الحالات إلى تصحيح جراحي لعظام الفكين لتحقيق نتيجة وظيفية وجمالية مستقرة.");

            Question(column, "ماذا سيعمل التقويم؟",
                plan?.OrthodonticObjectives ?? "يقوم أخصائي التقويم بتحضير الأسنان قبل الجراحة وضبطها بعدها لتحقيق إطباق سليم.");

            Question(column, "ماذا سيعمل الجراح؟",
                !string.IsNullOrWhiteSpace(plan?.ProcedureType) ? $"الإجراء المقترح: {plan!.ProcedureType}" : "سيحدد أخصائي جراحة الفكين نوع الإجراء المناسب بعد المراجعة والفحص.");

            Question(column, "ما مراحل العلاج؟",
                !string.IsNullOrWhiteSpace(plan?.Timing) ? plan!.Timing! : "تحضير تقويمي، ثم الجراحة، ثم تقويم واستقرار ما بعد الجراحة.");

            Question(column, "ما المتوقع بعد العملية؟",
                plan?.PostSurgicalPlan ?? "سيقدَّم لكم برنامج متابعة ونظافة وغذاء بعد الجراحة من الفريق الطبي.");

            Question(column, "ما المخاطر العامة؟",
                plan?.Risks ?? "كأي إجراء جراحي، توجد مخاطر عامة سيشرحها لكم الجراح بالتفصيل قبل الموافقة على الجراحة.");

            if (!string.IsNullOrWhiteSpace(plan?.PatientExplanation))
            {
                Section(column, "ملاحظة من فريقكم الطبي");
                column.Item().Text(plan!.PatientExplanation!).FontFamily(FontName);
            }

            column.Item().PaddingTop(6).Background(Colors.Amber.Lighten4).Padding(8)
                .Text("ما حدود المحاكاة؟\n" + SimulationDisclaimer)
                .FontSize(9).FontColor(Colors.Grey.Darken2).FontFamily(FontName);

            SignatureBlock(column, identity);
        });
    }

    // ── Shared layout ──────────────────────────────────────────────────────────────
    private static void ComposeHeader(IContainer container, OrthoSurgicalCase c, CephReportClinicIdentity identity, string title)
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
                    col.Item().AlignLeft().Text(title).Bold().FontSize(13).FontFamily(FontName);
                    col.Item().AlignLeft().Text($"رقم الحالة: {c.CaseNumber}").FontSize(9).FontFamily(FontName);
                    col.Item().AlignLeft().Text($"تاريخ التقرير: {CephReportPdfGenerator.FormatArabicDate(ClinicTimeProvider.ClinicToday())}")
                        .FontSize(9).FontFamily(FontName);
                });
            });
            column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
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

    private static void SignatureBlock(ColumnDescriptor column, CephReportClinicIdentity identity) =>
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

    private static void Question(ColumnDescriptor column, string question, string answer)
    {
        column.Item().Text(question).Bold().FontSize(11).FontColor(Colors.Blue.Darken2).FontFamily(FontName);
        column.Item().Text(answer).FontFamily(FontName);
    }

    // ── Layout helpers (mirrors OrthoCaseSummaryReportPdfGenerator) ──
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

    private static IContainer HeaderCell(IContainer c) =>
        c.Background(Colors.Grey.Lighten3).PaddingVertical(3).PaddingHorizontal(4)
            .DefaultTextStyle(s => s.Bold().FontSize(9));

    private static void NoData(ColumnDescriptor column, string message) =>
        column.Item().Text(message).FontSize(9).FontColor(Colors.Grey.Darken1).FontFamily(FontName);

    // ── Formatting ──
    private static string NumUnit(decimal? v, string? unit) =>
        v.HasValue ? string.Create(Inv, $"{v.Value:0.0} {unit}") : "—";

    private static string PatientName(Patient? p)
    {
        if (p is null) return "غير محدد";
        var name = $"{p.FirstName} {p.MiddleName} {p.LastName}".Replace("  ", " ").Trim();
        return name.Length > 0 ? name : "غير محدد";
    }

    private static string AgeText(DateOnly? dob)
    {
        if (!dob.HasValue) return "—";
        var today = ClinicTimeProvider.ClinicToday();
        var age = today.Year - dob.Value.Year;
        if (dob.Value > today.AddYears(-age)) age--;
        return age >= 0 ? $"{age} سنة" : "—";
    }

    private static string DecisionAr(string decision) => decision switch
    {
        "Approved" => "موافق",
        "RequestChanges" => "يحتاج تعديلًا",
        "NotCandidate" => "غير مرشّح للجراحة",
        "NeedsImaging" => "يحتاج صورًا إضافية",
        _ => decision,
    };
}
