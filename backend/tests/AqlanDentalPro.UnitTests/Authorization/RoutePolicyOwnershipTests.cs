using System.Reflection;
using AqlanDentalPro.API.Configuration;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AqlanDentalPro.UnitTests.Authorization;

/// <summary>
/// CORE-P1-S4 — every canonical capability's backend owner is pinned to the authorization
/// policy it is supposed to enforce.
///
/// <para>
/// <c>CORE-REQ-007</c> says server authorization is the authority and hidden UI controls do
/// not substitute for it. Nothing enforced that. A controller that lost its
/// <c>[Authorize]</c> in a refactor, had its policy swapped for a broader one, or gained a
/// new <c>[AllowAnonymous]</c> endpoint would compile, pass every existing test, and ship —
/// the frontend would still hide the button, so nobody would notice until someone called the
/// endpoint directly.
/// </para>
/// <para>
/// These tests read the compiled assembly rather than the source text, so they see what ASP.NET
/// will actually enforce: attributes merged across partial classes and inherited from base
/// types. <c>FinanceV3Controller</c> alone is spread over nine files, only some of which carry
/// an attribute.
/// </para>
/// <para>
/// <b>This pins current behaviour; it does not claim current behaviour is ideal.</b> Where a
/// policy looks broader than a capability warrants, the entry below says so. The value is that
/// changing any of it now requires changing this file, which puts it in front of a reviewer.
/// </para>
/// </summary>
public sealed class RoutePolicyOwnershipTests
{
    /// <summary>
    /// The declared owner policy for every API controller, keyed by type name.
    ///
    /// <para>
    /// <c>null</c> means the controller is deliberately unauthenticated at class level. Only
    /// the WhatsApp webhook qualifies: Meta calls it with no bearer token, and it authenticates
    /// by verifying the request signature instead.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string?> ExpectedPolicies = new()
    {
        // ── Patients, appointments, daily operations ────────────────────────────────
        ["PatientsController"] = "StaffOnly",
        ["PatientJourneyController"] = "StaffOnly",
        ["PatientSegmentsController"] = "AdminOnly",
        ["AppointmentsController"] = "StaffOnly",
        ["DailyOperationsController"] = "StaffOnly",
        ["ClinicQueueController"] = "StaffOnly",
        ["BookingRequestsController"] = "AdminOrReception",
        ["SearchController"] = "StaffOnly",
        ["DashboardController"] = "StaffOnly",

        // ── Clinical records ───────────────────────────────────────────────────────
        // ClinicalRead is Admin + the three licensed clinical roles. Reception cannot read
        // a visit, a prescription, a clinical photo or a patient document.
        ["VisitsController"] = AuthorizationPolicyNames.ClinicalRead,
        ["PrescriptionsController"] = AuthorizationPolicyNames.ClinicalRead,
        ["ClinicalPhotosController"] = AuthorizationPolicyNames.ClinicalRead,
        // Declared in ClinicalPhotosController.cs, not in a file of its own — which is why a
        // survey by filename missed it and reflection did not.
        ["RadiographsController"] = AuthorizationPolicyNames.ClinicalRead,
        ["DocumentsController"] = AuthorizationPolicyNames.ClinicalRead,
        ["TreatmentPlanController"] = "StaffOnly",
        ["RadiologyOrdersController"] = "StaffOnly",
        ["ReferralsController"] = "StaffOnly",

        // ── Specialty workspaces ───────────────────────────────────────────────────
        ["OrthoCasesController"] = "OrthoAccess",
        ["OrthoCaseAiController"] = "OrthoAccess",
        ["OrthoModelAnalysesController"] = "OrthoAccess",
        ["OrthoSurgicalCasesController"] = "OrthoSurgicalAccess",
        ["GeneralController"] = "GeneralAccess",
        ["SurgeryController"] = "SurgeryAccess",

        // ── Cephalometry (frozen; policies still pinned) ────────────────────────────
        ["CephController"] = "OrthoAccess",
        ["CephPilotController"] = "OrthoAccess",
        ["CephAiModelsController"] = "AdminOnly",
        ["CephBenchmarkController"] = "AdminOnly",
        ["PhotoAnalysisController"] = "OrthoAccess",
        // Reference norms are reference data, not patient data — readable by any staff.
        ["CephNormsController"] = "StaffOnly",

        // ── Lab ────────────────────────────────────────────────────────────────────
        ["LabOrdersController"] = "StaffOnly",
        ["LabsController"] = "StaffOnly",
        ["LabPayablesController"] = "StaffOnly",
        ["LabReportsController"] = "StaffOnly",
        ["LabWorkTypesController"] = "StaffOnly",
        ["LabWorkPricesController"] = "StaffOnly",

        // ── Finance ────────────────────────────────────────────────────────────────
        // FinanceAccess is Admin + Reception + Accountant (reception takes payments);
        // ReportsAccess and FinanceWrite are Admin + Accountant only.
        ["InvoicesController"] = "FinanceAccess",
        ["PaymentsController"] = "FinanceAccess",
        ["ContractsController"] = "FinanceAccess",
        ["CashierSessionsController"] = "FinanceAccess",
        ["TreasuriesController"] = "FinanceAccess",
        ["VaultTransfersController"] = "FinanceAccess",
        ["FinanceV3Controller"] = "ReportsAccess",
        ["FinanceV3SuppliersController"] = "FinanceAccess",
        ["AdvancePaymentController"] = "ReportsAccess",
        ["OperationalExpensesController"] = "ReportsAccess",
        ["PartyAccountStatementsController"] = "ReportsAccess",
        ["SupplierBillsController"] = "ReportsAccess",
        ["CommissionsController"] = "CommissionView",
        ["ReportsController"] = "ReportsAccess",
        ["OperationalReportsController"] = "ReportsAccess",

        // ── Inventory and purchasing ───────────────────────────────────────────────
        ["InventoryController"] = "AdminOnly",
        ["PurchaseOrdersController"] = "AdminOnly",
        ["SuppliersController"] = "AdminOnly",
        ["ServiceConsumablesController"] = "StaffOnly",
        ["TreatmentPackagesController"] = "StaffOnly",

        // ── Staff, HR, payroll ─────────────────────────────────────────────────────
        ["EmployeesController"] = "AdminOnly",
        ["EmployeeDocumentsController"] = "StaffOnly",
        ["AttendanceController"] = "StaffOnly",
        ["LeaveController"] = "StaffOnly",
        ["SalaryController"] = "ReportsAccess",
        ["DoctorsController"] = "StaffOnly",
        ["DoctorSchedulesController"] = "StaffOnly",

        // ── Administration and settings ────────────────────────────────────────────
        ["UsersController"] = "AdminOnly",
        ["SettingsController"] = "AdminOnly",
        // Class default is the floor, not the whole story: the active-list read is StaffOnly
        // and every mutation carries its own AdminOnly, which combines with this rather than
        // replacing it. Declaring the floor is what lets this test catch a controller losing
        // authorization entirely.
        ["ServicesSettingsController"] = "StaffOnly",
        ["RoomsSettingsController"] = "StaffOnly",
        ["AiSettingsController"] = "AdminOnly",
        ["DocumentTemplatesController"] = "AdminOnly",
        ["AuditLogsController"] = "AdminOnly",
        ["BackupController"] = "AdminOnly",
        ["EmailStatsController"] = "AdminOnly",
        ["BranchesController"] = "StaffOnly",

        // ── Communications ─────────────────────────────────────────────────────────
        ["MessagesController"] = "StaffOnly",
        ["NotificationsController"] = "StaffOnly",
        ["SmsController"] = "StaffOnly",
        ["WhatsAppController"] = "StaffOnly",

        // ── Auth, portal, public ───────────────────────────────────────────────────
        ["AuthController"] = "StaffOnly",
        // Serves both patients (PatientAccess) and staff managing credentials
        // (AdminOrReception). No single role policy fits both, so the class default only
        // requires authentication and each action carries the policy that does the role work.
        ["PatientPortalController"] = null,
        ["PatientPortalMessagesController"] = "PatientAccess",
        ["PublicController"] = "StaffOnly",
        ["UploadsController"] = "StaffOnly",

        // Authenticates by verifying Meta's request signature, not by bearer token.
        ["WhatsAppWebhookController"] = null,
    };

