
import { useEffect, useState } from "react";
import { useParams } from "@/lib/nextNavCompat";
import { PrintLayout } from "@/components/print/PrintLayout";
import api from "@/lib/api";
import { formatArabicDate } from "@/lib/utils";
import type { PatientProfile } from "@/types/patient";

// ─── Types ────────────────────────────────────────────────────────────────────

interface DocumentDto {
  id: string;
  documentType?: string;
  title?: string;
  fileName?: string;
  fileSize?: number;
  mimeType?: string;
  notes?: string;
  signed: boolean;
  signedAt?: string;
  createdAt: string;
  isActive: boolean;
}

interface ClinicalPhotoDto {
  id: string;
  photoDate: string;
  category?: string;
  photoType?: string;
  stage?: string;
  notes?: string;
  isActive: boolean;
}

interface RadiographDto {
  id: string;
  xrayDate: string;
  xrayType?: string;
  toothRelated?: string;
  notes?: string;
  fileName?: string;
  mimeType?: string;
  isActive: boolean;
}

// ─── Labels ───────────────────────────────────────────────────────────────────

const DOCUMENT_TYPES: Record<string, string> = {
  Consent: "موافقة",
  Report: "تقرير",
  Prescription: "وصفة",
  Contract: "عقد",
  Referral: "إحالة",
  Lab: "مختبر",
  Other: "أخرى",
};

const PHOTO_CATEGORIES: Record<string, string> = {
  Intraoral: "داخل الفم",
  Extraoral: "خارج الفم",
  Smile: "ابتسامة",
  Profile: "جانبي",
};

// ─── Component ────────────────────────────────────────────────────────────────

