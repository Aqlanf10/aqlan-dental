from pathlib import Path
import re


def replace_once(path: Path, pattern: str, replacement: str, *, flags: int = 0, label: str) -> None:
    text = path.read_text(encoding="utf-8")
    updated, count = re.subn(pattern, replacement, text, count=1, flags=flags)
    if count != 1:
        raise SystemExit(f"CORE-PAT-025: expected one {label} match in {path}, found {count}")
    path.write_text(updated, encoding="utf-8")


# 1) Service contract: preserve global pagination, add a patient-scoped complete-history read.
interface_path = Path("backend/src/AqlanDentalPro.Application/Interfaces/Services/IPaymentService.cs")
replace_once(
    interface_path,
    re.escape("    Task<List<PaymentDto>> GetPaymentsAsync(int page, int pageSize, Guid? patientId);\n"),
    "    Task<List<PaymentDto>> GetPaymentsAsync(int page, int pageSize, Guid? patientId);\n"
    "    Task<List<PaymentDto>> GetPatientPaymentsAsync(Guid patientId);\n",
    label="payment service contract",
)

# 2) Service implementation: return all active payments for one patient, still branch-scoped.
service_path = Path("backend/src/AqlanDentalPro.Infrastructure/Services/PaymentService.cs")
service_text = service_path.read_text(encoding="utf-8")
marker = '''    public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest req)
'''
if marker not in service_text:
    raise SystemExit("CORE-PAT-025: PaymentService insertion marker not found")
method = '''    public async Task<List<PaymentDto>> GetPatientPaymentsAsync(Guid patientId)
    {
        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        var query = db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Where(p => p.PatientId == patientId && p.IsActive)
            .AsQueryable();

        if (branchId.HasValue)
            query = query.Where(p => p.BranchId == branchId.Value);

        return await query
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .Select(p => FinanceMappers.MapPayment(p))
            .ToListAsync();
    }

'''
service_path.write_text(service_text.replace(marker, method + marker, 1), encoding="utf-8")

# 3) Dedicated endpoint for a patient's complete payment history.
controller_path = Path("backend/src/AqlanDentalPro.API/Controllers/PaymentsController.cs")
controller_text = controller_path.read_text(encoding="utf-8")
controller_marker = '''    [HttpGet("payments/{id:guid}")]
'''
if controller_marker not in controller_text:
    raise SystemExit("CORE-PAT-025: PaymentsController insertion marker not found")
endpoint = '''    [HttpGet("patients/{patientId:guid}/payments")]
    public async Task<IActionResult> GetPatientPayments(Guid patientId)
    {
        if (!await CanAsync("finance.payments", "view")) return Deny();
        var result = await service.GetPatientPaymentsAsync(patientId);
        return Ok(result);
    }

'''
controller_path.write_text(controller_text.replace(controller_marker, endpoint + controller_marker, 1), encoding="utf-8")

# 4) Account statement DTO: full history plus legacy recent window for compatibility.
dto_path = Path("backend/src/AqlanDentalPro.Application/DTOs/Finance/ContractDto.cs")
replace_once(
    dto_path,
    re.escape("    public List<ContractStatementDto> Contracts { get; set; } = [];\n    public List<PaymentDto> RecentPayments { get; set; } = [];\n"),
    "    public List<ContractStatementDto> Contracts { get; set; } = [];\n"
    "    public int TotalPaymentsCount { get; set; }\n"
    "    public List<PaymentDto> Payments { get; set; } = [];\n"
    "    // Backward-compatible 20-item window for older clients.\n"
    "    public List<PaymentDto> RecentPayments { get; set; } = [];\n",
    label="account statement payment fields",
)

# 5) Account statement query: load complete active history once; expose all + legacy first 20.
read_path = Path("backend/src/AqlanDentalPro.Infrastructure/Services/FinanceReadService.cs")
read_text = read_path.read_text(encoding="utf-8")
old_query = '''        // FIX: Filter recentPayments to active only — inactive/refunded/cancelled
        // payments should not appear in the recent list.
        var recentPayments = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Where(p => p.PatientId == patientId && p.IsActive)
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .Take(20)
            .ToListAsync();
'''
new_query = '''        // CORE-PAT-025: the patient file needs the complete active payment history.
        // Keep RecentPayments below as a 20-item compatibility window for older clients,
        // but query and map the full patient-scoped list once.
        var paymentRows = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Where(p => p.PatientId == patientId && p.IsActive)
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .ToListAsync();
        var paymentHistory = paymentRows.Select(p => FinanceMappers.MapPayment(p)).ToList();
'''
if old_query not in read_text:
    raise SystemExit("CORE-PAT-025: FinanceReadService payment query not found")
