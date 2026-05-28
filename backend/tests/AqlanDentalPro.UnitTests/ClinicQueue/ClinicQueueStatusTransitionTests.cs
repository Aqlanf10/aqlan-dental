using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.ClinicQueue;

/// <summary>
/// CON-01 FIX: Comprehensive tests for ClinicQueueStatusTransitions.
/// Validates all valid/invalid state transitions, terminal states,
/// idempotent transitions, Arabic error messages, and the full queue lifecycle flow.
/// </summary>
public class ClinicQueueStatusTransitionTests
{
    // ─── Idempotent Transitions (same → same) ─────────────────────────────

    [Theory]
    [InlineData(ClinicQueueStatus.Waiting)]
    [InlineData(ClinicQueueStatus.Called)]
    [InlineData(ClinicQueueStatus.InRoom)]
    [InlineData(ClinicQueueStatus.InProgress)]
    [InlineData(ClinicQueueStatus.Completed)]
    [InlineData(ClinicQueueStatus.Cancelled)]
    public void SameStatus_Transition_IsAlwaysValid(ClinicQueueStatus status)
    {
        ClinicQueueStatusTransitions.IsValidTransition(status, status)
            .Should().BeTrue("transitioning to the same status should always be allowed");
    }

    // ─── Waiting Transitions ─────────────────────────────────────────────

