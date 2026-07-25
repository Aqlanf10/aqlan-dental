from pathlib import Path
import re


def replace_once(path: Path, pattern: str, replacement: str, *, flags: int = 0, label: str) -> None:
    text = path.read_text(encoding="utf-8")
    updated, count = re.subn(pattern, replacement, text, count=1, flags=flags)
    if count != 1:
        raise SystemExit(f"CORE-PAT-027: expected one {label} match in {path}, found {count}")
    path.write_text(updated, encoding="utf-8")


# ── Backend DTO contract ──────────────────────────────────────────────────────
dto = Path("backend/src/AqlanDentalPro.Application/DTOs/Patients/PatientSummaryDto.cs")
dto.write_text('''namespace AqlanDentalPro.Application.DTOs.Patients;

public sealed class PatientSummaryDto
{
    public int TotalAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public int ActiveOrthoCases { get; set; }
    public decimal? TotalPaid { get; set; }
    public decimal? TotalOutstanding { get; set; }
    public decimal? UnbilledVisitsAmount { get; set; }
    public int PrescriptionsCount { get; set; }

    public string? LastVisitDate { get; set; }
    public string? LastVisitDoctor { get; set; }
    public string? LastVisitDiagnosis { get; set; }
    public string? NextAppointmentDate { get; set; }
    public string? NextAppointmentTime { get; set; }
    public string? NextAppointmentType { get; set; }
    public string? NextAppointmentDoctor { get; set; }
    public string? ChiefComplaint { get; set; }
    public string? CurrentDiagnosis { get; set; }
    public string? NextPlannedStep { get; set; }
    public List<PatientOrthoSummaryDto> ActiveOrthoSummary { get; set; } = [];
    public List<PatientSurgerySummaryDto> ActiveSurgerySummary { get; set; } = [];
    public List<string> MedicalAlerts { get; set; } = [];
}

public sealed class PatientOrthoSummaryDto
{
    public string CaseNumber { get; set; } = string.Empty;
    public string? ApplianceType { get; set; }
    public int StagePercentage { get; set; }
}

public sealed class PatientSurgerySummaryDto
{
    public string CaseNumber { get; set; } = string.Empty;
    public string SurgeryType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
''', encoding="utf-8")

# ── PatientsController: canonical clinical summary ───────────────────────────
controller = Path("backend/src/AqlanDentalPro.API/Controllers/PatientsController.cs")
text = controller.read_text(encoding="utf-8")
if 'using AqlanDentalPro.Domain.Entities;\n' not in text:
    text = text.replace('using AqlanDentalPro.Domain.Enums;\n', 'using AqlanDentalPro.Domain.Entities;\nusing AqlanDentalPro.Domain.Enums;\n', 1)
if 'using AqlanDentalPro.Infrastructure.Services;\n' not in text:
    text = text.replace('using AqlanDentalPro.Infrastructure.Data;\n', 'using AqlanDentalPro.Infrastructure.Data;\nusing AqlanDentalPro.Infrastructure.Services;\n', 1)

