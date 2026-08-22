"use client";

import { useEffect, useState, useCallback, useMemo } from "react";
import Link from "next/link";
import {
  ClipboardList, Plus, Pencil, Trash2, X, Stethoscope,
  ChevronDown, ChevronUp, Calendar, Search,
  FileText, Activity, HeartPulse, Scissors, Syringe,
  AlertCircle, CheckCircle2, Link2, User, Printer,
} from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate, localDateString } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { useDoctors } from "@/hooks/useDoctors";
import { EmptyState } from "./EmptyState";
// FE-09: centralized appointment status colors/labels
import { APPOINTMENT_STATUS_COLORS, APPOINTMENT_STATUS_LABELS } from "@/lib/statusStyles";

// ─── Types ──────────────────────────────────────────────────────────────────────

interface AppointmentInfo {
  appointmentDate: string;
  appointmentTime: string;
  appointmentType: string;
  appointmentStatus: string;
  doctorName?: string;
}

interface VisitDto {
  id: string;
  patientId: string;
  appointmentId?: string;
  visitDate: string;
  visitType?: string;
  specialty?: string;
  doctorId?: string;
  doctorName?: string;
  chiefComplaint?: string;
  clinicalNotes?: string;
  treatmentDone?: string;
  diagnosis?: string;
  instructions?: string;
  nextVisitPlan?: string;
  cost?: number;
  nextVisitDate?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  appointment?: AppointmentInfo | null;
  serviceId?: string;
  serviceName?: string;
  amountDueReference?: number;
  checkoutStatus?: string;
  proposedProcedure?: string;
  prescriptionsCount?: number;
  labOrdersCount?: number;
}

interface VisitForm {
  visitDate: string;
  visitType: string;
  specialty: string;
  doctorId: string;
  chiefComplaint: string;
  clinicalNotes: string;
  treatmentDone: string;
  diagnosis: string;
  instructions: string;
  nextVisitPlan: string;
  cost: string;
  nextVisitDate: string;
}

const EMPTY_FORM: VisitForm = {
  visitDate: localDateString(),
  visitType: "",
  specialty: "",
  doctorId: "",
  chiefComplaint: "",
  clinicalNotes: "",
  treatmentDone: "",
  diagnosis: "",
  instructions: "",
  nextVisitPlan: "",
  cost: "",
  nextVisitDate: "",
};

const VISIT_TYPES: Record<string, string> = {
  Consultation: "استشارة",
  FollowUp: "متابعة",
  Emergency: "حالة إسعافية",
  Treatment: "علاج",
  Review: "مراجعة",
};

const SPECIALTY_LABELS: Record<string, string> = {
  General: "طب أسنان عام",
  Orthodontics: "تقويم أسنان",
  OralSurgery: "جراحة فم",
  Periodontics: "لثة",
  Endodontics: "علاج عصب",
  Prosthodontics: "تركيبات",
};

const SPECIALTY_ICONS: Record<string, typeof Stethoscope> = {
  General: Stethoscope,
  Orthodontics: Activity,
  OralSurgery: Scissors,
  Periodontics: HeartPulse,
  Endodontics: Syringe,
  Prosthodontics: FileText,
};

const SPECIALTY_COLORS: Record<string, { bg: string; text: string; icon: string }> = {
  General:       { bg: "bg-[#3d7ab518]", text: "text-[#3d7ab5]", icon: "text-[#3d7ab5]" },
  Orthodontics:  { bg: "bg-purple-50",   text: "text-purple-700", icon: "text-purple-600" },
  OralSurgery:   { bg: "bg-red-50",      text: "text-red-700",    icon: "text-red-600" },
  Periodontics:  { bg: "bg-pink-50",     text: "text-pink-700",   icon: "text-pink-600" },
  Endodontics:   { bg: "bg-amber-50",    text: "text-amber-700",  icon: "text-amber-600" },
  Prosthodontics:{ bg: "bg-blue-50",     text: "text-blue-700",   icon: "text-blue-600" },
};

// ─── Filter Types ────────────────────────────────────────────────────────────────

type SpecialtyFilter = "all" | "General" | "Orthodontics" | "OralSurgery" | "Periodontics" | "Endodontics" | "Prosthodontics";
type DateFilter = "all" | "today" | "thisMonth";

// ─── Clinical Notes Parser ────────────────────────────────────────────────────────

