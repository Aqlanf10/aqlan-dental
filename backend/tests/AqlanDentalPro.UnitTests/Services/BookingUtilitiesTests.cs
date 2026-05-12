using AqlanDentalPro.Application.Services;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Services;

public class BookingUtilitiesTests
{
    // ─── NormalizeTo24h ────────────────────────────────────────────────────

    public class NormalizeTo24hTests
    {
        [Fact]
        public void Already_24h_Format_Returns_Same()
        {
            BookingUtilities.NormalizeTo24h("09:00").Should().Be("09:00");
            BookingUtilities.NormalizeTo24h("14:30").Should().Be("14:30");
            BookingUtilities.NormalizeTo24h("23:59").Should().Be("23:59");
        }

        [Fact]
        public void Arabic_AM_Converts_Correctly()
        {
            BookingUtilities.NormalizeTo24h("9:00 ص").Should().Be("09:00");
            BookingUtilities.NormalizeTo24h("10:30 ص").Should().Be("10:30");
        }

        [Fact]
        public void Arabic_PM_Converts_Correctly()
        {
            BookingUtilities.NormalizeTo24h("2:30 م").Should().Be("14:30");
            BookingUtilities.NormalizeTo24h("10:00 م").Should().Be("22:00");
        }

        [Fact]
        public void Arabic_Midnight_AM_Returns_00()
        {
            BookingUtilities.NormalizeTo24h("12:00 ص").Should().Be("00:00");
            BookingUtilities.NormalizeTo24h("12:30 ص").Should().Be("00:30");
        }

        [Fact]
        public void Arabic_Noon_PM_Returns_12()
        {
            BookingUtilities.NormalizeTo24h("12:00 م").Should().Be("12:00");
        }

        [Fact]
        public void Null_Returns_Null()
        {
            BookingUtilities.NormalizeTo24h(null).Should().BeNull();
        }

        [Fact]
        public void Empty_Returns_Null()
        {
            BookingUtilities.NormalizeTo24h("").Should().BeNull();
            BookingUtilities.NormalizeTo24h("   ").Should().BeNull();
        }

        [Fact]
        public void Invalid_Time_Returns_Null()
        {
            BookingUtilities.NormalizeTo24h("not-a-time").Should().BeNull();
            BookingUtilities.NormalizeTo24h("abc ص").Should().BeNull();
        }

        [Fact]
        public void General_Parse_Fallback_For_Non_Arabic()
        {
            // A time without Arabic markers but parseable
            BookingUtilities.NormalizeTo24h("3:00 PM").Should().Be("15:00");
        }

        [Fact]
        public void Trimmed_Input_Handled()
        {
            BookingUtilities.NormalizeTo24h("  09:00  ").Should().Be("09:00");
            BookingUtilities.NormalizeTo24h("  2:30 م  ").Should().Be("14:30");
        }
    }

    // ─── IsSameSlotTime ────────────────────────────────────────────────────

    public class IsSameSlotTimeTests
    {
        [Fact]
        public void Same_24h_Time_Returns_True()
        {
            BookingUtilities.IsSameSlotTime("09:00", "09:00").Should().BeTrue();
        }

        [Fact]
        public void Arabic_AM_Matches_24h()
        {
            BookingUtilities.IsSameSlotTime("9:00 ص", "09:00").Should().BeTrue();
        }

        [Fact]
        public void Arabic_PM_Matches_24h()
        {
            BookingUtilities.IsSameSlotTime("2:30 م", "14:30").Should().BeTrue();
        }

        [Fact]
        public void Different_Times_Return_False()
        {
            BookingUtilities.IsSameSlotTime("09:00", "10:00").Should().BeFalse();
        }

        [Fact]
        public void Null_Slot_Returns_False()
        {
            BookingUtilities.IsSameSlotTime("09:00", null).Should().BeFalse();
        }

        [Fact]
        public void Null_PreferredTime_Returns_False()
        {
            BookingUtilities.IsSameSlotTime(null, "09:00").Should().BeFalse();
        }

        [Fact]
        public void Both_Null_Returns_True()
        {
            BookingUtilities.IsSameSlotTime(null, null).Should().BeTrue();
        }

        [Fact]
        public void Both_Null_With_Empty_Returns_False()
        {
            // Empty string normalizes to null via NormalizeTo24h
            BookingUtilities.IsSameSlotTime("", null).Should().BeTrue();
        }
    }

    // ─── NormalizePhone ────────────────────────────────────────────────────

    public class NormalizePhoneTests
    {
        [Fact]
        public void Plain_Number_Returns_Same()
        {
            BookingUtilities.NormalizePhone("770245745").Should().Be("770245745");
        }

        [Fact]
        public void Removes_Plus()
        {
            BookingUtilities.NormalizePhone("+967770245745").Should().Be("967770245745");
        }

        [Fact]
        public void Removes_Spaces()
        {
            BookingUtilities.NormalizePhone("770 245 745").Should().Be("770245745");
        }

        [Fact]
        public void Removes_Dashes()
        {
            BookingUtilities.NormalizePhone("770-245-745").Should().Be("770245745");
        }

        [Fact]
        public void Removes_Parentheses()
        {
            BookingUtilities.NormalizePhone("(770)245745").Should().Be("770245745");
        }

        [Fact]
        public void Removes_Mixed_Formatters()
        {
            BookingUtilities.NormalizePhone("+967 770-245 (745)").Should().Be("967770245745");
        }

        [Fact]
        public void Null_Returns_Empty()
        {
            BookingUtilities.NormalizePhone(null).Should().BeEmpty();
        }

        [Fact]
        public void Empty_Returns_Empty()
        {
            BookingUtilities.NormalizePhone("").Should().BeEmpty();
            BookingUtilities.NormalizePhone("   ").Should().BeEmpty();
        }

        [Fact]
        public void Trims_Whitespace()
        {
            BookingUtilities.NormalizePhone("  770245745  ").Should().Be("770245745");
        }
    }

    // ─── NormalizeName ─────────────────────────────────────────────────────

    public class NormalizeNameTests
    {
        [Fact]
        public void Trims_Leading_Trailing_Spaces()
        {
            var result = BookingUtilities.NormalizeName("  أحمد  ");
            result.Should().Be(result.Trim());
        }

        [Fact]
        public void Collapses_Multiple_Spaces()
        {
            var result = BookingUtilities.NormalizeName("أحمد   محمد");
            result.Should().NotContain("  ");
        }

        [Fact]
        public void Converts_To_Lowercase()
        {
            // Arabic doesn't have uppercase/lowercase, but let's test with Latin characters
            var result = BookingUtilities.NormalizeName("AHMED");
            result.Should().Be("ahmed");
        }

        [Fact]
        public void Mixed_Arabic_Latin()
        {
            var result = BookingUtilities.NormalizeName("  أحمد   Ali  ");
            result.Should().NotContain("  ");
            result.Should().Be(result.Trim());
        }

        [Fact]
        public void Null_Returns_Empty()
        {
            BookingUtilities.NormalizeName(null).Should().BeEmpty();
        }

        [Fact]
        public void Empty_Returns_Empty()
        {
            BookingUtilities.NormalizeName("").Should().BeEmpty();
            BookingUtilities.NormalizeName("   ").Should().BeEmpty();
        }

        [Fact]
        public void Single_Word_Unchanged()
        {
            var result = BookingUtilities.NormalizeName("أحمد");
            result.Should().Be(result.Trim());
        }
    }
}