    /// <summary>
    /// Every action reachable without authentication, as "Controller.Action".
    ///
    /// <para>
    /// This is the system's entire unauthenticated attack surface. A new entry appearing here
    /// is the single change most worth a second reader, which is the whole reason the list is
    /// checked in rather than derived.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> ExpectedAnonymousActions =
    [
        // Staff sign-in and recovery.
        "AuthController.Login",
        "AuthController.RefreshToken",
        "AuthController.UnlockAccount",
        "AuthController.ForgotPassword",
        "AuthController.ResetPassword",

        // Patient portal sign-in and recovery, plus the clinic card shown before login.
        "PatientPortalController.Login",
        "PatientPortalController.RefreshToken",
        "PatientPortalController.ForgotPassword",
        "PatientPortalController.ResetPassword",
        "PatientPortalController.GetClinicInfo",

        // Public website booking.
        "PublicController.GetWebsiteSettings",
        "PublicController.GetBookingServices",
        "PublicController.GetDoctors",
        "PublicController.GetAvailableSlots",
        "BookingRequestsController.GetAvailability",
        "BookingRequestsController.Create",
        "BookingRequestsController.CancelPublicBooking",

        // The waiting-room room list, for the display screen that runs with nobody signed in.
        "ClinicQueueController.GetRooms",

        // GetDisplay carries [AllowAnonymous] but is NOT actually anonymous: CORE-PAT-020's
        // QueueDisplayAuthenticationMiddleware demands StaffOnly for its path before endpoint
        // authorization runs, precisely so a loosened attribute cannot expose the live queue
        // to the internet. Verified at runtime: the path answers 401 with no token. It is
        // listed because the attribute is real and the reflection above finds it; the
        // middleware test below is what keeps the second lock on.
        "ClinicQueueController.GetDisplay",

        // Serves an uploaded file by name. Anonymous because <img> and <a> cannot carry a
        // bearer token; guarded by a path-traversal check and an unguessable stored name.
        "UploadsController.Download",
    ];

