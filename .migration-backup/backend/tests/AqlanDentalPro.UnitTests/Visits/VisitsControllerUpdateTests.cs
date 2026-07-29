using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Visits;

/// <summary>
/// Unit tests for <see cref="VisitsController.UpdateVisit"/> (PUT /api/visits/{id}).
///
/// Coverage map:
///   - 404 for unknown id (Arabic)
///   - 400 for soft-deleted visit (Arabic — "لا يمكن تعديل زيارة محذوفة")
///   - 403 for cross-patient doctor (no mutation)
///   - Happy-path update: scalar fields persisted, UpdatedAt refreshed
///   - Date-format validation: bad VisitDate / NextVisitDate → 400 Arabic
///   - S1 structured clinical fields: ExtraoralExamination / IntraoralExamination /
///     AdditionalServicesText / HandoffNotes appended to ClinicalNotes with Arabic labels
///     (prevents interim-save data loss — same pattern as PatientJourneyController.HandoffToReception)
/// </summary>
public class VisitsControllerUpdateTests : IDisposable
{
    private readonly AppDbContext _db;

    public VisitsControllerUpdateTests()
    {
        _db = VisitsTestData.CreateDb();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task UpdateVisit_UnknownId_Returns404_Arabic()
    {
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateVisit(Guid.NewGuid(), new UpdateVisitRequest
        {
            VisitType = "متابعة"
        });

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);
        ExtractMessage(notFound.Value).Should().Be("الزيارة غير موجودة");
    }

    [Fact]
    public async Task UpdateVisit_OnSoftDeletedVisit_Returns400_Arabic()
    {
        var seeded = await SeedVisitAsync();
        var visit = await _db.Visits.FirstAsync(v => v.Id == seeded.VisitId);
        visit.IsActive = false;
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateVisit(seeded.VisitId, new UpdateVisitRequest
        {
            VisitType = "متابعة"
        });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(400);
        ExtractMessage(bad.Value).Should().Be("لا يمكن تعديل زيارة محذوفة");
    }

    [Fact]
    public async Task UpdateVisit_CrossPatientDoctor_Returns403_Arabic_DoesNotMutate()
    {
        var seeded = await SeedVisitAsync();
        var originalNotes = (await _db.Visits.FirstAsync(v => v.Id == seeded.VisitId)).ClinicalNotes;

        var accessMock = new Mock<IPatientAccessService>();
        VisitsTestData.SetupDoctorWithAccess(accessMock); // no accessible patients
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.GeneralDentist);

        var controller = VisitsTestData.BuildController(_db, accessMock, currentUserMock);

        var result = await controller.UpdateVisit(seeded.VisitId, new UpdateVisitRequest
        {
            ClinicalNotes = "هذه الملاحظات يجب ألا تُكتب"
        });

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");

