"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import {
  AlertTriangle,
  ArrowRight,
  CheckCircle2,
  ExternalLink,
  FileSpreadsheet,
  FileText,
  FolderPlus,
  Loader2,
  LockKeyhole,
  Ruler,
  Save,
  ShieldCheck,
  Upload,
} from "lucide-react";
import api from "@/lib/api";
import { createOpaquePilotToken, toMmPerPixel, type CalibrationInputMode } from "@/lib/cephPilotImport";
import { extractErrorMessage } from "@/lib/errors";
import { useAuthStore } from "@/stores/authStore";
import type { CephPilotCase, CephPilotProject, PilotEligibleUser } from "@/types/cephPilot";
import CephDeidentificationCanvas, { type SanitizedPilotImage } from "./CephDeidentificationCanvas";

const QUALITY_OPTIONS = [
  ["Normal", "طبيعية"],
  ["LowContrast", "تباين منخفض"],
  ["Blur", "ضبابية"],
  ["DoubleContour", "حدود مزدوجة"],
  ["Metal", "أجسام معدنية"],
  ["MissingAnatomy", "تشريح ناقص"],
  ["SevereCrop", "قص شديد"],
  ["LowResolution", "دقة منخفضة"],
  ["Mixed", "مشكلات متعددة"],
] as const;

interface ConfirmationState {
  deIdentification: boolean;
  pixels: boolean;
  noCodes: boolean;
  legalBasis: boolean;
  orientation: boolean;
}

const EMPTY_CONFIRMATIONS: ConfirmationState = {
  deIdentification: false,
  pixels: false,
  noCodes: false,
  legalBasis: false,
  orientation: false,
};

const inputClass = "h-10 w-full rounded-md border border-gray-300 bg-white px-3 text-sm outline-none focus:border-clinic-blue focus:ring-2 focus:ring-blue-100";

