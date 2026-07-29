using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.OrthoSurgical;

/// <summary>
/// Pure-logic tests for <see cref="OrthoSurgicalStatusTransitions"/> — the workflow
/// state machine of the shared ortho-surgical planning case. No DB, no mocks.
/// </summary>
public class OrthoSurgicalStatusTransitionsTests
{
    [Fact]
    public void SameStatus_IsAlwaysValid_Idempotent()
    {
        foreach (OrthoSurgicalStatus s in Enum.GetValues(typeof(OrthoSurgicalStatus)))
            OrthoSurgicalStatusTransitions.IsValidTransition(s, s).Should().BeTrue();
    }

    [Theory]
    [InlineData(OrthoSurgicalStatus.DraftByOrthodontist, OrthoSurgicalStatus.SentToSurgeon)]
    [InlineData(OrthoSurgicalStatus.CephReady, OrthoSurgicalStatus.VtoDraft)]
    [InlineData(OrthoSurgicalStatus.SentToSurgeon, OrthoSurgicalStatus.SurgeonReviewPending)]
    [InlineData(OrthoSurgicalStatus.SurgeonReviewPending, OrthoSurgicalStatus.JointPlanApproved)]
    [InlineData(OrthoSurgicalStatus.SurgeonReviewPending, OrthoSurgicalStatus.SurgeonRequestedChanges)]
    [InlineData(OrthoSurgicalStatus.JointPlanApproved, OrthoSurgicalStatus.ReadyForSurgery)]
    [InlineData(OrthoSurgicalStatus.ReadyForSurgery, OrthoSurgicalStatus.SurgeryScheduled)]
    [InlineData(OrthoSurgicalStatus.SurgeryScheduled, OrthoSurgicalStatus.SurgeryDone)]
    [InlineData(OrthoSurgicalStatus.SurgeryDone, OrthoSurgicalStatus.PostOpOrthodontics)]
    [InlineData(OrthoSurgicalStatus.PostOpOrthodontics, OrthoSurgicalStatus.Completed)]
    public void ValidForwardTransitions_AreAllowed(OrthoSurgicalStatus from, OrthoSurgicalStatus to)
    {
        OrthoSurgicalStatusTransitions.IsValidTransition(from, to).Should().BeTrue();
        OrthoSurgicalStatusTransitions.GetValidationError(from, to).Should().BeNull();
    }

    [Theory]
    // Cannot skip surgeon review straight to ready-for-surgery.
    [InlineData(OrthoSurgicalStatus.DraftByOrthodontist, OrthoSurgicalStatus.ReadyForSurgery)]
    // Cannot approve the joint plan before the surgeon reviews.
    [InlineData(OrthoSurgicalStatus.SentToSurgeon, OrthoSurgicalStatus.JointPlanApproved)]
    // Cannot reopen a completed case.
    [InlineData(OrthoSurgicalStatus.Completed, OrthoSurgicalStatus.DraftByOrthodontist)]
    // Cancelled and NotSurgicalCandidate are terminal.
    [InlineData(OrthoSurgicalStatus.Cancelled, OrthoSurgicalStatus.DraftByOrthodontist)]
    [InlineData(OrthoSurgicalStatus.NotSurgicalCandidate, OrthoSurgicalStatus.SentToSurgeon)]
    // Cannot jump back from scheduled surgery to planning.
    [InlineData(OrthoSurgicalStatus.SurgeryScheduled, OrthoSurgicalStatus.DraftByOrthodontist)]
    public void InvalidTransitions_AreRejected_WithArabicMessage(OrthoSurgicalStatus from, OrthoSurgicalStatus to)
    {
        OrthoSurgicalStatusTransitions.IsValidTransition(from, to).Should().BeFalse();
        var error = OrthoSurgicalStatusTransitions.GetValidationError(from, to);
        error.Should().NotBeNull();
        error.Should().Contain("لا يمكن تغيير حالة");
    }

    [Fact]
    public void Cancellation_IsAllowedFromEveryNonTerminalState()
    {
        var terminal = new[]
        {
            OrthoSurgicalStatus.Completed,
            OrthoSurgicalStatus.Cancelled,
            OrthoSurgicalStatus.NotSurgicalCandidate,
            // SurgeryDone converges to Completed/PostOp only — cancelling a done surgery is meaningless.
            OrthoSurgicalStatus.SurgeryDone,
            OrthoSurgicalStatus.PostOpOrthodontics,
        };
        foreach (OrthoSurgicalStatus s in Enum.GetValues(typeof(OrthoSurgicalStatus)))
        {
            if (terminal.Contains(s)) continue;
            OrthoSurgicalStatusTransitions.IsValidTransition(s, OrthoSurgicalStatus.Cancelled)
                .Should().BeTrue($"{s} should be cancellable");
        }
    }

    [Fact]
    public void GetAllowedTransitions_AlwaysIncludesSelf()
    {
        OrthoSurgicalStatusTransitions.GetAllowedTransitions(OrthoSurgicalStatus.CephReady)
            .Should().Contain(OrthoSurgicalStatus.CephReady);
    }

    [Fact]
    public void EveryStatus_HasAnArabicLabel()
    {
        foreach (OrthoSurgicalStatus s in Enum.GetValues(typeof(OrthoSurgicalStatus)))
        {
            var label = OrthoSurgicalStatusTransitions.GetArabicLabel(s);
            label.Should().NotBeNullOrWhiteSpace();
            label.Should().NotBe(s.ToString(), $"{s} must have a proper Arabic label, not the enum name");
        }
    }
}
