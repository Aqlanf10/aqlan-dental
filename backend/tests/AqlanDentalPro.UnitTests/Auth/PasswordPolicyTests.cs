using AqlanDentalPro.Application.Common;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Auth;

/// <summary>
/// SEC-11 — unit tests for the centralized PasswordPolicy validator.
/// The policy is the single source of truth for password complexity
/// across portal + staff. These tests pin its contract.
/// </summary>
public class PasswordPolicyTests
{
    // ─── Required: 6 cases from the SEC-11 spec ──────────────────────────────

    [Fact]
    public void Validate_TooShort_ReturnsArabicError()
    {
        var (valid, error) = PasswordPolicy.Validate("Ab1xxxx"); // 7 chars — below MinLength=8

        valid.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
        error.Should().Contain("8");
        error.Should().Contain("كلمة المرور");
    }

    [Fact]
    public void Validate_NoDigit_ReturnsArabicError()
    {
        var (valid, error) = PasswordPolicy.Validate("NoDigitsHere");

        valid.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
        error.Should().Contain("رقم");
    }

    [Fact]
    public void Validate_NoUppercase_ReturnsArabicError()
    {
        var (valid, error) = PasswordPolicy.Validate("alllowercase1");

        valid.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
        error.Should().Contain("كبير");
    }

    [Fact]
    public void Validate_ValidPassword_ReturnsTrue()
    {
        var (valid, error) = PasswordPolicy.Validate("ValidPass1");

        valid.Should().BeTrue();
        error.Should().BeEmpty();
    }

    [Fact]
    public void Validate_Null_ReturnsArabicError()
    {
        var (valid, error) = PasswordPolicy.Validate(null);

        valid.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
        error.Should().Contain("مطلوبة");
    }

    [Fact]
    public void Validate_TooLong_ReturnsArabicError()
    {
        // MaxLength = 128 — build a 129-char string that otherwise meets policy.
        var tooLong = "Aa1" + new string('a', 126); // 3 + 126 = 129 chars
        tooLong.Length.Should().BeGreaterThan(PasswordPolicy.MaxLength);

        var (valid, error) = PasswordPolicy.Validate(tooLong);

        valid.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
        error.Should().Contain("128");
    }

    // ─── Bonus: edge cases & contract pins ───────────────────────────────────

    [Fact]
    public void Validate_WhitespaceOnly_ReturnsArabicError()
    {
        var (valid, error) = PasswordPolicy.Validate("   ");

        valid.Should().BeFalse();
        error.Should().Contain("مطلوبة");
    }

    [Fact]
    public void Validate_EmptyString_ReturnsArabicError()
    {
        var (valid, error) = PasswordPolicy.Validate("");

        valid.Should().BeFalse();
        error.Should().Contain("مطلوبة");
    }

    [Fact]
    public void Validate_NoLowercase_ReturnsArabicError()
    {
        var (valid, error) = PasswordPolicy.Validate("ALLUPPER1");

        valid.Should().BeFalse();
        error.Should().Contain("صغير");
    }

    [Fact]
    public void Validate_ExactlyMinLength_WithAllCategories_Passes()
    {
        // 8 chars with upper + lower + digit, no special — should pass since
        // RequireNonAlphanumeric = false (user-friendly for Arabic patients).
        var (valid, error) = PasswordPolicy.Validate("Abcd1234");

        valid.Should().BeTrue();
        error.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithSpecialChar_Passes()
    {
        // Special chars are allowed (just not required).
        var (valid, error) = PasswordPolicy.Validate("ValidPass1!");

        valid.Should().BeTrue();
        error.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ExactlyMaxLength_Passes()
    {
        var exact = "Aa1" + new string('a', PasswordPolicy.MaxLength - 3);
        exact.Length.Should().Be(PasswordPolicy.MaxLength);

        var (valid, error) = PasswordPolicy.Validate(exact);

        valid.Should().BeTrue();
        error.Should().BeEmpty();
    }
}