    private static IReadOnlyList<Type> ApiControllers() =>
        typeof(AqlanDentalPro.API.Controllers.PatientsController).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && t is { IsAbstract: false, IsPublic: true })
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The class-level policy ASP.NET will enforce, or null when the controller allows
    /// anonymous access at class level. Walks base types because inherited attributes apply.
    /// </summary>
    private static string? EffectivePolicy(Type controller)
    {
        if (controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null)
            return null;

        return controller.GetCustomAttribute<AuthorizeAttribute>(inherit: true)?.Policy;
    }

    [Fact]
    public void Every_controller_enforces_its_declared_policy()
    {
        var actual = ApiControllers().ToDictionary(c => c.Name, EffectivePolicy);

        var wrong = actual
            .Where(pair => ExpectedPolicies.TryGetValue(pair.Key, out var expected) && expected != pair.Value)
            .Select(pair => $"{pair.Key}: declared '{ExpectedPolicies[pair.Key] ?? "(anonymous)"}' "
                          + $"but enforces '{pair.Value ?? "(anonymous)"}'")
            .ToList();

        wrong.Should().BeEmpty(
            "a controller's authorization was changed without updating this contract. If the "
            + "change is intended, update the entry here so the change is visible in review.");
    }

    /// <summary>
    /// A controller nobody declared is the dangerous case: it is new, and its authorization has
    /// never been read by anyone but its author.
    /// </summary>
    [Fact]
    public void Every_controller_appears_in_the_contract()
    {
        var undeclared = ApiControllers()
            .Select(c => c.Name)
            .Where(name => !ExpectedPolicies.ContainsKey(name))
            .ToList();

        undeclared.Should().BeEmpty(
            "a new API controller must declare its owner policy in RoutePolicyOwnershipTests "
            + "before it ships, so that its authorization is read by someone other than its author");
    }

    /// <summary>
    /// Guards against the contract outliving the code: an entry left behind after a controller
    /// is deleted or renamed silently stops protecting anything.
    /// </summary>
    [Fact]
    public void The_contract_names_no_controller_that_no_longer_exists()
    {
        var live = ApiControllers().Select(c => c.Name).ToHashSet(StringComparer.Ordinal);

        ExpectedPolicies.Keys.Where(name => !live.Contains(name)).Should().BeEmpty(
            "a stale entry protects nothing — remove it when its controller is removed or renamed");
    }

    /// <summary>
    /// The single check with the most direct security value: no controller may sit behind no
    /// authorization at all unless it was declared that way on purpose.
    /// </summary>
    [Fact]
    public void No_controller_is_left_without_authorization_by_accident()
    {
        var unprotected = ApiControllers()
            .Where(c => EffectivePolicy(c) is null)
            .Select(c => c.Name)
            .Where(name => !(ExpectedPolicies.TryGetValue(name, out var declared) && declared is null))
            .ToList();

        unprotected.Should().BeEmpty(
            "this controller enforces no class-level authorization. Add a policy, or declare it "
            + "as deliberately anonymous here with the reason it is safe.");
    }

    /// <summary>
    /// The unauthenticated attack surface must not grow without someone noticing.
    /// </summary>
    [Fact]
    public void The_anonymous_surface_is_exactly_what_was_declared()
    {
        var actual = AnonymousActions();

        var added = actual.Except(ExpectedAnonymousActions).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var removed = ExpectedAnonymousActions.Except(actual).OrderBy(x => x, StringComparer.Ordinal).ToList();

        added.Should().BeEmpty(
            "a new endpoint is reachable with no authentication. If that is intended, add it here "
            + "with the reason it is safe to expose.");

        removed.Should().BeEmpty(
            "an endpoint listed here is no longer anonymous — remove the stale entry so the list "
            + "keeps describing the real surface");
    }

    /// <summary>
    /// A misspelled policy name is not a compile error. ASP.NET raises it only when a request
    /// arrives, turning it into a 500 on a live endpoint rather than a failed build.
    /// </summary>
    [Fact]
    public async Task Every_policy_a_controller_names_is_actually_registered()
    {
        var services = new ServiceCollection();
        services.AddAuthorizationPolicies();
        var provider = services.BuildServiceProvider().GetRequiredService<IAuthorizationPolicyProvider>();

        var named = ApiControllers()
            .SelectMany(c => c.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .Concat(c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>(inherit: true))))
            .Select(a => a.Policy)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        named.Should().NotBeEmpty("the reflection above must actually find policies to check");

        var missing = new List<string>();
        foreach (var policy in named)
        {
            if (await provider.GetPolicyAsync(policy!) is null) missing.Add(policy!);
        }

        missing.Should().BeEmpty(
            "a controller names an authorization policy that is never registered. ASP.NET throws "
            + "on the first request to it, so this is a 500 in production, not a build failure.");
    }

    /// <summary>
    /// Every action must be covered by an explicit decision — a class-level policy, its own
    /// <c>[Authorize]</c>, or its own <c>[AllowAnonymous]</c>.
    ///
    /// <para>
    /// This closes a structural gap rather than a live hole. <c>AuthController</c> carried no
    /// class-level policy and relied on each action opting in; all of them did, but an action
    /// added later that forgot both attributes would have been publicly reachable — on the
    /// controller that issues tokens, unlocks accounts and impersonates users. The controller
    /// now denies by default, and this test keeps any other controller from drifting into the
    /// same shape.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_action_is_covered_by_an_explicit_authorization_decision()
    {
        var uncovered = new List<string>();

        foreach (var controller in ApiControllers())
        {
            var classLevelDecision =
                controller.GetCustomAttribute<AuthorizeAttribute>(inherit: true) is not null ||
                controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null;

            if (classLevelDecision) continue;

            foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.DeclaringType?.Assembly != controller.Assembly) continue;
                if (method.IsSpecialName) continue;
                if (method.GetCustomAttributes().All(attr => attr is not IActionHttpMethodProvider)) continue;

                var covered =
                    method.GetCustomAttribute<AuthorizeAttribute>(inherit: true) is not null ||
                    method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null;

                if (!covered) uncovered.Add($"{controller.Name}.{method.Name}");
            }
        }

        uncovered.Should().BeEmpty(
            "this action inherits no class-level policy and declares none of its own, so it is "
            + "reachable without authentication. Give its controller a default policy.");
    }

    /// <summary>
    /// CORE-PAT-020's second lock must stay on.
    ///
    /// <para>
    /// <c>ClinicQueueController.GetDisplay</c> carries <c>[AllowAnonymous]</c> so the waiting-room
    /// screen can poll it, and <c>QueueDisplayAuthenticationMiddleware</c> is what actually keeps
    /// the live queue — patient names, on a public URL — off the internet. Deleting the
    /// middleware would leave the attribute behind and silently publish it, with every test
    /// still green. So the paths it guards are pinned here.
    /// </para>
    /// </summary>
    [Fact]
    public void The_queue_display_paths_are_still_guarded_by_middleware()
    {
        var guard = typeof(AqlanDentalPro.API.Middleware.QueueDisplayAuthenticationMiddleware);

        var paths = (string[])guard
            .GetField("ProtectedPaths", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        paths.Should().Contain("/api/clinic-queue/display",
            "the anonymous display endpoint relies on this middleware, not on its own attribute");
        paths.Should().Contain("/api/public/queue");
    }

    private static List<string> AnonymousActions()
    {
        var found = new List<string>();

        foreach (var controller in ApiControllers())
        {
            // A class-level [AllowAnonymous] is covered by the policy contract above; listing
            // each of its actions here would bury the per-action entries that matter.
            if (controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null)
                continue;

            foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.DeclaringType?.Assembly != controller.Assembly) continue;
                if (method.IsSpecialName) continue;
                if (method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is null) continue;

                found.Add($"{controller.Name}.{method.Name}");
            }
        }

        return found.Distinct(StringComparer.Ordinal).ToList();
    }
}