export default function CephPilotImportWorkspace() {
  const user = useAuthStore(state => state.user);
  const isAdmin = user?.role === "Admin";
  const [projects, setProjects] = useState<CephPilotProject[]>([]);
  const [users, setUsers] = useState<PilotEligibleUser[]>([]);
  const [cases, setCases] = useState<CephPilotCase[]>([]);
  const [selectedProjectId, setSelectedProjectId] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [showProjectForm, setShowProjectForm] = useState(false);
  const [projectName, setProjectName] = useState("WebCeph comparison pilot");
  const [projectCode, setProjectCode] = useState("ADP-CEPH-WEBCEPH-PILOT-001");
  const [reviewerA, setReviewerA] = useState("");
  const [reviewerB, setReviewerB] = useState("");

  const [preparedImage, setPreparedImage] = useState<SanitizedPilotImage | null>(null);
  const [resetKey, setResetKey] = useState(0);
  const [sourceReference, setSourceReference] = useState("");
  const [siteCode, setSiteCode] = useState("SITE-01");
  const [deviceCode, setDeviceCode] = useState("DEVICE-01");
  const [qualityCategory, setQualityCategory] = useState("Normal");
  const [calibrationMode, setCalibrationMode] = useState<CalibrationInputMode>("pixelsPerMm");
  const [calibrationValue, setCalibrationValue] = useState("");
  const [confirmations, setConfirmations] = useState<ConfirmationState>(EMPTY_CONFIRMATIONS);

  const [calibrationCaseId, setCalibrationCaseId] = useState("");
  const [lateCalibrationValue, setLateCalibrationValue] = useState("");
  const [lateCalibrationMode, setLateCalibrationMode] = useState<CalibrationInputMode>("pixelsPerMm");

  const [artifactCaseId, setArtifactCaseId] = useState("");
  const [artifactType, setArtifactType] = useState("WebCephLandmarkTable");
  const [artifactFile, setArtifactFile] = useState<File | null>(null);
  const [artifactConfirmed, setArtifactConfirmed] = useState(false);
  const [artifactResetKey, setArtifactResetKey] = useState(0);

  const selectedProject = projects.find(project => project.id === selectedProjectId) ?? null;
  const pendingCalibrationCases = cases.filter(item => item.status === "Uploaded");
  const allConfirmed = Object.values(confirmations).every(Boolean);
  const eligibleUsers = useMemo(
    () => users.filter(item => item.isActive && ["Admin", "Orthodontist"].includes(item.role)),
    [users],
  );

  const handlePreparedImage = useCallback((image: SanitizedPilotImage | null) => {
    setPreparedImage(image);
    setConfirmations(EMPTY_CONFIRMATIONS);
  }, []);

  const loadCases = useCallback(async (projectId: string) => {
    if (!projectId) {
      setCases([]);
      return;
    }
    const response = await api.get<CephPilotCase[]>(`/api/ceph-pilot/projects/${encodeURIComponent(projectId)}/cases`);
    setCases(response.data);
  }, []);

  const loadWorkspace = useCallback(async () => {
    if (!isAdmin) {
      setLoading(false);
      return;
    }
    setLoading(true);
    setError("");
    try {
      const [projectResponse, userResponse] = await Promise.all([
        api.get<CephPilotProject[]>("/api/ceph-pilot/projects"),
        api.get<PilotEligibleUser[]>("/api/users"),
      ]);
      setProjects(projectResponse.data);
      setUsers(userResponse.data);
      const initialProject = projectResponse.data.find(item => item.status === "Draft") ?? projectResponse.data[0];
      if (initialProject) setSelectedProjectId(current => current || initialProject.id);
    } catch (requestError) {
      setError(extractErrorMessage(requestError, "تعذر تحميل مساحة Pilot."));
    } finally {
      setLoading(false);
    }
  }, [isAdmin]);

  useEffect(() => { void loadWorkspace(); }, [loadWorkspace]);
  useEffect(() => {
    if (!selectedProjectId) return;
    void loadCases(selectedProjectId).catch(requestError => {
      setError(extractErrorMessage(requestError, "تعذر تحميل حالات Pilot."));
    });
  }, [loadCases, selectedProjectId]);

  const createProject = async () => {
    if (!projectName.trim() || !projectCode.trim() || !reviewerA || !reviewerB) {
      setError("أكمل اسم المشروع ورمزه والمراجعين.");
      return;
    }
    if (reviewerA === reviewerB) {
      setError("يجب أن يكون Reviewer A وReviewer B مستخدمين مختلفين.");
      return;
    }
    setBusy(true);
    setError("");
    setNotice("");
    try {
      const response = await api.post<CephPilotProject>("/api/ceph-pilot/projects", {
        name: projectName.trim(),
        code: projectCode.trim(),
        description: "Account-owned WebCeph comparator pilot",
        datasetVersion: "pilot-v1",
        reviewerAUserId: reviewerA,
        reviewerBUserId: reviewerB,
      });
      setProjects(current => [response.data, ...current]);
      setSelectedProjectId(response.data.id);
      setShowProjectForm(false);
      setNotice("تم إنشاء مشروع Pilot مع تفعيل التعمية بين المراجعين.");
    } catch (requestError) {
      setError(extractErrorMessage(requestError, "تعذر إنشاء مشروع Pilot."));
    } finally {
      setBusy(false);
    }
  };

  const createCase = async () => {
    if (!selectedProject || selectedProject.status !== "Draft") {
      setError("اختر مشروعًا بحالة Draft لإضافة الحالة.");
      return;
    }
    if (!preparedImage || !allConfirmed) {
      setError("أنشئ المعاينة النهائية وأكمل جميع تأكيدات الخصوصية والاتجاه.");
      return;
    }
    const mmPerPixel = calibrationValue.trim()
      ? toMmPerPixel(calibrationValue, calibrationMode)
      : null;
    if (calibrationValue.trim() && mmPerPixel === null) {
      setError("قيمة المعايرة غير صالحة. اتركها فارغة فقط عندما لا توجد معايرة موثوقة.");
      return;
    }

    setBusy(true);
    setError("");
    setNotice("");
    try {
      const token = await createOpaquePilotToken();
      const form = new FormData();
      form.append("Image", new File([preparedImage.blob], "pilot-deidentified.png", { type: "image/png" }));
      form.append("SourceType", "AccountOwnedWebCephExport");
      if (sourceReference.trim()) form.append("SourceReference", sourceReference.trim());
      if (mmPerPixel !== null) {
        form.append("MmPerPixel", String(mmPerPixel));
        form.append("CalibrationSource", "CalibrationRuler");
      }
      form.append("SiteCode", siteCode.trim());
      form.append("DeviceCode", deviceCode.trim());
      form.append("PatientGroupToken", token);
      form.append("Split", "Pilot");
      form.append("QualityCategory", qualityCategory);
      form.append("DeIdentificationVisuallyConfirmed", "true");
      form.append("PixelInspectionConfirmed", "true");
      form.append("NoBarcodeOrQrConfirmed", "true");
      form.append("LegalBasisConfirmed", "true");
      form.append("OrientationFacingRightConfirmed", "true");

      const response = await api.post<CephPilotCase>(
        `/api/ceph-pilot/projects/${encodeURIComponent(selectedProject.id)}/cases`,
        form,
      );
      await loadCases(selectedProject.id);
      setArtifactCaseId(response.data.id);
      setCalibrationCaseId(response.data.status === "Uploaded" ? response.data.id : "");
      setPreparedImage(null);
      setResetKey(current => current + 1);
      setSourceReference("");
      setCalibrationValue("");
      setConfirmations(EMPTY_CONFIRMATIONS);
      setNotice(`${response.data.caseCode} حُفظت داخل Pilot دون إنشاء سجل مريض إنتاجي.`);
    } catch (requestError) {
      setError(extractErrorMessage(requestError, "تعذر إدخال الحالة إلى Pilot."));
    } finally {
      setBusy(false);
    }
  };

  const updateCalibration = async () => {
    const pilotCase = cases.find(item => item.id === calibrationCaseId);
    const mmPerPixel = toMmPerPixel(lateCalibrationValue, lateCalibrationMode);
    if (!pilotCase || mmPerPixel === null) {
      setError("اختر حالة Pending Calibration وأدخل قيمة موثوقة.");
      return;
    }
    setBusy(true);
    setError("");
    setNotice("");
    try {
      await api.patch(`/api/ceph-pilot/cases/${encodeURIComponent(pilotCase.id)}/calibration`, {
        expectedRevision: pilotCase.revision,
        mmPerPixel,
        calibrationSource: "CalibrationRuler",
      });
      await loadCases(pilotCase.projectId);
      setCalibrationCaseId("");
      setLateCalibrationValue("");
      setNotice(`${pilotCase.caseCode} أصبحت جاهزة بعد تثبيت المعايرة.`);
    } catch (requestError) {
      setError(extractErrorMessage(requestError, "تعذر تحديث المعايرة."));
    } finally {
      setBusy(false);
    }
  };

  const openForBlindedReview = async () => {
    if (!selectedProject || selectedProject.status !== "Draft") return;
    if (!cases.some(item => item.status === "Ready")) {
      setError("يلزم وجود حالة واحدة على الأقل منزوعة الهوية ومعايرة وجاهزة قبل فتح المراجعة.");
      return;
    }
    setBusy(true);
    setError("");
    setNotice("");
    try {
      const response = await api.patch<CephPilotProject>(`/api/ceph-pilot/projects/${encodeURIComponent(selectedProject.id)}`, {
        expectedRevision: selectedProject.revision,
        status: "ReadyForAnnotation",
      });
      setProjects(current => current.map(item => item.id === response.data.id ? response.data : item));
      setNotice("تم قفل إعدادات المشروع وفتحه للمراجعتين المعمّاتين A وB.");
    } catch (requestError) {
      setError(extractErrorMessage(requestError, "تعذر فتح المشروع للمراجعة."));
    } finally {
      setBusy(false);
    }
  };

  const stageArtifact = async () => {
    if (!artifactCaseId || !artifactFile || !artifactConfirmed) {
      setError("اختر الحالة والملف وأكد فحصه بصريًا قبل الإرفاق.");
      return;
    }
    setBusy(true);
    setError("");
    setNotice("");
    try {
      const form = new FormData();
      const safeArtifactName = artifactType === "WebCephMeasurementReport"
        ? "webceph-report.pdf"
        : "webceph-landmarks.csv";
      form.append("File", new File([artifactFile], safeArtifactName, { type: artifactFile.type }));
      form.append("ArtifactType", artifactType);
      form.append("DeIdentificationVisuallyConfirmed", "true");
      await api.post(`/api/ceph-pilot/cases/${encodeURIComponent(artifactCaseId)}/artifacts`, form);
      await loadCases(selectedProjectId);
      setArtifactFile(null);
      setArtifactConfirmed(false);
      setArtifactResetKey(current => current + 1);
      setNotice("تم حفظ التصدير كمقارن منفصل وغير معتمد سريريًا.");
    } catch (requestError) {
      setError(extractErrorMessage(requestError, "تعذر إرفاق تصدير WebCeph."));
    } finally {
      setBusy(false);
    }
  };

  if (loading) {
    return <div className="flex min-h-80 items-center justify-center text-sm text-gray-500"><Loader2 className="me-2 h-5 w-5 animate-spin" />جاري تحميل Pilot...</div>;
  }

  if (!isAdmin) {
    return (
      <div className="mx-auto max-w-3xl border border-amber-200 bg-amber-50 p-5 text-amber-900">
        <div className="flex items-start gap-3"><LockKeyhole className="mt-0.5 h-5 w-5" /><div><h1 className="font-bold">مساحة الإدخال محصورة بالمدير</h1><p className="mt-1 text-sm">يمكن للمراجع فتح الحالات المسندة إليه بعد انتقال المشروع إلى مرحلة المراجعة.</p></div></div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-7xl space-y-5" dir="rtl">
      <header className="flex flex-col gap-3 border-b border-gray-200 pb-4 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-extrabold text-gray-900"><ShieldCheck className="h-6 w-6 text-emerald-700" />Pilot السيفالومتري</h1>
          <p className="mt-1 text-sm text-gray-500">إدخال منزوعة الهوية، معايرة موثقة، ومقارنات منفصلة عن السجل السريري.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button type="button" disabled={busy || selectedProject?.status !== "Draft" || !cases.some(item => item.status === "Ready")} onClick={() => void openForBlindedReview()} className="inline-flex items-center gap-2 rounded-md bg-gray-900 px-3 py-2 text-xs font-bold text-white disabled:opacity-40"><LockKeyhole className="h-4 w-4" />فتح المراجعة المعمّاة</button>
          <a href="https://webceph.com/en/records/" target="_blank" rel="noreferrer" className="inline-flex items-center gap-2 rounded-md border border-gray-300 px-3 py-2 text-xs font-bold text-gray-700 hover:bg-gray-50"><ExternalLink className="h-4 w-4" />فتح WebCeph</a>
          <Link href="/ceph" className="inline-flex items-center gap-2 rounded-md border border-gray-300 px-3 py-2 text-xs font-bold text-gray-700 hover:bg-gray-50"><ArrowRight className="h-4 w-4" />العودة للسيفالو</Link>
        </div>
      </header>

      {error ? <div role="alert" className="flex items-start gap-2 border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />{error}</div> : null}
      {notice ? <div role="status" className="flex items-start gap-2 border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-900"><CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" />{notice}</div> : null}

      <section className="border-b border-gray-200 pb-5">
        <div className="mb-3 flex flex-wrap items-end gap-3">
          <div className="min-w-64 flex-1">
            <label htmlFor="pilot-project" className="mb-1 block text-xs font-bold text-gray-700">مشروع Pilot</label>
            <select id="pilot-project" value={selectedProjectId} onChange={event => setSelectedProjectId(event.target.value)} className={inputClass}>
              <option value="">اختر المشروع</option>
              {projects.map(project => <option key={project.id} value={project.id}>{project.code} - {project.status} ({project.readyCaseCount}/{project.caseCount})</option>)}
            </select>
          </div>
          <button type="button" onClick={() => setShowProjectForm(current => !current)} className="inline-flex h-10 items-center gap-2 rounded-md border border-gray-300 px-3 text-xs font-bold text-gray-700 hover:bg-gray-50"><FolderPlus className="h-4 w-4" />مشروع جديد</button>
        </div>

        {showProjectForm ? (
          <div className="grid gap-3 border-t border-gray-200 pt-3 md:grid-cols-2 xl:grid-cols-4">
            <label className="text-xs font-bold text-gray-700">اسم المشروع<input value={projectName} onChange={event => setProjectName(event.target.value)} className={`${inputClass} mt-1`} /></label>
            <label className="text-xs font-bold text-gray-700">رمز المشروع<input value={projectCode} onChange={event => setProjectCode(event.target.value)} className={`${inputClass} mt-1 font-mono`} /></label>
            <label className="text-xs font-bold text-gray-700">Reviewer A<select value={reviewerA} onChange={event => setReviewerA(event.target.value)} className={`${inputClass} mt-1`}><option value="">اختر</option>{eligibleUsers.map(item => <option key={item.id} value={item.id}>{item.doctorName ?? item.username} - {item.role}</option>)}</select></label>
            <label className="text-xs font-bold text-gray-700">Reviewer B<select value={reviewerB} onChange={event => setReviewerB(event.target.value)} className={`${inputClass} mt-1`}><option value="">اختر</option>{eligibleUsers.map(item => <option key={item.id} value={item.id}>{item.doctorName ?? item.username} - {item.role}</option>)}</select></label>
            <button type="button" disabled={busy} onClick={() => void createProject()} className="inline-flex h-10 items-center justify-center gap-2 rounded-md bg-clinic-blue px-3 text-xs font-bold text-white disabled:opacity-50"><Save className="h-4 w-4" />إنشاء المشروع</button>
          </div>
        ) : null}
      </section>

      <div className="grid gap-5 xl:grid-cols-[minmax(0,2fr)_minmax(320px,1fr)]">
        <section className="min-w-0">
          <div className="mb-3"><h2 className="text-base font-bold text-gray-900">1. إزالة الهوية داخل المتصفح</h2><p className="mt-1 text-xs text-gray-500">لا تعتمد على Privacy Mode أو OCR وحدهما. افحص البكسلات النهائية بنفسك.</p></div>
          <CephDeidentificationCanvas resetKey={resetKey} onPrepared={handlePreparedImage} />
        </section>

        <aside className="space-y-5 border-s border-gray-200 ps-5">
          <section>
            <h2 className="mb-3 text-base font-bold text-gray-900">2. المصدر والتصنيف</h2>
            <div className="space-y-3">
              <label className="block text-xs font-bold text-gray-700">مرجع WebCeph غير حساس<input value={sourceReference} onChange={event => setSourceReference(event.target.value)} placeholder="WC-RECORD-001" className={`${inputClass} mt-1 font-mono`} /></label>
              <div className="grid grid-cols-2 gap-2">
                <label className="text-xs font-bold text-gray-700">رمز الموقع<input value={siteCode} onChange={event => setSiteCode(event.target.value)} className={`${inputClass} mt-1 font-mono`} /></label>
                <label className="text-xs font-bold text-gray-700">رمز الجهاز<input value={deviceCode} onChange={event => setDeviceCode(event.target.value)} className={`${inputClass} mt-1 font-mono`} /></label>
              </div>
              <label className="block text-xs font-bold text-gray-700">جودة الصورة<select value={qualityCategory} onChange={event => setQualityCategory(event.target.value)} className={`${inputClass} mt-1`}>{QUALITY_OPTIONS.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
            </div>
          </section>

          <section className="border-t border-gray-200 pt-4">
            <h2 className="mb-3 flex items-center gap-2 text-base font-bold text-gray-900"><Ruler className="h-4 w-4" />3. المعايرة</h2>
            <div className="mb-2 inline-flex rounded-md border border-gray-300 p-0.5">
              {(["pixelsPerMm", "mmPerPixel"] as const).map(mode => <button key={mode} type="button" onClick={() => setCalibrationMode(mode)} className={`rounded px-2 py-1 text-xs ${calibrationMode === mode ? "bg-gray-900 text-white" : "text-gray-600"}`}>{mode === "pixelsPerMm" ? "px/mm" : "mm/pixel"}</button>)}
            </div>
            <input inputMode="decimal" value={calibrationValue} onChange={event => setCalibrationValue(event.target.value)} placeholder="اتركها فارغة إذا لم تثبت" className={inputClass} />
            {calibrationValue && toMmPerPixel(calibrationValue, calibrationMode) ? <p className="mt-1 text-xs text-gray-500">القيمة المحفوظة: {toMmPerPixel(calibrationValue, calibrationMode)} mm/pixel</p> : null}
          </section>

          <section className="border-t border-gray-200 pt-4">
            <h2 className="mb-3 text-base font-bold text-gray-900">4. تأكيدات البوابة</h2>
            <div className="space-y-2">
              {([
                ["deIdentification", "فحصت المعاينة النهائية وأؤكد إزالة الهوية بصريًا"],
                ["pixels", "فحصت كل النصوص المحروقة داخل البكسلات"],
                ["noCodes", "لا يوجد Barcode أو QR أو رقم معرف"],
                ["legalBasis", "الحالة مصرح باستخدامها في Pilot"],
                ["orientation", "الصورة Lateral والمريض مواجه لليمين"],
              ] as const).map(([key, label]) => (
                <label key={key} className="flex items-start gap-2 text-xs leading-5 text-gray-700">
                  <input type="checkbox" disabled={!preparedImage} checked={confirmations[key]} onChange={event => setConfirmations(current => ({ ...current, [key]: event.target.checked }))} className="mt-0.5 h-4 w-4 rounded border-gray-300 disabled:opacity-40" />
                  {label}
                </label>
              ))}
            </div>
          </section>

          <button type="button" disabled={busy || !preparedImage || !allConfirmed || selectedProject?.status !== "Draft"} onClick={() => void createCase()} className="inline-flex h-11 w-full items-center justify-center gap-2 rounded-md bg-emerald-700 px-4 text-sm font-bold text-white hover:bg-emerald-800 disabled:opacity-45">{busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Upload className="h-4 w-4" />}إدخال النسخة المنزوعة الهوية</button>
        </aside>
      </div>

      <section className="border-t border-gray-200 pt-5">
        <h2 className="mb-3 text-base font-bold text-gray-900">حالات المشروع</h2>
        <div className="overflow-x-auto border border-gray-200">
          <table className="w-full text-sm"><thead className="bg-gray-50 text-xs text-gray-600"><tr><th className="px-3 py-2 text-start">CaseCode</th><th className="px-3 py-2 text-start">الحالة</th><th className="px-3 py-2 text-start">الأبعاد</th><th className="px-3 py-2 text-start">mm/pixel</th><th className="px-3 py-2 text-start">WebCeph</th></tr></thead><tbody className="divide-y divide-gray-100">{cases.length ? cases.map(item => <tr key={item.id}><td className="px-3 py-2 font-mono font-bold">{item.caseCode}</td><td className="px-3 py-2">{item.status}</td><td className="px-3 py-2 tabular-nums">{item.imageWidth}×{item.imageHeight}</td><td className="px-3 py-2 tabular-nums">{item.mmPerPixel ?? "Pending"}</td><td className="px-3 py-2 text-xs">{item.hasWebCephLandmarkTable ? "Landmarks" : "—"} {item.hasWebCephMeasurementReport ? "Report" : ""}</td></tr>) : <tr><td colSpan={5} className="px-3 py-8 text-center text-sm text-gray-400">لا توجد حالات بعد</td></tr>}</tbody></table>
        </div>
      </section>

      <div className="grid gap-5 border-t border-gray-200 pt-5 lg:grid-cols-2">
        <section>
          <h2 className="mb-3 flex items-center gap-2 text-base font-bold text-gray-900"><Ruler className="h-4 w-4" />تثبيت معايرة معلقة</h2>
          <div className="grid gap-2 sm:grid-cols-[1fr_auto_auto]">
            <select value={calibrationCaseId} onChange={event => setCalibrationCaseId(event.target.value)} className={inputClass}><option value="">اختر Pending Calibration</option>{pendingCalibrationCases.map(item => <option key={item.id} value={item.id}>{item.caseCode}</option>)}</select>
            <div className="inline-flex h-10 rounded-md border border-gray-300 p-0.5">{(["pixelsPerMm", "mmPerPixel"] as const).map(mode => <button key={mode} type="button" onClick={() => setLateCalibrationMode(mode)} className={`rounded px-2 text-xs ${lateCalibrationMode === mode ? "bg-gray-900 text-white" : "text-gray-600"}`}>{mode === "pixelsPerMm" ? "px/mm" : "mm/pixel"}</button>)}</div>
            <input inputMode="decimal" value={lateCalibrationValue} onChange={event => setLateCalibrationValue(event.target.value)} placeholder="القيمة" className={inputClass} />
          </div>
          <button type="button" disabled={busy || !calibrationCaseId} onClick={() => void updateCalibration()} className="mt-2 inline-flex items-center gap-2 rounded-md bg-gray-900 px-3 py-2 text-xs font-bold text-white disabled:opacity-45"><Save className="h-4 w-4" />حفظ المعايرة وقفلها للمراجعة</button>
        </section>

        <section>
          <h2 className="mb-3 flex items-center gap-2 text-base font-bold text-gray-900">{artifactType === "WebCephLandmarkTable" ? <FileSpreadsheet className="h-4 w-4" /> : <FileText className="h-4 w-4" />}إرفاق تصدير رسمي</h2>
          <div className="grid gap-2 sm:grid-cols-2">
            <select value={artifactCaseId} onChange={event => setArtifactCaseId(event.target.value)} className={inputClass}><option value="">اختر الحالة</option>{cases.map(item => <option key={item.id} value={item.id}>{item.caseCode}</option>)}</select>
            <select value={artifactType} onChange={event => setArtifactType(event.target.value)} className={inputClass}><option value="WebCephLandmarkTable">Landmark Table</option><option value="WebCephMeasurementReport">Measurement Report PDF</option></select>
            <label className="inline-flex h-10 cursor-pointer items-center justify-center gap-2 rounded-md border border-gray-300 px-3 text-xs font-bold text-gray-700 hover:bg-gray-50">
              <Upload className="h-4 w-4" />
              {artifactFile ? "تم اختيار الملف" : "اختيار التصدير"}
              <input key={artifactResetKey} type="file" accept={artifactType === "WebCephMeasurementReport" ? ".pdf,application/pdf" : ".csv,.xls,.html,.htm,text/csv"} onChange={event => setArtifactFile(event.target.files?.[0] ?? null)} className="sr-only" />
            </label>
            <label className="flex items-center gap-2 text-xs text-gray-700"><input type="checkbox" checked={artifactConfirmed} onChange={event => setArtifactConfirmed(event.target.checked)} className="h-4 w-4 rounded border-gray-300" />فحصت الملف ولا يحتوي بيانات تعريفية</label>
          </div>
          <button type="button" disabled={busy || !artifactFile || !artifactConfirmed} onClick={() => void stageArtifact()} className="mt-2 inline-flex items-center gap-2 rounded-md border border-gray-300 px-3 py-2 text-xs font-bold text-gray-700 hover:bg-gray-50 disabled:opacity-45"><Upload className="h-4 w-4" />حفظ كمقارن منفصل</button>
        </section>
      </div>
    </div>
  );
}