read_text = read_text.replace(old_query, new_query, 1)
old_assignment = '''            Contracts        = contracts,
            RecentPayments   = recentPayments.Select(p => FinanceMappers.MapPayment(p)).ToList()
'''
new_assignment = '''            Contracts        = contracts,
            TotalPaymentsCount = paymentHistory.Count,
            Payments          = paymentHistory,
            RecentPayments    = paymentHistory.Take(20).ToList()
'''
if old_assignment not in read_text:
    raise SystemExit("CORE-PAT-025: FinanceReadService DTO assignment not found")
read_path.write_text(read_text.replace(old_assignment, new_assignment, 1), encoding="utf-8")

# 6) Frontend API types: optional full-history fields allow rolling deployment fallback.
type_path = Path("frontend/src/types/finance.ts")
replace_once(
    type_path,
    re.escape("  contracts: ContractStatement[];\n  recentPayments: Payment[];\n"),
    "  contracts: ContractStatement[];\n"
    "  totalPaymentsCount?: number;\n"
    "  payments?: Payment[];\n"
    "  recentPayments: Payment[];\n",
    label="frontend account statement fields",
)

# 7) PaymentsTab: use dedicated complete-history endpoint.
payments_tab = Path("frontend/src/components/patient/tabs/PaymentsTab.tsx")
replace_once(
    payments_tab,
    re.escape('api.get<Payment[]>(`/api/payments?patientId=${patientId}`).then((response) => response.data),'),
    'api.get<Payment[]>(`/api/patients/${patientId}/payments`).then((response) => response.data),',
    label="PaymentsTab complete-history endpoint",
)

# Update its regression test to fail the new endpoint rather than the old paginated one.
payments_test = Path("frontend/src/__tests__/components/patient/PaymentsTabLoadFailure.test.tsx")
replace_once(
    payments_test,
    re.escape('      if (url.startsWith("/api/payments")) {'),
    '      if (url.startsWith("/api/patients/patient-1/payments")) {',
    label="PaymentsTab test endpoint",
)

# 8) FinanceTab: display the complete history with backward-compatible fallback.
finance_tab = Path("frontend/src/components/patient/tabs/FinanceTab.tsx")
finance_text = finance_tab.read_text(encoding="utf-8")
old_destructure = '''  const { totalContracted, totalDiscounts, totalPaid, totalRemaining, activeContracts, completedContracts, contracts, recentPayments } = statement;
'''
new_destructure = '''  const { totalContracted, totalDiscounts, totalPaid, totalRemaining, activeContracts, completedContracts, contracts, recentPayments } = statement;
  const paymentHistory = statement.payments ?? recentPayments;
  const totalPaymentsCount = statement.totalPaymentsCount ?? paymentHistory.length;
'''
if old_destructure not in finance_text:
    raise SystemExit("CORE-PAT-025: FinanceTab destructure not found")
finance_text = finance_text.replace(old_destructure, new_destructure, 1)
finance_text = finance_text.replace("          المدفوعات الأخيرة\n", "          سجل المدفوعات ({totalPaymentsCount})\n", 1)
finance_text = finance_text.replace("        {recentPayments.length === 0 ? (", "        {paymentHistory.length === 0 ? (", 1)
finance_text = finance_text.replace("            {recentPayments.map((p) => (", "            {paymentHistory.map((p) => (", 1)
finance_tab.write_text(finance_text, encoding="utf-8")

