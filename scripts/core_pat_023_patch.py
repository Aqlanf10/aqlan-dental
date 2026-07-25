from pathlib import Path
import re

path = Path("frontend/src/components/patient/tabs/PortalAccessTab.tsx")
text = path.read_text(encoding="utf-8")


def replace_once(pattern: str, replacement: str, *, flags: int = 0, label: str) -> None:
    global text
    updated, count = re.subn(pattern, replacement, text, count=1, flags=flags)
    if count != 1:
        raise SystemExit(f"CORE-PAT-023: expected one {label} match, found {count}")
    text = updated


replace_once(
    re.escape('import api from "@/lib/api";\n'),
    'import api from "@/lib/api";\nimport { extractErrorMessage } from "@/lib/errors";\n',
    label="error helper import",
)

replace_once(
    re.escape('  const [loading, setLoading] = useState(true);\n'),
    '  const [loading, setLoading] = useState(true);\n  const [loadError, setLoadError] = useState("");\n',
    label="load error state",
)

replace_once(
    r"  const fetchCredentials = useCallback\(async \(\) => \{.*?^  \}, \[patientId\]\);",
    '''  const fetchCredentials = useCallback(async () => {
    setLoading(true);
    setLoadError("");
    try {
      const { data } = await api.get<PatientPortalCredentials>(`/api/portal/credentials/${patientId}`);
      setCreds(data);
    } catch (error: unknown) {
      setCreds(null);
      setLoadError(
        extractErrorMessage(error, "تعذر تحميل حالة حساب بوابة المريض — تحقق من الاتصال وحاول مجدداً")
      );
    } finally {
      setLoading(false);
    }
  }, [patientId]);''',
    flags=re.MULTILINE | re.DOTALL,
    label="fetchCredentials block",
)

replace_once(
    r'''    \} catch \(err: unknown\) \{\n      const msg = \(err as \{ response\?: \{ data\?: \{ message\?: string \} \} \}\)\?\.response\?\.data\?\.message;\n      toast\.error\(msg \?\? "فشل إنشاء حساب البوابة"\);\n    \} finally \{''',
    '''    } catch (error: unknown) {
      toast.error(extractErrorMessage(error, "فشل إنشاء حساب البوابة"));
    } finally {''',
    label="create account error handler",
)

replace_once(
    r'''    \} catch \(err: unknown\) \{\n      const msg = \(err as \{ response\?: \{ data\?: \{ message\?: string \} \} \}\)\?\.response\?\.data\?\.message;\n      toast\.error\(msg \?\? "فشل إعادة تعيين كلمة المرور"\);\n    \} finally \{''',
    '''    } catch (error: unknown) {
      toast.error(extractErrorMessage(error, "فشل إعادة تعيين كلمة المرور"));
    } finally {''',
    label="reset password error handler",
)

loading_block = '''  if (loading) {
    return (
      <div className="space-y-3 animate-pulse">
        <div className="h-8 bg-gray-100 rounded-lg w-1/3" />
        <div className="h-20 bg-gray-100 rounded-lg" />
        <div className="h-10 bg-gray-100 rounded-lg w-1/2" />
      </div>
    );
  }

'''
if loading_block not in text:
    raise SystemExit("CORE-PAT-023: loading block not found")
error_block = loading_block + '''  if (loadError) {
    return (
      <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-6 text-center">
        <AlertTriangle className="w-10 h-10 mx-auto mb-3 text-red-400" />
        <p className="text-sm font-semibold text-red-700">{loadError}</p>
        <p className="text-xs text-red-500 mt-1">
          لم نعتبر فشل الطلب «لا يوجد حساب»، ولم نعرض إجراءات إنشاء أو إعادة تعيين قبل معرفة الحالة الحقيقية.
        </p>
        <button
          type="button"
          onClick={fetchCredentials}
          className="mt-4 px-4 py-2 text-sm font-semibold rounded-lg bg-white border border-red-200 text-red-700 hover:bg-red-100 transition"
        >
          إعادة المحاولة
        </button>
      </div>
    );
  }

'''
text = text.replace(loading_block, error_block, 1)
path.write_text(text, encoding="utf-8")

test = Path("frontend/src/__tests__/components/patient/PortalAccessTabLoadFailure.test.tsx")
test.parent.mkdir(parents=True, exist_ok=True)
test.write_text(
    '''import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "@/lib/api";
import { PortalAccessTab } from "@/components/patient/tabs/PortalAccessTab";

vi.mock("@/lib/api", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
  },
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

const NO_ACCOUNT = {
  username: "GM0001",
  accountActive: false,
  mustChangePassword: false,
  hasPortalAccount: false,
};

describe("PortalAccessTab honest credential loading", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("shows a retryable Arabic error instead of claiming the patient has no account", async () => {
    vi.mocked(api.get).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 503,
        data: { message: "خدمة بوابة المرضى غير متاحة حالياً" },
      },
    });

    render(<PortalAccessTab patientId="patient-1" patientNumber="GM0001" />);

    expect(await screen.findByRole("alert")).toHaveTextContent("خدمة بوابة المرضى غير متاحة حالياً");
    expect(screen.getByText(/لم نعتبر فشل الطلب/)).toBeInTheDocument();
    expect(screen.queryByText("لا يوجد حساب بوابة لهذا المريض")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "إنشاء حساب بوابة" })).not.toBeInTheDocument();
  });

  it("retries the request and then renders the genuine no-account state", async () => {
    vi.mocked(api.get)
      .mockRejectedValueOnce({
        isAxiosError: true,
        response: { status: 503, data: { message: "انقطاع مؤقت" } },
      })
      .mockResolvedValueOnce({ data: NO_ACCOUNT });

    render(<PortalAccessTab patientId="patient-1" patientNumber="GM0001" />);

    fireEvent.click(await screen.findByRole("button", { name: "إعادة المحاولة" }));

    expect(await screen.findByText("لا يوجد حساب بوابة لهذا المريض")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "إنشاء حساب بوابة" })).toBeInTheDocument();
    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(2));
  });

  it("preserves the genuine no-account response without showing an error", async () => {
    vi.mocked(api.get).mockResolvedValue({ data: NO_ACCOUNT });

    render(<PortalAccessTab patientId="patient-1" patientNumber="GM0001" />);

    expect(await screen.findByText("لا يوجد حساب بوابة لهذا المريض")).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});
''',
    encoding="utf-8",
)

for temporary in (
    Path(".github/workflows/core-pat-023-bootstrap.yml"),
    Path("scripts/core_pat_023_patch.py"),
):
    temporary.unlink(missing_ok=True)
