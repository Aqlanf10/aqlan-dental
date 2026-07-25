from pathlib import Path

path = Path("frontend/src/components/patient/tabs/FinanceTab.tsx")
text = path.read_text(encoding="utf-8")

old = 'import api from "@/lib/api";\nimport { EmptyState } from "./EmptyState";'
new = 'import api from "@/lib/api";\nimport { extractErrorMessage } from "@/lib/errors";\nimport { EmptyState } from "./EmptyState";'
if old not in text:
    raise SystemExit("FinanceTab import target not found")
text = text.replace(old, new, 1)

old = '''  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [invoicesError, setInvoicesError] = useState(false);

  useEffect(() => {
    api
      .get<AccountStatement>(`/api/patients/${patientId}/account-statement`)
      .then((r) => {
        setStatement(r.data);
        setLoading(false);
      })
      .catch(() => {
        setError("تعذّر تحميل البيانات المالية");
        setLoading(false);
      });

    setInvoicesError(false);
    api
      .get<Invoice[]>(`/api/patients/${patientId}/invoices`)
      .then((r) => setInvoices(r.data))
      .catch(() => { setInvoicesError(true); });
  }, [patientId, refreshKey]);
'''
new = '''  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [invoicesError, setInvoicesError] = useState<string | null>(null);
  const [retryKey, setRetryKey] = useState(0);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    setInvoicesError(null);

    Promise.allSettled([
      api.get<AccountStatement>(`/api/patients/${patientId}/account-statement`),
      api.get<Invoice[]>(`/api/patients/${patientId}/invoices`),
    ]).then(([statementResult, invoicesResult]) => {
      if (cancelled) return;

      if (statementResult.status === "fulfilled") {
        setStatement(statementResult.value.data);
      } else {
        setStatement(null);
        setError(extractErrorMessage(statementResult.reason, "تعذّر تحميل البيانات المالية"));
      }

      if (invoicesResult.status === "fulfilled") {
        setInvoices(invoicesResult.value.data);
      } else {
        setInvoices([]);
        setInvoicesError(extractErrorMessage(invoicesResult.reason, "فشل تحميل الفواتير"));
      }

      setLoading(false);
    });

    return () => { cancelled = true; };
  }, [patientId, refreshKey, retryKey]);
'''
if old not in text:
    raise SystemExit("FinanceTab loading effect target not found")
text = text.replace(old, new, 1)

old = '''  if (error || !statement) {
    return error ? (
      <p className="text-sm text-red-500 text-center py-4">{error}</p>
    ) : (
      <EmptyState icon={Wallet} title="لا توجد بيانات مالية" description="لم يتم تسجيل أي معاملات مالية لهذا المريض" />
    );
  }
'''
new = '''  if (error || !statement) {
    return error ? (
      <div role="alert" className="text-center py-8">
        <p className="text-sm text-red-600 mb-2">{error}</p>
        <button
          type="button"
          onClick={() => setRetryKey((key) => key + 1)}
          className="text-xs font-semibold text-blue-600 underline decoration-dotted"
        >
          إعادة المحاولة
        </button>
      </div>
    ) : (
      <EmptyState icon={Wallet} title="لا توجد بيانات مالية" description="لم يتم تسجيل أي معاملات مالية لهذا المريض" />
    );
  }
'''
if old not in text:
    raise SystemExit("FinanceTab core error target not found")
text = text.replace(old, new, 1)

old = '''        {invoicesError ? (
          <div className="p-3 text-center">
            <p className="text-sm text-red-600 mb-2">فشل في تحميل الفواتير</p>
          </div>
'''
new = '''        {invoicesError ? (
          <div role="alert" className="p-3 text-center rounded-lg border border-amber-200 bg-amber-50">
            <p className="text-sm text-amber-800 mb-2">{invoicesError}</p>
            <button
              type="button"
              onClick={() => setRetryKey((key) => key + 1)}
              className="text-xs font-semibold text-blue-600 underline decoration-dotted"
            >
              إعادة المحاولة
            </button>
          </div>
'''
if old not in text:
    raise SystemExit("FinanceTab invoices error target not found")
text = text.replace(old, new, 1)
path.write_text(text, encoding="utf-8")