function parseClinicalNotes(notes: string | null | undefined): { label: string; content: string }[] {
  if (!notes) return [];
  const sections: { label: string; content: string }[] = [];
  const labelMap: Record<string, string> = {
    "فحص خارج الفم": "فحص خارج الفم",
    "فحص داخل الفم": "فحص داخل الفم",
    "ملاحظات التسليم": "ملاحظات التسليم",
    "خدمات إضافية": "خدمات إضافية",
  };

  // Split by | and identify labeled sections
  const parts = notes.split(" | ");
  const unlabeledParts: string[] = [];

  for (const part of parts) {
    let matched = false;
    for (const [key, label] of Object.entries(labelMap)) {
      if (part.startsWith(`[${key}]`)) {
        sections.push({ label, content: part.replace(`[${key}]`, "").trim() });
        matched = true;
        break;
      }
    }
    if (!matched) {
      unlabeledParts.push(part.trim());
    }
  }

  // If there are unlabeled parts, add them as "ملاحظات سريرية"
  if (unlabeledParts.length > 0) {
    sections.unshift({ label: "ملاحظات سريرية", content: unlabeledParts.join(" | ") });
  }

  return sections;
}

const CHECKOUT_STATUS_LABELS: Record<string, string> = {
  ReadyForCheckout: "جاهز للخروج",
  CheckedOut: "تم الخروج",
};

// ─── Component ──────────────────────────────────────────────────────────────────

interface VisitsTabProps {
  patientId: string;
  onVisitChanged?: () => void;
  openAddModal?: boolean;
  onModalOpened?: () => void;
}

