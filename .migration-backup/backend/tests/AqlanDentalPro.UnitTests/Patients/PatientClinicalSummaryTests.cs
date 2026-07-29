using AqlanDentalPro.API.Controllers;
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
