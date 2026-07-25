from pathlib import Path

path = Path("frontend/src/components/patient/tabs/TreatmentPlanTab.tsx")
text = path.read_text(encoding="utf-8")
text = text.replace(
    'import { useEffect, useState, useCallback } from "react";',
    'import { useEffect, useState, useCallback, useRef } from "react";',
    1,
)

old = '''  const [servicesError, setServicesError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
'''
new = '''  const [servicesError, setServicesError] = useState<string | null>(null);
  const [servicesLoading, setServicesLoading] = useState(true);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const stepsRequestRef = useRef(0);
  const servicesRequestRef = useRef(0);
'''
if old not in text:
    raise SystemExit("state block not found")
text = text.replace(old, new, 1)

old = '''  const fetchSteps = useCallback(() => {
    setLoading(true);
    setError("");
    api
      .get<TreatmentStep[]>(`/api/patients/${patientId}/treatment-plan`)
      .then((r) => setSteps(r.data))
      .catch(() => setError("فشل تحميل خطة العلاج"))
      .finally(() => setLoading(false));
  }, [patientId]);

  useEffect(() => {
    fetchSteps();
  }, [fetchSteps]);

  useEffect(() => {
    api
      .get<ServiceOption[]>("/api/settings/services/active")
      .then((r) => { setServices(r.data); setServicesError(null); })
      .catch((err) => setServicesError(extractErrorMessage(err, "تعذر تحميل كتالوج الخدمات")));
  }, []);
'''
new = '''  const fetchSteps = useCallback(() => {
    const requestId = ++stepsRequestRef.current;
    setLoading(true);
    setError("");
    api
      .get<TreatmentStep[]>(`/api/patients/${patientId}/treatment-plan`)
      .then((r) => {
        if (stepsRequestRef.current === requestId) setSteps(r.data);
      })
      .catch((loadError: unknown) => {
        if (stepsRequestRef.current === requestId) {
          setSteps([]);
          setError(extractErrorMessage(loadError, "فشل تحميل خطة العلاج"));
        }
      })
      .finally(() => {
        if (stepsRequestRef.current === requestId) setLoading(false);
      });
  }, [patientId]);

  const fetchServices = useCallback(() => {
    const requestId = ++servicesRequestRef.current;
    setServicesLoading(true);
    setServicesError(null);
    api
      .get<ServiceOption[]>("/api/settings/services/active")
      .then((r) => {
        if (servicesRequestRef.current === requestId) {
          setServices(r.data);
          setServicesError(null);
        }
      })
      .catch((catalogError: unknown) => {
        if (servicesRequestRef.current === requestId) {
          setServices([]);
          setServicesError(extractErrorMessage(catalogError, "تعذر تحميل كتالوج الخدمات"));
        }
      })
      .finally(() => {
        if (servicesRequestRef.current === requestId) setServicesLoading(false);
      });
  }, []);

  useEffect(() => {
    fetchSteps();
  }, [fetchSteps]);

  useEffect(() => {
    fetchServices();
  }, [fetchServices]);
'''
if old not in text:
    raise SystemExit("loading block not found")
text = text.replace(old, new, 1)

text = text.replace(
'''    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "فشل حفظ خطوة العلاج");
''',
'''    } catch (err: unknown) {
      toast.error(extractErrorMessage(err, "فشل حفظ خطوة العلاج"));
''',
1,
)
text = text.replace(
'''    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "فشل تغيير الحالة");
''',
'''    } catch (err: unknown) {
      toast.error(extractErrorMessage(err, "فشل تغيير الحالة"));
''',
1,
)
text = text.replace(
'''    } catch {
      toast.error("فشل حذف خطوة العلاج");
''',
'''    } catch (err: unknown) {
      toast.error(extractErrorMessage(err, "فشل حذف خطوة العلاج"));
''',
1,
)
text = text.replace(
'''    } catch {
      toast.error("فشل إعادة الترتيب");
''',
'''    } catch (err: unknown) {
      toast.error(extractErrorMessage(err, "فشل إعادة ترتيب خطة العلاج"));
''',
1,
)

