using System.Text;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/reports/operations")]
[Authorize(Policy = "ReportsAccess")]
public sealed class OperationalReportsController(
    AppDbContext db,
    ICurrentUserService currentUser) : ControllerBase
{
    private static readonly HashSet<string> SupportedTypes =
    [
        "new-patients",
        "treated-patients",
        "income",
        "outstanding-balances",
        "treatment-progress",
        "returning-patients",
        "ortho-cases"
    ];

    [HttpGet("details")]
    public async Task<IActionResult> GetDetails(
        [FromQuery] string type,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int absenceDays = 90,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var validation = ValidateRequest(type, from, to, absenceDays, page, pageSize);
        if (validation.Error is not null) return validation.Error;
        if (!TryGetBranchScope(out var branchId, out var forbid)) return forbid!;

        var report = await BuildReportAsync(
            validation.Type,
            validation.FromDate,
            validation.ToDate,
            branchId,
            validation.AbsenceDays);

        return Ok(report.ToPage(validation.Page, validation.PageSize));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string type,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int absenceDays = 90)
    {
        var validation = ValidateRequest(type, from, to, absenceDays, 1, 100);
        if (validation.Error is not null) return validation.Error;
        if (!TryGetBranchScope(out var branchId, out var forbid)) return forbid!;

        var report = await BuildReportAsync(
            validation.Type,
            validation.FromDate,
            validation.ToDate,
            branchId,
            validation.AbsenceDays);

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", report.Columns.Select(column => CsvEscape(column.Label))));
        foreach (var row in report.Rows)
        {
            csv.AppendLine(string.Join(",", report.Columns.Select(column =>
                CsvEscape(FormatCsvValue(row.GetValueOrDefault(column.Key))))));
        }

        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(csv.ToString()))
            .ToArray();
        return File(
            bytes,
            "text/csv; charset=utf-8",
            $"{validation.Type}_{validation.FromDate:yyyyMMdd}_{validation.ToDate:yyyyMMdd}.csv");
    }

    private async Task<OperationalReportData> BuildReportAsync(
        string type,
        DateOnly from,
        DateOnly to,
        Guid? branchId,
        int absenceDays) =>
        type switch
        {
            "new-patients" => await BuildNewPatientsAsync(from, to, branchId),
            "treated-patients" => await BuildTreatedPatientsAsync(from, to, branchId),
            "income" => await BuildIncomeAsync(from, to, branchId),
            "outstanding-balances" => await BuildOutstandingBalancesAsync(from, to, branchId),
            "treatment-progress" => await BuildTreatmentProgressAsync(from, to, branchId),
            "returning-patients" => await BuildReturningPatientsAsync(from, to, branchId, absenceDays),
            "ortho-cases" => await BuildOrthoCasesAsync(from, to, branchId),
            _ => throw new InvalidOperationException($"Unsupported report type: {type}")
        };

    private async Task<OperationalReportData> BuildNewPatientsAsync(
        DateOnly from,
        DateOnly to,
        Guid? branchId)
    {
        var (utcStart, _) = ClinicTimeProvider.ToUtcRange(from);
        var (_, utcEnd) = ClinicTimeProvider.ToUtcRange(to);

        var patients = await db.Patients
            .Where(patient => patient.IsActive
                && patient.CreatedAt >= utcStart && patient.CreatedAt < utcEnd
                && (!branchId.HasValue || patient.BranchId == branchId.Value))
            .OrderByDescending(patient => patient.CreatedAt)
            .Select(patient => new
            {
                patient.Id,
                patient.PatientNumber,
                PatientName = (patient.FirstName + " " + (patient.MiddleName ?? "") + " " + patient.LastName).Trim(),
                patient.Phone,
                patient.CreatedAt,
                DoctorName = patient.PrimaryDoctor != null ? patient.PrimaryDoctor.Name : "",
                VisitsCount = patient.Visits.Count(visit => visit.IsActive),
                LastVisit = patient.Visits
                    .Where(visit => visit.IsActive)
                    .Max(visit => (DateOnly?)visit.VisitDate)
            })
            .ToListAsync();

        var rows = patients.Select(patient => Row(
            ("patientId", patient.Id),
            ("patientNumber", patient.PatientNumber),
            ("patientName", patient.PatientName),
            ("phone", patient.Phone),
            ("registeredDate", ClinicTimeProvider.ClinicDateFromUtc(patient.CreatedAt)),
            ("doctorName", patient.DoctorName),
            ("visitsCount", patient.VisitsCount),
            ("lastVisit", patient.LastVisit))).ToList();

        return new(
            "المرضى الجدد",
            from,
            to,
            [
                new("patientNumber", "رقم المريض"),
                new("patientName", "اسم المريض"),
                new("phone", "الهاتف"),
                new("registeredDate", "تاريخ التسجيل", "date"),
                new("doctorName", "الطبيب الأساسي"),
                new("visitsCount", "عدد الزيارات", "number"),
                new("lastVisit", "آخر زيارة", "date")
            ],
            rows,
            [new("عدد المرضى الجدد", rows.Count)]);
    }

    private async Task<OperationalReportData> BuildTreatedPatientsAsync(
        DateOnly from,
        DateOnly to,
        Guid? branchId)
    {
        var visits = await db.Visits
            .Where(visit => visit.IsActive
                && visit.Patient.IsActive
                && visit.VisitDate >= from && visit.VisitDate <= to
                && (!branchId.HasValue || visit.Patient.BranchId == branchId.Value))
            .OrderByDescending(visit => visit.VisitDate)
            .ThenBy(visit => visit.Patient.PatientNumber)
            .Select(visit => new
            {
                visit.Id,
                visit.PatientId,
                visit.Patient.PatientNumber,
                PatientName = (visit.Patient.FirstName + " " + visit.Patient.LastName).Trim(),
                DoctorName = visit.Doctor != null ? visit.Doctor.Name : "",
                visit.VisitDate,
                visit.VisitType,
                Specialty = visit.Specialty.HasValue ? visit.Specialty.Value.ToString() : "",
                visit.Diagnosis,
                visit.TreatmentDone,
                visit.CheckoutStatus
            })
            .ToListAsync();

        var rows = visits.Select(visit => Row(
            ("visitId", visit.Id),
            ("patientId", visit.PatientId),
            ("patientNumber", visit.PatientNumber),
            ("patientName", visit.PatientName),
            ("visitDate", visit.VisitDate),
            ("doctorName", visit.DoctorName),
            ("specialty", visit.Specialty),
            ("visitType", visit.VisitType),
            ("diagnosis", visit.Diagnosis),
            ("treatmentDone", visit.TreatmentDone),
            ("checkoutStatus", visit.CheckoutStatus))).ToList();

        return new(
            "المرضى الذين تم علاجهم",
            from,
            to,
            [
                new("patientNumber", "رقم المريض"),
                new("patientName", "اسم المريض"),
                new("visitDate", "تاريخ الزيارة", "date"),
                new("doctorName", "الطبيب"),
                new("specialty", "التخصص"),
                new("visitType", "نوع الزيارة"),
                new("diagnosis", "التشخيص"),
                new("treatmentDone", "العلاج المنفذ"),
                new("checkoutStatus", "حالة الإغلاق")
            ],
            rows,
            [
                new("عدد المرضى", visits.Select(visit => visit.PatientId).Distinct().Count()),
                new("عدد الزيارات", rows.Count)
            ]);
    }

    private async Task<OperationalReportData> BuildIncomeAsync(
        DateOnly from,
        DateOnly to,
        Guid? branchId)
    {
        var payments = await db.Payments
            .Where(payment => payment.IsActive
                && payment.Patient.IsActive
                && payment.PaymentDate >= from && payment.PaymentDate <= to
                && payment.Amount > 0
                && (!branchId.HasValue || payment.BranchId == branchId.Value))
            .OrderByDescending(payment => payment.PaymentDate)
            .ThenByDescending(payment => payment.CreatedAt)
            .Select(payment => new
            {
                payment.Id,
                payment.PatientId,
                payment.Patient.PatientNumber,
                PatientName = (payment.Patient.FirstName + " " + payment.Patient.LastName).Trim(),
                payment.ReceiptNumber,
                payment.PaymentDate,
                payment.Amount,
                payment.Currency,
                payment.AppliedAmount,
                payment.AccountCurrency,
                payment.PaymentMethod,
                payment.ServiceDescription,
                DoctorName = payment.Doctor != null ? payment.Doctor.Name : ""
            })
            .ToListAsync();

        var rows = payments.Select(payment =>
        {
            var currency = NormalizeCurrency(payment.Currency);
            var accountCurrency = NormalizeCurrency(payment.AccountCurrency);
            return Row(
                ("paymentId", payment.Id),
                ("patientId", payment.PatientId),
                ("receiptNumber", payment.ReceiptNumber),
                ("paymentDate", payment.PaymentDate),
                ("patientNumber", payment.PatientNumber),
                ("patientName", payment.PatientName),
                ("amount", payment.Amount),
                ("currency", currency),
                ("appliedAmount", payment.AppliedAmount == 0 ? payment.Amount : payment.AppliedAmount),
                ("accountCurrency", accountCurrency),
                ("paymentMethod", payment.PaymentMethod),
                ("serviceDescription", payment.ServiceDescription),
                ("doctorName", payment.DoctorName));
        }).ToList();

        var totals = payments
            .GroupBy(payment => NormalizeCurrency(payment.Currency))
            .OrderBy(group => group.Key)
            .Select(group => new ReportSummary($"إجمالي المقبوض {group.Key}", group.Sum(payment => payment.Amount), group.Key))
            .ToList();
        totals.Insert(0, new ReportSummary("عدد سندات القبض", rows.Count));

        return new(
            "الدخل والتحصيل التفصيلي",
            from,
            to,
            [
                new("receiptNumber", "رقم السند"),
                new("paymentDate", "التاريخ", "date"),
                new("patientNumber", "رقم المريض"),
                new("patientName", "اسم المريض"),
                new("amount", "المبلغ", "money"),
                new("currency", "عملة القبض", "currency"),
                new("appliedAmount", "المبلغ المحتسب", "money"),
                new("accountCurrency", "عملة الحساب", "currency"),
                new("paymentMethod", "طريقة الدفع"),
                new("serviceDescription", "الخدمة"),
                new("doctorName", "الطبيب")
            ],
            rows,
            totals);
    }

    private async Task<OperationalReportData> BuildOutstandingBalancesAsync(
        DateOnly from,
        DateOnly to,
        Guid? branchId)
    {
        var contracts = await db.Contracts
            .Where(contract => contract.IsActive
                && contract.Patient.IsActive
                && contract.Status != ContractStatus.Cancelled
                && (!branchId.HasValue || contract.Patient.BranchId == branchId.Value))
            .Select(contract => new BalancePart(
                contract.PatientId,
                contract.Patient.PatientNumber,
                (contract.Patient.FirstName + " " + contract.Patient.LastName).Trim(),
                contract.Patient.Phone,
                NormalizeCurrency(contract.Currency),
                contract.TotalAmount - contract.DiscountAmount,
                contract.Payments.Where(payment => payment.IsActive)
                    .Sum(payment => payment.AppliedAmount == 0 ? payment.Amount : payment.AppliedAmount),
                1,
                0))
            .ToListAsync();

        var invoices = await db.Invoices
            .Where(invoice => invoice.IsActive
                && invoice.Patient.IsActive
                && invoice.Status != InvoiceStatus.Draft
                && invoice.Status != InvoiceStatus.Cancelled
                && !invoice.IsOpeningBalance
                && (!branchId.HasValue || invoice.Patient.BranchId == branchId.Value))
            .Select(invoice => new BalancePart(
                invoice.PatientId,
                invoice.Patient.PatientNumber,
                (invoice.Patient.FirstName + " " + invoice.Patient.LastName).Trim(),
                invoice.Patient.Phone,
                NormalizeCurrency(invoice.Currency),
                invoice.TotalAmount,
                invoice.Payments.Where(payment => payment.IsActive)
                    .Sum(payment => payment.AppliedAmount == 0 ? payment.Amount : payment.AppliedAmount)
                    + invoice.PaymentAllocations.Where(allocation => allocation.IsActive).Sum(allocation => allocation.Amount),
                0,
                1))
            .ToListAsync();

        var balances = contracts.Concat(invoices)
            .GroupBy(part => new
            {
                part.PatientId,
                part.PatientNumber,
                part.PatientName,
                part.Phone,
                part.Currency
            })
            .Select(group => new
            {
                group.Key.PatientId,
                group.Key.PatientNumber,
                group.Key.PatientName,
                group.Key.Phone,
                group.Key.Currency,
                Total = group.Sum(part => part.Total),
                Paid = group.Sum(part => part.Paid),
                Remaining = Math.Max(0, group.Sum(part => part.Total) - group.Sum(part => part.Paid)),
                ContractsCount = group.Sum(part => part.ContractsCount),
                InvoicesCount = group.Sum(part => part.InvoicesCount)
            })
            .Where(balance => balance.Remaining > 0)
            .OrderByDescending(balance => balance.Remaining)
            .ToList();

        var rows = balances.Select(balance => Row(
            ("patientId", balance.PatientId),
            ("patientNumber", balance.PatientNumber),
            ("patientName", balance.PatientName),
            ("phone", balance.Phone),
            ("currency", balance.Currency),
            ("total", balance.Total),
            ("paid", balance.Paid),
            ("remaining", balance.Remaining),
            ("contractsCount", balance.ContractsCount),
            ("invoicesCount", balance.InvoicesCount))).ToList();

        var summaries = balances
            .GroupBy(balance => balance.Currency)
            .OrderBy(group => group.Key)
            .Select(group => new ReportSummary($"إجمالي المتبقي {group.Key}", group.Sum(balance => balance.Remaining), group.Key))
            .ToList();
        summaries.Insert(0, new ReportSummary("عدد المرضى ذوي الرصيد", balances.Select(balance => balance.PatientId).Distinct().Count()));

        return new(
            "أرصدة المرضى المتبقية",
            from,
            to,
            [
                new("patientNumber", "رقم المريض"),
                new("patientName", "اسم المريض"),
                new("phone", "الهاتف"),
                new("currency", "العملة", "currency"),
                new("total", "إجمالي المطالبات", "money"),
                new("paid", "المدفوع", "money"),
                new("remaining", "المتبقي", "money"),
                new("contractsCount", "العقود", "number"),
                new("invoicesCount", "الفواتير", "number")
            ],
            rows,
            summaries);
    }

    private async Task<OperationalReportData> BuildTreatmentProgressAsync(
        DateOnly from,
        DateOnly to,
        Guid? branchId)
    {
        var (utcStart, _) = ClinicTimeProvider.ToUtcRange(from);
        var (_, utcEnd) = ClinicTimeProvider.ToUtcRange(to);
        var activePatientIds = await db.PatientTreatmentPlanSteps
            .Where(step => step.IsActive
                && (step.CreatedAt >= utcStart && step.CreatedAt < utcEnd
                    || step.PlannedDate >= from && step.PlannedDate <= to
                    || step.CompletedDate >= from && step.CompletedDate <= to)
                && (!branchId.HasValue || step.Patient.BranchId == branchId.Value))
            .Select(step => step.PatientId)
            .Distinct()
            .ToListAsync();

        // The date range selects the patients whose plans were active in the
        // period. Progress itself must use every active step in those plans;
        // otherwise a single completed step in the period can falsely appear
        // as a 100% completed treatment plan.
        var steps = await db.PatientTreatmentPlanSteps
            .Where(step => step.IsActive
                && activePatientIds.Contains(step.PatientId)
                && (!branchId.HasValue || step.Patient.BranchId == branchId.Value))
            .Select(step => new
            {
                step.PatientId,
                step.Patient.PatientNumber,
                PatientName = (step.Patient.FirstName + " " + step.Patient.LastName).Trim(),
                step.Status,
                step.Title,
                step.SequenceNumber,
                step.PlannedDate,
                step.CompletedDate,
                DoctorName = step.ResponsibleDoctor != null ? step.ResponsibleDoctor.Name : ""
            })
            .ToListAsync();

        var progress = steps
            .GroupBy(step => new { step.PatientId, step.PatientNumber, step.PatientName })
            .Select(group =>
            {
                var next = group
                    .Where(step => step.Status is not TreatmentStepStatus.Completed and not TreatmentStepStatus.Cancelled)
                    .OrderBy(step => step.SequenceNumber)
                    .FirstOrDefault();
                return new
                {
                    group.Key.PatientId,
                    group.Key.PatientNumber,
                    group.Key.PatientName,
                    Total = group.Count(),
                    Completed = group.Count(step => step.Status == TreatmentStepStatus.Completed),
                    InProgress = group.Count(step => step.Status == TreatmentStepStatus.InProgress),
                    Remaining = group.Count(step => step.Status is TreatmentStepStatus.Planned or TreatmentStepStatus.InProgress or TreatmentStepStatus.Deferred),
                    NextStep = next?.Title,
                    NextDate = next?.PlannedDate,
                    DoctorName = next?.DoctorName
                };
            })
            .OrderByDescending(item => item.Remaining)
            .ThenBy(item => item.PatientName)
            .ToList();

        var rows = progress.Select(item => Row(
            ("patientId", item.PatientId),
            ("patientNumber", item.PatientNumber),
            ("patientName", item.PatientName),
            ("totalSteps", item.Total),
            ("completedSteps", item.Completed),
            ("inProgressSteps", item.InProgress),
            ("remainingSteps", item.Remaining),
            ("completionRate", item.Total == 0 ? 0 : Math.Round(item.Completed * 100m / item.Total, 1)),
            ("nextStep", item.NextStep),
            ("nextDate", item.NextDate),
            ("doctorName", item.DoctorName))).ToList();

        return new(
            "تقدم خطط العلاج",
            from,
            to,
            [
                new("patientNumber", "رقم المريض"),
                new("patientName", "اسم المريض"),
                new("totalSteps", "كل الخطوات", "number"),
                new("completedSteps", "المكتمل", "number"),
                new("inProgressSteps", "قيد التنفيذ", "number"),
                new("remainingSteps", "المتبقي", "number"),
                new("completionRate", "نسبة الإنجاز", "percent"),
                new("nextStep", "الخطوة التالية"),
                new("nextDate", "موعدها", "date"),
                new("doctorName", "الطبيب")
            ],
            rows,
            [
                new("عدد المرضى", rows.Count),
                new("الخطوات المكتملة", progress.Sum(item => item.Completed)),
                new("الخطوات المتبقية", progress.Sum(item => item.Remaining))
            ]);
    }

    private async Task<OperationalReportData> BuildReturningPatientsAsync(
        DateOnly from,
        DateOnly to,
        Guid? branchId,
        int absenceDays)
    {
        var visits = await db.Visits
            .Where(visit => visit.IsActive
                && visit.Patient.IsActive
                && visit.VisitDate <= to
                && (!branchId.HasValue || visit.Patient.BranchId == branchId.Value))
            .OrderBy(visit => visit.PatientId)
            .ThenBy(visit => visit.VisitDate)
            .ThenBy(visit => visit.CreatedAt)
            .Select(visit => new
            {
                visit.Id,
                visit.PatientId,
                visit.Patient.PatientNumber,
                PatientName = (visit.Patient.FirstName + " " + visit.Patient.LastName).Trim(),
                visit.VisitDate,
                visit.VisitType,
                Specialty = visit.Specialty.HasValue ? visit.Specialty.Value.ToString() : "",
                visit.TreatmentDone,
                DoctorName = visit.Doctor != null ? visit.Doctor.Name : ""
            })
            .ToListAsync();

        var returning = new List<Dictionary<string, object?>>();
        foreach (var patientVisits in visits.GroupBy(visit => visit.PatientId))
        {
            var ordered = patientVisits.ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                var current = ordered[index];
                var previous = ordered[index - 1];
                var gapDays = current.VisitDate.DayNumber - previous.VisitDate.DayNumber;
                if (current.VisitDate < from || gapDays < absenceDays) continue;

                var currentTreatment = current.TreatmentDone ?? current.VisitType ?? current.Specialty;
                var previousTreatment = previous.TreatmentDone ?? previous.VisitType ?? previous.Specialty;
                returning.Add(Row(
                    ("patientId", current.PatientId),
                    ("patientNumber", current.PatientNumber),
                    ("patientName", current.PatientName),
                    ("returnDate", current.VisitDate),
                    ("previousVisitDate", previous.VisitDate),
                    ("absenceDays", gapDays),
                    ("previousTreatment", previousTreatment),
                    ("currentTreatment", currentTreatment),
                    ("differentTreatment", !string.Equals(previousTreatment, currentTreatment, StringComparison.OrdinalIgnoreCase)),
                    ("doctorName", current.DoctorName)));
            }
        }

        returning = returning
            .OrderByDescending(row => (DateOnly)row["returnDate"]!)
            .ToList();

        return new(
            "المرضى العائدون بعد انقطاع",
            from,
            to,
            [
                new("patientNumber", "رقم المريض"),
                new("patientName", "اسم المريض"),
                new("returnDate", "تاريخ العودة", "date"),
                new("previousVisitDate", "الزيارة السابقة", "date"),
                new("absenceDays", "مدة الانقطاع بالأيام", "number"),
                new("previousTreatment", "العلاج السابق"),
                new("currentTreatment", "العلاج الحالي"),
                new("differentTreatment", "غرض علاجي مختلف", "boolean"),
                new("doctorName", "الطبيب")
            ],
            returning,
            [
                new("عدد حالات العودة", returning.Count),
                new("مرضى عادوا لعلاج مختلف", returning.Count(row => Equals(row["differentTreatment"], true)))
            ]);
    }

    private async Task<OperationalReportData> BuildOrthoCasesAsync(
        DateOnly from,
        DateOnly to,
        Guid? branchId)
    {
        var cases = await db.OrthoCases
            .Where(orthoCase => orthoCase.IsActive
                && orthoCase.Patient.IsActive
                && (orthoCase.Status == OrthoCaseStatus.Active
                    || orthoCase.StartDate >= from && orthoCase.StartDate <= to
                    || orthoCase.Visits.Any(visit => visit.IsActive && visit.VisitDate >= from && visit.VisitDate <= to))
                && (!branchId.HasValue || orthoCase.BranchId == branchId.Value))
            .Select(orthoCase => new
            {
                orthoCase.Id,
                orthoCase.CaseNumber,
                orthoCase.PatientId,
                orthoCase.Patient.PatientNumber,
                PatientName = (orthoCase.Patient.FirstName + " " + orthoCase.Patient.LastName).Trim(),
                DoctorName = orthoCase.Doctor != null ? orthoCase.Doctor.Name : "",
                orthoCase.Status,
                orthoCase.StartDate,
                orthoCase.ApplianceType,
                orthoCase.CurrentStage,
                orthoCase.StagePercentage,
                orthoCase.ExpectedDurationMonths,
                LastVisit = orthoCase.Visits
                    .Where(visit => visit.IsActive)
                    .Max(visit => (DateOnly?)visit.VisitDate),
                NextVisit = orthoCase.Visits
                    .Where(visit => visit.IsActive)
                    .OrderByDescending(visit => visit.VisitDate)
                    .Select(visit => visit.NextAppointmentDate)
                    .FirstOrDefault(),
                VisitsInPeriod = orthoCase.Visits.Count(visit =>
                    visit.IsActive && visit.VisitDate >= from && visit.VisitDate <= to),
                Contract = db.Contracts
                    .Where(contract => contract.IsActive
                        && contract.RelatedCaseId == orthoCase.Id
                        && contract.Status != ContractStatus.Cancelled)
                    .Select(contract => new
                    {
                        contract.Currency,
                        Total = contract.TotalAmount - contract.DiscountAmount,
                        Paid = contract.Payments.Where(payment => payment.IsActive)
                            .Sum(payment => payment.AppliedAmount == 0 ? payment.Amount : payment.AppliedAmount)
                    })
                    .FirstOrDefault()
            })
            .OrderBy(item => item.PatientName)
            .ToListAsync();

        var rows = cases.Select(item => Row(
            ("caseId", item.Id),
            ("patientId", item.PatientId),
            ("caseNumber", item.CaseNumber),
            ("patientNumber", item.PatientNumber),
            ("patientName", item.PatientName),
            ("doctorName", item.DoctorName),
            ("status", item.Status.ToString()),
            ("startDate", item.StartDate),
            ("applianceType", item.ApplianceType),
            ("currentStage", item.CurrentStage),
            ("stagePercentage", item.StagePercentage),
            ("lastVisit", item.LastVisit),
            ("nextVisit", item.NextVisit),
            ("visitsInPeriod", item.VisitsInPeriod),
            ("currency", item.Contract == null ? null : NormalizeCurrency(item.Contract.Currency)),
            ("totalAmount", item.Contract?.Total),
            ("paidAmount", item.Contract?.Paid),
            ("remainingAmount", item.Contract == null ? null : Math.Max(0, item.Contract.Total - item.Contract.Paid)))).ToList();

        return new(
            "حالات التقويم التفصيلية",
            from,
            to,
            [
                new("caseNumber", "رقم الحالة"),
                new("patientNumber", "رقم المريض"),
                new("patientName", "اسم المريض"),
                new("doctorName", "الطبيب"),
                new("status", "الحالة"),
                new("startDate", "تاريخ البدء", "date"),
                new("applianceType", "نوع الجهاز"),
                new("currentStage", "المرحلة الحالية"),
                new("stagePercentage", "نسبة التقدم", "percent"),
                new("lastVisit", "آخر زيارة", "date"),
                new("nextVisit", "الموعد القادم", "date"),
                new("visitsInPeriod", "زيارات الفترة", "number"),
                new("currency", "عملة العقد", "currency"),
                new("totalAmount", "قيمة العقد", "money"),
                new("paidAmount", "المدفوع", "money"),
                new("remainingAmount", "المتبقي", "money")
            ],
            rows,
            [
                new("إجمالي الحالات", rows.Count),
                new("الحالات النشطة", cases.Count(item => item.Status == OrthoCaseStatus.Active)),
                new("زيارات التقويم في الفترة", cases.Sum(item => item.VisitsInPeriod))
            ]);
    }

    private RequestValidation ValidateRequest(
        string? type,
        string? from,
        string? to,
        int absenceDays,
        int page,
        int pageSize)
    {
        var normalizedType = (type ?? "").Trim().ToLowerInvariant();
        if (!SupportedTypes.Contains(normalizedType))
            return RequestValidation.Fail(BadRequest(new { message = "نوع التقرير غير مدعوم." }));

        var (fromDate, fromError) = DateParsingHelper.TryParseDateOrDefault(
            from,
            ClinicTimeProvider.ClinicToday().AddDays(-30),
            "تاريخ البداية");
        if (fromError is not null) return RequestValidation.Fail(fromError);
        var (toDate, toError) = DateParsingHelper.TryParseDateOrDefault(
            to,
            ClinicTimeProvider.ClinicToday(),
            "تاريخ النهاية");
        if (toError is not null) return RequestValidation.Fail(toError);
        if (fromDate > toDate)
            return RequestValidation.Fail(BadRequest(new { message = "تاريخ البداية يجب أن يكون قبل تاريخ النهاية." }));
        if (toDate.DayNumber - fromDate.DayNumber > 3660)
            return RequestValidation.Fail(BadRequest(new { message = "الفترة القصوى للتقرير عشر سنوات." }));

        return new(
            normalizedType,
            fromDate,
            toDate,
            Math.Clamp(absenceDays, 1, 3650),
            Math.Max(1, page),
            Math.Clamp(pageSize, 10, 100),
            null);
    }

    private bool TryGetBranchScope(out Guid? branchId, out IActionResult? forbid)
    {
        branchId = null;
        forbid = null;
        if (currentUser.IsAdmin) return true;
        if (currentUser.BranchId is { } assignedBranch && assignedBranch != Guid.Empty)
        {
            branchId = assignedBranch;
            return true;
        }

        forbid = StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });
        return false;
    }

    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] cells) =>
        cells.ToDictionary(cell => cell.Key, cell => cell.Value);

    private static string NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? "YER" : currency.Trim().ToUpperInvariant();

    private static string FormatCsvValue(object? value) => value switch
    {
        null => "",
        DateOnly date => date.ToString("yyyy-MM-dd"),
        DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm"),
        bool boolean => boolean ? "نعم" : "لا",
        decimal number => number.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };

    private static string CsvEscape(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private sealed record RequestValidation(
        string Type,
        DateOnly FromDate,
        DateOnly ToDate,
        int AbsenceDays,
        int Page,
        int PageSize,
        IActionResult? Error)
    {
        public static RequestValidation Fail(IActionResult error) =>
            new("", default, default, 90, 1, 50, error);
    }

    private sealed record BalancePart(
        Guid PatientId,
        string PatientNumber,
        string PatientName,
        string? Phone,
        string Currency,
        decimal Total,
        decimal Paid,
        int ContractsCount,
        int InvoicesCount);
}

public sealed record ReportColumn(string Key, string Label, string Kind = "text");
public sealed record ReportSummary(string Label, object Value, string? Currency = null);

public sealed record OperationalReportData(
    string Title,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<Dictionary<string, object?>> Rows,
    IReadOnlyList<ReportSummary> Summary)
{
    public object ToPage(int page, int pageSize)
    {
        var totalRows = Rows.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalRows / (double)pageSize));
        var normalizedPage = Math.Min(page, totalPages);
        return new
        {
            title = Title,
            fromDate = FromDate.ToString("yyyy-MM-dd"),
            toDate = ToDate.ToString("yyyy-MM-dd"),
            columns = Columns,
            rows = Rows.Skip((normalizedPage - 1) * pageSize).Take(pageSize),
            summary = Summary,
            totalRows,
            page = normalizedPage,
            pageSize,
            totalPages
        };
    }
}