old_method_pattern = r'''    \[HttpGet\("\{id:guid\}/summary"\)\]\n    public async Task<IActionResult> GetSummary\(Guid id\)\n    \{.*?^    \}\n\n    // ── Timeline'''
new_method = '''    [HttpGet("{id:guid}/summary")]
    public async Task<IActionResult> GetSummary(Guid id)
    {
        var denied = await DenyIfDoctorCannotAccess(id);
        if (denied != null) return denied;

        // CORE-PAT-015: the profile deliberately serves archived patients, so the
        // summary must use the same existence rule instead of returning a partial 404.
        var exists = await db.Patients.IgnoreQueryFilters().AnyAsync(p => p.Id == id);
        if (!exists) return NotFound(new { message = "المريض غير موجود" });

        var clinicNow = ClinicTimeProvider.ClinicNow();
        var today = DateOnly.FromDateTime(clinicNow);
        var currentTime = TimeOnly.FromDateTime(clinicNow);

        var latestVisit = await db.Visits
            .AsNoTracking()
            .Include(v => v.Doctor)
            .Where(v => v.PatientId == id)
            .OrderByDescending(v => v.VisitDate)
            .ThenByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync();

        var nextAppointment = await db.Appointments
            .AsNoTracking()
            .Include(a => a.Doctor)
            .Where(a => a.PatientId == id
                && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed)
                && (a.AppointmentDate > today
                    || (a.AppointmentDate == today && a.StartTime >= currentTime)))
            .OrderBy(a => a.AppointmentDate)
            .ThenBy(a => a.StartTime)
            .FirstOrDefaultAsync();

        var dentalHistory = await db.DentalHistories
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.PatientId == id);
        var medicalHistory = await db.MedicalHistories
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.PatientId == id);

        var nextTreatmentStep = await db.PatientTreatmentPlanSteps
            .AsNoTracking()
            .Where(step => step.PatientId == id
                && step.Status != TreatmentStepStatus.Completed
                && step.Status != TreatmentStepStatus.Cancelled)
            .OrderBy(step => step.Status == TreatmentStepStatus.InProgress ? 0
                : step.Status == TreatmentStepStatus.Planned ? 1 : 2)
            .ThenBy(step => step.SequenceNumber)
            .FirstOrDefaultAsync();

        var activeOrthoSummary = await db.OrthoCases
            .AsNoTracking()
            .Where(o => o.PatientId == id && o.Status == OrthoCaseStatus.Active)
            .OrderBy(o => o.CaseNumber)
            .Select(o => new PatientOrthoSummaryDto
            {
                CaseNumber = o.CaseNumber,
                ApplianceType = o.ApplianceType,
                StagePercentage = o.StagePercentage,
            })
            .ToListAsync();

        var activeSurgerySummary = await db.SurgeryCases
            .AsNoTracking()
            .Where(s => s.PatientId == id
                && (s.Status == SurgeryCaseStatus.Scheduled
                    || s.Status == SurgeryCaseStatus.InProgress
                    || s.Status == SurgeryCaseStatus.Postponed))
            .OrderBy(s => s.CaseNumber)
            .Select(s => new PatientSurgerySummaryDto
            {
                CaseNumber = s.CaseNumber,
                SurgeryType = s.SurgeryType,
                Status = s.Status.ToString(),
            })
            .ToListAsync();

        var summary = new PatientSummaryDto
        {
            TotalAppointments = await db.Appointments.CountAsync(a => a.PatientId == id),
            CompletedAppointments = await db.Appointments.CountAsync(a => a.PatientId == id
                && a.Status == AppointmentStatus.Completed),
            ActiveOrthoCases = activeOrthoSummary.Count,
            PrescriptionsCount = await db.Prescriptions.CountAsync(p => p.PatientId == id),
            LastVisitDate = latestVisit?.VisitDate.ToString("yyyy-MM-dd"),
            LastVisitDoctor = latestVisit?.Doctor?.Name,
            LastVisitDiagnosis = latestVisit?.Diagnosis,
            NextAppointmentDate = nextAppointment?.AppointmentDate.ToString("yyyy-MM-dd"),
            NextAppointmentTime = nextAppointment?.StartTime.ToString("HH:mm"),
            NextAppointmentType = nextAppointment?.AppointmentType,
            NextAppointmentDoctor = nextAppointment?.Doctor?.Name,
            ChiefComplaint = FirstNonBlank(latestVisit?.ChiefComplaint, dentalHistory?.ChiefComplaint),
            CurrentDiagnosis = latestVisit?.Diagnosis,
            NextPlannedStep = FirstNonBlank(
                nextTreatmentStep?.Title,
                nextTreatmentStep?.ServiceNameSnapshot,
                nextTreatmentStep?.Description,
                latestVisit?.NextVisitPlan),
            ActiveOrthoSummary = activeOrthoSummary,
            ActiveSurgerySummary = activeSurgerySummary,
            MedicalAlerts = BuildMedicalAlerts(medicalHistory),
        };

        // CORE-PAT-019: clinical fields are always returned, while financial fields
        // remain null unless the granular owner-configurable permission is granted.
        var canViewFinanceTotals = !patientAccess.IsDoctor
            && await CanViewFinanceAsync("finance.patient_balance");
        if (canViewFinanceTotals)
        {
            var financeSummary = await financeReadService.GetPatientFinanceSummaryAsync(id);
            summary.TotalPaid = financeSummary.TotalPaid;
            summary.TotalOutstanding = financeSummary.OutstandingBalance;
            summary.UnbilledVisitsAmount = financeSummary.UnbilledVisitsAmount;

            await audit.LogAsync(AuditAction.View, "PatientFinanceSummary", id,
                newData: new { role = currentUser.Role?.ToString() });
        }

        return Ok(summary);
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static List<string> BuildMedicalAlerts(MedicalHistory? history)
    {
        if (history == null) return [];

        var alerts = new List<string>();
        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                alerts.Add($"{label}: {value.Trim()}");
        }

        Add("حساسية أدوية", history.DrugAllergies);
        Add("أمراض مزمنة", history.ChronicDiseases);
        Add("أدوية حالية", history.CurrentMedications);
        if (history.BleedingDisorders) alerts.Add("اضطرابات نزف");
        if (history.TmjProblems) alerts.Add("مشكلات مفصل الفك");

        var pregnancy = history.IsPregnant?.Trim();
        if (!string.IsNullOrWhiteSpace(pregnancy)
            && !pregnancy.Equals("لا", StringComparison.OrdinalIgnoreCase)
            && !pregnancy.Equals("no", StringComparison.OrdinalIgnoreCase)
            && !pregnancy.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            alerts.Add($"حمل: {pregnancy}");
        }

        return alerts.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ── Timeline'''
