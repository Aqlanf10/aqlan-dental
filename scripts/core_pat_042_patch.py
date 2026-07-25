from pathlib import Path

# Lab orders
path = Path("frontend/src/components/patient/tabs/LabOrdersTab.tsx")
text = path.read_text(encoding="utf-8")
text = text.replace('import { useEffect, useState } from "react";', 'import { useEffect, useRef, useState } from "react";', 1)
text = text.replace('import api from "@/lib/api";\nimport { EmptyState } from "./EmptyState";', 'import api from "@/lib/api";\nimport { extractErrorMessage } from "@/lib/errors";\nimport { EmptyState } from "./EmptyState";', 1)
old = '''  const [orders, setOrders] = useState<LabOrderDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState(false);
  const [retryKey, setRetryKey] = useState(0);

  useEffect(() => {
    setFetchError(false);
    setLoading(true);
    api.get(`/api/lab-orders?patientId=${patientId}`)
      .then((r) => setOrders(extractOrders(r.data)))
      .catch(() => { setFetchError(true); })
      .finally(() => setLoading(false));
  }, [patientId, retryKey]);
'''
new = '''  const [orders, setOrders] = useState<LabOrderDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState<string | null>(null);
  const [retryKey, setRetryKey] = useState(0);
  const requestRef = useRef(0);

  useEffect(() => {
    const requestId = ++requestRef.current;
    setFetchError(null);
    setLoading(true);
    api.get(`/api/lab-orders?patientId=${patientId}`)
      .then((r) => {
        if (requestRef.current === requestId) setOrders(extractOrders(r.data));
      })
      .catch((error: unknown) => {
        if (requestRef.current === requestId) {
          setOrders([]);
          setFetchError(extractErrorMessage(error, "فشل تحميل طلبات المختبر"));
        }
      })
      .finally(() => {
        if (requestRef.current === requestId) setLoading(false);
      });
  }, [patientId, retryKey]);
'''
if old not in text: raise SystemExit("LabOrders block not found")
text = text.replace(old, new, 1)
text = text.replace('''      <div className="p-4 text-center">
        <p className="text-sm text-red-600 mb-2">فشل في تحميل البيانات</p>''', '''      <div role="alert" className="p-4 text-center">
        <p className="text-sm text-red-600 mb-2">{fetchError}</p>''', 1)
path.write_text(text, encoding="utf-8")

# Contracts
path = Path("frontend/src/components/patient/tabs/ContractsTab.tsx")
text = path.read_text(encoding="utf-8")
text = text.replace('import { useEffect, useState, useCallback } from "react";', 'import { useEffect, useState, useCallback, useRef } from "react";', 1)
text = text.replace('import api from "@/lib/api";\nimport { cn, formatArabicDate } from "@/lib/utils";', 'import api from "@/lib/api";\nimport { extractErrorMessage } from "@/lib/errors";\nimport { cn, formatArabicDate } from "@/lib/utils";', 1)
old = '''  const [contracts, setContracts] = useState<Contract[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const fetchContracts = useCallback(() => {
    setLoading(true);
    setError("");
    api.get<Contract[]>(`/api/contracts?patientId=${patientId}`)
      .then((r) => setContracts(r.data))
      .catch(() => setError("فشل تحميل العقود"))
      .finally(() => setLoading(false));
  }, [patientId]);
'''
new = '''  const [contracts, setContracts] = useState<Contract[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const requestRef = useRef(0);

  const fetchContracts = useCallback(() => {
    const requestId = ++requestRef.current;
    setLoading(true);
    setError("");
    api.get<Contract[]>(`/api/contracts?patientId=${patientId}`)
      .then((r) => {
        if (requestRef.current === requestId) setContracts(r.data);
      })
      .catch((loadError: unknown) => {
        if (requestRef.current === requestId) {
          setContracts([]);
          setError(extractErrorMessage(loadError, "فشل تحميل العقود"));
        }
      })
      .finally(() => {
        if (requestRef.current === requestId) setLoading(false);
      });
  }, [patientId]);
'''
if old not in text: raise SystemExit("Contracts block not found")
text = text.replace(old, new, 1)
text = text.replace('<div className="text-center py-12">', '<div role="alert" className="text-center py-12">', 1)
path.write_text(text, encoding="utf-8")