# 9) Backend regression tests: >20 rows remain complete, legacy window stays 20.
backend_test = Path("backend/tests/AqlanDentalPro.UnitTests/Finance/CompletePatientPaymentHistoryTests.cs")
backend_test.write_text('''using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

public class CompletePatientPaymentHistoryTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static (PaymentService payments, FinanceReadService financeRead) CreateServices(AppDbContext db)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.IsAdmin).Returns(true);
        currentUser.SetupGet(user => user.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(user => user.BranchId).Returns((Guid?)null);

        var payments = new PaymentService(
            db,
            currentUser.Object,
            new Mock<INotificationService>().Object,
            NullLogger<PaymentService>.Instance,
            new Mock<ICommissionService>().Object,
            new Mock<IJournalEntryService>().Object);
        var financeRead = new FinanceReadService(db, currentUser.Object);
        return (payments, financeRead);
    }

    private static async Task<Guid> SeedPatientWithPayments(AppDbContext db, int count)
    {
        var patientId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        db.Patients.Add(new Patient
        {
            Id = patientId,
            PatientNumber = "P-HISTORY-001",
            FirstName = "سجل",
            LastName = "كامل",
            BranchId = branchId,
        });

        for (var index = 1; index <= count; index++)
        {
            db.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                BranchId = branchId,
                Amount = index * 1_000m,
                PaymentDate = new DateOnly(2026, 1, 1).AddDays(index),
                PaymentMethod = "cash",
                ServiceDescription = $"دفعة {index}",
                IsActive = true,
            });
        }

        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            BranchId = branchId,
            Amount = 999_000m,
            PaymentDate = new DateOnly(2026, 2, 1),
            PaymentMethod = "cash",
            IsActive = false,
        });
        await db.SaveChangesAsync();
        return patientId;
    }

    [Fact]
    public async Task PatientScopedPaymentRead_ReturnsAllActiveRowsBeyondDefaultPageSize()
    {
        await using var db = CreateDb();
        var patientId = await SeedPatientWithPayments(db, 25);
        var (payments, _) = CreateServices(db);

        var result = await payments.GetPatientPaymentsAsync(patientId);

        result.Should().HaveCount(25);
        result.Select(payment => payment.Amount).Should().BeInDescendingOrder();
        result.Should().NotContain(payment => payment.Amount == 999_000m);
    }

    [Fact]
    public async Task AccountStatement_ExposesFullHistoryAndKeepsLegacyRecentWindow()
    {
        await using var db = CreateDb();
        var patientId = await SeedPatientWithPayments(db, 25);
        var (_, financeRead) = CreateServices(db);

        var result = await financeRead.GetAccountStatementAsync(patientId);

        result.Should().NotBeNull();
        result!.TotalPaymentsCount.Should().Be(25);
        result.Payments.Should().HaveCount(25);
        result.RecentPayments.Should().HaveCount(20);
        result.Payments.First().Amount.Should().Be(25_000m);
        result.Payments.Last().Amount.Should().Be(1_000m);
    }
}
''', encoding="utf-8")

# 10) Frontend regression: full field wins over 20-item legacy window.
frontend_test = Path("frontend/src/__tests__/components/patient/FinanceTabCompleteHistory.test.tsx")
frontend_test.write_text('''import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "@/lib/api";
import { FinanceTab } from "@/components/patient/tabs/FinanceTab";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn() },
}));

const payment = (index: number) => ({
  id: `payment-${index}`,
  patientId: "patient-1",
  patientName: "مريض تجريبي",
  amount: index * 1000,
  paymentDate: `2026-01-${String(Math.min(index, 28)).padStart(2, "0")}`,
  serviceDescription: `دفعة رقم ${index}`,
});

describe("FinanceTab complete patient payment history", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    const payments = Array.from({ length: 25 }, (_, index) => payment(index + 1));
    vi.mocked(api.get).mockImplementation((url: string) => {
      if (url.includes("account-statement")) {
        return Promise.resolve({
          data: {
            patientId: "patient-1",
            patientName: "مريض تجريبي",
            patientNumber: "P-001",
            totalContracted: 0,
            totalDiscounts: 0,
            totalPaid: 325000,
            totalRemaining: 0,
            activeContracts: 0,
            completedContracts: 0,
            contracts: [],
            totalPaymentsCount: 25,
            payments,
            recentPayments: payments.slice(0, 20),
          },
        });
      }
      return Promise.resolve({ data: [] });
    });
  });

  it("renders payments beyond the legacy twenty-row window", async () => {
    render(<FinanceTab patientId="patient-1" />);

    expect(await screen.findByText("سجل المدفوعات (25)")).toBeInTheDocument();
    expect(screen.getByText("دفعة رقم 25")).toBeInTheDocument();
    expect(screen.getAllByText(/دفعة رقم/)).toHaveLength(25);
  });
});
''', encoding="utf-8")

for temporary in (
    Path(".github/workflows/core-pat-025-bootstrap.yml"),
    Path("scripts/core_pat_025_patch.py"),
):
    temporary.unlink(missing_ok=True)
