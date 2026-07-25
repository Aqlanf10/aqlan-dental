from pathlib import Path

path = Path("frontend/src/app/(dashboard)/patients/[id]/page.tsx")
text = path.read_text(encoding="utf-8")

old = '''  const [summaryError, setSummaryError] = useState<string | null>(null);
  const [relatedCasesError, setRelatedCasesError] = useState<string | null>(null);
  const retrySummary = () => setFinanceRefreshKey(k => k + 1);
  const [orthoCases,   setOrthoCases]   = useState<OrthoCase[]>([]);
  const [surgeryCases, setSurgeryCases] = useState<SurgeryCase[]>([]);
'''
new = '''  const [summaryError, setSummaryError] = useState<string | null>(null);
  const retrySummary = () => setFinanceRefreshKey(k => k + 1);
  const [orthoCases,   setOrthoCases]   = useState<OrthoCase[]>([]);
  const [surgeryCases, setSurgeryCases] = useState<SurgeryCase[]>([]);
  const [orthoCasesError, setOrthoCasesError] = useState<string | null>(null);
  const [surgeryCasesError, setSurgeryCasesError] = useState<string | null>(null);
  const [relatedCasesLoading, setRelatedCasesLoading] = useState(true);
  const [relatedCasesRetryKey, setRelatedCasesRetryKey] = useState(0);
  const retryRelatedCases = () => setRelatedCasesRetryKey(key => key + 1);
  const relatedCasesError = [
    orthoCasesError ? `حالات التقويم: ${orthoCasesError}` : null,
    surgeryCasesError ? `الحالات الجراحية: ${surgeryCasesError}` : null,
  ].filter(Boolean).join(" — ");
'''
if old not in text:
    raise SystemExit("related state block not found")
text = text.replace(old, new, 1)

old = '''    setRelatedCasesError(null);
    api.get<OrthoCase[]>(`/api/ortho-cases?patientId=${patientIdentifier}&pageSize=10`)
      .then(r => { if (!cancelled) setOrthoCases(r.data); })
      .catch(err => {
        if (!cancelled) setRelatedCasesError(extractErrorMessage(err, "تعذر تحميل الحالات المرتبطة"));
      });
    api.get<{ data: SurgeryCase[] }>(`/api/surgery-cases?patientId=${patientIdentifier}&pageSize=10`)
      .then(r => { if (!cancelled) setSurgeryCases(r.data.data ?? []); })
      .catch(err => {
        if (!cancelled) setRelatedCasesError(extractErrorMessage(err, "تعذر تحميل الحالات المرتبطة"));
      });

    return () => { cancelled = true; };
  }, [patientIdentifier, hasGuidPatientId, financeRefreshKey, profileRetryKey]);
'''
new = '''    return () => { cancelled = true; };
  }, [patientIdentifier, hasGuidPatientId, financeRefreshKey, profileRetryKey]);

  useEffect(() => {
    if (!patientIdentifier || !hasGuidPatientId) return;

    let cancelled = false;
    setRelatedCasesLoading(true);

    Promise.allSettled([
      api.get<OrthoCase[]>(`/api/ortho-cases?patientId=${patientIdentifier}&pageSize=10`),
      api.get<{ data: SurgeryCase[] }>(`/api/surgery-cases?patientId=${patientIdentifier}&pageSize=10`),
    ]).then(([orthoResult, surgeryResult]) => {
      if (cancelled) return;

      if (orthoResult.status === "fulfilled") {
        setOrthoCases(orthoResult.value.data);
        setOrthoCasesError(null);
      } else {
        setOrthoCases([]);
        setOrthoCasesError(extractErrorMessage(orthoResult.reason, "تعذر تحميل حالات التقويم"));
      }

      if (surgeryResult.status === "fulfilled") {
        setSurgeryCases(surgeryResult.value.data.data ?? []);
        setSurgeryCasesError(null);
      } else {
        setSurgeryCases([]);
        setSurgeryCasesError(extractErrorMessage(surgeryResult.reason, "تعذر تحميل الحالات الجراحية"));
      }

      setRelatedCasesLoading(false);
    });

    return () => { cancelled = true; };
  }, [patientIdentifier, hasGuidPatientId, relatedCasesRetryKey]);
'''
if old not in text:
    raise SystemExit("related fetching block not found")
text = text.replace(old, new, 1)

old = '''            {relatedCasesError && (
              <div role="alert" className="mb-3 flex items-center justify-between gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs font-bold text-amber-800">
                <span>{relatedCasesError}</span>
                <button type="button" onClick={retrySummary} className="underline decoration-dotted">إعادة المحاولة</button>
              </div>
            )}
'''
new = '''            {relatedCasesLoading ? (
              <div role="status" className="mb-3 rounded-lg border border-sky-100 bg-sky-50 px-3 py-2 text-xs font-bold text-sky-700">
                جارٍ تحميل الحالات المرتبطة…
              </div>
            ) : relatedCasesError ? (
              <div role="alert" className="mb-3 flex items-center justify-between gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs font-bold text-amber-800">
                <span>{relatedCasesError}</span>
                <button type="button" onClick={retryRelatedCases} className="underline decoration-dotted">إعادة المحاولة</button>
              </div>
            ) : null}
'''
if old not in text:
    raise SystemExit("related UI block not found")
text = text.replace(old, new, 1)
path.write_text(text, encoding="utf-8")

test = Path("frontend/src/__tests__/components/patient/PatientRelatedCasesReliability.contract.test.ts")
test.parent.mkdir(parents=True, exist_ok=True)
test.write_text('''import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const source = readFileSync(
  resolve(process.cwd(), "src/app/(dashboard)/patients/[id]/page.tsx"),
  "utf8"
);

describe("patient related cases reliability contract", () => {
  it("loads orthodontic and surgery cases independently", () => {
    expect(source).toContain("Promise.allSettled([");
    expect(source).toContain('setOrthoCasesError(extractErrorMessage(orthoResult.reason, "تعذر تحميل حالات التقويم"))');
    expect(source).toContain('setSurgeryCasesError(extractErrorMessage(surgeryResult.reason, "تعذر تحميل الحالات الجراحية"))');
    expect(source).toContain("setOrthoCases(orthoResult.value.data)");
    expect(source).toContain("setSurgeryCases(surgeryResult.value.data.data ?? [])");
  });

  it("uses a dedicated retry key instead of the finance refresh", () => {
    expect(source).toContain("const [relatedCasesRetryKey, setRelatedCasesRetryKey] = useState(0)");
    expect(source).toContain("const retryRelatedCases = () => setRelatedCasesRetryKey");
    expect(source).toContain("[patientIdentifier, hasGuidPatientId, relatedCasesRetryKey]");
    expect(source).toContain("onClick={retryRelatedCases}");
  });

  it("keeps loading and partial failure states explicit", () => {
    expect(source).toContain('role="status"');
    expect(source).toContain("جارٍ تحميل الحالات المرتبطة…");
    expect(source).toContain('orthoCasesError ? `حالات التقويم: ${orthoCasesError}`');
    expect(source).toContain('surgeryCasesError ? `الحالات الجراحية: ${surgeryCasesError}`');
  });
});
''', encoding="utf-8")

Path("scripts/core_pat_038_patch.py").unlink(missing_ok=True)
Path(".github/workflows/core-pat-038-bootstrap.yml").unlink(missing_ok=True)