export function VisitsTab({ patientId, onVisitChanged, openAddModal, onModalOpened }: VisitsTabProps) {
  const [visits, setVisits] = useState<VisitDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [showModal, setShowModal] = useState(false);
  const [editingVisit, setEditingVisit] = useState<VisitDto | null>(null);
  const [form, setForm] = useState<VisitForm>({ ...EMPTY_FORM });
  const [saving, setSaving] = useState(false);
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null);

  // Filters
  const [specialtyFilter, setSpecialtyFilter] = useState<SpecialtyFilter>("all");
  const [dateFilter, setDateFilter] = useState<DateFilter>("all");
  const [searchQuery, setSearchQuery] = useState("");

  // Doctors for dropdown
  const { data: doctors = [] } = useDoctors();

  const fetchVisits = useCallback(() => {
    setLoading(true);
    setError("");
    api.get<{ data: VisitDto[] }>(`/api/visits?patientId=${patientId}`)
      .then((r) => setVisits(r.data.data ?? []))
      .catch(() => setError("فشل تحميل الزيارات"))
      .finally(() => setLoading(false));
  }, [patientId]);

  useEffect(() => {
    fetchVisits();
  }, [fetchVisits]);

  // Handle external openAddModal trigger
  useEffect(() => {
    if (openAddModal) {
      openAddModalFn();
      onModalOpened?.();
    }
  }, [openAddModal]); // eslint-disable-line react-hooks/exhaustive-deps
    // Intentionally excluded: openAddModalFn is not stable (captures state setters).
    // This effect only needs to react to the external openAddModal trigger.

  // ─── Filtered Visits ───────────────────────────────────────────────────────

  const filteredVisits = useMemo(() => {
    let result = visits;

    // Specialty filter
    if (specialtyFilter !== "all") {
      result = result.filter((v) => v.specialty === specialtyFilter);
    }

    // Date filter
    if (dateFilter === "today") {
      const today = localDateString();
      result = result.filter((v) => v.visitDate === today);
    } else if (dateFilter === "thisMonth") {
      const now = new Date();
      const year = now.getFullYear();
      const month = now.getMonth();
      result = result.filter((v) => {
        const d = new Date(v.visitDate);
        return d.getFullYear() === year && d.getMonth() === month;
      });
    }

    // Search query
    if (searchQuery.trim()) {
      const q = searchQuery.trim().toLowerCase();
      result = result.filter(
        (v) =>
          (v.diagnosis && v.diagnosis.toLowerCase().includes(q)) ||
          (v.treatmentDone && v.treatmentDone.toLowerCase().includes(q)) ||
          (v.chiefComplaint && v.chiefComplaint.toLowerCase().includes(q)) ||
          (v.clinicalNotes && v.clinicalNotes.toLowerCase().includes(q)) ||
          (v.doctorName && v.doctorName.toLowerCase().includes(q))
      );
    }

    return result;
  }, [visits, specialtyFilter, dateFilter, searchQuery]);

  // ─── Stats ─────────────────────────────────────────────────────────────────

  const stats = useMemo(() => {
    const total = visits.length;
    const thisMonth = visits.filter((v) => {
      const now = new Date();
      const d = new Date(v.visitDate);
      return d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth();
    }).length;
    const today = visits.filter((v) => v.visitDate === localDateString()).length;
    const linked = visits.filter((v) => v.appointmentId).length;
    return { total, thisMonth, today, linked };
  }, [visits]);

  // ─── Handlers ──────────────────────────────────────────────────────────────

  const openAddModalFn = () => {
    setEditingVisit(null);
    setForm({ ...EMPTY_FORM });
    setShowModal(true);
  };

  const openEditModal = (visit: VisitDto) => {
    setEditingVisit(visit);
    setForm({
      visitDate: visit.visitDate,
      visitType: visit.visitType ?? "",
      specialty: visit.specialty ?? "",
      doctorId: visit.doctorId ?? "",
      chiefComplaint: visit.chiefComplaint ?? "",
      clinicalNotes: visit.clinicalNotes ?? "",
      treatmentDone: visit.treatmentDone ?? "",
      diagnosis: visit.diagnosis ?? "",
      instructions: visit.instructions ?? "",
      nextVisitPlan: visit.nextVisitPlan ?? "",
      cost: visit.cost?.toString() ?? "",
      nextVisitDate: visit.nextVisitDate ?? "",
    });
    setShowModal(true);
  };

  const handleSave = async () => {
    if (!form.visitDate) {
      toast.error("تاريخ الزيارة مطلوب");
      return;
    }
    setSaving(true);
    try {
      const payload: Record<string, unknown> = {
        patientId,
        visitDate: form.visitDate,
        visitType: form.visitType || null,
        specialty: form.specialty || null,
        doctorId: form.doctorId || null,
        chiefComplaint: form.chiefComplaint || null,
        clinicalNotes: form.clinicalNotes || null,
        treatmentDone: form.treatmentDone || null,
        diagnosis: form.diagnosis || null,
        instructions: form.instructions || null,
        nextVisitPlan: form.nextVisitPlan || null,
        cost: form.cost ? parseFloat(form.cost) : null,
        nextVisitDate: form.nextVisitDate || null,
      };

      if (editingVisit) {
        await api.put(`/api/visits/${editingVisit.id}`, payload);
        toast.success("تم تحديث الزيارة بنجاح");
      } else {
        await api.post("/api/visits", payload);
        toast.success("تم إضافة الزيارة بنجاح");
      }
      setShowModal(false);
      fetchVisits();
      onVisitChanged?.();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "فشل حفظ الزيارة");
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await api.delete(`/api/visits/${id}`);
      toast.success("تم حذف الزيارة بنجاح");
      setDeleteConfirm(null);
      fetchVisits();
      onVisitChanged?.();
    } catch {
      toast.error("فشل حذف الزيارة");
    }
  };

  const toggleExpand = (id: string) => {
    setExpandedId((prev) => (prev === id ? null : id));
  };

  const clearFilters = () => {
    setSpecialtyFilter("all");
    setDateFilter("all");
    setSearchQuery("");
  };

  const hasActiveFilters = specialtyFilter !== "all" || dateFilter !== "all" || searchQuery.trim() !== "";

  // ─── Render ──────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-bold text-[#0d2137]">الزيارات</h3>
        <button
          onClick={openAddModalFn}
          className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium rounded-lg bg-[#3d7ab5] text-white hover:opacity-90 transition"
        >
          <Plus className="w-3.5 h-3.5" />
          إضافة زيارة
        </button>
      </div>

      {/* Stats Row */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
        {[
          { label: "إجمالي الزيارات", value: stats.total, color: "text-[#3d7ab5]", bg: "bg-[#3d7ab518]" },
          { label: "هذا الشهر", value: stats.thisMonth, color: "text-green-600", bg: "bg-green-50" },
          { label: "اليوم", value: stats.today, color: "text-amber-600", bg: "bg-amber-50" },
          { label: "مرتبطة بموعد", value: stats.linked, color: "text-purple-600", bg: "bg-purple-50" },
        ].map(({ label, value, color, bg }) => (
          <div key={label} className={cn("rounded-xl px-3 py-2 flex items-center gap-2", bg)}>
            <div className="min-w-0">
              <p className="text-xs text-[#94a3b8] truncate">{label}</p>
              <p className={cn("text-sm font-bold truncate", color)}>{value}</p>
            </div>
          </div>
        ))}
      </div>

      {/* Filters & Search */}
      <div className="flex flex-wrap gap-2 items-center">
        {/* Search */}
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[#94a3b8]" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="بحث بالتشخيص أو العلاج أو الملاحظات..."
            className="w-full text-sm border border-[#e8f0f9] rounded-lg ps-9 pe-3 py-2 focus:outline-none focus:border-[#3d7ab5] placeholder:text-[#94a3b8]"
          />
        </div>

        {/* Specialty Filter */}
        <select
          value={specialtyFilter}
          onChange={(e) => setSpecialtyFilter(e.target.value as SpecialtyFilter)}
          className="text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] bg-white text-[#0d2137]"
        >
          <option value="all">كل التخصصات</option>
          {Object.entries(SPECIALTY_LABELS).map(([k, v]) => (
            <option key={k} value={k}>{v}</option>
          ))}
        </select>

        {/* Date Filter */}
        <select
          value={dateFilter}
          onChange={(e) => setDateFilter(e.target.value as DateFilter)}
          className="text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] bg-white text-[#0d2137]"
        >
          <option value="all">كل الأوقات</option>
          <option value="today">اليوم</option>
          <option value="thisMonth">هذا الشهر</option>
        </select>

        {/* Clear Filters */}
        {hasActiveFilters && (
          <button
            onClick={clearFilters}
            className="flex items-center gap-1 px-2.5 py-2 text-xs font-medium rounded-lg text-[#64748b] hover:bg-[#f1f5f9] transition"
          >
            <X className="w-3 h-3" />
            مسح الفلاتر
          </button>
        )}
      </div>

      {/* Loading */}
      {loading ? (
        <div className="space-y-3 animate-pulse">
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="h-28 bg-[#f1f5f9] rounded-xl" />
          ))}
        </div>
      ) : error ? (
        <div className="text-center py-10">
          <AlertCircle className="w-10 h-10 mx-auto mb-2 text-red-300" />
          <p className="text-sm text-red-500">{error}</p>
          <button onClick={fetchVisits} className="mt-2 text-xs text-[#3d7ab5] hover:underline">إعادة المحاولة</button>
        </div>
      ) : filteredVisits.length === 0 ? (
        hasActiveFilters ? (
          <EmptyState
            icon={ClipboardList}
            title="لا توجد زيارات تطابق الفلاتر"
            description="جرب تغيير معايير البحث أو مسح الفلاتر"
            actionLabel="مسح الفلاتر"
            actionOnClick={clearFilters}
          />
        ) : (
          <EmptyState
            icon={ClipboardList}
            title="لا توجد زيارات"
            description="لم يتم تسجيل أي زيارة لهذا المريض"
          />
        )
      ) : (
        <div className="space-y-3">
          {filteredVisits.map((visit) => {
            const isExpanded = expandedId === visit.id;
            const specColors = SPECIALTY_COLORS[visit.specialty ?? "General"] ?? SPECIALTY_COLORS.General;
            const SpecIcon = SPECIALTY_ICONS[visit.specialty ?? "General"] ?? Stethoscope;
            return (
              <div
                key={visit.id}
                className="border border-[#e8f0f9] rounded-xl overflow-hidden bg-white shadow-sm"
              >
                {/* Header row */}
                <button
                  onClick={() => toggleExpand(visit.id)}
                  className="w-full flex items-center justify-between p-3.5 hover:bg-[#f8fafc] transition text-start"
                >
                  <div className="flex items-center gap-3">
                    {/* Specialty icon */}
                    <div className={cn("w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0", specColors.bg)}>
                      <SpecIcon className={cn("w-5 h-5", specColors.icon)} />
                    </div>
                    <div>
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="text-sm font-semibold text-[#0d2137]">
                          {formatArabicDate(visit.visitDate)}
                        </span>
                        {visit.visitType && (
                          <span className="text-xs px-2 py-0.5 rounded-full bg-[#3d7ab518] text-[#3d7ab5] font-medium">
                            {VISIT_TYPES[visit.visitType] ?? visit.visitType}
                          </span>
                        )}
                        {visit.specialty && (
                          <span className={cn("text-xs px-2 py-0.5 rounded-full font-medium", specColors.bg, specColors.text)}>
                            {SPECIALTY_LABELS[visit.specialty] ?? visit.specialty}
                          </span>
                        )}
                      </div>
                      <div className="flex items-center gap-3 mt-0.5">
                        {visit.doctorName && (
                          <span className="flex items-center gap-1 text-xs text-[#64748b]">
                            <User className="w-3 h-3" />
                            {visit.doctorName}
                          </span>
                        )}
                        {visit.chiefComplaint && (
                          <span className="text-xs text-[#94a3b8] truncate max-w-[220px]">{visit.chiefComplaint}</span>
                        )}
                        {visit.appointmentId && (
                          <span className="flex items-center gap-1 text-xs text-[#3d7ab5] font-medium">
                            <Link2 className="w-3 h-3" />
                            مرتبط بموعد
                          </span>
                        )}
                      </div>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    {visit.cost != null && visit.cost > 0 && (
                      <span className="text-xs font-semibold text-[#3d7ab5]">{visit.cost.toLocaleString()} ر.ي</span>
                    )}
                    {isExpanded ? <ChevronUp className="w-4 h-4 text-[#94a3b8]" /> : <ChevronDown className="w-4 h-4 text-[#94a3b8]" />}
                  </div>
                </button>

                {/* Expanded detail */}
                {isExpanded && (
                  <div className="px-3.5 pb-3.5 space-y-3 border-t border-[#f1f5f9]">
                    {/* Linked Appointment Info */}
                    {visit.appointment && (
                      <div className="mt-3 rounded-lg p-2.5 bg-[#f0f7ff] border border-[#d6e8fa]">
                        <div className="flex items-center gap-2 mb-1.5">
                          <Calendar className="w-3.5 h-3.5 text-[#3d7ab5]" />
                          <span className="text-xs font-semibold text-[#3d7ab5]">موعد مرتبط</span>
                        </div>
                        <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs">
                          <span className="text-[#0d2137]">
                            {formatArabicDate(visit.appointment.appointmentDate)} · {visit.appointment.appointmentTime}
                          </span>
                          {visit.appointment.appointmentType && (
                            <span className="text-[#64748b]">{visit.appointment.appointmentType}</span>
                          )}
                          <span className={cn("px-1.5 py-0.5 rounded text-[10px] font-medium",
                            APPOINTMENT_STATUS_COLORS[visit.appointment.appointmentStatus] ?? "bg-[#f1f5f9] text-[#64748b]"
                          )}>
                            {APPOINTMENT_STATUS_LABELS[visit.appointment.appointmentStatus] ?? visit.appointment.appointmentStatus}
                          </span>
                        </div>
                      </div>
                    )}

                    {/* Clinical Sections */}
                    <div className="space-y-3 mt-3">
                      {/* Section: Chief Complaint & Diagnosis */}
                      {(visit.chiefComplaint || visit.diagnosis) && (
                        <div className="rounded-lg p-2.5 bg-[#fff8f0] border border-[#fde8c8]">
                          <div className="flex items-center gap-1.5 mb-1.5">
                            <AlertCircle className="w-3.5 h-3.5 text-amber-600" />
                            <span className="text-xs font-semibold text-amber-700">التقييم السريري</span>
                          </div>
                          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                            {visit.chiefComplaint && (
                              <div>
                                <p className="text-[10px] text-amber-600 font-medium">الشكوى الرئيسية</p>
                                <p className="text-sm text-[#0d2137]">{visit.chiefComplaint}</p>
                              </div>
                            )}
                            {visit.diagnosis && (
                              <div>
                                <p className="text-[10px] text-amber-600 font-medium">التشخيص</p>
                                <p className="text-sm text-[#0d2137]">{visit.diagnosis}</p>
                              </div>
                            )}
                          </div>
                        </div>
                      )}

                      {/* Section: Treatment Done */}
                      {visit.treatmentDone && (
                        <div className="rounded-lg p-2.5 bg-[#f0faf0] border border-[#d1f0d1]">
                          <div className="flex items-center gap-1.5 mb-1.5">
                            <CheckCircle2 className="w-3.5 h-3.5 text-green-600" />
                            <span className="text-xs font-semibold text-green-700">العلاج المنجز</span>
                          </div>
                          <p className="text-sm text-[#0d2137] whitespace-pre-wrap">{visit.treatmentDone}</p>
                        </div>
                      )}

                      {/* Section: Instructions */}
                      {visit.instructions && (
                        <div className="rounded-lg p-2.5 bg-[#f0f4ff] border border-[#d1daf0]">
                          <div className="flex items-center gap-1.5 mb-1.5">
                            <FileText className="w-3.5 h-3.5 text-blue-600" />
                            <span className="text-xs font-semibold text-blue-700">التعليمات للمريض</span>
                          </div>
                          <p className="text-sm text-[#0d2137] whitespace-pre-wrap">{visit.instructions}</p>
                        </div>
                      )}

                      {/* Section: Next Visit Plan */}
                      {visit.nextVisitPlan && (
                        <div className="rounded-lg p-2.5 bg-[#faf0ff] border border-[#e8d1f0]">
                          <div className="flex items-center gap-1.5 mb-1.5">
                            <Calendar className="w-3.5 h-3.5 text-purple-600" />
                            <span className="text-xs font-semibold text-purple-700">خطة الزيارة القادمة</span>
                          </div>
                          <p className="text-sm text-[#0d2137] whitespace-pre-wrap">{visit.nextVisitPlan}</p>
                          {visit.nextVisitDate && (
                            <p className="text-xs text-purple-600 mt-1">
                              الموعد القادم: {formatArabicDate(visit.nextVisitDate)}
                            </p>
                          )}
                        </div>
                      )}

                      {/* Section: Clinical Notes — parsed into labeled sections */}
                      {visit.clinicalNotes && parseClinicalNotes(visit.clinicalNotes).length > 0 && (
                        <div className="rounded-lg p-2.5 bg-[#f7fafd] border border-[#e8f0f9]">
                          <div className="flex items-center gap-1.5 mb-1.5">
                            <Stethoscope className="w-3.5 h-3.5 text-[#3d7ab5]" />
                            <span className="text-xs font-semibold text-[#3d7ab5]">ملاحظات سريرية</span>
                          </div>
                          {parseClinicalNotes(visit.clinicalNotes).map((section, i) => (
                            <div key={i} className="mb-2 last:mb-0">
                              <span className="text-[11px] font-semibold text-[#6b7280]">{section.label}:</span>
                              <p className="text-[12px] text-[#1a1a1a] mt-0.5 whitespace-pre-wrap">{section.content}</p>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>

                    {/* Additional Visit Fields */}
                    {(visit.serviceId || visit.amountDueReference != null || visit.checkoutStatus || visit.proposedProcedure || visit.prescriptionsCount != null || visit.labOrdersCount != null) && (
                      <div className="rounded-lg p-2.5 bg-[#fafafa] border border-[#f0f0f0] space-y-1.5">
                        <div className="flex flex-wrap gap-x-4 gap-y-1.5">
                          {visit.serviceId && (
                            <span className="text-xs text-[#0d2137]">
                              <span className="text-[#6b7280] font-medium">الخدمة:</span>{" "}
                              {visit.serviceName ?? visit.serviceId}
                            </span>
                          )}
                          {visit.amountDueReference != null && visit.amountDueReference > 0 && (
                            <span className="text-xs text-[#0d2137]">
                              <span className="text-[#6b7280] font-medium">المبلغ المستحق:</span>{" "}
                              {visit.amountDueReference.toLocaleString()} ر.س
                            </span>
                          )}
                          {visit.proposedProcedure && (
                            <span className="text-xs text-[#0d2137]">
                              <span className="text-[#6b7280] font-medium">الإجراء المقترح:</span>{" "}
                              {visit.proposedProcedure}
                            </span>
                          )}
                          {visit.prescriptionsCount != null && visit.prescriptionsCount > 0 && (
                            <span className="text-xs text-[#0d2137]">
                              <span className="text-[#6b7280] font-medium">الوصفات:</span>{" "}
                              {visit.prescriptionsCount}
                            </span>
                          )}
                          {visit.labOrdersCount != null && visit.labOrdersCount > 0 && (
                            <span className="text-xs text-[#0d2137]">
                              <span className="text-[#6b7280] font-medium">طلبات المختبر:</span>{" "}
                              {visit.labOrdersCount}
                            </span>
                          )}
                        </div>
                        {visit.checkoutStatus && (
                          <span className={cn(
                            "inline-block text-[10px] px-2 py-0.5 rounded-full font-medium",
                            visit.checkoutStatus === "CheckedOut"
                              ? "bg-green-50 text-green-700"
                              : "bg-amber-50 text-amber-700"
                          )}>
                            {CHECKOUT_STATUS_LABELS[visit.checkoutStatus] ?? visit.checkoutStatus}
                          </span>
                        )}
                      </div>
                    )}

                    {/* Meta info */}
                    <div className="flex flex-wrap items-center gap-3 text-[10px] text-[#94a3b8] pt-1">
                      <span>أُنشئ: {formatArabicDate(visit.createdAt)}</span>
                      {visit.createdAt !== visit.updatedAt && (
                        <span>آخر تعديل: {formatArabicDate(visit.updatedAt)}</span>
                      )}
                    </div>

                    {/* Actions */}
                    <div className="flex items-center gap-2 pt-1">
                      <Link
                        href={`/patients/${visit.patientId}/visits/${visit.id}/print`}
                        className="flex items-center gap-1 px-2.5 py-1.5 text-xs font-medium rounded-lg border border-[#3d7ab5] text-[#3d7ab5] hover:bg-[#eef3f9] transition"
                        onClick={(e) => e.stopPropagation()}
                      >
                        <Printer className="w-3 h-3" />
                        طباعة
                      </Link>
                      <button
                        onClick={(e) => { e.stopPropagation(); openEditModal(visit); }}
                        className="flex items-center gap-1 px-2.5 py-1.5 text-xs font-medium rounded-lg border border-[#3d7ab5] text-[#3d7ab5] hover:bg-[#eef3f9] transition"
                      >
                        <Pencil className="w-3 h-3" />
                        تعديل
                      </button>
                      {deleteConfirm === visit.id ? (
                        <div className="flex items-center gap-1">
                          <button
                            onClick={(e) => { e.stopPropagation(); handleDelete(visit.id); }}
                            className="px-2.5 py-1.5 text-xs font-medium rounded-lg bg-red-500 text-white hover:bg-red-600 transition"
                          >
                            تأكيد الحذف
                          </button>
                          <button
                            onClick={(e) => { e.stopPropagation(); setDeleteConfirm(null); }}
                            className="px-2.5 py-1.5 text-xs font-medium rounded-lg border border-gray-300 text-gray-600 hover:bg-gray-50 transition"
                          >
                            إلغاء
                          </button>
                        </div>
                      ) : (
                        <button
                          onClick={(e) => { e.stopPropagation(); setDeleteConfirm(visit.id); }}
                          className="flex items-center gap-1 px-2.5 py-1.5 text-xs font-medium rounded-lg text-red-500 hover:bg-red-50 transition"
                        >
                          <Trash2 className="w-3 h-3" />
                          حذف
                        </button>
                      )}
                    </div>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}

      {/* ─── Add/Edit Modal ──────────────────────────────────────────────────── */}
      {showModal && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4" onClick={() => setShowModal(false)}>
          <div
            className="bg-white rounded-2xl w-full max-w-lg shadow-2xl max-h-[90vh] overflow-y-auto"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="p-6 space-y-4">
              {/* Modal header */}
              <div className="flex items-center justify-between">
                <h3 className="text-lg font-bold text-[#0d2137]">
                  {editingVisit ? "تعديل الزيارة" : "إضافة زيارة جديدة"}
                </h3>
                <button onClick={() => setShowModal(false)} className="text-[#94a3b8] hover:text-[#0d2137]">
                  <X className="w-5 h-5" />
                </button>
              </div>

              {/* Form */}
              <div className="space-y-3">
                {/* Row 1: Date and Visit Type */}
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">تاريخ الزيارة *</label>
                    <input
                      type="date"
                      value={form.visitDate}
                      onChange={(e) => setForm({ ...form, visitDate: e.target.value })}
                      className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5]"
                    />
                  </div>
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">نوع الزيارة</label>
                    <select
                      value={form.visitType}
                      onChange={(e) => setForm({ ...form, visitType: e.target.value })}
                      className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] bg-white"
                    >
                      <option value="">— اختر —</option>
                      {Object.entries(VISIT_TYPES).map(([k, v]) => (
                        <option key={k} value={k}>{v}</option>
                      ))}
                    </select>
                  </div>
                </div>

                {/* Row 2: Specialty and Doctor */}
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">التخصص</label>
                    <select
                      value={form.specialty}
                      onChange={(e) => setForm({ ...form, specialty: e.target.value })}
                      className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] bg-white"
                    >
                      <option value="">— اختر —</option>
                      {Object.entries(SPECIALTY_LABELS).map(([k, v]) => (
                        <option key={k} value={k}>{v}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">الطبيب</label>
                    <select
                      value={form.doctorId}
                      onChange={(e) => setForm({ ...form, doctorId: e.target.value })}
                      className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] bg-white"
                    >
                      <option value="">— اختر —</option>
                      {doctors.filter((d) => d.isActive).map((d) => (
                        <option key={d.id} value={d.id}>{d.name}{d.specialty ? ` (${SPECIALTY_LABELS[d.specialty] ?? d.specialty})` : ""}</option>
                      ))}
                    </select>
                  </div>
                </div>

                {/* Chief Complaint */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">الشكوى الرئيسية</label>
                  <input
                    type="text"
                    value={form.chiefComplaint}
                    onChange={(e) => setForm({ ...form, chiefComplaint: e.target.value })}
                    placeholder="مثل: ألم في الضرس"
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5]"
                  />
                </div>

                {/* Diagnosis */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">التشخيص</label>
                  <input
                    type="text"
                    value={form.diagnosis}
                    onChange={(e) => setForm({ ...form, diagnosis: e.target.value })}
                    placeholder="مثل: التهاب لب سني"
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5]"
                  />
                </div>

                {/* Treatment Done */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">العلاج المنجز</label>
                  <textarea
                    value={form.treatmentDone}
                    onChange={(e) => setForm({ ...form, treatmentDone: e.target.value })}
                    placeholder="وصف العلاج المنجز"
                    rows={2}
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] resize-none"
                  />
                </div>

                {/* Clinical Notes */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">ملاحظات سريرية</label>
                  <textarea
                    value={form.clinicalNotes}
                    onChange={(e) => setForm({ ...form, clinicalNotes: e.target.value })}
                    placeholder="ملاحظات إضافية"
                    rows={2}
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] resize-none"
                  />
                </div>

                {/* Instructions */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">التعليمات للمريض</label>
                  <textarea
                    value={form.instructions}
                    onChange={(e) => setForm({ ...form, instructions: e.target.value })}
                    placeholder="تعليمات للمريض"
                    rows={2}
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] resize-none"
                  />
                </div>

                {/* Next Visit Plan */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">خطة الزيارة القادمة</label>
                  <textarea
                    value={form.nextVisitPlan}
                    onChange={(e) => setForm({ ...form, nextVisitPlan: e.target.value })}
                    placeholder="الخطة العلاجية للزيارة القادمة"
                    rows={2}
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] resize-none"
                  />
                </div>

                {/* Row: Cost and Next Visit Date */}
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">التكلفة (ر.ي)</label>
                    <input
                      type="number"
                      value={form.cost}
                      onChange={(e) => setForm({ ...form, cost: e.target.value })}
                      placeholder="0"
                      min="0"
                      step="0.01"
                      className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5]"
                    />
                  </div>
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">موعد الزيارة القادمة</label>
                    <input
                      type="date"
                      value={form.nextVisitDate}
                      onChange={(e) => setForm({ ...form, nextVisitDate: e.target.value })}
                      className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5]"
                    />
                  </div>
                </div>
              </div>

              {/* Actions */}
              <div className="flex gap-3 pt-2">
                <button
                  onClick={() => setShowModal(false)}
                  className="flex-1 py-2.5 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
                >
                  إلغاء
                </button>
                <button
                  onClick={handleSave}
                  disabled={saving}
                  className={cn(
                    "flex-1 py-2.5 text-sm font-medium rounded-lg text-white transition",
                    saving ? "bg-[#3d7ab5]/60 cursor-not-allowed" : "bg-[#3d7ab5] hover:bg-[#2d5e8e]"
                  )}
                >
                  {saving ? "جاري الحفظ..." : editingVisit ? "تحديث" : "إضافة"}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
