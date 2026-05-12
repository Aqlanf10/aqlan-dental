using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Services;

/// <summary>
/// Comprehensive tests for AppointmentStatusTransitions.
/// Validates all valid/invalid state transitions, terminal states,
/// idempotent transitions, and the full appointment lifecycle flow.
/// </summary>
public class AppointmentStatusTransitionTests
{
    // ─── Idempotent Transitions (same → same) ─────────────────────────────

    [Theory]
    [InlineData(AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Arrived)]
    [InlineData(AppointmentStatus.Waiting)]
    [InlineData(AppointmentStatus.Called)]
    [InlineData(AppointmentStatus.InRoom)]
    [InlineData(AppointmentStatus.InProgress)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void SameStatus_Transition_IsAlwaysValid(AppointmentStatus status)
    {
        AppointmentStatusTransitions.IsValidTransition(status, status)
            .Should().BeTrue("transitioning to the same status should always be allowed");
    }

    // ─── Scheduled Transitions ─────────────────────────────────────────────

    [Fact]
    public void Scheduled_ToConfirmed_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Scheduled, AppointmentStatus.Confirmed)
            .Should().BeTrue();
    }

    [Fact]
    public void Scheduled_ToArrived_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Scheduled, AppointmentStatus.Arrived)
            .Should().BeTrue();
    }

    [Fact]
    public void Scheduled_ToCancelled_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Scheduled, AppointmentStatus.Cancelled)
            .Should().BeTrue();
    }

    [Fact]
    public void Scheduled_ToNoShow_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Scheduled, AppointmentStatus.NoShow)
            .Should().BeTrue();
    }

    [Fact]
    public void Scheduled_ToWaiting_IsInvalid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Scheduled, AppointmentStatus.Waiting)
            .Should().BeFalse();
    }

    [Fact]
    public void Scheduled_ToCalled_IsInvalid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Scheduled, AppointmentStatus.Called)
            .Should().BeFalse();
    }

    [Fact]
    public void Scheduled_ToInRoom_IsInvalid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Scheduled, AppointmentStatus.InRoom)
            .Should().BeFalse();
    }

    [Fact]
    public void Scheduled_ToInProgress_IsInvalid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Scheduled, AppointmentStatus.InProgress)
            .Should().BeFalse();
    }

    [Fact]
    public void Scheduled_ToCompleted_IsInvalid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Scheduled, AppointmentStatus.Completed)
            .Should().BeFalse();
    }

    // ─── Confirmed Transitions ─────────────────────────────────────────────

    [Fact]
    public void Confirmed_ToScheduled_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Confirmed, AppointmentStatus.Scheduled)
            .Should().BeTrue();
    }

    [Fact]
    public void Confirmed_ToArrived_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Confirmed, AppointmentStatus.Arrived)
            .Should().BeTrue();
    }

    [Fact]
    public void Confirmed_ToWaiting_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Confirmed, AppointmentStatus.Waiting)
            .Should().BeTrue();
    }

    [Fact]
    public void Confirmed_ToCancelled_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Confirmed, AppointmentStatus.Cancelled)
            .Should().BeTrue();
    }

    [Fact]
    public void Confirmed_ToNoShow_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Confirmed, AppointmentStatus.NoShow)
            .Should().BeTrue();
    }

    [Fact]
    public void Confirmed_ToCalled_IsInvalid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Confirmed, AppointmentStatus.Called)
            .Should().BeFalse();
    }

    [Fact]
    public void Confirmed_ToInRoom_IsInvalid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Confirmed, AppointmentStatus.InRoom)
            .Should().BeFalse();
    }

    // ─── Arrived Transitions ───────────────────────────────────────────────

    [Fact]
    public void Arrived_ToScheduled_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Arrived, AppointmentStatus.Scheduled)
            .Should().BeTrue();
    }

    [Fact]
    public void Arrived_ToConfirmed_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Arrived, AppointmentStatus.Confirmed)
            .Should().BeTrue();
    }

    [Fact]
    public void Arrived_ToWaiting_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Arrived, AppointmentStatus.Waiting)
            .Should().BeTrue();
    }

    [Fact]
    public void Arrived_ToCancelled_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Arrived, AppointmentStatus.Cancelled)
            .Should().BeTrue();
    }

    [Fact]
    public void Arrived_ToNoShow_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Arrived, AppointmentStatus.NoShow)
            .Should().BeTrue();
    }

    [Fact]
    public void Arrived_ToCalled_IsInvalid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Arrived, AppointmentStatus.Called)
            .Should().BeFalse();
    }

    [Fact]
    public void Arrived_ToInProgress_IsInvalid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Arrived, AppointmentStatus.InProgress)
            .Should().BeFalse();
    }

    // ─── Waiting Transitions ───────────────────────────────────────────────

    [Fact]
    public void Waiting_ToCalled_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Waiting, AppointmentStatus.Called)
            .Should().BeTrue();
    }

    [Fact]
    public void Waiting_ToConfirmed_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Waiting, AppointmentStatus.Confirmed)
            .Should().BeTrue();
    }

    [Fact]
    public void Waiting_ToArrived_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Waiting, AppointmentStatus.Arrived)
            .Should().BeTrue();
    }

    [Fact]
    public void Waiting_ToCancelled_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Waiting, AppointmentStatus.Cancelled)
            .Should().BeTrue();
    }

    [Fact]
    public void Waiting_ToNoShow_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Waiting, AppointmentStatus.NoShow)
            .Should().BeTrue();
    }

    [Fact]
    public void Waiting_ToInRoom_IsInvalid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Waiting, AppointmentStatus.InRoom)
            .Should().BeFalse();
    }

    [Fact]
    public void Waiting_ToInProgress_IsInvalid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Waiting, AppointmentStatus.InProgress)
            .Should().BeFalse();
    }

    // ─── Called Transitions ────────────────────────────────────────────────

    [Fact]
    public void Called_ToInRoom_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Called, AppointmentStatus.InRoom)
            .Should().BeTrue();
    }

    [Fact]
    public void Called_ToWaiting_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Called, AppointmentStatus.Waiting)
            .Should().BeTrue();
    }

    [Fact]
    public void Called_ToCancelled_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Called, AppointmentStatus.Cancelled)
            .Should().BeTrue();
    }

    [Fact]
    public void Called_ToInProgress_IsInvalid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Called, AppointmentStatus.InProgress)
            .Should().BeFalse();
    }

    [Fact]
    public void Called_ToCompleted_IsInvalid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Called, AppointmentStatus.Completed)
            .Should().BeFalse();
    }

    // ─── InRoom Transitions ────────────────────────────────────────────────

    [Fact]
    public void InRoom_ToInProgress_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.InRoom, AppointmentStatus.InProgress)
            .Should().BeTrue();
    }

    [Fact]
    public void InRoom_ToCalled_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.InRoom, AppointmentStatus.Called)
            .Should().BeTrue();
    }

    [Fact]
    public void InRoom_ToCancelled_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.InRoom, AppointmentStatus.Cancelled)
            .Should().BeTrue();
    }

    [Fact]
    public void InRoom_ToCompleted_IsInvalid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.InRoom, AppointmentStatus.Completed)
            .Should().BeFalse();
    }

    // ─── InProgress Transitions ────────────────────────────────────────────

    [Fact]
    public void InProgress_ToCompleted_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.InProgress, AppointmentStatus.Completed)
            .Should().BeTrue();
    }

    [Fact]
    public void InProgress_ToCancelled_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.InProgress, AppointmentStatus.Cancelled)
            .Should().BeTrue();
    }

    [Fact]
    public void InProgress_ToInRoom_IsInvalid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.InProgress, AppointmentStatus.InRoom)
            .Should().BeFalse();
    }

    [Fact]
    public void InProgress_ToScheduled_IsInvalid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.InProgress, AppointmentStatus.Scheduled)
            .Should().BeFalse();
    }

    // ─── Terminal States — No Outgoing Transitions ─────────────────────────

    [Theory]
    [InlineData(AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Arrived)]
    [InlineData(AppointmentStatus.Waiting)]
    [InlineData(AppointmentStatus.Called)]
    [InlineData(AppointmentStatus.InRoom)]
    [InlineData(AppointmentStatus.InProgress)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void Completed_OnlyAllowsIdempotent(AppointmentStatus target)
    {
        // Completed → Completed (idempotent) is allowed
        // Completed → anything else is NOT allowed
        if (target == AppointmentStatus.Completed)
        {
            AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Completed, target)
                .Should().BeTrue();
        }
        else
        {
            AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Completed, target)
                .Should().BeFalse("Completed is a terminal state");
        }
    }

    [Theory]
    [InlineData(AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Arrived)]
    [InlineData(AppointmentStatus.Waiting)]
    [InlineData(AppointmentStatus.Called)]
    [InlineData(AppointmentStatus.InRoom)]
    [InlineData(AppointmentStatus.InProgress)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.NoShow)]
    public void Cancelled_OnlyAllowsIdempotent(AppointmentStatus target)
    {
        if (target == AppointmentStatus.Cancelled)
        {
            AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Cancelled, target)
                .Should().BeTrue();
        }
        else
        {
            AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Cancelled, target)
                .Should().BeFalse("Cancelled is a terminal state");
        }
    }

    [Theory]
    [InlineData(AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Arrived)]
    [InlineData(AppointmentStatus.Waiting)]
    [InlineData(AppointmentStatus.Called)]
    [InlineData(AppointmentStatus.InRoom)]
    [InlineData(AppointmentStatus.InProgress)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    public void NoShow_OnlyAllowsIdempotent(AppointmentStatus target)
    {
        if (target == AppointmentStatus.NoShow)
        {
            AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.NoShow, target)
                .Should().BeTrue();
        }
        else
        {
            AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.NoShow, target)
                .Should().BeFalse("NoShow is a terminal state");
        }
    }

    // ─── GetAllowedTransitions Tests ───────────────────────────────────────

    [Fact]
    public void GetAllowedTransitions_Scheduled_ContainsExpected()
    {
        var allowed = AppointmentStatusTransitions.GetAllowedTransitions(AppointmentStatus.Scheduled).ToList();
        allowed.Should().Contain(AppointmentStatus.Scheduled); // idempotent
        allowed.Should().Contain(AppointmentStatus.Confirmed);
        allowed.Should().Contain(AppointmentStatus.Arrived);
        allowed.Should().Contain(AppointmentStatus.Cancelled);
        allowed.Should().Contain(AppointmentStatus.NoShow);
        allowed.Count.Should().Be(5);
    }

    [Fact]
    public void GetAllowedTransitions_Completed_OnlyContainsItself()
    {
        var allowed = AppointmentStatusTransitions.GetAllowedTransitions(AppointmentStatus.Completed).ToList();
        allowed.Should().HaveCount(1);
        allowed.Should().Contain(AppointmentStatus.Completed);
    }

    [Fact]
    public void GetAllowedTransitions_Cancelled_OnlyContainsItself()
    {
        var allowed = AppointmentStatusTransitions.GetAllowedTransitions(AppointmentStatus.Cancelled).ToList();
        allowed.Should().HaveCount(1);
        allowed.Should().Contain(AppointmentStatus.Cancelled);
    }

    [Fact]
    public void GetAllowedTransitions_NoShow_OnlyContainsItself()
    {
        var allowed = AppointmentStatusTransitions.GetAllowedTransitions(AppointmentStatus.NoShow).ToList();
        allowed.Should().HaveCount(1);
        allowed.Should().Contain(AppointmentStatus.NoShow);
    }

    // ─── Full Lifecycle Flow ───────────────────────────────────────────────

    [Fact]
    public void FullNormalFlow_ScheduledToCompleted_AllStepsValid()
    {
        var flow = new[]
        {
            (AppointmentStatus.Scheduled, AppointmentStatus.Confirmed),
            (AppointmentStatus.Confirmed, AppointmentStatus.Arrived),
            (AppointmentStatus.Arrived, AppointmentStatus.Waiting),
            (AppointmentStatus.Waiting, AppointmentStatus.Called),
            (AppointmentStatus.Called, AppointmentStatus.InRoom),
            (AppointmentStatus.InRoom, AppointmentStatus.InProgress),
            (AppointmentStatus.InProgress, AppointmentStatus.Completed)
        };

        foreach (var (from, to) in flow)
        {
            AppointmentStatusTransitions.IsValidTransition(from, to)
                .Should().BeTrue($"transition from {from} to {to} should be valid in the normal flow");
        }
    }

    // ─── Edge Case: Skipping Steps ─────────────────────────────────────────

    [Fact]
    public void CannotSkipFromScheduled_ToInProgress()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Scheduled, AppointmentStatus.InProgress)
            .Should().BeFalse();
    }

    [Fact]
    public void CannotSkipFromScheduled_ToInRoom()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Scheduled, AppointmentStatus.InRoom)
            .Should().BeFalse();
    }

    [Fact]
    public void CannotSkipFromConfirmed_ToCalled()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Confirmed, AppointmentStatus.Called)
            .Should().BeFalse();
    }

    [Fact]
    public void CannotSkipFromWaiting_ToInRoom()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Waiting, AppointmentStatus.InRoom)
            .Should().BeFalse();
    }

    [Fact]
    public void CannotSkipFromWaiting_ToCompleted()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Waiting, AppointmentStatus.Completed)
            .Should().BeFalse();
    }

    // ─── Backward Transitions ──────────────────────────────────────────────

    [Fact]
    public void InProgress_CannotGoBackToWaiting()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.InProgress, AppointmentStatus.Waiting)
            .Should().BeFalse();
    }

    [Fact]
    public void InProgress_CannotGoBackToScheduled()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.InProgress, AppointmentStatus.Scheduled)
            .Should().BeFalse();
    }

    [Fact]
    public void Called_CannotGoBackToScheduled()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Called, AppointmentStatus.Scheduled)
            .Should().BeFalse();
    }

    // ─── GetAllowedTransitions For Active States ───────────────────────────

    [Fact]
    public void GetAllowedTransitions_InProgress_ContainsCompletedAndCancelled()
    {
        var allowed = AppointmentStatusTransitions.GetAllowedTransitions(AppointmentStatus.InProgress).ToList();
        allowed.Should().Contain(AppointmentStatus.InProgress);
        allowed.Should().Contain(AppointmentStatus.Completed);
        allowed.Should().Contain(AppointmentStatus.Cancelled);
        allowed.Count.Should().Be(3);
    }

    [Fact]
    public void GetAllowedTransitions_Waiting_ContainsExpected()
    {
        var allowed = AppointmentStatusTransitions.GetAllowedTransitions(AppointmentStatus.Waiting).ToList();
        allowed.Should().Contain(AppointmentStatus.Waiting);
        allowed.Should().Contain(AppointmentStatus.Called);
        allowed.Should().Contain(AppointmentStatus.Confirmed);
        allowed.Should().Contain(AppointmentStatus.Arrived);
        allowed.Should().Contain(AppointmentStatus.Cancelled);
        allowed.Should().Contain(AppointmentStatus.NoShow);
        allowed.Count.Should().Be(6);
    }

    [Fact]
    public void GetAllowedTransitions_Called_ContainsExpected()
    {
        var allowed = AppointmentStatusTransitions.GetAllowedTransitions(AppointmentStatus.Called).ToList();
        allowed.Should().Contain(AppointmentStatus.Called);
        allowed.Should().Contain(AppointmentStatus.InRoom);
        allowed.Should().Contain(AppointmentStatus.Waiting);
        allowed.Should().Contain(AppointmentStatus.Cancelled);
        allowed.Count.Should().Be(4);
    }

    // ─── All Statuses Enum Count ───────────────────────────────────────────

    [Fact]
    public void AppointmentStatus_HasExactlyTenValues()
    {
        Enum.GetValues<AppointmentStatus>().Length.Should().Be(10);
    }
}
