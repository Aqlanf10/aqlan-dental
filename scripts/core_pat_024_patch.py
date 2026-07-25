from pathlib import Path
import re

path = Path("frontend/src/components/patient/tabs/PaymentsTab.tsx")
text = path.read_text(encoding="utf-8")


def replace_once(pattern: str, replacement: str, *, flags: int = 0, label: str) -> None:
    global text
    updated, count = re.subn(pattern, replacement, text, count=1, flags=flags)
    if count != 1:
        raise SystemExit(f"CORE-PAT-024: expected one {label} match, found {count}")
    text = updated


replace_once(
    re.escape('import api from "@/lib/api";\n'),
    'import api from "@/lib/api";\nimport { extractErrorMessage } from "@/lib/errors";\n',
    label="error helper import",
)

replace_once(
    r"  const fetchPayments = useCallback\(\(\) => \{.*?^  \}, \[patientId\]\);",
    '''  const fetchPayments = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [paymentsResult, contractsResult] = await Promise.allSettled([
        api.get<Payment[]>(`/api/payments?patientId=${patientId}`).then((response) => response.data),
        api.get<Contract[]>(`/api/contracts?patientId=${patientId}&status=active`).then((response) => response.data),
      ]);

      if (paymentsResult.status === "rejected") {
        setPayments([]);
        setContracts(contractsResult.status === "fulfilled" ? contractsResult.value : []);
        setError(extractErrorMessage(paymentsResult.reason, "فشل تحميل مدفوعات المريض — حاول مجدداً"));
        return;
      }

      setPayments(paymentsResult.value);
      if (contractsResult.status === "fulfilled") {
        setContracts(contractsResult.value);
      } else {
        setContracts([]);
        toast.error(
          extractErrorMessage(
            contractsResult.reason,
            "تعذر تحميل عقود المريض؛ المدفوعات متاحة لكن ربطها بالعقود غير متاح حالياً"
          )
        );
      }
    } finally {
      setLoading(false);
    }
  }, [patientId]);''',
    flags=re.MULTILINE | re.DOTALL,
    label="fetchPayments block",
)

stats_block = '''      {/* Stats */}
      <div className="grid grid-cols-2 gap-3">
        <div className="rounded-xl px-3 py-2 bg-green-50 flex items-center gap-2">
          <CreditCard className="w-4 h-4 text-green-600 flex-shrink-0" />
          <div className="min-w-0">
            <p className="text-xs text-[#94a3b8]">إجمالي المدفوعات</p>
            <p className="text-sm font-bold text-green-600">{totalPaid.toLocaleString()} ر.ي</p>
          </div>
        </div>
        <div className="rounded-xl px-3 py-2 bg-[#3d7ab518] flex items-center gap-2">
          <FileText className="w-4 h-4 text-[#3d7ab5] flex-shrink-0" />
          <div className="min-w-0">
            <p className="text-xs text-[#94a3b8]">عدد المدفوعات</p>
            <p className="text-sm font-bold text-[#3d7ab5]">{payments.length}</p>
          </div>
        </div>
      </div>
'''
if stats_block not in text:
    raise SystemExit("CORE-PAT-024: stats block not found")
text = text.replace(
    stats_block,
    '''      {/* Stats — only trustworthy after a successful payments response */}
      {!loading && !error && (
        <div className="grid grid-cols-2 gap-3">
          <div className="rounded-xl px-3 py-2 bg-green-50 flex items-center gap-2">
            <CreditCard className="w-4 h-4 text-green-600 flex-shrink-0" />
            <div className="min-w-0">
              <p className="text-xs text-[#94a3b8]">إجمالي المدفوعات</p>
              <p className="text-sm font-bold text-green-600">{totalPaid.toLocaleString()} ر.ي</p>
            </div>
          </div>
          <div className="rounded-xl px-3 py-2 bg-[#3d7ab518] flex items-center gap-2">
            <FileText className="w-4 h-4 text-[#3d7ab5] flex-shrink-0" />
            <div className="min-w-0">
              <p className="text-xs text-[#94a3b8]">عدد المدفوعات</p>
              <p className="text-sm font-bold text-[#3d7ab5]">{payments.length}</p>
            </div>
          </div>
        </div>
      )}
''',
    1,
)

path.write_text(text, encoding="utf-8")

test = Path("frontend/src/__tests__/components/patient/PaymentsTabLoadFailure.test.tsx")
test.parent.mkdir(parents=True, exist_ok=True)
test.write_text(
    '''import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "@/lib/api";
import { PaymentsTab } from "@/components/patient/tabs/PaymentsTab";

vi.mock("@/lib/api", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

vi.mock("@/hooks/useDoctors", () => ({
  useDoctors: () => ({ data: [] }),
}));

vi.mock("@/hooks/useCashierSession", () => ({
  useActiveCashierSession: () => ({ data: null }),
}));

vi.mock("@/stores/authStore", () => ({
  useAuthStore: () => ({ user: { id: "admin-1", role: "Admin" } }),
}));

vi.mock("@/stores/toastStore", () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
  },
}));

describe("PaymentsTab honest loading", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("does not render a trusted zero total when the payments request fails", async () => {
    vi.mocked(api.get).mockImplementation((url: string) => {
      if (url.startsWith("/api/payments")) {
        return Promise.reject({
          isAxiosError: true,
          response: { status: 503, data: { message: "خدمة المدفوعات غير متاحة حالياً" } },
        });
      }
      return Promise.resolve({ data: [] });
    });

    render(<PaymentsTab patientId="patient-1" />);

    expect(await screen.findByText("خدمة المدفوعات غير متاحة حالياً")).toBeInTheDocument();
    expect(screen.queryByText("إجمالي المدفوعات")).not.toBeInTheDocument();
    expect(screen.queryByText("لا توجد مدفوعات")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "إعادة المحاولة" })).toBeInTheDocument();
  });

  it("keeps zero as a valid value only after a successful empty response", async () => {
    vi.mocked(api.get).mockResolvedValue({ data: [] });

    render(<PaymentsTab patientId="patient-1" />);

    expect(await screen.findByText("لا توجد مدفوعات")).toBeInTheDocument();
    expect(screen.getByText("إجمالي المدفوعات")).toBeInTheDocument();
    expect(screen.getByText("0 ر.ي")).toBeInTheDocument();
  });
});
''',
    encoding="utf-8",
)

for temporary in (
    Path(".github/workflows/core-pat-024-bootstrap.yml"),
    Path("scripts/core_pat_024_patch.py"),
):
    temporary.unlink(missing_ok=True)
