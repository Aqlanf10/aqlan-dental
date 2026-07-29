using System.Text;
using System.Text.Json;
using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Reports;

public sealed class OperationalReportsControllerTests
{
    [Fact]
    public void Controller_RequiresReportsAccess()
    {
        var authorize = typeof(OperationalReportsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        authorize.Policy.Should().Be("ReportsAccess");
    }

    [Fact]
    public async Task Income_SeparatesCurrencies_AndScopesNonAdminToTheirBranch()
    {
        await using var db = CreateDb();
        var today = ClinicTimeProvider.ClinicToday();
        var branchA = AddBranch(db, "A");
        var branchB = AddBranch(db, "B");
        var patientA = AddPatient(db, branchA, "P-A");
        var patientB = AddPatient(db, branchB, "P-B");
        var inactive = NewPayment(patientA.Id, branchA.Id, today, 777m, "YER", "R-INACTIVE");
        inactive.IsActive = false;
        db.Payments.AddRange(
            NewPayment(patientA.Id, branchA.Id, today, 10_000m, "YER", "R-YER"),
            NewPayment(patientA.Id, branchA.Id, today, 100m, "SAR", "R-SAR"),
            NewPayment(patientA.Id, branchA.Id, today, 20m, "USD", "R-USD"),
            NewPayment(patientB.Id, branchB.Id, today, 999m, "SAR", "R-OTHER"),
            inactive);
        await db.SaveChangesAsync();

        var controller = BuildController(db, branchA.Id);
        var result = await controller.GetDetails("income", today.ToString("yyyy-MM-dd"), today.ToString("yyyy-MM-dd"));
        using var json = ResultJson(result);

        json.RootElement.GetProperty("totalRows").GetInt32().Should().Be(3);
        var summary = json.RootElement.GetProperty("summary").EnumerateArray().ToList();
        summary.Any(item =>
            item.TryGetProperty("currency", out var currency) && currency.GetString() == "YER"
            && item.GetProperty("value").GetDecimal() == 10_000m).Should().BeTrue();
        summary.Any(item =>
            item.TryGetProperty("currency", out var currency) && currency.GetString() == "SAR"
            && item.GetProperty("value").GetDecimal() == 100m).Should().BeTrue();
        summary.Any(item =>
            item.TryGetProperty("currency", out var currency) && currency.GetString() == "USD"
            && item.GetProperty("value").GetDecimal() == 20m).Should().BeTrue();

        var serialized = json.RootElement.GetRawText();
        serialized.Should().Contain("R-SAR");
        serialized.Should().NotContain("R-OTHER");
        serialized.Should().NotContain("R-INACTIVE");
    }

    [Fact]
    public async Task OutstandingBalances_GroupsPatientAmountsByCurrencyWithoutCrossCurrencyAddition()
    {
        await using var db = CreateDb();
        var branch = AddBranch(db, "A");
        var patient = AddPatient(db, branch, "P-001");
        var sarContract = new Contract
        {
            PatientId = patient.Id,
            Currency = "SAR",
            TotalAmount = 1_000m,
            DiscountAmount = 100m,
            Status = ContractStatus.Active
        };
        var usdContract = new Contract
        {
            PatientId = patient.Id,
            Currency = "USD",
            TotalAmount = 500m,
            Status = ContractStatus.Active
        };
        db.Contracts.AddRange(sarContract, usdContract);
        db.Invoices.Add(new Invoice
        {
            PatientId = patient.Id,
            InvoiceNumber = "OPEN-001",
            Currency = "SAR",
            Status = InvoiceStatus.Issued,
            Subtotal = 300m,
            TotalAmount = 300m,
            IsOpeningBalance = true
        });
        db.Payments.Add(NewPayment(patient.Id, branch.Id, ClinicTimeProvider.ClinicToday(), 200m, "SAR", "R-1", sarContract.Id));
        await db.SaveChangesAsync();

        var controller = BuildController(db, branch.Id);
        var result = await controller.GetDetails("outstanding-balances", null, null);
        using var json = ResultJson(result);
        var rows = json.RootElement.GetProperty("rows").EnumerateArray().ToList();

        rows.Should().HaveCount(2);
        rows.Single(row => row.GetProperty("currency").GetString() == "SAR")
            .GetProperty("remaining").GetDecimal().Should().Be(1_000m);
        rows.Single(row => row.GetProperty("currency").GetString() == "USD")
            .GetProperty("remaining").GetDecimal().Should().Be(500m);
    }

    [Fact]
    public async Task ReturningPatients_RequiresConfiguredAbsenceGap_AndFlagsDifferentTreatment()
    {
        await using var db = CreateDb();
        var today = ClinicTimeProvider.ClinicToday();
        var branch = AddBranch(db, "A");
        var patient = AddPatient(db, branch, "P-001");
        db.Visits.AddRange(
            new Visit
            {
                PatientId = patient.Id,
                VisitDate = today.AddDays(-120),
                TreatmentDone = "حشوة"
            },
            new Visit
            {
                PatientId = patient.Id,
                VisitDate = today,
                TreatmentDone = "تنظيف"
            });
        await db.SaveChangesAsync();

        var controller = BuildController(db, branch.Id);
        var result = await controller.GetDetails(
            "returning-patients",
            today.AddDays(-1).ToString("yyyy-MM-dd"),
            today.ToString("yyyy-MM-dd"),
            absenceDays: 90);
        using var json = ResultJson(result);
        var row = json.RootElement.GetProperty("rows").EnumerateArray().Single();

        row.GetProperty("absenceDays").GetInt32().Should().Be(120);
        row.GetProperty("differentTreatment").GetBoolean().Should().BeTrue();
        row.GetProperty("previousTreatment").GetString().Should().Be("حشوة");
        row.GetProperty("currentTreatment").GetString().Should().Be("تنظيف");
    }

    [Fact]
    public async Task TreatmentProgress_UsesThePatientsFullPlanAfterPeriodSelection()
    {
        await using var db = CreateDb();
        var today = ClinicTimeProvider.ClinicToday();
        var (utcStart, _) = ClinicTimeProvider.ToUtcRange(today);
        var branch = AddBranch(db, "A");
        var patient = AddPatient(db, branch, "P-001");
        db.PatientTreatmentPlanSteps.AddRange(
            new PatientTreatmentPlanStep
            {
                PatientId = patient.Id,
                SequenceNumber = 1,
                Title = "فحص",
                Status = TreatmentStepStatus.Completed,
                CompletedDate = today,
                CreatedAt = utcStart.AddHours(1)
            },
            new PatientTreatmentPlanStep
            {
                PatientId = patient.Id,
                SequenceNumber = 2,
                Title = "علاج",
                Status = TreatmentStepStatus.InProgress,
                PlannedDate = today,
                CreatedAt = utcStart.AddHours(1)
            },
            new PatientTreatmentPlanStep
            {
                PatientId = patient.Id,
                SequenceNumber = 3,
                Title = "متابعة لاحقة",
                Status = TreatmentStepStatus.Planned,
                PlannedDate = today.AddDays(30),
                CreatedAt = utcStart.AddDays(-30)
            });
        await db.SaveChangesAsync();

        var controller = BuildController(db, branch.Id);
        var result = await controller.GetDetails(
            "treatment-progress",
            today.ToString("yyyy-MM-dd"),
            today.ToString("yyyy-MM-dd"));
        using var json = ResultJson(result);
        var row = json.RootElement.GetProperty("rows").EnumerateArray().Single();

        row.GetProperty("totalSteps").GetInt32().Should().Be(3);
        row.GetProperty("completedSteps").GetInt32().Should().Be(1);
        row.GetProperty("remainingSteps").GetInt32().Should().Be(2);
        row.GetProperty("completionRate").GetDecimal().Should().Be(33.3m);
        row.GetProperty("nextStep").GetString().Should().Be("علاج");
    }

    [Fact]
    public async Task NewPatients_UsesClinicLocalDayBoundary()
    {
        await using var db = CreateDb();
        var today = ClinicTimeProvider.ClinicToday();
        var (utcStart, utcEnd) = ClinicTimeProvider.ToUtcRange(today);
        var branch = AddBranch(db, "A");
        var inside = AddPatient(db, branch, "IN");
        var before = AddPatient(db, branch, "BEFORE");
        var after = AddPatient(db, branch, "AFTER");
        await db.SaveChangesAsync();
        inside.CreatedAt = utcStart.AddMinutes(1);
        before.CreatedAt = utcStart.AddTicks(-1);
        after.CreatedAt = utcEnd;
        await db.SaveChangesAsync();

        var controller = BuildController(db, branch.Id);
        var result = await controller.GetDetails(
            "new-patients",
            today.ToString("yyyy-MM-dd"),
            today.ToString("yyyy-MM-dd"));
        using var json = ResultJson(result);
        var raw = json.RootElement.GetRawText();

        json.RootElement.GetProperty("totalRows").GetInt32().Should().Be(1);
        raw.Should().Contain("IN");
        raw.Should().NotContain("BEFORE");
        raw.Should().NotContain("AFTER");
    }

    [Fact]
    public async Task ExportIncome_IncludesCurrencyColumns_AndUtf8Bom()
    {
        await using var db = CreateDb();
        var today = ClinicTimeProvider.ClinicToday();
        var branch = AddBranch(db, "A");
        var patient = AddPatient(db, branch, "P-001");
        db.Payments.Add(NewPayment(patient.Id, branch.Id, today, 100m, "SAR", "R-SAR"));
        await db.SaveChangesAsync();

        var controller = BuildController(db, branch.Id);
        var result = await controller.Export(
            "income",
            today.ToString("yyyy-MM-dd"),
            today.ToString("yyyy-MM-dd"));

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.FileContents.Take(3).Should().Equal(Encoding.UTF8.GetPreamble());
        var csv = Encoding.UTF8.GetString(file.FileContents);
        csv.Should().Contain("عملة القبض");
        csv.Should().Contain("SAR");
        csv.Should().Contain("R-SAR");
    }

    [Fact]
    public async Task ExportPdfIncome_GeneratesPrintableArabicPdf()
    {
        await using var db = CreateDb();
        var today = ClinicTimeProvider.ClinicToday();
        var branch = AddBranch(db, "A");
        var patient = AddPatient(db, branch, "P-001");
        db.Payments.AddRange(
            NewPayment(patient.Id, branch.Id, today, 10_000m, "YER", "R-YER"),
            NewPayment(patient.Id, branch.Id, today, 100m, "SAR", "R-SAR"),
            NewPayment(patient.Id, branch.Id, today, 20m, "USD", "R-USD"));
        await db.SaveChangesAsync();

        var controller = BuildController(db, branch.Id);
        var result = await controller.ExportPdf(
            "income",
            today.ToString("yyyy-MM-dd"),
            today.ToString("yyyy-MM-dd"));

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("application/pdf");
        file.FileDownloadName.Should().EndWith(".pdf");
        file.FileContents.Take(5).Should().Equal("%PDF-"u8.ToArray());
        file.FileContents.Length.Should().BeGreaterThan(5_000);
    }

    [Fact]
    public async Task LegacyFinancialReport_AlsoKeepsCurrencyTotalsSeparate()
    {
        await using var db = CreateDb();
        var today = ClinicTimeProvider.ClinicToday();
        var branch = AddBranch(db, "A");
        var patient = AddPatient(db, branch, "P-001");
        var inactive = NewPayment(patient.Id, branch.Id, today, 999m, "YER", "R-INACTIVE");
        inactive.IsActive = false;
        db.Payments.AddRange(
            NewPayment(patient.Id, branch.Id, today, 5_000m, "YER", "R-YER"),
            NewPayment(patient.Id, branch.Id, today, 50m, "SAR", "R-SAR"),
            inactive);
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            Type = TransactionType.Outflow,
            Category = FinancialCategory.OperationalExpense,
            Amount = 10m,
            Currency = "SAR",
            TransactionDate = today,
            BranchId = branch.Id,
            Description = "مصروف",
            PerformedBy = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var currentUser = CreateCurrentUser(branch.Id);
        var controller = new ReportsController(
            db,
            new Mock<IPdfService>().Object,
            new Mock<ILogger<ReportsController>>().Object,
            currentUser);
        var result = await controller.GetFinancialReport(
            today.ToString("yyyy-MM-dd"),
            today.ToString("yyyy-MM-dd"));
        using var json = ResultJson(result);
        var totals = json.RootElement.GetProperty("totalsByCurrency").EnumerateArray().ToList();

        totals.Single(total => total.GetProperty("currency").GetString() == "YER")
            .GetProperty("collected").GetDecimal().Should().Be(5_000m);
        var sar = totals.Single(total => total.GetProperty("currency").GetString() == "SAR");
        sar.GetProperty("collected").GetDecimal().Should().Be(50m);
        sar.GetProperty("expenses").GetDecimal().Should().Be(10m);
        sar.GetProperty("net").GetDecimal().Should().Be(40m);
    }

    [Fact]
    public async Task LegacyCenterSummary_YerScalarDoesNotAddForeignCurrency()
    {
        await using var db = CreateDb();
        var today = ClinicTimeProvider.ClinicToday();
        var branch = AddBranch(db, "A");
        var patient = AddPatient(db, branch, "P-001");
        var inactive = NewPayment(patient.Id, branch.Id, today, 999m, "YER", "R-INACTIVE");
        inactive.IsActive = false;
        db.Payments.AddRange(
            NewPayment(patient.Id, branch.Id, today, 5_000m, "YER", "R-YER"),
            NewPayment(patient.Id, branch.Id, today, 500m, "SAR", "R-SAR"),
            inactive);
        await db.SaveChangesAsync();

        var controller = new ReportsController(
            db,
            new Mock<IPdfService>().Object,
            new Mock<ILogger<ReportsController>>().Object,
            CreateCurrentUser(branch.Id));
        var result = await controller.GetCenterSummary(
            today.ToString("yyyy-MM-dd"),
            today.ToString("yyyy-MM-dd"));
        using var json = ResultJson(result);

        json.RootElement.GetProperty("totalRevenue").GetDecimal().Should().Be(5_000m);
    }

    [Theory]
    [InlineData("unsupported", "2026-01-01", "2026-01-02")]
    [InlineData("income", "2026-02-01", "2026-01-01")]
    public async Task InvalidRequests_Return400(string type, string from, string to)
    {
        await using var db = CreateDb();
        var controller = BuildController(db, isAdmin: true);

        var result = await controller.GetDetails(type, from, to);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task BranchlessNonAdmin_Returns403()
    {
        await using var db = CreateDb();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(user => user.IsAdmin).Returns(false);
        currentUser.Setup(user => user.BranchId).Returns((Guid?)null);
        var controller = new OperationalReportsController(db, currentUser.Object);

        var result = await controller.GetDetails("income", null, null);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static OperationalReportsController BuildController(
        AppDbContext db,
        Guid? branchId = null,
        bool isAdmin = false)
    {
        var currentUser = CreateCurrentUser(branchId, isAdmin);
        return new OperationalReportsController(db, currentUser);
    }

    private static ICurrentUserService CreateCurrentUser(Guid? branchId = null, bool isAdmin = false)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(user => user.IsAdmin).Returns(isAdmin);
        currentUser.Setup(user => user.BranchId).Returns(branchId);
        currentUser.Setup(user => user.Role).Returns(isAdmin ? UserRole.Admin : UserRole.Accountant);
        return currentUser.Object;
    }

    private static Branch AddBranch(AppDbContext db, string name)
    {
        var branch = new Branch { Name = name };
        db.Branches.Add(branch);
        return branch;
    }

    private static Patient AddPatient(
        AppDbContext db,
        Branch branch,
        string patientNumber,
        DateTime? createdAt = null)
    {
        var patient = new Patient
        {
            BranchId = branch.Id,
            PatientNumber = patientNumber,
            FirstName = "مريض",
            LastName = patientNumber,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
        db.Patients.Add(patient);
        return patient;
    }

    private static Payment NewPayment(
        Guid patientId,
        Guid branchId,
        DateOnly date,
        decimal amount,
        string currency,
        string receipt,
        Guid? contractId = null) =>
        new()
        {
            PatientId = patientId,
            BranchId = branchId,
            ContractId = contractId,
            PaymentDate = date,
            Amount = amount,
            AppliedAmount = amount,
            Currency = currency,
            AccountCurrency = currency,
            ReceiptNumber = receipt,
            PaymentMethod = "cash"
        };

    private static JsonDocument ResultJson(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        return JsonDocument.Parse(JsonSerializer.Serialize(
            ok.Value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