updated, count = re.subn(old_method_pattern, new_method, text, count=1, flags=re.MULTILINE | re.DOTALL)
if count != 1:
    raise SystemExit(f"CORE-PAT-027: expected one GetSummary block, found {count}")
controller.write_text(updated, encoding="utf-8")

# ── Shared frontend contract; remove four divergent local interfaces ─────────
frontend_type = Path("frontend/src/types/patientSummary.ts")
frontend_type.write_text('''export interface PatientOrthoSummary {
  caseNumber: string;
  applianceType?: string | null;
  stagePercentage: number;
}

export interface PatientSurgerySummary {
  caseNumber: string;
  surgeryType: string;
  status: string;
}

export interface PatientSummary {
  totalAppointments: number;
  completedAppointments: number;
  activeOrthoCases: number;
  totalPaid: number | null;
  totalOutstanding: number | null;
  unbilledVisitsAmount?: number | null;
  prescriptionsCount: number;
  lastVisitDate?: string | null;
  lastVisitDoctor?: string | null;
  lastVisitDiagnosis?: string | null;
  nextAppointmentDate?: string | null;
  nextAppointmentTime?: string | null;
  nextAppointmentType?: string | null;
  nextAppointmentDoctor?: string | null;
  chiefComplaint?: string | null;
  currentDiagnosis?: string | null;
  nextPlannedStep?: string | null;
  activeOrthoSummary?: PatientOrthoSummary[];
  activeSurgerySummary?: PatientSurgerySummary[];
  medicalAlerts?: string[];
}
''', encoding="utf-8")

frontend_files = [
    Path("frontend/src/components/patient/tabs/OverviewTab.tsx"),
    Path("frontend/src/app/(dashboard)/patients/[id]/print/summary/page.tsx"),
    Path("frontend/src/app/(dashboard)/patients/[id]/page.tsx"),
    Path("frontend/src/components/patients/PatientTable.tsx"),
]
for file in frontend_files:
    text = file.read_text(encoding="utf-8")
    text, count = re.subn(r"\ninterface PatientSummary \{.*?^\}\n", "\n", text, count=1, flags=re.MULTILINE | re.DOTALL)
    if count != 1:
        raise SystemExit(f"CORE-PAT-027: expected one PatientSummary interface in {file}, found {count}")

    import_line = 'import type { PatientSummary } from "@/types/patientSummary";\n'
    if import_line not in text:
        patient_import_match = re.search(r'import type \{[^\n]+\} from "@/types/patient";\n', text)
        if not patient_import_match:
            raise SystemExit(f"CORE-PAT-027: patient type import not found in {file}")
        insert_at = patient_import_match.end()
        text = text[:insert_at] + import_line + text[insert_at:]
    file.write_text(text, encoding="utf-8")

# ── Existing finance tests: support DTO PascalCase direct-controller values ──
finance_test = Path("backend/tests/AqlanDentalPro.UnitTests/Patients/PatientFinancePermissionEnforcementTests.cs")
text = finance_test.read_text(encoding="utf-8")
old_helper = '''    private static object? PropertyValue(IActionResult result, string propertyName)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        return ok.Value!.GetType().GetProperty(propertyName)!.GetValue(ok.Value);
    }
'''
new_helper = '''    private static object? PropertyValue(IActionResult result, string propertyName)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var property = ok.Value!.GetType().GetProperties()
            .Single(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));
        return property.GetValue(ok.Value);
    }
'''
if old_helper not in text:
    raise SystemExit("CORE-PAT-027: finance test property helper not found")
finance_test.write_text(text.replace(old_helper, new_helper, 1), encoding="utf-8")

