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

        // Second dry run: "أصدِر الفاتورة أولًا" was itself wrong for the person who reads it.
        // Reception holds finance.payments.create but is view-only on finance.invoices, so the
        // instruction pointed at a 403. Verified live — the issue endpoint answered
        // «غير مصرح لك بهذا الإجراء المالي» to the same account the refusal was addressed to.
        source.Should().Contain("الإصدار من صلاحية المحاسب أو المدير",
            "the refusal must name who can issue, not tell reception to do it themselves");
        source.Should().Contain("سجّل الدفعة على حساب المريض مباشرة بدون فاتورة",
            "reception's own path still works and the message should say so");
        source.Should().NotContain("أصدِر الفاتورة أولًا ثم سجّل الدفعة",
            "the old wording instructed reception to perform an action they are refused");
    }

    // ── Second dry run, walked as Reception ──────────────────────────────────

    /// <summary>
    /// Reception is told to set the due amount, but reception cannot: the amount comes from
    /// the doctor's handoff (<c>HandoffRequest.AmountDue</c>) or from the price of the service
    /// linked to the appointment. The old wording sent the receptionist looking for a field
    /// that does not exist on their screen.
    /// </summary>
    [Fact]
    public void The_zero_amount_refusal_names_who_sets_the_amount()
    {
        var source = Read("src", "AqlanDentalPro.Infrastructure", "Services", "CheckoutService.cs");

        source.Should().Contain("المبلغ المستحق يحدده الطبيب عند تسليم المريض للاستقبال",
            "reception cannot set this value, so the refusal must point at who can");
        source.Should().NotContain("بمبلغ صفر — حدد المبلغ المستحق أولاً",
            "the old wording asked reception to do something reception has no field for");
    }

    /// <summary>
    /// Every resource the API actually guards must carry an Arabic label, otherwise the owner
    /// sees raw keys such as "finance.invoices" in the roles screen. Found when the finance
    /// family was added to that screen: all twelve were enforced and none had a label.
    /// </summary>
    [Fact]
    public void Every_finance_resource_the_api_guards_has_an_arabic_label()
    {
        var users = Read("src", "AqlanDentalPro.API", "Controllers", "UsersController.cs");

        var guarded = new HashSet<string>();
        foreach (var dir in new[] { "AqlanDentalPro.API", "AqlanDentalPro.Infrastructure" })
        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(RepoRoot(), "src", dir), "*.cs", SearchOption.AllDirectories))
        foreach (Match call in Regex.Matches(
                     File.ReadAllText(file), @"(?:PermissionGuard\.HasAsync|CanAsync)\([^)]*\)"))
        foreach (Match literal in Regex.Matches(call.Value, "\"(finance\\.[a-z_]+)\""))
            guarded.Add(literal.Groups[1].Value);

        guarded.Should().NotBeEmpty("the scan must actually find the finance guards");

        foreach (var resource in guarded)
            users.Should().Contain($"[\"{resource}\"] = \"",
                $"{resource} is enforced by the API, so the roles screen must name it in Arabic");
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
