using AqlanDentalPro.Domain.Constants;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.ClinicQueue;

/// <summary>
/// Tests for Sprint 7 clinic queue functionality.
/// Validates ClinicQueueItem entity behavior, enum values,
/// and business rules without requiring database context.
/// </summary>
public class ClinicQueueItemTests
{
    // ─── Entity Property Tests ──────────────────────────────────────────────

    [Fact]
    public void ClinicQueueItem_DefaultStatus_IsWaiting()
    {
        var item = new ClinicQueueItem();
        item.Status.Should().Be(ClinicQueueStatus.Waiting);
    }

    [Fact]
    public void ClinicQueueItem_DefaultQueueDate_IsToday()
    {
        var item = new ClinicQueueItem();
        item.QueueDate.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [Fact]
    public void ClinicQueueItem_Properties_CanBeSet()
    {
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var visitId = Guid.NewGuid();

        var item = new ClinicQueueItem
        {
            PatientId = patientId,
            DoctorId = doctorId,
            AppointmentId = appointmentId,
            VisitId = visitId,
            RoomName = "غرفة 1",
            Status = ClinicQueueStatus.Called,
            QueueDate = new DateOnly(2026, 5, 14)
        };

        item.PatientId.Should().Be(patientId);
        item.DoctorId.Should().Be(doctorId);
        item.AppointmentId.Should().Be(appointmentId);
        item.VisitId.Should().Be(visitId);
        item.RoomName.Should().Be("غرفة 1");
        item.Status.Should().Be(ClinicQueueStatus.Called);
        item.QueueDate.Should().Be(new DateOnly(2026, 5, 14));
    }

    // ─── Status Flow Tests ──────────────────────────────────────────────────

    [Fact]
    public void ClinicQueueItem_FullFlow_WaitingToCompleted()
    {
        var item = new ClinicQueueItem();
        item.Status.Should().Be(ClinicQueueStatus.Waiting);

        // Waiting → Called
        item.Status = ClinicQueueStatus.Called;
        item.CalledAt = DateTime.UtcNow;
        item.RoomName = "غرفة 1";
        item.Status.Should().Be(ClinicQueueStatus.Called);
        item.CalledAt.Should().NotBeNull();

        // Called → InRoom
        item.Status = ClinicQueueStatus.InRoom;
        item.InRoomAt = DateTime.UtcNow;
        item.Status.Should().Be(ClinicQueueStatus.InRoom);
        item.InRoomAt.Should().NotBeNull();

        // InRoom → InProgress
        item.Status = ClinicQueueStatus.InProgress;
        item.StartedAt = DateTime.UtcNow;
        item.Status.Should().Be(ClinicQueueStatus.InProgress);
        item.StartedAt.Should().NotBeNull();

        // InProgress → Completed
        item.Status = ClinicQueueStatus.Completed;
        item.CompletedAt = DateTime.UtcNow;
        item.Status.Should().Be(ClinicQueueStatus.Completed);
        item.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void ClinicQueueItem_CancelFromWaiting()
    {
        var item = new ClinicQueueItem();
        item.Status = ClinicQueueStatus.Cancelled;
        item.CancelledAt = DateTime.UtcNow;
        item.Status.Should().Be(ClinicQueueStatus.Cancelled);
        item.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void ClinicQueueItem_CancelFromCalled()
    {
        var item = new ClinicQueueItem
        {
            Status = ClinicQueueStatus.Called,
            CalledAt = DateTime.UtcNow
        };
        item.Status = ClinicQueueStatus.Cancelled;
        item.CancelledAt = DateTime.UtcNow;
        item.Status.Should().Be(ClinicQueueStatus.Cancelled);
    }

    // ─── Enum Tests ─────────────────────────────────────────────────────────

    [Fact]
    public void ClinicQueueStatus_HasAllExpectedValues()
    {
        var expected = new[]
        {
            ClinicQueueStatus.Waiting, ClinicQueueStatus.Called,
            ClinicQueueStatus.InRoom, ClinicQueueStatus.InProgress,
            ClinicQueueStatus.Completed, ClinicQueueStatus.Cancelled
        };
        Enum.GetValues<ClinicQueueStatus>().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void ClinicQueueStatus_HasCorrectNumericOrder()
    {
        ((int)ClinicQueueStatus.Waiting).Should().BeLessThan((int)ClinicQueueStatus.Called);
        ((int)ClinicQueueStatus.Called).Should().BeLessThan((int)ClinicQueueStatus.InRoom);
        ((int)ClinicQueueStatus.InRoom).Should().BeLessThan((int)ClinicQueueStatus.InProgress);
        ((int)ClinicQueueStatus.InProgress).Should().BeLessThan((int)ClinicQueueStatus.Completed);
    }

    // ─── Room Validation Tests ──────────────────────────────────────────────

    [Fact]
    public void ClinicRoomNames_DefaultRooms_AreValid()
    {
        ClinicRoomNames.DefaultRooms.Should().Contain(["غرفة 1", "غرفة 2", "غرفة 3"]);
    }

    [Fact]
    public void ClinicRoomNames_IsValid_ReturnsTrueForDefaultRooms()
    {
        ClinicRoomNames.IsValid("غرفة 1").Should().BeTrue();
        ClinicRoomNames.IsValid("غرفة 2").Should().BeTrue();
        ClinicRoomNames.IsValid("غرفة 3").Should().BeTrue();
    }

    [Fact]
    public void ClinicRoomNames_IsValid_ReturnsFalseForInvalidRooms()
    {
        ClinicRoomNames.IsValid("").Should().BeFalse();
        ClinicRoomNames.IsValid(null).Should().BeFalse();
        ClinicRoomNames.IsValid("Room 1").Should().BeFalse();
        ClinicRoomNames.IsValid("غرفة 4").Should().BeFalse();
    }

    // ─── Active Status Check (replicates controller logic) ──────────────────

    private static readonly HashSet<ClinicQueueStatus> ActiveStatuses =
    [
        ClinicQueueStatus.Waiting,
        ClinicQueueStatus.Called,
        ClinicQueueStatus.InRoom,
        ClinicQueueStatus.InProgress
    ];

    [Fact]
    public void ActiveStatuses_ContainsCorrectValues()
    {
        ActiveStatuses.Should().Contain(ClinicQueueStatus.Waiting);
        ActiveStatuses.Should().Contain(ClinicQueueStatus.Called);
        ActiveStatuses.Should().Contain(ClinicQueueStatus.InRoom);
        ActiveStatuses.Should().Contain(ClinicQueueStatus.InProgress);
    }

    [Fact]
    public void ActiveStatuses_DoesNotContainTerminalStatuses()
    {
        ActiveStatuses.Should().NotContain(ClinicQueueStatus.Completed);
        ActiveStatuses.Should().NotContain(ClinicQueueStatus.Cancelled);
    }

    [Theory]
    [InlineData(ClinicQueueStatus.Waiting, true)]
    [InlineData(ClinicQueueStatus.Called, true)]
    [InlineData(ClinicQueueStatus.InRoom, true)]
    [InlineData(ClinicQueueStatus.InProgress, true)]
    [InlineData(ClinicQueueStatus.Completed, false)]
    [InlineData(ClinicQueueStatus.Cancelled, false)]
    public void ActiveStatuses_Check(ClinicQueueStatus status, bool expected)
    {
        ActiveStatuses.Contains(status).Should().Be(expected);
    }
}
