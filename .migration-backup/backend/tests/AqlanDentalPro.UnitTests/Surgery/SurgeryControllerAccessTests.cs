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

namespace AqlanDentalPro.UnitTests.Surgery;

/// <summary>
/// Unit tests for the per-patient access + listing behavior of <see cref="SurgeryController"/>.
/// Covers:
///   - GetById (GET /api/surgery-cases/{id}) — 404 for unknown id, 403 for cross-patient, 200 for own patient.
///   - GetAll (GET /api/surgery-cases) — pagination + filter by doctorId / status / patientId / search.
///   - Delete (DELETE /api/surgery-cases/{id}) — 404, cross-patient 403, soft-delete + cascade.
/// </summary>
public class SurgeryControllerAccessTests : IDisposable
{
    private readonly AppDbContext _db;

    public SurgeryControllerAccessTests()
    {
        _db = SurgeryTestData.CreateDb();
    }

    public void Dispose() => _db.Dispose();

    // ── GetById ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_UnknownId_Returns404_Arabic()
    {
        var controller = BuildControllerAsAdmin();

        var result = await controller.GetById(Guid.NewGuid());

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);
        ExtractMessage(notFound.Value).Should().Be("الحالة الجراحية غير موجودة");
    }

    [Fact]
    public async Task GetById_SamePatient_Returns200()
    {
        var seeded = await SeedSurgeryAsync();

        var accessMock = new Mock<IPatientAccessService>();
        SurgeryTestData.SetupDoctorWithAccess(accessMock, seeded.PatientId);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.OralSurgeon);

        var controller = SurgeryTestData.BuildController(_db, accessMock, currentUserMock);

        var result = await controller.GetById(seeded.SurgeryId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);

        // Verify the response shape — patient + doctor fields, status, etc.
        dynamic payload = ok.Value!;
        ((Guid)payload.Id).Should().Be(seeded.SurgeryId);
        ((string)payload.SurgeryType).Should().Be("خلع ضرس");
        ((string)payload.PatientName).Should().Be("أحمد المريض");
    }

    [Fact]
    public async Task GetById_CrossPatientDoctor_Returns403_Arabic()
    {
        // Doctor B tries to view Doctor A's patient's surgery case.
        var seeded = await SeedSurgeryAsync();

        var accessMock = new Mock<IPatientAccessService>();
        SurgeryTestData.SetupDoctorWithAccess(accessMock); // no accessible patients
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.OralSurgeon);

        var controller = SurgeryTestData.BuildController(_db, accessMock, currentUserMock);

        var result = await controller.GetById(seeded.SurgeryId);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");
    }

    [Fact]
    public async Task GetById_AdminRole_BypassesAccessCheck_Returns200()
    {
        var seeded = await SeedSurgeryAsync();
        var controller = BuildControllerAsAdmin();

        var result = await controller.GetById(seeded.SurgeryId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_SoftDeletedCase_Returns404_Arabic()
    {
        // The ISoftDeletable IsActive global filter must hide soft-deleted cases.
        var seeded = await SeedSurgeryAsync();
        var surgery = await _db.SurgeryCases.FirstAsync(s => s.Id == seeded.SurgeryId);
        surgery.IsActive = false;
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.GetById(seeded.SurgeryId);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);
        ExtractMessage(notFound.Value).Should().Be("الحالة الجراحية غير موجودة");
    }

    // ── GetAll — filtering ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_WithDoctorIdFilter_ReturnsOnlyMatching()
    {
        var (doctorA, _) = await SeedDoctorAsync();
        var (doctorB, _) = await SeedDoctorAsync(name: "د. ب");
        var patientA = SurgeryTestData.BuildPatient();
        var patientB = SurgeryTestData.BuildPatient("سعيد", "علي");
        _db.Patients.AddRange(patientA, patientB);

        var surgeryA = SurgeryTestData.BuildSurgeryCase(patientA.Id, doctorId: doctorA.Id);
        var surgeryB = SurgeryTestData.BuildSurgeryCase(patientB.Id, doctorId: doctorB.Id);
        _db.SurgeryCases.AddRange(surgeryA, surgeryB);
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.GetAll(status: null, doctorId: doctorA.Id, search: null, patientId: null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        var data = (IEnumerable<dynamic>)payload.data;
        data.Should().ContainSingle("only the surgery for doctorA should be returned");
        ((string?)data.First().DoctorName).Should().Be(doctorA.Name,
            "the doctor filter must return only surgeries for the requested doctor");
    }

    [Fact]
    public async Task GetAll_WithStatusFilter_ReturnsOnlyMatching()
    {
        var patient = SurgeryTestData.BuildPatient();
        _db.Patients.Add(patient);
        var scheduled = SurgeryTestData.BuildSurgeryCase(patient.Id, status: SurgeryCaseStatus.Scheduled);
        var completed = SurgeryTestData.BuildSurgeryCase(patient.Id, status: SurgeryCaseStatus.Completed);
        _db.SurgeryCases.AddRange(scheduled, completed);
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.GetAll(status: "completed", doctorId: null, search: null, patientId: null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        var data = (IEnumerable<dynamic>)payload.data;
        data.Should().ContainSingle("only completed surgeries should match the status filter");
        ((string)data.First().Status.ToString()).Should().Be("Completed");
    }

    [Fact]
    public async Task GetAll_WithPatientIdFilter_ReturnsOnlyMatching()
    {
        var patientA = SurgeryTestData.BuildPatient();
        var patientB = SurgeryTestData.BuildPatient("سعيد", "علي");
        _db.Patients.AddRange(patientA, patientB);

        _db.SurgeryCases.AddRange(
            SurgeryTestData.BuildSurgeryCase(patientA.Id),
            SurgeryTestData.BuildSurgeryCase(patientA.Id),
            SurgeryTestData.BuildSurgeryCase(patientB.Id));
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.GetAll(status: null, doctorId: null, search: null, patientId: patientA.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        var data = (IEnumerable<dynamic>)payload.data;
        data.Count().Should().Be(2, "two surgeries belong to patientA");
        foreach (var row in data)
            ((Guid)row.PatientId).Should().Be(patientA.Id);
    }

    [Fact]
    public async Task GetAll_WithSearch_MatchesCaseNumber()
    {
        var patient = SurgeryTestData.BuildPatient();
        _db.Patients.Add(patient);
        var surgery = SurgeryTestData.BuildSurgeryCase(patient.Id);
        surgery.CaseNumber = "SU-2026-999-XYZ";
        _db.SurgeryCases.Add(surgery);
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.GetAll(status: null, doctorId: null, search: "999", patientId: null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        var data = (IEnumerable<dynamic>)payload.data;
        data.Should().ContainSingle();
        ((string)data.First().CaseNumber).Should().Contain("999");
    }

    [Fact]
    public async Task GetAll_WithSearch_MatchesPatientName()
    {
        var patient = SurgeryTestData.BuildPatient("زينب", "الحسيني");
        _db.Patients.Add(patient);
        _db.SurgeryCases.Add(SurgeryTestData.BuildSurgeryCase(patient.Id));
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.GetAll(status: null, doctorId: null, search: "زينب", patientId: null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        var data = (IEnumerable<dynamic>)payload.data;
        data.Should().ContainSingle("search must match the patient's first name");
    }

    [Fact]
    public async Task GetAll_Paginates_WithPageSizeClampedTo100()
    {
        var patient = SurgeryTestData.BuildPatient();
        _db.Patients.Add(patient);
        // Add 5 surgery cases — page 1 with pageSize 2 should return 2 rows + total 5.
        for (var i = 0; i < 5; i++)
            _db.SurgeryCases.Add(SurgeryTestData.BuildSurgeryCase(patient.Id));
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.GetAll(status: null, doctorId: null, search: null, patientId: null, page: 1, pageSize: 2);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        ((int)payload.total).Should().Be(5);
        ((int)payload.page).Should().Be(1);
        ((int)payload.pageSize).Should().Be(2);
        ((IEnumerable<dynamic>)payload.data).Count().Should().Be(2);
    }

    [Fact]
    public async Task GetAll_PageSizeOver100_ClampedTo100()
    {
        var controller = BuildControllerAsAdmin();

        var result = await controller.GetAll(status: null, doctorId: null, search: null, patientId: null, page: 1, pageSize: 500);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        ((int)payload.pageSize).Should().Be(100, "pageSize must be clamped to a maximum of 100");
    }

    [Fact]
    public async Task GetAll_PageSizeUnder1_NormalizedTo1()
    {
        var controller = BuildControllerAsAdmin();

        var result = await controller.GetAll(status: null, doctorId: null, search: null, patientId: null, page: 1, pageSize: 0);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        ((int)payload.pageSize).Should().Be(1, "pageSize must be normalized to 1 when below 1");
    }

    [Fact]
    public async Task GetAll_Empty_ReturnsZeroTotal_AndEmptyData()
    {
        var controller = BuildControllerAsAdmin();

        var result = await controller.GetAll(status: null, doctorId: null, search: null, patientId: null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        ((int)payload.total).Should().Be(0);
        ((IEnumerable<dynamic>)payload.data).Should().BeEmpty();
    }

    // ── Delete ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_UnknownId_Returns404_Arabic()
    {
        var controller = BuildControllerAsAdmin();

        var result = await controller.Delete(Guid.NewGuid());

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);
        ExtractMessage(notFound.Value).Should().Be("الحالة الجراحية غير موجودة");
    }

    [Fact]
    public async Task Delete_CrossPatientDoctor_Returns403_Arabic()
    {
        var seeded = await SeedSurgeryAsync();

        var accessMock = new Mock<IPatientAccessService>();
        SurgeryTestData.SetupDoctorWithAccess(accessMock);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.OralSurgeon);

        var controller = SurgeryTestData.BuildController(_db, accessMock, currentUserMock);

        var result = await controller.Delete(seeded.SurgeryId);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");

        // Nothing should be soft-deleted
        _db.ChangeTracker.Clear();
        var stored = await _db.SurgeryCases.SingleOrDefaultAsync(s => s.Id == seeded.SurgeryId);
        stored.Should().NotBeNull();
        stored!.IsActive.Should().BeTrue("a denied Delete must NOT mutate the persisted entity");
    }

    [Fact]
    public async Task Delete_AdminRole_SoftDeletesCase_AndCascadesToChildren()
    {
        var seeded = await SeedSurgeryAsync();
        // Attach child records that should be cascade-soft-deleted.
        var preop = SurgeryTestData.BuildPreopReport(seeded.SurgeryId);
        var operative = SurgeryTestData.BuildOperativeReport(seeded.SurgeryId);
        var postop = SurgeryTestData.BuildPostopRecord(seeded.SurgeryId);
        var referral = new HospitalReferral
        {
            SurgeryCaseId = seeded.SurgeryId,
            HospitalName = "مستشفى الثورة",
            Status = "pending",
            IsActive = true
        };
        _db.PreopReports.Add(preop);
        _db.OperativeReports.Add(operative);
        _db.PostopRecords.Add(postop);
        _db.HospitalReferrals.Add(referral);
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.Delete(seeded.SurgeryId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ExtractMessage(ok.Value).Should().Be("تم حذف الحالة والسجلات المرتبطة بنجاح");

        _db.ChangeTracker.Clear();

        var surgery = await _db.SurgeryCases.IgnoreQueryFilters().FirstAsync(s => s.Id == seeded.SurgeryId);
        surgery.IsActive.Should().BeFalse("the surgery case must be soft-deleted");
        surgery.DeletedAt.Should().NotBeNull();

        var preopStored = await _db.PreopReports.IgnoreQueryFilters().FirstAsync(p => p.Id == preop.Id);
        preopStored.IsActive.Should().BeFalse("preop report must be cascade-soft-deleted");

        var opStored = await _db.OperativeReports.IgnoreQueryFilters().FirstAsync(o => o.Id == operative.Id);
        opStored.IsActive.Should().BeFalse("operative report must be cascade-soft-deleted");

        var postopStored = await _db.PostopRecords.IgnoreQueryFilters().FirstAsync(p => p.Id == postop.Id);
        postopStored.IsActive.Should().BeFalse("postop record must be cascade-soft-deleted");

        var referralStored = await _db.HospitalReferrals.IgnoreQueryFilters().FirstAsync(r => r.Id == referral.Id);
        referralStored.IsActive.Should().BeFalse("hospital referral must be cascade-soft-deleted");
    }

    // ── Update (PUT /api/surgery-cases/{id}) — basic happy + 404 ─────────────────

    [Fact]
    public async Task Update_UnknownId_Returns404_Arabic()
    {
        var controller = BuildControllerAsAdmin();

        var result = await controller.Update(Guid.NewGuid(), new UpdateSurgeryCaseRequest
        {
            SurgeryType = "new type"
        });

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        ExtractMessage(notFound.Value).Should().Be("الحالة الجراحية غير موجودة");
    }

    [Fact]
    public async Task Update_AdminRole_UpdatesFields_AndPersists()
    {
        var seeded = await SeedSurgeryAsync();
        var (doctor, _) = await SeedDoctorAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.Update(seeded.SurgeryId, new UpdateSurgeryCaseRequest
        {
            DoctorId = doctor.Id,
            SurgeryType = "زراعة ضرس",
            TeethInvolved = "11,21"
        });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ExtractMessage(ok.Value).Should().Be("تم التحديث");

        _db.ChangeTracker.Clear();
        var stored = await _db.SurgeryCases.FirstAsync(s => s.Id == seeded.SurgeryId);
        stored.DoctorId.Should().Be(doctor.Id);
        stored.SurgeryType.Should().Be("زراعة ضرس");
        stored.TeethInvolved.Should().Be("11,21");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<(Guid SurgeryId, Guid PatientId)> SeedSurgeryAsync()
    {
        var patient = SurgeryTestData.BuildPatient();
        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();

        var surgery = SurgeryTestData.BuildSurgeryCase(patient.Id);
        _db.SurgeryCases.Add(surgery);
        await _db.SaveChangesAsync();

        return (surgery.Id, patient.Id);
    }

    private async Task<(Doctor Doctor, User User)> SeedDoctorAsync(string name = "د. جراح")
    {
        var (user, doctor) = SurgeryTestData.BuildDoctor(name: name);
        _db.Users.Add(user);
        _db.Doctors.Add(doctor);
        await _db.SaveChangesAsync();
        return (doctor, user);
    }

    private SurgeryController BuildControllerAsAdmin()
    {
        var accessMock = new Mock<IPatientAccessService>();
        SurgeryTestData.SetupNonDoctor(accessMock);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.Admin);
        currentUserMock.SetupGet(c => c.IsAdmin).Returns(true);
        return SurgeryTestData.BuildController(_db, accessMock, currentUserMock);
    }

    private static string ExtractMessage(object? value)
    {
        value.Should().NotBeNull("the 4xx response must carry a payload");
        var prop = value!.GetType().GetProperty("message");
        prop.Should().NotBeNull("the 4xx response must carry a 'message' field (Arabic)");
        return (string)prop!.GetValue(value)!;
    }
}