export default function MediaListPrintPage() {
  const { id } = useParams<{ id: string }>();
  const [patient, setPatient] = useState<PatientProfile | null>(null);
  const [documents, setDocuments] = useState<DocumentDto[]>([]);
  const [photos, setPhotos] = useState<ClinicalPhotoDto[]>([]);
  const [radiographs, setRadiographs] = useState<RadiographDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      api.get<PatientProfile>(`/api/patients/${id}`).then((r) => r.data).catch(() => null),
      api.get<{ data: DocumentDto[] }>(`/api/documents?patientId=${id}`).then((r) => r.data.data ?? []).catch(() => []),
      api.get<ClinicalPhotoDto[]>(`/api/clinical-photos/${id}`).then((r) => r.data).catch(() => []),
      api.get<RadiographDto[]>(`/api/radiographs/${id}`).then((r) => r.data).catch(() => []),
    ]).then(([p, d, ph, r]) => {
      setPatient(p);
      setDocuments(Array.isArray(d) ? d.filter((doc: DocumentDto) => doc.isActive) : []);
      setPhotos(Array.isArray(ph) ? ph.filter((ph2: ClinicalPhotoDto) => ph2.isActive) : []);
      setRadiographs(Array.isArray(r) ? r.filter((r2: RadiographDto) => r2.isActive) : []);
    }).finally(() => setLoading(false));
  }, [id]);

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <div className="w-8 h-8 border-2 border-[#3d7ab5] border-t-transparent rounded-full animate-spin mx-auto mb-3" />
          <p className="text-sm text-[#64748b]">جاري تحميل التقرير...</p>
        </div>
      </div>
    );
  }

  if (!patient) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <p className="text-lg font-bold text-red-500 mb-2">المريض غير موجود</p>
          <button onClick={() => window.history.back()} className="text-sm text-[#3d7ab5] hover:underline">رجوع</button>
        </div>
      </div>
    );
  }

  const patientName = `${patient.firstName} ${patient.middleName ? patient.middleName + " " : ""}${patient.lastName}`;

  return (
    <PrintLayout
      title="قائمة صور وأشعة ومستندات المريض"
      subtitle={`${patientName} — ${patient.patientNumber}`}
      backUrl={`/patients/${id}?tab=documents`}
    >
      <div style={{ fontSize: "13px", lineHeight: 1.8 }}>

        {/* ═══ Patient Info ═══ */}
        <div className="avoid-break" style={{ marginBottom: "20px" }}>
          <h3 style={{ fontSize: "14px", fontWeight: 700, color: "#3d7ab5", borderBottom: "1px solid #e8f0f9", paddingBottom: "6px", marginBottom: "10px" }}>
            معلومات المريض
          </h3>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "4px 24px" }}>
            <div><span style={{ color: "#64748b" }}>رقم المريض: </span><strong>{patient.patientNumber}</strong></div>
            <div><span style={{ color: "#64748b" }}>الاسم: </span><strong>{patientName}</strong></div>
          </div>
        </div>

        {/* ═══ Documents ═══ */}
        <div className="avoid-break" style={{ marginBottom: "20px" }}>
          <h3 style={{ fontSize: "14px", fontWeight: 700, color: "#3d7ab5", borderBottom: "1px solid #e8f0f9", paddingBottom: "6px", marginBottom: "10px" }}>
            المستندات ({documents.length})
          </h3>
          {documents.length === 0 ? (
            <p style={{ color: "#94a3b8", textAlign: "center", padding: "10px" }}>لا توجد مستندات مسجلة</p>
          ) : (
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "12px" }}>
              <thead>
                <tr style={{ borderBottom: "2px solid #e8f0f9" }}>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>العنوان</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>النوع</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>تاريخ الرفع</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>الحالة</th>
                </tr>
              </thead>
              <tbody>
                {documents.map((d) => (
                  <tr key={d.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                    <td style={{ padding: "5px 8px", fontWeight: 500 }}>{d.title || "بدون عنوان"}</td>
                    <td style={{ padding: "5px 8px" }}>{d.documentType ? DOCUMENT_TYPES[d.documentType] ?? d.documentType : "—"}</td>
                    <td style={{ padding: "5px 8px" }}>{formatArabicDate(d.createdAt)}</td>
                    <td style={{ padding: "5px 8px" }}>{d.signed ? "موقّع" : "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* ═══ Clinical Photos ═══ */}
        <div className="avoid-break" style={{ marginBottom: "20px" }}>
          <h3 style={{ fontSize: "14px", fontWeight: 700, color: "#3d7ab5", borderBottom: "1px solid #e8f0f9", paddingBottom: "6px", marginBottom: "10px" }}>
            الصور السريرية ({photos.length})
          </h3>
          {photos.length === 0 ? (
            <p style={{ color: "#94a3b8", textAlign: "center", padding: "10px" }}>لا توجد صور سريرية مسجلة</p>
          ) : (
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "12px" }}>
              <thead>
                <tr style={{ borderBottom: "2px solid #e8f0f9" }}>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>التصنيف</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>النوع</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>المرحلة/التاريخ</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>ملاحظات</th>
                </tr>
              </thead>
              <tbody>
                {photos.map((p) => (
                  <tr key={p.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                    <td style={{ padding: "5px 8px" }}>{p.category ? PHOTO_CATEGORIES[p.category] ?? p.category : "—"}</td>
                    <td style={{ padding: "5px 8px" }}>{p.photoType || "—"}</td>
                    <td style={{ padding: "5px 8px" }}>{formatArabicDate(p.photoDate)}{p.stage ? ` · ${p.stage}` : ""}</td>
                    <td style={{ padding: "5px 8px", color: "#64748b" }}>{p.notes || "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* ═══ Radiographs ═══ */}
        <div className="avoid-break">
          <h3 style={{ fontSize: "14px", fontWeight: 700, color: "#3d7ab5", borderBottom: "1px solid #e8f0f9", paddingBottom: "6px", marginBottom: "10px" }}>
            الأشعة ({radiographs.length})
          </h3>
          {radiographs.length === 0 ? (
            <p style={{ color: "#94a3b8", textAlign: "center", padding: "10px" }}>لا توجد أشعة مسجلة</p>
          ) : (
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "12px" }}>
              <thead>
                <tr style={{ borderBottom: "2px solid #e8f0f9" }}>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>النوع</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>التاريخ</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>السن</th>
                  <th style={{ textAlign: "right", padding: "6px 8px", color: "#64748b", fontWeight: 600 }}>ملاحظات</th>
                </tr>
              </thead>
              <tbody>
                {radiographs.map((r) => (
                  <tr key={r.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                    <td style={{ padding: "5px 8px" }}>{r.xrayType || "—"}</td>
                    <td style={{ padding: "5px 8px" }}>{formatArabicDate(r.xrayDate)}</td>
                    <td style={{ padding: "5px 8px" }}>{r.toothRelated || "—"}</td>
                    <td style={{ padding: "5px 8px", color: "#64748b" }}>{r.notes || "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </PrintLayout>
  );
}
