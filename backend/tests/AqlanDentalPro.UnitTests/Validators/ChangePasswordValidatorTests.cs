using AqlanDentalPro.Application.DTOs.Auth;
using AqlanDentalPro.Application.Validators;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Validators;

/// <summary>
/// SEC-11: After centralizing password complexity into PasswordPolicy, the
/// ChangePasswordRequestValidator only enforces:
///   - CurrentPassword is non-empty
///   - NewPassword is non-empty
///   - NewPassword differs from CurrentPassword
/// All complexity rules (length, digit, upper, lower) are enforced
/// explicitly in AuthController.ChangePassword via PasswordPolicy.Validate,
/// and are tested in PasswordPolicyTests.cs.
/// </summary>
public class ChangePasswordValidatorTests
{
    private readonly ChangePasswordRequestValidator _validator = new();

    // ─── Valid password ────────────────────────────────────────────────────

    [Fact]
    public void Valid_Strong_Password_Should_Pass()
    {
        var req = new ChangePasswordRequest
        {
            CurrentPassword = "OldPass123!",
            NewPassword = "NewStrong1"
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeTrue();
    }

    // ─── CurrentPassword ───────────────────────────────────────────────────

    [Fact]
    public void Empty_CurrentPassword_Should_Fail()
    {
        var req = new ChangePasswordRequest
        {
            CurrentPassword = "",
            NewPassword = "NewStrong1"
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CurrentPassword");
    }

    // ─── NewPassword required ───────────────────────────────────────────────

    [Fact]
    public void Empty_NewPassword_Should_Fail()
    {
        var req = new ChangePasswordRequest
        {
            CurrentPassword = "OldPass123!",
            NewPassword = ""
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }

    // ─── Same as current ───────────────────────────────────────────────────

    [Fact]
    public void Same_As_Current_Password_Should_Fail()
    {
        var req = new ChangePasswordRequest
        {
            CurrentPassword = "SamePass1",
            NewPassword = "SamePass1"
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword" && e.ErrorMessage.Contains("مختلفة"));
    }

    // ─── Edge cases ────────────────────────────────────────────────────────

    /// <summary>
    /// SEC-11: the validator no longer enforces complexity — that's the
    /// controller's job via PasswordPolicy. So a 4-char new password PASSES
    /// the validator (the controller will reject it). This test documents
    /// the contract split.
    /// </summary>
    [Fact]
    public void Short_NewPassword_Passes_Validator_Complexity_Is_Enforced_In_Controller()
    {
        var req = new ChangePasswordRequest
        {
            CurrentPassword = "OldPass1",
            NewPassword = "Ab1" // only 3 chars — validator allows, controller rejects
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeTrue("complexity is enforced in the controller via PasswordPolicy, not the validator");
    }

    [Fact]
    public void Very_Long_Password_Should_Pass()
    {
        var req = new ChangePasswordRequest
        {
            CurrentPassword = "OldPass1!",
            NewPassword = "VeryLongPassword123"
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeTrue();
    }
}