# General dentistry
path = Path("frontend/src/components/patient/tabs/GeneralDentistryTab.tsx")
text = path.read_text(encoding="utf-8")
text = text.replace('import { useEffect, useState } from "react";', 'import { useEffect, useRef, useState } from "react";', 1)
text = text.replace('import api from "@/lib/api";\nimport { EmptyState } from "./EmptyState";', 'import api from "@/lib/api";\nimport { extractErrorMessage } from "@/lib/errors";\nimport { EmptyState } from "./EmptyState";', 1)
old = '''  const [treatments, setTreatments] = useState<GeneralTreatmentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState(false);
  const [retryKey, setRetryKey] = useState(0);

  useEffect(() => {
    setLoading(true);
    setFetchError(false);
    api.get<GeneralTreatmentDto[]>(`/api/general-treatments/${patientId}`)
      .then((r) => setTreatments(r.data))
      .catch(() => { setFetchError(true); })
      .finally(() => setLoading(false));
  }, [patientId, retryKey]);
'''
new = '''  const [treatments, setTreatments] = useState<GeneralTreatmentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState<string | null>(null);
  const [retryKey, setRetryKey] = useState(0);
  const requestRef = useRef(0);

  useEffect(() => {
    const requestId = ++requestRef.current;
    setLoading(true);
    setFetchError(null);
    api.get<GeneralTreatmentDto[]>(`/api/general-treatments/${patientId}`)
      .then((r) => {
        if (requestRef.current === requestId) setTreatments(r.data);
      })
      .catch((error: unknown) => {
        if (requestRef.current === requestId) {
          setTreatments([]);
          setFetchError(extractErrorMessage(error, "فشل تحميل بيانات طب الأسنان العام"));
        }
      })
      .finally(() => {
        if (requestRef.current === requestId) setLoading(false);
      });
  }, [patientId, retryKey]);
'''
if old not in text: raise SystemExit("General dentistry block not found")
text = text.replace(old, new, 1)
text = text.replace('''          <div className="p-4 text-center">
            <p className="text-sm text-red-600 mb-2">فشل في تحميل بيانات طب الأسنان العام</p>''', '''          <div role="alert" className="p-4 text-center">
            <p className="text-sm text-red-600 mb-2">{fetchError}</p>''', 1)
path.write_text(text, encoding="utf-8")

test = Path("frontend/src/__tests__/components/patient/SecondaryPatientTabsReliability.contract.test.ts")
test.parent.mkdir(parents=True, exist_ok=True)
test.write_text('''import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const tabs = [
  { file: "LabOrdersTab.tsx", fallback: "فشل تحميل طلبات المختبر", reset: "setOrders([])" },
  { file: "ContractsTab.tsx", fallback: "فشل تحميل العقود", reset: "setContracts([])" },
  { file: "GeneralDentistryTab.tsx", fallback: "فشل تحميل بيانات طب الأسنان العام", reset: "setTreatments([])" },
];

describe("secondary patient tabs read reliability", () => {
  for (const tab of tabs) {
    const source = readFileSync(resolve(process.cwd(), `src/components/patient/tabs/${tab.file}`), "utf8");

    it(`${tab.file} rejects stale responses and resets stale patient data`, () => {
      expect(source).toContain("const requestId = ++requestRef.current");
      expect(source).toContain("requestRef.current === requestId");
      expect(source).toContain(tab.reset);
    });

    it(`${tab.file} surfaces backend messages and an explicit error state`, () => {
      expect(source).toContain(`extractErrorMessage(`);
      expect(source).toContain(tab.fallback);
      expect(source).toContain('role="alert"');
      expect(source).toContain("إعادة المحاولة");
    });
  }
});
''', encoding="utf-8")

Path("scripts/core_pat_042_patch.py").unlink(missing_ok=True)
Path(".github/workflows/core-pat-042-bootstrap.yml").unlink(missing_ok=True)