# ── Regression test: every field used by overview/print is populated ─────────
clinical_test = Path("backend/tests/AqlanDentalPro.UnitTests/Patients/PatientClinicalSummaryTests.cs")
clinical_test.write_text('''using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Patients;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Patients;

public class PatientClinicalSummaryTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Summary_PopulatesClinicalFieldsUsedByOverviewAndPrint()
    {
        await using var db = CreateDb();
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var today = ClinicTimeProvider.ClinicToday();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            PatientNumber = "P-SUMMARY-001",
            FirstName = "مريض",
            LastName = "الملخص",
        });
        db.Doctors.Add(new Doctor
        {
            Id = doctorId,
            UserId = Guid.NewGuid(),
            Name = "الطبيب المعالج",
        });
        db.DentalHistories.Add(new DentalHistory
        {
            PatientId = patientId,
            ChiefComplaint = "ألم عند المضغ",
        });
        db.MedicalHistories.Add(new MedicalHistory
        {
            PatientId = patientId,
            DrugAllergies = "بنسلين",
            ChronicDiseases = "سكري",
            CurrentMedications = "ميتفورمين",
            BleedingDisorders = true,
        });
        db.Visits.Add(new Visit
        {
            PatientId = patientId,
            DoctorId = doctorId,
            VisitDate = today.AddDays(-1),
            ChiefComplaint = "ألم حاد",
            Diagnosis = "التهاب لب",
            NextVisitPlan = "علاج جذور",
        });
        db.Appointments.Add(new Appointment
        {
            PatientId = patientId,
            DoctorId = doctorId,
            AppointmentDate = today.AddDays(1),
            StartTime = new TimeOnly(10, 30),
            EndTime = new TimeOnly(11, 0),
            AppointmentType = "متابعة علاج الجذور",
            Status = AppointmentStatus.Confirmed,
        });
        db.PatientTreatmentPlanSteps.Add(new PatientTreatmentPlanStep
        {
            PatientId = patientId,
            SequenceNumber = 1,
            Title = "علاج السن 36",
            Status = TreatmentStepStatus.InProgress,
        });
        db.OrthoCases.Add(new OrthoCase
        {
            PatientId = patientId,
            CaseNumber = "ORTHO-001",
            ApplianceType = "ثابت",
            StagePercentage = 40,
            Status = OrthoCaseStatus.Active,
        });
        db.SurgeryCases.Add(new SurgeryCase
        {
            PatientId = patientId,
            CaseNumber = "SURG-001",
            SurgeryType = "خلع جراحي",
            Status = SurgeryCaseStatus.Scheduled,
        });
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(user => user.Role).Returns(UserRole.Reception);
        currentUser.SetupGet(user => user.IsAuthenticated).Returns(true);
        var patientAccess = new Mock<IPatientAccessService>();
        patientAccess.SetupGet(access => access.IsDoctor).Returns(false);
        patientAccess.SetupGet(access => access.HasFullAccess).Returns(true);
        var financeRead = new Mock<IFinanceReadService>();

        var controller = new PatientsController(
            service: null!,
            db: db,
            portalService: new Mock<IPatientPortalService>().Object,
            financeReadService: financeRead.Object,
            currentUser: currentUser.Object,
            patientAccess: patientAccess.Object,
            audit: new Mock<IAuditService>().Object,
            logger: NullLogger<PatientsController>.Instance);

        var response = await controller.GetSummary(patientId);

        var summary = response.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<PatientSummaryDto>().Subject;
        summary.LastVisitDate.Should().Be(today.AddDays(-1).ToString("yyyy-MM-dd"));
        summary.LastVisitDoctor.Should().Be("الطبيب المعالج");
        summary.LastVisitDiagnosis.Should().Be("التهاب لب");
        summary.NextAppointmentDate.Should().Be(today.AddDays(1).ToString("yyyy-MM-dd"));
        summary.NextAppointmentTime.Should().Be("10:30");
        summary.NextAppointmentDoctor.Should().Be("الطبيب المعالج");
        summary.NextAppointmentType.Should().Be("متابعة علاج الجذور");
        summary.ChiefComplaint.Should().Be("ألم حاد");
        summary.CurrentDiagnosis.Should().Be("التهاب لب");
        summary.NextPlannedStep.Should().Be("علاج السن 36");
        summary.ActiveOrthoSummary.Should().ContainSingle(item => item.CaseNumber == "ORTHO-001");
        summary.ActiveSurgerySummary.Should().ContainSingle(item => item.CaseNumber == "SURG-001");
        summary.MedicalAlerts.Should().Contain("حساسية أدوية: بنسلين");
        summary.MedicalAlerts.Should().Contain("أمراض مزمنة: سكري");
        summary.MedicalAlerts.Should().Contain("أدوية حالية: ميتفورمين");
        summary.MedicalAlerts.Should().Contain("اضطرابات نزف");
        summary.TotalPaid.Should().BeNull("clinical summary must not bypass finance permissions");
        summary.TotalOutstanding.Should().BeNull();
        financeRead.VerifyNoOtherCalls();
    }
}
''', encoding="utf-8")

for temporary in (
    Path(".github/workflows/core-pat-027-bootstrap.yml"),
    Path("scripts/core_pat_027_patch.py"),
):
    temporary.unlink(missing_ok=True)