        _db.ChangeTracker.Clear();
        var stored = await _db.Visits.FirstAsync(v => v.Id == seeded.VisitId);
        stored.ClinicalNotes.Should().Be(originalNotes,
            "a denied Update must NOT mutate the persisted entity");
    }

    [Fact]
    public async Task UpdateVisit_AdminRole_UpdatesScalarFields_AndPersists()
    {
        var seeded = await SeedVisitAsync();
        var (user, doctor) = VisitsTestData.BuildDoctor(name: "د. سالم");
        _db.Users.Add(user);
        _db.Doctors.Add(doctor);
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateVisit(seeded.VisitId, new UpdateVisitRequest
        {
            VisitDate = "2026-07-01",
            VisitType = "متابعة",
            Specialty = "Orthodontics",
            DoctorId = doctor.Id,
            ChiefComplaint = "ألم جديد",
            ClinicalNotes = "ملاحظات محدثة",
            TreatmentDone = "تغيير سلك",
            Diagnosis = "تقويم",
            Instructions = "مراجعة بعد أسبوع",
            NextVisitPlan = "تبديل سلك",
            Cost = 10000m,
            NextVisitDate = "2026-07-08"
        });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ExtractMessage(ok.Value).Should().Be("تم تحديث الزيارة بنجاح");

        _db.ChangeTracker.Clear();
        var stored = await _db.Visits.FirstAsync(v => v.Id == seeded.VisitId);
        stored.VisitDate.Should().Be(new DateOnly(2026, 7, 1));
        stored.VisitType.Should().Be("متابعة");
        stored.Specialty.Should().Be(Specialty.Orthodontics);
        stored.DoctorId.Should().Be(doctor.Id);
        stored.ChiefComplaint.Should().Be("ألم جديد");
        stored.ClinicalNotes.Should().Be("ملاحظات محدثة");
        stored.TreatmentDone.Should().Be("تغيير سلك");
        stored.Diagnosis.Should().Be("تقويم");
        stored.Instructions.Should().Be("مراجعة بعد أسبوع");
        stored.NextVisitPlan.Should().Be("تبديل سلك");
        stored.Cost.Should().Be(10000m);
        stored.NextVisitDate.Should().Be(new DateOnly(2026, 7, 8));
        stored.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5),
            "UpdatedAt must be refreshed to ~now on every successful Update");
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("2026-13-45")]
    public async Task UpdateVisit_InvalidVisitDate_Returns400_Arabic(string badDate)
    {
        var seeded = await SeedVisitAsync();
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateVisit(seeded.VisitId, new UpdateVisitRequest
        {
            VisitDate = badDate
        });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(400);
        ExtractMessage(bad.Value).Should().Be("تنسيق تاريخ الزيارة غير صالح. استخدم YYYY-MM-DD");
    }

    [Fact]
    public async Task UpdateVisit_InvalidNextVisitDate_Returns400_Arabic()
    {
        var seeded = await SeedVisitAsync();
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateVisit(seeded.VisitId, new UpdateVisitRequest
        {
            NextVisitDate = "garbage"
        });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(400);
        ExtractMessage(bad.Value).Should().Be("تنسيق تاريخ الزيارة القادمة غير صالح. استخدم YYYY-MM-DD");
    }

    [Fact]
    public async Task UpdateVisit_AppendsStructuredClinicalFields_WithArabicLabels()
    {
        // S1 fix: ExtraoralExamination / IntraoralExamination / AdditionalServicesText /
        // HandoffNotes are appended to ClinicalNotes with Arabic labels, separated by " | ".
        // This prevents interim-save data loss when the doctor fills the structured form
        // before formally closing the visit.
        var seeded = await SeedVisitAsync();
        var originalNotes = (await _db.Visits.FirstAsync(v => v.Id == seeded.VisitId)).ClinicalNotes;

        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateVisit(seeded.VisitId, new UpdateVisitRequest
        {
            ExtraoralExamination = "تناظر طبيعي",
            IntraoralExamination = "تسوس في 36",
            AdditionalServicesText = "تنظيف جير",
            HandoffNotes = "يحتاج موعد متابعة"
        });

        result.Should().BeOfType<OkObjectResult>();

        _db.ChangeTracker.Clear();
        var stored = await _db.Visits.FirstAsync(v => v.Id == seeded.VisitId);
        stored.ClinicalNotes.Should().Contain("[فحص خارج الفم] تناظر طبيعي");
        stored.ClinicalNotes.Should().Contain("[فحص داخل الفم] تسوس في 36");
        stored.ClinicalNotes.Should().Contain("[خدمات إضافية] تنظيف جير");
        stored.ClinicalNotes.Should().Contain("[ملاحظات التسليم] يحتاج موعد متابعة");
        stored.ClinicalNotes.Should().Contain(" | ",
            "the structured fields must be joined with the Arabic pipe separator");
        // The original notes must be preserved (appended, not replaced)
        if (!string.IsNullOrWhiteSpace(originalNotes))
            stored.ClinicalNotes.Should().StartWith(originalNotes,
                "existing ClinicalNotes must be preserved — structured fields are APPENDED");
    }

    [Fact]
    public async Task UpdateVisit_StructuredFieldsOnly_WhenClinicalNotesWereNull()
    {
        // Edge case: a visit with no prior ClinicalNotes receiving only structured fields
        // must end up with the structured block (no leading separator).
        var seeded = await SeedVisitAsync();
        var visit = await _db.Visits.FirstAsync(v => v.Id == seeded.VisitId);
        visit.ClinicalNotes = null;
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateVisit(seeded.VisitId, new UpdateVisitRequest
        {
            ExtraoralExamination = "طبيعي",
            HandoffNotes = "متابعة"
        });

        result.Should().BeOfType<OkObjectResult>();

        _db.ChangeTracker.Clear();
        var stored = await _db.Visits.FirstAsync(v => v.Id == seeded.VisitId);
        stored.ClinicalNotes.Should().NotStartWith(" | ",
            "when the visit had no prior ClinicalNotes, the appended block must not begin with a separator");
        stored.ClinicalNotes.Should().Contain("[فحص خارج الفم] طبيعي");
        stored.ClinicalNotes.Should().Contain("[ملاحظات التسليم] متابعة");
    }

    [Fact]
    public async Task UpdateVisit_NullFields_DoNotOverwriteExistingValues()
    {
        // PATCH-like semantics: null fields in the request must NOT clear the persisted value.
        var seeded = await SeedVisitAsync();
        var original = await _db.Visits.FirstAsync(v => v.Id == seeded.VisitId);
        var originalNotes = original.ClinicalNotes;
        var originalDiagnosis = original.Diagnosis;

        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateVisit(seeded.VisitId, new UpdateVisitRequest
        {
            VisitType = "متابعة"
            // Everything else is null — must be ignored
        });

        result.Should().BeOfType<OkObjectResult>();

        _db.ChangeTracker.Clear();
        var stored = await _db.Visits.FirstAsync(v => v.Id == seeded.VisitId);
        stored.VisitType.Should().Be("متابعة");
        stored.ClinicalNotes.Should().Be(originalNotes,
            "a null ClinicalNotes in the request must NOT clear the existing value");
        stored.Diagnosis.Should().Be(originalDiagnosis,
            "a null Diagnosis in the request must NOT clear the existing value");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<(Guid VisitId, Guid PatientId)> SeedVisitAsync()
    {
        var patient = VisitsTestData.BuildPatient();
        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();

        var visit = VisitsTestData.BuildVisit(patient.Id);
        _db.Visits.Add(visit);
        await _db.SaveChangesAsync();

        return (visit.Id, patient.Id);
    }

    private VisitsController BuildControllerAsAdmin()
    {
        var accessMock = new Mock<IPatientAccessService>();
        VisitsTestData.SetupNonDoctor(accessMock);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.Admin);
        currentUserMock.SetupGet(c => c.IsAdmin).Returns(true);
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        return VisitsTestData.BuildController(_db, accessMock, currentUserMock);
    }

    private static string ExtractMessage(object? value)
    {
        value.Should().NotBeNull("the 4xx response must carry a payload");
        var prop = value!.GetType().GetProperty("message");
        prop.Should().NotBeNull("the 4xx response must carry a 'message' field (Arabic)");
        return (string)prop!.GetValue(value)!;
    }
}