test = Path("frontend/src/__tests__/components/patient/FinanceTabRetryReliability.test.tsx")
test.parent.mkdir(parents=True, exist_ok=True)
test.write_text('''import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "@/lib/api";
import { FinanceTab } from "@/components/patient/tabs/FinanceTab";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn() },
}));

const statement = {
  patientId: "patient-1",
  patientName: "مريض تجريبي",
  patientNumber: "GM-2026-001",
  totalContracted: 100000,
  totalDiscounts: 5000,
  totalPaid: 25000,
  totalRemaining: 70000,
  activeContracts: 1,
  completedContracts: 0,
  contracts: [],
  totalPaymentsCount: 0,
  payments: [],
  recentPayments: [],
};

const invoice = {
  id: "invoice-1",
  invoiceNumber: "INV-001",
  status: "Issued",
  totalAmount: 10000,
  createdAt: "2026-07-25T00:00:00Z",
  updatedAt: "2026-07-25T00:00:00Z",
};

describe("FinanceTab retry reliability", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("retries a failed account statement without showing a false empty state", async () => {
    let recovered = false;
    vi.mocked(api.get).mockImplementation((url) => {
      if (String(url).endsWith("/account-statement")) {
        return recovered
          ? Promise.resolve({ data: statement })
          : Promise.reject({ response: { status: 503, data: { message: "كشف الحساب غير متاح" } } });
      }
      return Promise.resolve({ data: [] });
    });

    render(<FinanceTab patientId="patient-1" />);

    expect(await screen.findByRole("alert")).toHaveTextContent("كشف الحساب غير متاح");
    expect(screen.queryByText("لا توجد بيانات مالية")).not.toBeInTheDocument();

    recovered = true;
    fireEvent.click(screen.getByRole("button", { name: "إعادة المحاولة" }));

    expect(await screen.findByText("إجمالي العقود")).toBeInTheDocument();
    expect(screen.queryByText("كشف الحساب غير متاح")).not.toBeInTheDocument();
  });

  it("keeps the statement visible when only invoices fail and retries them", async () => {
    let invoicesRecovered = false;
    vi.mocked(api.get).mockImplementation((url) => {
      if (String(url).endsWith("/account-statement")) {
        return Promise.resolve({ data: statement });
      }
      return invoicesRecovered
        ? Promise.resolve({ data: [invoice] })
        : Promise.reject({ response: { status: 500, data: { message: "تعذر تحميل الفواتير الآن" } } });
    });

    render(<FinanceTab patientId="patient-1" />);

    expect(await screen.findByText("إجمالي العقود")).toBeInTheDocument();
    expect(screen.getByRole("alert")).toHaveTextContent("تعذر تحميل الفواتير الآن");
    expect(screen.queryByText("لا توجد فواتير مسجّلة")).not.toBeInTheDocument();

    invoicesRecovered = true;
    fireEvent.click(screen.getByRole("button", { name: "إعادة المحاولة" }));

    expect(await screen.findByText("INV-001")).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.queryByText("تعذر تحميل الفواتير الآن")).not.toBeInTheDocument();
    });
  });

  it("clears a previous error when refreshKey triggers a successful reload", async () => {
    let recovered = false;
    vi.mocked(api.get).mockImplementation((url) => {
      if (String(url).endsWith("/account-statement")) {
        return recovered
          ? Promise.resolve({ data: statement })
          : Promise.reject({ response: { status: 500, data: { message: "فشل أولي" } } });
      }
      return Promise.resolve({ data: [] });
    });

    const { rerender } = render(<FinanceTab patientId="patient-1" refreshKey={0} />);
    expect(await screen.findByRole("alert")).toHaveTextContent("فشل أولي");

    recovered = true;
    rerender(<FinanceTab patientId="patient-1" refreshKey={1} />);

    expect(await screen.findByText("إجمالي العقود")).toBeInTheDocument();
    expect(screen.queryByText("فشل أولي")).not.toBeInTheDocument();
  });
});
''', encoding="utf-8")

Path("scripts/core_pat_034_patch.py").unlink(missing_ok=True)
Path(".github/workflows/core-pat-034-bootstrap.yml").unlink(missing_ok=True)