old = '''      {/* Summary Cards */}
      <div className="grid grid-cols-2 sm:grid-cols-5 gap-2">
        {summaryCards.map(({ label, count, bg, color }) => (
          <div key={label} className={cn("rounded-xl px-3 py-2", bg)}>
            <p className="text-xs text-[#94a3b8]">{label}</p>
            <p className={cn("text-lg font-bold", color)}>{count}</p>
          </div>
        ))}
      </div>
'''
new = '''      {/* Summary Cards — never display zeroes while the plan is unknown. */}
      {!loading && !error && (
        <div className="grid grid-cols-2 sm:grid-cols-5 gap-2">
          {summaryCards.map(({ label, count, bg, color }) => (
            <div key={label} className={cn("rounded-xl px-3 py-2", bg)}>
              <p className="text-xs text-[#94a3b8]">{label}</p>
              <p className={cn("text-lg font-bold", color)}>{count}</p>
            </div>
          ))}
        </div>
      )}
'''
if old not in text:
    raise SystemExit("summary cards block not found")
text = text.replace(old, new, 1)

text = text.replace(
'''        <div className="text-center py-10">
          <AlertTriangle className="w-10 h-10 mx-auto mb-2 text-red-300" />
          <p className="text-sm text-red-500">{error}</p>
''',
'''        <div role="alert" className="text-center py-10">
          <AlertTriangle className="w-10 h-10 mx-auto mb-2 text-red-300" />
          <p className="text-sm text-red-500">{error}</p>
''',
1,
)

old = '''                  <select value={form.serviceId} onChange={(e) => handleServiceSelect(e.target.value)} className={inputCls}>
                    <option value="">— اختر خدمة —</option>
                    {services.map((s) => (
                      <option key={s.id} value={s.id}>{s.arabicName}</option>
                    ))}
                  </select>
                  {servicesError && (
                    <p role="alert" className="mt-1 text-[11px] font-bold text-amber-700">
                      {servicesError} — يمكنك إدخال العنوان يدويًا.
                    </p>
                  )}
'''
new = '''                  <select
                    value={form.serviceId}
                    onChange={(e) => handleServiceSelect(e.target.value)}
                    className={inputCls}
                    disabled={servicesLoading}
                  >
                    <option value="">{servicesLoading ? "جارٍ تحميل الخدمات…" : "— اختر خدمة —"}</option>
                    {services.map((s) => (
                      <option key={s.id} value={s.id}>{s.arabicName}</option>
                    ))}
                  </select>
                  {servicesError && (
                    <div role="alert" className="mt-1 flex items-center justify-between gap-2 text-[11px] font-bold text-amber-700">
                      <span>{servicesError} — يمكنك إدخال العنوان يدويًا.</span>
                      <button type="button" onClick={fetchServices} className="underline decoration-dotted">إعادة المحاولة</button>
                    </div>
                  )}
'''
if old not in text:
    raise SystemExit("service select block not found")
text = text.replace(old, new, 1)
path.write_text(text, encoding="utf-8")

test = Path("frontend/src/__tests__/components/patient/TreatmentPlanReliability.contract.test.ts")
test.parent.mkdir(parents=True, exist_ok=True)
test.write_text('''import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const source = readFileSync(
  resolve(process.cwd(), "src/components/patient/tabs/TreatmentPlanTab.tsx"),
  "utf8"
);

describe("TreatmentPlanTab reliability contract", () => {
  it("rejects stale plan and catalog responses", () => {
    expect(source).toContain("const requestId = ++stepsRequestRef.current");
    expect(source).toContain("stepsRequestRef.current === requestId");
    expect(source).toContain("const requestId = ++servicesRequestRef.current");
    expect(source).toContain("servicesRequestRef.current === requestId");
  });

  it("does not display zero summary cards while data is unknown", () => {
    expect(source).toContain("{!loading && !error && (");
    expect(source.indexOf("{!loading && !error && (")).toBeLessThan(source.indexOf("{/* Summary Cards"));
  });

  it("keeps manual entry available and makes the service catalog retryable", () => {
    expect(source).toContain("disabled={servicesLoading}");
    expect(source).toContain("يمكنك إدخال العنوان يدويًا");
    expect(source).toContain("onClick={fetchServices}");
  });

  it("surfaces backend messages for every treatment-plan mutation", () => {
    expect(source).toContain('extractErrorMessage(err, "فشل حفظ خطوة العلاج")');
    expect(source).toContain('extractErrorMessage(err, "فشل تغيير الحالة")');
    expect(source).toContain('extractErrorMessage(err, "فشل حذف خطوة العلاج")');
    expect(source).toContain('extractErrorMessage(err, "فشل إعادة ترتيب خطة العلاج")');
  });
});
''', encoding="utf-8")

Path("scripts/core_pat_039_patch.py").unlink(missing_ok=True)
Path(".github/workflows/core-pat-039-bootstrap.yml").unlink(missing_ok=True)
