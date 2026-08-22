using System.Reflection;
using System.Text.RegularExpressions;
using AqlanDentalPro.API.Controllers;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.GoLive;

/// <summary>
/// Findings from the first end-to-end go-live dry run — a full clinic day walked on a clean
/// install: patient, appointment, confirm, arrive, call, room, visit, invoice, issue, cashier
/// shift, payment, complete, close, report.
///
/// <para>
/// None of these were caught by the existing suite, because each one is a *usable* system
/// behaving in a way that stops a real person completing a real task.
/// </para>
/// </summary>
public class OperationalFrictionTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "AqlanDentalPro.API")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return dir!.FullName;
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot() }.Concat(parts).ToArray()));

    /// <summary>
    /// The drawer count must be optional in the contract so that "not stated" is distinguishable
    /// from "counted, and empty". As plain decimals these defaulted to 0, and a close that
    /// omitted them booked a full shortage against whoever was on the till.
    /// </summary>
    [Fact]
    public void The_closing_cash_count_is_nullable_so_unstated_is_not_read_as_zero()
    {
        var property = typeof(CloseSessionRequest).GetProperty(
            nameof(CloseSessionRequest.ActualClosingCash), BindingFlags.Public | BindingFlags.Instance);

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(decimal?),
            "a non-nullable decimal defaults to 0, which is indistinguishable from a counted empty drawer");
    }

    /// <summary>
    /// Both close endpoints must refuse an unstated count. There are two — the dedicated
    /// controller and the FinanceV3 partial — and fixing only one leaves the hole open.
    /// </summary>
    [Theory]
    [InlineData("CashierSessionsController.cs")]
    [InlineData("FinanceV3Controller.CashierSessions.cs")]
    public void Every_close_endpoint_refuses_a_shift_closed_without_counting_the_drawer(string file)
    {
        var source = Read("src", "AqlanDentalPro.API", "Controllers", file);

        source.Should().Contain("ActualClosingCash is null",
            $"{file} must refuse a close that never states what was in the drawer");
        source.Should().Contain("أدخل النقد الفعلي",
            $"{file} must say so in Arabic, and say that an empty drawer is entered as an explicit zero");
    }

    /// <summary>
    /// The threshold is compared directly against a shortage computed from a rial drawer. It was
    /// documented as SAR — a figure larger by more than two orders of magnitude — which would
    /// make the manager co-sign unreachable in practice.
    /// </summary>
    [Fact]
    public void The_shortage_threshold_states_the_currency_it_is_actually_compared_in()
    {
        var source = Read("src", "AqlanDentalPro.API", "Controllers", "CashierSessionsController.cs");

        source.Should().NotContain("defaults to 5000 SAR",
            "the value is compared against a YER shortage; calling it SAR misstates the guard by ~100x");
        source.Should().Contain("BASE CURRENCY (YER)",
            "the unit has to be stated where the number is, or the next reader re-derives it wrong");
    }

    /// <summary>
    /// A refusal that only restates the rule leaves reception stuck at the desk. Both of these
    /// blocked the dry run until the source was read to discover the missing step.
    /// </summary>
    [Fact]
    public void Check_in_refusal_names_the_step_that_unblocks_it()
    {
        var source = Read("src", "AqlanDentalPro.API", "Controllers", "ClinicQueueController.cs");

        source.Should().Contain("أكّد الموعد أولًا",
            "reception must be told to confirm the appointment, not just that the transition is invalid");
        source.Should().NotMatch("*لا يمكن تغيير حالة الموعد من*إلى*",
            "the old message restated the state machine and named no remedy");
    }

    [Fact]
    public void Payment_refusal_on_a_draft_invoice_names_the_step_that_unblocks_it()
    {
        var source = Read("src", "AqlanDentalPro.Infrastructure", "Services", "PaymentService.cs");

        source.Should().Contain("أصدِر الفاتورة أولًا",
            "the cashier must be told to issue the invoice, not merely that only issued ones can be paid");
    }

    /// <summary>
    /// Guards the tests above against passing for the wrong reason: if the files could not be
    /// read, every Contain assertion would be checking an empty string.
    /// </summary>
    [Fact]
    public void The_sources_these_tests_read_are_actually_present()
    {
        foreach (var file in new[] { "CashierSessionsController.cs", "ClinicQueueController.cs" })
        {
            Read("src", "AqlanDentalPro.API", "Controllers", file).Length
                .Should().BeGreaterThan(1000, $"{file} must be readable for these assertions to mean anything");
        }
    }
}
