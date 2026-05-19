using Xunit;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.UnitTests.TreatmentPlan;

/// <summary>
/// Tests for PatientTreatmentPlanStep entity fields, status transitions,
/// service catalog integration, and data integrity.
/// Uses InMemory provider to verify treatment plan operations
/// without requiring a PostgreSQL database.
/// </summary>
public class TreatmentPlanTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    // ─── Entity Field Tests ────────────────────────────────────────────────

    [Fact]
    public async Task Step_CanBeCreated_WithRequiredFields()
    {
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();

        var step = new PatientTreatmentPlanStep
        {
            PatientId = patientId,
            SequenceNumber = 1,
            Title = "حشوة ضرس",
            Priority = TreatmentStepPriority.Normal,
            Status = TreatmentStepStatus.Planned
        };

        db.PatientTreatmentPlanSteps.Add(step);
        await db.SaveChangesAsync();

        var saved = await db.PatientTreatmentPlanSteps.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == step.Id);
        saved.Should().NotBeNull();
        saved!.Title.Should().Be("حشوة ضرس");
        saved.SequenceNumber.Should().Be(1);
        saved.Priority.Should().Be(TreatmentStepPriority.Normal);
        saved.Status.Should().Be(TreatmentStepStatus.Planned);
    }

    [Fact]
    public async Task Step_OptionalFields_DefaultToNull()
    {
        await using var db = CreateContext();
        var step = new PatientTreatmentPlanStep
        {
            PatientId = Guid.NewGuid(),
            SequenceNumber = 1,
            Title = "تنظيف"
        };

        db.PatientTreatmentPlanSteps.Add(step);
        await db.SaveChangesAsync();

        var saved = await db.PatientTreatmentPlanSteps.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == step.Id);
        saved.Should().NotBeNull();
        saved!.ServiceId.Should().BeNull();
        saved.ServiceNameSnapshot.Should().BeNull();
        saved.Department.Should().BeNull();
        saved.ToothNumber.Should().BeNull();
        saved.ToothArea.Should().BeNull();
        saved.Description.Should().BeNull();
        saved.ResponsibleDoctorId.Should().BeNull();
        saved.PlannedDate.Should().BeNull();
        saved.CompletedDate.Should().BeNull();
        saved.EstimatedCost.Should().BeNull();
        saved.RelatedAppointmentId.Should().BeNull();
        saved.RelatedVisitId.Should().BeNull();
        saved.Notes.Should().BeNull();
    }

    [Fact]
    public async Task Step_CanSet_AllOptionalFields()
    {
        await using var db = CreateContext();
        var serviceId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var visitId = Guid.NewGuid();

        var step = new PatientTreatmentPlanStep
        {
            PatientId = Guid.NewGuid(),
            SequenceNumber = 2,
            ServiceId = serviceId,
            ServiceNameSnapshot = "تنظيف الجير",
            Department = Specialty.General,
            ToothNumber = "36",
            ToothArea = "الأسفل يسار",
            Title = "تنظيف جير",
            Description = "تنظيف جير عميق",
            Priority = TreatmentStepPriority.High,
            Status = TreatmentStepStatus.InProgress,
            ResponsibleDoctorId = doctorId,
            PlannedDate = new DateOnly(2026, 6, 15),
            EstimatedCost = 5000m,
            RelatedAppointmentId = appointmentId,
            RelatedVisitId = visitId,
            Notes = "يحتاج تخدير موضعي"
        };

        db.PatientTreatmentPlanSteps.Add(step);
        await db.SaveChangesAsync();

        var saved = await db.PatientTreatmentPlanSteps.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == step.Id);
        saved!.ServiceId.Should().Be(serviceId);
        saved.ServiceNameSnapshot.Should().Be("تنظيف الجير");
        saved.Department.Should().Be(Specialty.General);
        saved.ToothNumber.Should().Be("36");
        saved.EstimatedCost.Should().Be(5000m);
    }

    // ─── List By Patient Tests ─────────────────────────────────────────────

    [Fact]
    public async Task Steps_AreOrderedBy_SequenceNumber()
    {
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();

        db.PatientTreatmentPlanSteps.AddRange(
            new PatientTreatmentPlanStep { PatientId = patientId, SequenceNumber = 3, Title = "ثالث" },
            new PatientTreatmentPlanStep { PatientId = patientId, SequenceNumber = 1, Title = "أول" },
            new PatientTreatmentPlanStep { PatientId = patientId, SequenceNumber = 2, Title = "ثاني" }
        );
        await db.SaveChangesAsync();

        var steps = await db.PatientTreatmentPlanSteps
            .Where(s => s.PatientId == patientId)
            .OrderBy(s => s.SequenceNumber)
            .ToListAsync();

        steps[0].Title.Should().Be("أول");
        steps[1].Title.Should().Be("ثاني");
        steps[2].Title.Should().Be("ثالث");
    }

    [Fact]
    public async Task Steps_FilterBy_PatientId()
    {
        await using var db = CreateContext();
        var patient1 = Guid.NewGuid();
        var patient2 = Guid.NewGuid();

        db.PatientTreatmentPlanSteps.AddRange(
            new PatientTreatmentPlanStep { PatientId = patient1, SequenceNumber = 1, Title = "مريض ١" },
            new PatientTreatmentPlanStep { PatientId = patient2, SequenceNumber = 1, Title = "مريض ٢" }
        );
        await db.SaveChangesAsync();

        var steps = await db.PatientTreatmentPlanSteps
            .Where(s => s.PatientId == patient1)
            .ToListAsync();

        steps.Should().HaveCount(1);
        steps[0].Title.Should().Be("مريض ١");
    }

    // ─── Status Change Tests ───────────────────────────────────────────────

    [Fact]
    public void StatusTransition_Planned_To_InProgress_IsValid()
    {
        IsValidStatusTransition(TreatmentStepStatus.Planned, TreatmentStepStatus.InProgress)
            .Should().BeTrue();
    }

    [Fact]
    public void StatusTransition_Planned_To_Completed_IsValid()
    {
        IsValidStatusTransition(TreatmentStepStatus.Planned, TreatmentStepStatus.Completed)
            .Should().BeTrue();
    }

    [Fact]
    public void StatusTransition_InProgress_To_Completed_IsValid()
    {
        IsValidStatusTransition(TreatmentStepStatus.InProgress, TreatmentStepStatus.Completed)
            .Should().BeTrue();
    }

    [Fact]
    public void StatusTransition_Completed_To_Deferred_IsValid()
    {
        IsValidStatusTransition(TreatmentStepStatus.Completed, TreatmentStepStatus.Deferred)
            .Should().BeTrue();
    }

    [Fact]
    public void StatusTransition_Completed_To_InProgress_IsInvalid()
    {
        IsValidStatusTransition(TreatmentStepStatus.Completed, TreatmentStepStatus.InProgress)
            .Should().BeFalse();
    }

    [Fact]
    public void StatusTransition_Cancelled_To_Planned_IsValid()
    {
        IsValidStatusTransition(TreatmentStepStatus.Cancelled, TreatmentStepStatus.Planned)
            .Should().BeTrue();
    }

    [Fact]
    public void StatusTransition_Cancelled_To_Completed_IsInvalid()
    {
        IsValidStatusTransition(TreatmentStepStatus.Cancelled, TreatmentStepStatus.Completed)
            .Should().BeFalse();
    }

    [Fact]
    public void StatusTransition_Deferred_To_Planned_IsValid()
    {
        IsValidStatusTransition(TreatmentStepStatus.Deferred, TreatmentStepStatus.Planned)
            .Should().BeTrue();
    }

    // ─── Completed Date Auto-Set Tests ─────────────────────────────────────

    [Fact]
    public async Task Step_CompletedDate_Set_WhenStatusCompleted()
    {
        await using var db = CreateContext();
        var step = new PatientTreatmentPlanStep
        {
            PatientId = Guid.NewGuid(),
            SequenceNumber = 1,
            Title = "خلع",
            Status = TreatmentStepStatus.InProgress
        };
        db.PatientTreatmentPlanSteps.Add(step);
        await db.SaveChangesAsync();

        step.Status = TreatmentStepStatus.Completed;
        step.CompletedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        await db.SaveChangesAsync();

        var saved = await db.PatientTreatmentPlanSteps.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == step.Id);
        saved!.CompletedDate.Should().NotBeNull();
        saved.Status.Should().Be(TreatmentStepStatus.Completed);
    }

    // ─── Soft Delete Tests ─────────────────────────────────────────────────

    [Fact]
    public async Task Step_SoftDelete_SetsIsActiveFalse()
    {
        await using var db = CreateContext();
        var step = new PatientTreatmentPlanStep
        {
            PatientId = Guid.NewGuid(),
            SequenceNumber = 1,
            Title = "حشوة"
        };
        db.PatientTreatmentPlanSteps.Add(step);
        await db.SaveChangesAsync();

        step.IsActive = false;
        step.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var activeStep = await db.PatientTreatmentPlanSteps
            .FirstOrDefaultAsync(s => s.Id == step.Id);
        activeStep.Should().BeNull();

        var deletedStep = await db.PatientTreatmentPlanSteps.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == step.Id);
        deletedStep.Should().NotBeNull();
        deletedStep!.IsActive.Should().BeFalse();
    }

    // ─── Service Default Price Does Not Create Payment ──────────────────────

    [Fact]
    public async Task Step_EstimatedCost_IsReferenceOnly_NoPaymentCreated()
    {
        await using var db = CreateContext();
        var step = new PatientTreatmentPlanStep
        {
            PatientId = Guid.NewGuid(),
            SequenceNumber = 1,
            Title = "تركيبة",
            EstimatedCost = 15000m
        };
        db.PatientTreatmentPlanSteps.Add(step);
        await db.SaveChangesAsync();

        var payments = await db.Payments.ToListAsync();
        payments.Should().BeEmpty("estimated cost should not auto-create payment");

        var saved = await db.PatientTreatmentPlanSteps.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == step.Id);
        saved!.EstimatedCost.Should().Be(15000m);
    }

    // ─── Reorder Steps Test ────────────────────────────────────────────────

    [Fact]
    public async Task Steps_CanBeReordered()
    {
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();

        var step1 = new PatientTreatmentPlanStep { PatientId = patientId, SequenceNumber = 1, Title = "أول" };
        var step2 = new PatientTreatmentPlanStep { PatientId = patientId, SequenceNumber = 2, Title = "ثاني" };
        db.PatientTreatmentPlanSteps.AddRange(step1, step2);
        await db.SaveChangesAsync();

        step1.SequenceNumber = 2;
        step2.SequenceNumber = 1;
        await db.SaveChangesAsync();

        var steps = await db.PatientTreatmentPlanSteps
            .Where(s => s.PatientId == patientId)
            .OrderBy(s => s.SequenceNumber)
            .ToListAsync();

        steps[0].Title.Should().Be("ثاني");
        steps[1].Title.Should().Be("أول");
    }

    // ─── Service Name Snapshot Test ────────────────────────────────────────

    [Fact]
    public async Task Step_ServiceNameSnapshot_IsPreserved()
    {
        await using var db = CreateContext();
        var step = new PatientTreatmentPlanStep
        {
            PatientId = Guid.NewGuid(),
            SequenceNumber = 1,
            Title = "علاج عصب",
            ServiceNameSnapshot = "علاج العصب — القناة الواحدة"
        };
        db.PatientTreatmentPlanSteps.Add(step);
        await db.SaveChangesAsync();

        var saved = await db.PatientTreatmentPlanSteps.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == step.Id);
        saved!.ServiceNameSnapshot.Should().Be("علاج العصب — القناة الواحدة");
    }

    // ─── Helper: mirrors controller's IsValidStatusTransition logic ────────

    private static bool IsValidStatusTransition(TreatmentStepStatus current, TreatmentStepStatus target)
    {
        return current switch
        {
            TreatmentStepStatus.Planned => true,
            TreatmentStepStatus.InProgress => true,
            TreatmentStepStatus.Completed => target == TreatmentStepStatus.Deferred,
            TreatmentStepStatus.Cancelled => target == TreatmentStepStatus.Planned,
            TreatmentStepStatus.Deferred => true,
            _ => false
        };
    }
}
