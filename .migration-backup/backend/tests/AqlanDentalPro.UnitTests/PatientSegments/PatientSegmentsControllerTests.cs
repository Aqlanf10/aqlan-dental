using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.PatientSegments;

/// <summary>
/// YOLO-S5: PatientSegmentsController tests.
///
/// Verifies:
///   - GetList always returns exactly 4 built-in dynamic segments with the
///     stable PatientSegmentBuiltInKeys (computed counts come back as 0 on an
///     empty database — the math itself is exercised by integration tests).
///   - Custom segment CRUD: Create → GetList shows it → Delete hides it.
///   - AddMember on a custom segment persists a row; duplicate add is rejected.
///   - AddMember on a built-in (read-only) segment is rejected with 400.
///   - RemoveMember soft-deletes the member (IsActive = false).
///
/// Uses EF Core InMemory — no live PostgreSQL required.
/// </summary>
public class PatientSegmentsControllerTests
{
    private static AppDbContext CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        // InMemory does not apply OnModelCreating configurations, so ensure
        // the soft-delete query filters are at least registered. AppDbContext
        // already calls ApplyConfigurationsFromAssembly in OnModelCreating —
        // for InMemory the filter convention works off ISoftDeletable.
        return db;
    }

    private static PatientSegmentsController CreateController(AppDbContext? db = null)
    {
        db ??= CreateDb();
        return new PatientSegmentsController(db);
    }

    private static Patient SeedPatient(AppDbContext db, string patientNumber = "P-001")
    {
        var p = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = patientNumber,
            FirstName = "أحمد",
            MiddleName = "محمد",
            LastName = "العقلي",
            Phone = "967770000000",
            IsActive = true,
        };
        db.Patients.Add(p);
        db.SaveChanges();
        return p;
    }

    // ── GetList: built-in segments ────────────────────────────────────────────

    [Fact]
    public async Task GetList_AlwaysReturns_FourBuiltInDynamicSegments()
    {
        var controller = CreateController();

        var result = await controller.GetList();

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        dynamic payload = ok.Value!;
        // The payload is { builtIn: [...], custom: [...] }.
        // We use reflection / dynamic because the DTO is anonymous.
        var builtIn = (IEnumerable<object>)payload.GetType().GetProperty("builtIn")!.GetValue(payload)!;

        builtIn.Count().Should().Be(4);
    }

    [Fact]
    public async Task GetList_BuiltInSegments_HaveStableKeysAndArabicNames()
    {
        var controller = CreateController();

        var result = await controller.GetList();
        var ok = (OkObjectResult)result;
        dynamic payload = ok.Value!;
        var builtIn = (IEnumerable<object>)payload.GetType().GetProperty("builtIn")!.GetValue(payload)!;

        // Extract Key + Name via reflection (anonymous type).
        var keys = builtIn.Select(b => (string)b.GetType().GetProperty("Key")!.GetValue(b)!).ToList();
        var names = builtIn.Select(b => (string)b.GetType().GetProperty("Name")!.GetValue(b)!).ToList();

        keys.Should().Contain(PatientSegmentBuiltInKeys.OrthoOverdue);
        keys.Should().Contain(PatientSegmentBuiltInKeys.OutstandingBalance);
        keys.Should().Contain(PatientSegmentBuiltInKeys.NoRecentVisit);
        keys.Should().Contain(PatientSegmentBuiltInKeys.LabReady);

        // Arabic labels — owner cares about RTL quality.
        names.Should().Contain("مرضى تقويم متأخرون");
        names.Should().Contain("مرضى عليهم مبالغ");
        names.Should().Contain("مرضى لم يحضروا");
        names.Should().Contain("مرضى المختبر الجاهز");
    }

    [Fact]
    public async Task GetList_BuiltInSegments_AreFlaggedDynamicAndBuiltIn()
    {
        var controller = CreateController();

        var result = await controller.GetList();
        var ok = (OkObjectResult)result;
        dynamic payload = ok.Value!;
        var builtIn = (IEnumerable<object>)payload.GetType().GetProperty("builtIn")!.GetValue(payload)!;

        foreach (var b in builtIn)
        {
            var isDynamic = (bool)b.GetType().GetProperty("IsDynamic")!.GetValue(b)!;
            var isBuiltIn = (bool)b.GetType().GetProperty("IsBuiltIn")!.GetValue(b)!;
            isDynamic.Should().BeTrue("built-in segments are dynamic by definition");
            isBuiltIn.Should().BeTrue();
        }
    }

    [Fact]
    public async Task GetList_EmptyDatabase_ReturnsZeroMemberCountsForBuiltIns()
    {
        var controller = CreateController();

        var result = await controller.GetList();
        var ok = (OkObjectResult)result;
        dynamic payload = ok.Value!;
        var builtIn = (IEnumerable<object>)payload.GetType().GetProperty("builtIn")!.GetValue(payload)!;

        foreach (var b in builtIn)
        {
            var count = (int)b.GetType().GetProperty("MemberCount")!.GetValue(b)!;
            count.Should().Be(0, "no patients exist in the database");
        }
    }

    // ── Custom segment CRUD ───────────────────────────────────────────────────

    [Fact]
    public async Task Create_CustomSegment_PersistsAndAppearsInGetList()
    {
        var db = CreateDb();
        var controller = CreateController(db);

        var createResult = await controller.Create(new CreatePatientSegmentRequest
        {
            Name = "متابعة شهرية",
            Description = "المرضى الذين يحتاجون متابعة شهرية",
            Color = "#3d7ab5",
        });

        createResult.Should().BeOfType<CreatedAtActionResult>();
        var created = (CreatedAtActionResult)createResult;
        dynamic createdPayload = created.Value!;
        var segmentId = (Guid)createdPayload.GetType().GetProperty("Id")!.GetValue(createdPayload)!;
        segmentId.Should().NotBe(Guid.Empty);

        // Verify it shows up in GetList
        var listResult = await controller.GetList();
        var ok = (OkObjectResult)listResult;
        dynamic payload = ok.Value!;
        var custom = (IEnumerable<object>)payload.GetType().GetProperty("custom")!.GetValue(payload)!;

        custom.Count().Should().Be(1);
        var first = custom.Single();
        ((string)first.GetType().GetProperty("Name")!.GetValue(first)!).Should().Be("متابعة شهرية");
        ((bool)first.GetType().GetProperty("IsBuiltIn")!.GetValue(first)!).Should().BeFalse();
        ((bool)first.GetType().GetProperty("IsDynamic")!.GetValue(first)!).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_CustomSegment_SoftDeletesAndHidesFromGetList()
    {
        var db = CreateDb();
        var controller = CreateController(db);

        var createResult = await controller.Create(new CreatePatientSegmentRequest { Name = "مجموعة مؤقتة" });
        var created = (CreatedAtActionResult)createResult;
        dynamic createdPayload = created.Value!;
        var segmentId = (Guid)createdPayload.GetType().GetProperty("Id")!.GetValue(createdPayload)!;

        var deleteResult = await controller.Delete(segmentId);
        deleteResult.Should().BeOfType<OkObjectResult>();

        // Soft-delete sets IsActive = false; the query filter should exclude it.
        var softDeleted = await db.PatientSegments.IgnoreQueryFilters().FirstAsync(s => s.Id == segmentId);
        softDeleted.IsActive.Should().BeFalse();

        var listResult = await controller.GetList();
        var ok = (OkObjectResult)listResult;
        dynamic payload = ok.Value!;
        var custom = (IEnumerable<object>)payload.GetType().GetProperty("custom")!.GetValue(payload)!;
        custom.Count().Should().Be(0, "soft-deleted segment should be excluded from GetList");
    }

    [Fact]
    public async Task Delete_NonExistentSegment_Returns404()
    {
        var controller = CreateController();

        var result = await controller.Delete(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── AddMember / RemoveMember ──────────────────────────────────────────────

    [Fact]
    public async Task AddMember_CustomSegment_PersistsMemberRow()
    {
        var db = CreateDb();
        var controller = CreateController(db);
        var patient = SeedPatient(db);

        var createResult = await controller.Create(new CreatePatientSegmentRequest { Name = "متابعة" });
        dynamic createdPayload = ((CreatedAtActionResult)createResult).Value!;
        var segmentId = (Guid)createdPayload.GetType().GetProperty("Id")!.GetValue(createdPayload)!;

        var addResult = await controller.AddMember(segmentId, new AddSegmentMemberRequest { PatientId = patient.Id });

        addResult.Should().BeOfType<OkObjectResult>();
        var member = await db.PatientSegmentMembers.FirstAsync(m => m.SegmentId == segmentId);
        member.PatientId.Should().Be(patient.Id);
        member.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task AddMember_DuplicatePatient_Returns400WithArabicMessage()
    {
        var db = CreateDb();
        var controller = CreateController(db);
        var patient = SeedPatient(db);

        var createResult = await controller.Create(new CreatePatientSegmentRequest { Name = "متابعة" });
        dynamic createdPayload = ((CreatedAtActionResult)createResult).Value!;
        var segmentId = (Guid)createdPayload.GetType().GetProperty("Id")!.GetValue(createdPayload)!;

        await controller.AddMember(segmentId, new AddSegmentMemberRequest { PatientId = patient.Id });
        var second = await controller.AddMember(segmentId, new AddSegmentMemberRequest { PatientId = patient.Id });

        second.Should().BeOfType<BadRequestObjectResult>();
        var bad = (BadRequestObjectResult)second;
        dynamic msg = bad.Value!;
        var message = (string)msg.GetType().GetProperty("message")!.GetValue(msg)!;
        message.Should().Contain("موجود مسبقاً", "Arabic error message required by CLAUDE.md");
    }

    [Fact]
    public async Task AddMember_NonExistentPatient_Returns404()
    {
        var db = CreateDb();
        var controller = CreateController(db);

        var createResult = await controller.Create(new CreatePatientSegmentRequest { Name = "متابعة" });
        dynamic createdPayload = ((CreatedAtActionResult)createResult).Value!;
        var segmentId = (Guid)createdPayload.GetType().GetProperty("Id")!.GetValue(createdPayload)!;

        var result = await controller.AddMember(segmentId, new AddSegmentMemberRequest { PatientId = Guid.NewGuid() });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AddMember_NonExistentSegment_Returns404()
    {
        var controller = CreateController();

        var result = await controller.AddMember(Guid.NewGuid(), new AddSegmentMemberRequest { PatientId = Guid.NewGuid() });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RemoveMember_SoftDeletesMemberRow()
    {
        var db = CreateDb();
        var controller = CreateController(db);
        var patient = SeedPatient(db);

        var createResult = await controller.Create(new CreatePatientSegmentRequest { Name = "متابعة" });
        dynamic createdPayload = ((CreatedAtActionResult)createResult).Value!;
        var segmentId = (Guid)createdPayload.GetType().GetProperty("Id")!.GetValue(createdPayload)!;

        await controller.AddMember(segmentId, new AddSegmentMemberRequest { PatientId = patient.Id });
        var removeResult = await controller.RemoveMember(segmentId, patient.Id);

        removeResult.Should().BeOfType<OkObjectResult>();
        var member = await db.PatientSegmentMembers.IgnoreQueryFilters().FirstAsync(m => m.SegmentId == segmentId);
        member.IsActive.Should().BeFalse("RemoveMember soft-deletes the row (per CLAUDE.md — no hard deletes)");
    }

    [Fact]
    public async Task RemoveMember_NonExistentMember_Returns404()
    {
        var db = CreateDb();
        var controller = CreateController(db);
        var patient = SeedPatient(db);

        var createResult = await controller.Create(new CreatePatientSegmentRequest { Name = "متابعة" });
        dynamic createdPayload = ((CreatedAtActionResult)createResult).Value!;
        var segmentId = (Guid)createdPayload.GetType().GetProperty("Id")!.GetValue(createdPayload)!;

        var result = await controller.RemoveMember(segmentId, patient.Id);
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── GetMembers ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMembers_CustomSegment_ReturnsMemberListWithPatientInfo()
    {
        var db = CreateDb();
        var controller = CreateController(db);
        var patient = SeedPatient(db, "P-042");

        var createResult = await controller.Create(new CreatePatientSegmentRequest { Name = "متابعة" });
        dynamic createdPayload = ((CreatedAtActionResult)createResult).Value!;
        var segmentId = (Guid)createdPayload.GetType().GetProperty("Id")!.GetValue(createdPayload)!;

        await controller.AddMember(segmentId, new AddSegmentMemberRequest { PatientId = patient.Id });

        var membersResult = await controller.GetMembers(segmentId.ToString());
        membersResult.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)membersResult;
        var members = (IEnumerable<object>)ok.Value!;
        members.Count().Should().Be(1);

        var m = members.Single();
        var patientNumber = (string)m.GetType().GetProperty("PatientNumber")!.GetValue(m)!;
        patientNumber.Should().Be("P-042");
    }

    [Fact]
    public async Task GetMembers_BuiltInSegment_ReturnsComputedMembers()
    {
        var controller = CreateController();

        // Built-in segments are computed in code — no stored members.
        var membersResult = await controller.GetMembers(PatientSegmentBuiltInKeys.LabReady);

        membersResult.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)membersResult;
        var members = (IEnumerable<object>)ok.Value!;
        members.Should().BeEmpty("no LabOrders exist on an empty database");
    }

    [Fact]
    public async Task GetMembers_InvalidKey_Returns400()
    {
        var controller = CreateController();

        // Not a built-in key, not a parseable GUID.
        var result = await controller.GetMembers("not-a-valid-key");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetMembers_UnknownCustomSegmentId_Returns404()
    {
        var controller = CreateController();

        var result = await controller.GetMembers(Guid.NewGuid().ToString());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── Built-in segment computation smoke tests ──────────────────────────────

    [Fact]
    public async Task GetMembers_OrthoOverdue_ReturnsPatientsWithOverdueNextAppointment()
    {
        var db = CreateDb();
        var controller = CreateController(db);

        // Seed an active OrthoCase with a latest OrthoVisit whose
        // NextAppointmentDate is in the past → should appear in the segment.
        var patient = SeedPatient(db, "P-OV1");
        var orthoCase = new OrthoCase
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Status = OrthoCaseStatus.Active,
            IsActive = true,
        };
        orthoCase.Visits.Add(new OrthoVisit
        {
            Id = Guid.NewGuid(),
            OrthoCaseId = orthoCase.Id,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            NextAppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)), // overdue
            IsActive = true,
        });
        db.OrthoCases.Add(orthoCase);
        await db.SaveChangesAsync();

        var membersResult = await controller.GetMembers(PatientSegmentBuiltInKeys.OrthoOverdue);
        var ok = (OkObjectResult)membersResult;
        var members = (IEnumerable<object>)ok.Value!;
        members.Count().Should().Be(1);
    }

    // ── CORE-PAT-049: cancelled contracts must not count as outstanding debt ──

    [Fact]
    public async Task GetList_OutstandingBalance_ExcludesCancelledContracts()
    {
        var db = CreateDb();
        var controller = CreateController(db);
        var patient = SeedPatient(db, "P-CANC1");

        // A cancelled treatment plan is not an obligation (CORE-PAT-012) — must
        // not inflate the "مرضى عليهم مبالغ" segment count, same as
        // FinanceReadService.GetPatientFinanceSummaryAsync already excludes it.
        db.Contracts.Add(new Contract
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            TotalAmount = 50_000m,
            Status = ContractStatus.Cancelled,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var result = await controller.GetList();
        var ok = (OkObjectResult)result;
        dynamic payload = ok.Value!;
        var builtIn = (IEnumerable<object>)payload.GetType().GetProperty("builtIn")!.GetValue(payload)!;

        var outstanding = builtIn.Single(b =>
            (string)b.GetType().GetProperty("Key")!.GetValue(b)! == PatientSegmentBuiltInKeys.OutstandingBalance);
        var count = (int)outstanding.GetType().GetProperty("MemberCount")!.GetValue(outstanding)!;
        count.Should().Be(0, "a cancelled contract is not an outstanding obligation");
    }

    [Fact]
    public async Task GetMembers_OutstandingBalance_ExcludesCancelledContracts()
    {
        var db = CreateDb();
        var controller = CreateController(db);
        var patient = SeedPatient(db, "P-CANC2");

        db.Contracts.Add(new Contract
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            TotalAmount = 50_000m,
            Status = ContractStatus.Cancelled,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var membersResult = await controller.GetMembers(PatientSegmentBuiltInKeys.OutstandingBalance);
        var ok = (OkObjectResult)membersResult;
        var members = (IEnumerable<object>)ok.Value!;
        members.Should().BeEmpty("a patient whose only contract is cancelled owes nothing and must not appear on the collections list");
    }

    [Fact]
    public async Task GetList_OutstandingBalance_StillCountsActiveContractDebt()
    {
        var db = CreateDb();
        var controller = CreateController(db);
        var patient = SeedPatient(db, "P-ACTIVE1");

        db.Contracts.Add(new Contract
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            TotalAmount = 50_000m,
            Status = ContractStatus.Active,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var result = await controller.GetList();
        var ok = (OkObjectResult)result;
        dynamic payload = ok.Value!;
        var builtIn = (IEnumerable<object>)payload.GetType().GetProperty("builtIn")!.GetValue(payload)!;

        var outstanding = builtIn.Single(b =>
            (string)b.GetType().GetProperty("Key")!.GetValue(b)! == PatientSegmentBuiltInKeys.OutstandingBalance);
        var count = (int)outstanding.GetType().GetProperty("MemberCount")!.GetValue(outstanding)!;
        count.Should().Be(1, "an active contract with no payments is genuine outstanding debt");
    }

    [Fact]
    public async Task GetMembers_LabReady_ReturnsPatientsWithReadyLabOrders()
    {
        var db = CreateDb();
        var controller = CreateController(db);

        var patient = SeedPatient(db, "P-LR1");
        db.LabOrders.Add(new LabOrder
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Status = "Ready",
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var membersResult = await controller.GetMembers(PatientSegmentBuiltInKeys.LabReady);
        var ok = (OkObjectResult)membersResult;
        var members = (IEnumerable<object>)ok.Value!;
        members.Count().Should().Be(1);
    }
}
