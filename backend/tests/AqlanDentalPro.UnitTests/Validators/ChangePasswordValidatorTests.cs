using AqlanDentalPro.Application.DTOs.Auth;
using AqlanDentalPro.Application.Validators;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Validators;

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
            NewPassword = "NewStrong1!"
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
            NewPassword = "NewStrong1!"
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

    // ─── Minimum length ────────────────────────────────────────────────────

    [Fact]
    public void Less_Than_8_Chars_Should_Fail()
    {
        var req = new ChangePasswordRequest
        {
            CurrentPassword = "OldPass123!",
            NewPassword = "Ab1!" // only 4 chars
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "NewPassword" && e.ErrorMessage.Contains("8"));
    }

    // ─── Uppercase ─────────────────────────────────────────────────────────

    [Fact]
    public void No_Uppercase_Should_Fail()
    {
        var req = new ChangePasswordRequest
        {
            CurrentPassword = "OldPass123!",
            NewPassword = "nouppercase1!"
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "NewPassword" && e.ErrorMessage.Contains("كبير"));
    }

    // ─── Lowercase ─────────────────────────────────────────────────────────

    [Fact]
    public void No_Lowercase_Should_Fail()
    {
        var req = new ChangePasswordRequest
        {
            CurrentPassword = "OldPass123!",
            NewPassword = "NOLOWERCASE1!"
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "NewPassword" && e.ErrorMessage.Contains("صغير"));
    }

    // ─── Digit ─────────────────────────────────────────────────────────────

    [Fact]
    public void No_Digit_Should_Fail()
    {
        var req = new ChangePasswordRequest
        {
            CurrentPassword = "OldPass123!",
            NewPassword = "NoDigits!!"
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "NewPassword" && e.ErrorMessage.Contains("رقم"));
    }

    // ─── Special character ─────────────────────────────────────────────────

    [Fact]
    public void No_Special_Char_Should_Fail()
    {
        var req = new ChangePasswordRequest
        {
            CurrentPassword = "OldPass123!",
            NewPassword = "NoSpecial1"
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "NewPassword" && e.ErrorMessage.Contains("رمز"));
    }

    // ─── Same as current ───────────────────────────────────────────────────

    [Fact]
    public void Same_As_Current_Password_Should_Fail()
    {
        var req = new ChangePasswordRequest
        {
            CurrentPassword = "SamePass1!",
            NewPassword = "SamePass1!"
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "NewPassword" && e.ErrorMessage.Contains("مختلفة"));
    }

    // ─── Edge cases ────────────────────────────────────────────────────────

    [Fact]
    public void Exactly_8_Chars_With_All_Requirements_Should_Pass()
    {
        var req = new ChangePasswordRequest
        {
            CurrentPassword = "OldPass1!",
            NewPassword = "Abcd123!" // exactly 8 chars
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Very_Long_Password_Should_Pass()
    {
        var req = new ChangePasswordRequest
        {
            CurrentPassword = "OldPass1!",
            NewPassword = "VeryLongPassword123!@#"
        };

        var result = _validator.Validate(req);
        result.IsValid.Should().BeTrue();
    }
}