    [Fact]
    public void Waiting_ToCalled_IsValid()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.Waiting, ClinicQueueStatus.Called)
            .Should().BeTrue();
    }

    [Fact]
    public void Waiting_ToInRoom_IsValid()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.Waiting, ClinicQueueStatus.InRoom)
            .Should().BeTrue();
    }

    [Fact]
    public void Waiting_ToCancelled_IsValid()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.Waiting, ClinicQueueStatus.Cancelled)
            .Should().BeTrue();
    }

    [Fact]
    public void Waiting_ToInProgress_IsInvalid()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.Waiting, ClinicQueueStatus.InProgress)
            .Should().BeFalse("cannot skip from Waiting directly to InProgress");
    }

    [Fact]
    public void Waiting_ToCompleted_IsInvalid()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.Waiting, ClinicQueueStatus.Completed)
            .Should().BeFalse();
    }

    // ─── Called Transitions ──────────────────────────────────────────────

    [Fact]
    public void Called_ToInRoom_IsValid()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.Called, ClinicQueueStatus.InRoom)
            .Should().BeTrue();
    }

    [Fact]
    public void Called_ToWaiting_IsValid()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.Called, ClinicQueueStatus.Waiting)
            .Should().BeTrue();
    }

    [Fact]
    public void Called_ToCancelled_IsValid()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.Called, ClinicQueueStatus.Cancelled)
            .Should().BeTrue();
    }

    [Fact]
    public void Called_ToInProgress_IsInvalid()
    {
        // CON-01 FIX: Called → InProgress removed — must go through InRoom first
        // This prevents asymmetry with AppointmentStatusTransitions which doesn't allow Called → InProgress
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.Called, ClinicQueueStatus.InProgress)
            .Should().BeFalse("cannot skip from Called directly to InProgress — must go through InRoom");
    }

    [Fact]
    public void Called_ToCompleted_IsInvalid()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.Called, ClinicQueueStatus.Completed)
            .Should().BeFalse();
    }

    // ─── InRoom Transitions ──────────────────────────────────────────────

    [Fact]
    public void InRoom_ToInProgress_IsValid()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.InRoom, ClinicQueueStatus.InProgress)
            .Should().BeTrue();
    }

    [Fact]
    public void InRoom_ToCalled_IsValid()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.InRoom, ClinicQueueStatus.Called)
            .Should().BeTrue();
    }

    [Fact]
    public void InRoom_ToCancelled_IsValid()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.InRoom, ClinicQueueStatus.Cancelled)
            .Should().BeTrue();
    }

    [Fact]
    public void InRoom_ToCompleted_IsInvalid()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.InRoom, ClinicQueueStatus.Completed)
            .Should().BeFalse();
    }

    [Fact]
    public void InRoom_ToWaiting_IsInvalid()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.InRoom, ClinicQueueStatus.Waiting)
            .Should().BeFalse();
    }

    // ─── InProgress Transitions ──────────────────────────────────────────

    [Fact]
    public void InProgress_ToCompleted_IsValid()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.InProgress, ClinicQueueStatus.Completed)
            .Should().BeTrue();
    }

    [Fact]
    public void InProgress_ToCancelled_IsValid()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.InProgress, ClinicQueueStatus.Cancelled)
            .Should().BeTrue();
    }

    [Fact]
    public void InProgress_ToInRoom_IsInvalid()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.InProgress, ClinicQueueStatus.InRoom)
            .Should().BeFalse();
    }

    [Fact]
    public void InProgress_ToWaiting_IsInvalid()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.InProgress, ClinicQueueStatus.Waiting)
            .Should().BeFalse();
    }

    // ─── Terminal States — No Outgoing Transitions ───────────────────────

    [Theory]
    [InlineData(ClinicQueueStatus.Waiting)]
    [InlineData(ClinicQueueStatus.Called)]
    [InlineData(ClinicQueueStatus.InRoom)]
    [InlineData(ClinicQueueStatus.InProgress)]
    [InlineData(ClinicQueueStatus.Cancelled)]
    public void Completed_OnlyAllowsIdempotent(ClinicQueueStatus target)
    {
        if (target == ClinicQueueStatus.Completed)
        {
            ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.Completed, target)
                .Should().BeTrue();
        }
        else
        {
            ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.Completed, target)
                .Should().BeFalse("Completed is a terminal state");
        }
    }

    [Theory]
    [InlineData(ClinicQueueStatus.Waiting)]
    [InlineData(ClinicQueueStatus.Called)]
    [InlineData(ClinicQueueStatus.InRoom)]
    [InlineData(ClinicQueueStatus.InProgress)]
    [InlineData(ClinicQueueStatus.Completed)]
    public void Cancelled_OnlyAllowsIdempotent(ClinicQueueStatus target)
    {
        if (target == ClinicQueueStatus.Cancelled)
        {
            ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.Cancelled, target)
                .Should().BeTrue();
        }
        else
        {
            ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.Cancelled, target)
                .Should().BeFalse("Cancelled is a terminal state");
        }
    }

    // ─── GetAllowedTransitions Tests ─────────────────────────────────────

    [Fact]
    public void GetAllowedTransitions_Waiting_ContainsExpected()
    {
        var allowed = ClinicQueueStatusTransitions.GetAllowedTransitions(ClinicQueueStatus.Waiting).ToList();
        allowed.Should().Contain(ClinicQueueStatus.Waiting); // idempotent
        allowed.Should().Contain(ClinicQueueStatus.Called);
        allowed.Should().Contain(ClinicQueueStatus.InRoom);
        allowed.Should().Contain(ClinicQueueStatus.Cancelled);
        allowed.Should().Contain(ClinicQueueStatus.NoShow);
        allowed.Count.Should().Be(5);
    }

    [Fact]
    public void GetAllowedTransitions_Called_ContainsExpected()
    {
        var allowed = ClinicQueueStatusTransitions.GetAllowedTransitions(ClinicQueueStatus.Called).ToList();
        allowed.Should().Contain(ClinicQueueStatus.Called); // idempotent
        allowed.Should().Contain(ClinicQueueStatus.InRoom);
        allowed.Should().Contain(ClinicQueueStatus.Waiting);
        allowed.Should().Contain(ClinicQueueStatus.Cancelled);
        allowed.Should().Contain(ClinicQueueStatus.NoShow);
        allowed.Count.Should().Be(5);
    }

    [Fact]
    public void GetAllowedTransitions_InRoom_ContainsExpected()
    {
        var allowed = ClinicQueueStatusTransitions.GetAllowedTransitions(ClinicQueueStatus.InRoom).ToList();
        allowed.Should().Contain(ClinicQueueStatus.InRoom); // idempotent
        allowed.Should().Contain(ClinicQueueStatus.InProgress);
        allowed.Should().Contain(ClinicQueueStatus.Called);
        allowed.Should().Contain(ClinicQueueStatus.Cancelled);
        allowed.Count.Should().Be(4); // InRoom did not gain NoShow transition
    }

    [Fact]
    public void GetAllowedTransitions_InProgress_ContainsExpected()
    {
        var allowed = ClinicQueueStatusTransitions.GetAllowedTransitions(ClinicQueueStatus.InProgress).ToList();
        allowed.Should().Contain(ClinicQueueStatus.InProgress); // idempotent
        allowed.Should().Contain(ClinicQueueStatus.Completed);
        allowed.Should().Contain(ClinicQueueStatus.Cancelled);
        allowed.Count.Should().Be(3);
    }

    [Fact]
    public void GetAllowedTransitions_Completed_OnlyContainsItself()
    {
        var allowed = ClinicQueueStatusTransitions.GetAllowedTransitions(ClinicQueueStatus.Completed).ToList();
        allowed.Should().HaveCount(1);
        allowed.Should().Contain(ClinicQueueStatus.Completed);
    }

    [Fact]
    public void GetAllowedTransitions_Cancelled_OnlyContainsItself()
    {
        var allowed = ClinicQueueStatusTransitions.GetAllowedTransitions(ClinicQueueStatus.Cancelled).ToList();
        allowed.Should().HaveCount(1);
        allowed.Should().Contain(ClinicQueueStatus.Cancelled);
    }

    // ─── Full Lifecycle Flow ─────────────────────────────────────────────

    [Fact]
    public void FullNormalFlow_WaitingToCompleted_AllStepsValid()
    {
        var flow = new[]
        {
            (ClinicQueueStatus.Waiting, ClinicQueueStatus.Called),
            (ClinicQueueStatus.Called, ClinicQueueStatus.InRoom),
            (ClinicQueueStatus.InRoom, ClinicQueueStatus.InProgress),
            (ClinicQueueStatus.InProgress, ClinicQueueStatus.Completed)
        };

        foreach (var (from, to) in flow)
        {
            ClinicQueueStatusTransitions.IsValidTransition(from, to)
                .Should().BeTrue($"transition from {from} to {to} should be valid in the normal flow");
        }
    }

    [Fact]
    public void FullShortenedFlow_WaitingDirectToInRoom_AllStepsValid()
    {
        var flow = new[]
        {
            (ClinicQueueStatus.Waiting, ClinicQueueStatus.InRoom),
            (ClinicQueueStatus.InRoom, ClinicQueueStatus.InProgress),
            (ClinicQueueStatus.InProgress, ClinicQueueStatus.Completed)
        };

        foreach (var (from, to) in flow)
        {
            ClinicQueueStatusTransitions.IsValidTransition(from, to)
                .Should().BeTrue($"transition from {from} to {to} should be valid in the shortened flow");
        }
    }

    // ─── Skipping Steps ──────────────────────────────────────────────────

    [Fact]
    public void CannotSkipFromWaiting_ToInProgress()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.Waiting, ClinicQueueStatus.InProgress)
            .Should().BeFalse();
    }

    [Fact]
    public void CannotSkipFromWaiting_ToCompleted()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.Waiting, ClinicQueueStatus.Completed)
            .Should().BeFalse();
    }

    [Fact]
    public void CannotSkipFromCalled_ToInProgress()
    {
        // CON-01 FIX: This was previously allowed, causing asymmetry with AppointmentStatusTransitions
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.Called, ClinicQueueStatus.InProgress)
            .Should().BeFalse();
    }

    [Fact]
    public void CannotSkipFromCalled_ToCompleted()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.Called, ClinicQueueStatus.Completed)
            .Should().BeFalse();
    }

    // ─── Backward Transitions ────────────────────────────────────────────

    [Fact]
    public void InProgress_CannotGoBackToInRoom()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.InProgress, ClinicQueueStatus.InRoom)
            .Should().BeFalse();
    }

    [Fact]
    public void InProgress_CannotGoBackToWaiting()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.InProgress, ClinicQueueStatus.Waiting)
            .Should().BeFalse();
    }

    [Fact]
    public void InRoom_CanGoBackToCalled()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.InRoom, ClinicQueueStatus.Called)
            .Should().BeTrue();
    }

    [Fact]
    public void Called_CanGoBackToWaiting()
    {
        ClinicQueueStatusTransitions.IsValidTransition(ClinicQueueStatus.Called, ClinicQueueStatus.Waiting)
            .Should().BeTrue();
    }

    // ─── GetValidationError Tests ────────────────────────────────────────

    [Fact]
    public void GetValidationError_ReturnsNullForValidTransition()
    {
        var error = ClinicQueueStatusTransitions.GetValidationError(ClinicQueueStatus.Waiting, ClinicQueueStatus.Called);
        error.Should().BeNull();
    }

    [Fact]
    public void GetValidationError_ReturnsNullForIdempotentTransition()
    {
        var error = ClinicQueueStatusTransitions.GetValidationError(ClinicQueueStatus.InProgress, ClinicQueueStatus.InProgress);
        error.Should().BeNull();
    }

    [Fact]
    public void GetValidationError_ReturnsArabicMessageForInvalidTransition()
    {
        var error = ClinicQueueStatusTransitions.GetValidationError(ClinicQueueStatus.Waiting, ClinicQueueStatus.InProgress);
        error.Should().NotBeNull();
        error.Should().Contain("لا يمكن تغيير حالة الطابور");
        error.Should().Contain("في الانتظار");
        error.Should().Contain("قيد المعالجة");
    }

    [Fact]
    public void GetValidationError_CancelFromTerminalState_ReturnsArabicMessage()
    {
        var error = ClinicQueueStatusTransitions.GetValidationError(ClinicQueueStatus.Completed, ClinicQueueStatus.Cancelled);
        error.Should().NotBeNull();
        error.Should().Contain("لا يمكن تغيير حالة الطابور");
        error.Should().Contain("مكتمل");
        error.Should().Contain("ملغي");
    }

    // ─── GetArabicLabel Tests ────────────────────────────────────────────

    [Theory]
    [InlineData(ClinicQueueStatus.Waiting, "في الانتظار")]
    [InlineData(ClinicQueueStatus.Called, "تم النداء")]
    [InlineData(ClinicQueueStatus.InRoom, "داخل الغرفة")]
    [InlineData(ClinicQueueStatus.InProgress, "قيد المعالجة")]
    [InlineData(ClinicQueueStatus.Completed, "مكتمل")]
    [InlineData(ClinicQueueStatus.Cancelled, "ملغي")]
    public void GetArabicLabel_ReturnsCorrectLabel(ClinicQueueStatus status, string expectedLabel)
    {
        ClinicQueueStatusTransitions.GetArabicLabel(status).Should().Be(expectedLabel);
    }

    // ─── Enum Count Test ─────────────────────────────────────────────────

    [Fact]
    public void ClinicQueueStatus_HasExactlySevenValues()
    {
        Enum.GetValues<ClinicQueueStatus>().Length.Should().Be(7);
    }
}
