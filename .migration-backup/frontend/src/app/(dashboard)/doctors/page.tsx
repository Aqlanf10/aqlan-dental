"use client";

import { useState, useEffect, useCallback, useMemo } from "react";
import { useRouter } from "next/navigation";
import {
  Stethoscope,
  Plus,
  Search,
  Pencil,
  Trash2,
  Power,
  PowerOff,
  Loader2,
  AlertTriangle,
  CheckCircle,
  X,
  MapPin,
  CalendarClock,
  BadgeCheck,
  ChevronDown,
  ChevronUp,
  Hash,
  Palette,
  UserCircle,
  Banknote,
  FileText,
  DoorOpen,
} from "lucide-react";
import { cn } from "@/lib/utils";
import {
  useDoctorsFiltered,
  useCreateDoctor,
  useUpdateDoctor,
  useToggleDoctorStatus,
  useDeleteDoctor,
  type DoctorDto,
  type CreateDoctorRequest,
  type UpdateDoctorRequest,
} from "@/hooks/useDoctors";
import { useBranches } from "@/hooks/useBranches";
import { useClinicRooms } from "@/hooks/useClinicRooms";
import { useAuthStore } from "@/stores/authStore";

// ─── Constants ──────────────────────────────────────────────────────────────

type StatusFilter = "all" | "active" | "inactive";

const STATUS_OPTIONS: { value: StatusFilter; label: string }[] = [
  { value: "all", label: "الكل" },
  { value: "active", label: "نشط" },
  { value: "inactive", label: "غير نشط" },
];

const SPECIALTY_OPTIONS = [
  { value: "أخصائي تقويم الأسنان", label: "أخصائي تقويم الأسنان" },
  { value: "طب أسنان عام", label: "طب أسنان عام" },
  { value: "أخصائي جراحة وجه وفكين", label: "أخصائي جراحة وجه وفكين" },
  { value: "orthodontics", label: "تقويم الأسنان" },
  { value: "general", label: "طب الأسنان العام" },
  { value: "surgery", label: "جراحة الوجه والفكين" },
  { value: "pediatric", label: "طب أسنان الأطفال" },
  { value: "periodontics", label: "أمراض اللثة" },
  { value: "endodontics", label: "علاج الجذور" },
] as const;

const SPECIALTY_LABELS: Record<string, string> = {
  orthodontics: "تقويم الأسنان",
  general: "طب الأسنان العام",
  surgery: "جراحة الوجه والفكين",
  pediatric: "طب أسنان الأطفال",
  periodontics: "أمراض اللثة",
  endodontics: "علاج الجذور",
  "أخصائي تقويم الأسنان": "أخصائي تقويم الأسنان",
  "طب أسنان عام": "طب أسنان عام",
  "أخصائي جراحة وجه وفكين": "أخصائي جراحة وجه وفكين",
};

const COMPENSATION_LABELS: Record<string, string> = {
  None: "لا يوجد",
  FixedSalary: "راتب ثابت",
  Percentage: "نسبة مئوية",
  Mixed: "مختلط (راتب + نسبة)",
};

const DOCTOR_COLORS = [
  "#3d7ab5",
  "#7C3AED",
  "#059669",
  "#f5922e",
  "#DC2626",
  "#374151",
  "#2563EB",
  "#DB2777",
  "#65A30D",
  "#EA580C",
];

// ─── Form State ─────────────────────────────────────────────────────────────

interface DoctorFormData {
  name: string;
  specialty: string;
  licenseNumber: string;
  branchId: string;
  color: string;
  avatarInitials: string;
  compensationType: string;
  defaultCommissionPercentage: string;
  compensationNotes: string;
  defaultClinicRoomId: string;
}

const EMPTY_FORM: DoctorFormData = {
  name: "",
  specialty: "",
  licenseNumber: "",
  branchId: "",
  color: DOCTOR_COLORS[0],
  avatarInitials: "",
  compensationType: "None",
  defaultCommissionPercentage: "",
  compensationNotes: "",
  defaultClinicRoomId: "",
};

// ─── Confirm Dialog State ───────────────────────────────────────────────────

interface ConfirmDialogState {
  open: boolean;
  title: string;
  message: string;
  confirmLabel: string;
  variant: "danger" | "warning";
  onConfirm: () => void;
}

const EMPTY_CONFIRM: ConfirmDialogState = {
  open: false,
  title: "",
  message: "",
  confirmLabel: "",
  variant: "danger",
  onConfirm: () => {},
};

// ─── Helpers ────────────────────────────────────────────────────────────────

function generateInitials(name: string): string {
  if (!name.trim()) return "";
  const words = name.trim().split(/\s+/);
  if (words.length >= 2) {
    return (words[0][0] + words[1][0]).replace(/[د.]/g, "").slice(0, 2) || words[0].replace(/[د.]/g, "").slice(0, 2);
  }
  return words[0].replace(/[د.]/g, "").slice(0, 2) || "د";
}

function getDisplaySpecialty(specialty?: string | null): string | null {
  if (!specialty) return null;
  return SPECIALTY_LABELS[specialty] ?? specialty;
}

function getDisplayCompensation(type?: string | null): string | null {
  if (!type || type === "None") return null;
  return COMPENSATION_LABELS[type] ?? type;
}

function getErrorMessage(err: unknown): string {
  if (err && typeof err === "object" && "response" in err) {
    const resp = (err as { response?: { data?: { message?: string } } }).response;
    if (resp?.data?.message) return resp.data.message;
  }
  if (err instanceof Error) return err.message;
  return "حدث خطأ غير متوقع";
}

// ─── Page Component ─────────────────────────────────────────────────────────

export default function DoctorsPage() {
  const router = useRouter();
  const { user } = useAuthStore();
  const isAdmin = user?.role === "Admin";

  // Filters
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("all");
  const [branchFilter, setBranchFilter] = useState<string>("");
  const [search, setSearch] = useState("");

  // Modal
  const [showModal, setShowModal] = useState(false);
  const [editingDoctor, setEditingDoctor] = useState<DoctorDto | null>(null);
  const [form, setForm] = useState<DoctorFormData>(EMPTY_FORM);
  const [formError, setFormError] = useState<string | null>(null);
  const [showCompensation, setShowCompensation] = useState(false);

  // Confirm dialog
  const [confirm, setConfirm] = useState<ConfirmDialogState>(EMPTY_CONFIRM);

  // Toast
  const [toast, setToast] = useState<{
    type: "success" | "error";
    message: string;
  } | null>(null);

  // Auto-dismiss toast
  useEffect(() => {
    if (!toast) return;
    const timer = setTimeout(() => setToast(null), 4000);
    return () => clearTimeout(timer);
  }, [toast]);

  // Data fetching
  const queryStatus =
    statusFilter === "all" ? "all" : statusFilter;
  const queryBranchId = branchFilter || undefined;

  const {
    data: doctors,
    isLoading,
    isError,
    error: queryError,
    refetch,
  } = useDoctorsFiltered(queryStatus, queryBranchId);

  const { data: branches } = useBranches();
  const { data: clinicRooms } = useClinicRooms();

  const createDoctor = useCreateDoctor();
  const updateDoctor = useUpdateDoctor();
  const toggleDoctorStatus = useToggleDoctorStatus();
  const deleteDoctor = useDeleteDoctor();

  // ── Toast helper ──

  const showToast = useCallback(
    (type: "success" | "error", message: string) => {
      setToast({ type, message });
    },
    []
  );

  // ── Filtered doctors (client-side search) ──

  const filteredDoctors = useMemo(() => {
    return (doctors ?? []).filter((d) => {
      if (!search.trim()) return true;
      const q = search.trim().toLowerCase();
      return (
        d.name.toLowerCase().includes(q) ||
        (getDisplaySpecialty(d.specialty) ?? "").toLowerCase().includes(q) ||
        (d.specialty ?? "").toLowerCase().includes(q) ||
        (d.licenseNumber ?? "").toLowerCase().includes(q)
      );
    });
  }, [doctors, search]);

  // ── Modal handlers ──

  const openAddModal = () => {
    setEditingDoctor(null);
    setForm(EMPTY_FORM);
    setFormError(null);
    setShowCompensation(false);
    setShowModal(true);
  };

  const openEditModal = (doctor: DoctorDto) => {
    setEditingDoctor(doctor);
    setForm({
      name: doctor.name,
      specialty: doctor.specialty ?? "",
      licenseNumber: doctor.licenseNumber ?? "",
      branchId: doctor.branchId ?? "",
      color: doctor.color ?? DOCTOR_COLORS[0],
      avatarInitials: doctor.avatarInitials ?? "",
      compensationType: doctor.compensationType ?? "None",
      defaultCommissionPercentage:
        doctor.defaultCommissionPercentage != null
          ? String(doctor.defaultCommissionPercentage)
          : "",
      compensationNotes: doctor.compensationNotes ?? "",
      defaultClinicRoomId: doctor.defaultClinicRoomId ?? "",
    });
    setFormError(null);
    setShowCompensation(
      doctor.compensationType != null && doctor.compensationType !== "None"
    );
    setShowModal(true);
  };

  const closeModal = () => {
    setShowModal(false);
    setEditingDoctor(null);
    setForm(EMPTY_FORM);
    setFormError(null);
    setShowCompensation(false);
  };

  // Auto-suggest initials when name changes
  const handleNameChange = (value: string) => {
    const initials = generateInitials(value);
    setForm((f) => ({
      ...f,
      name: value,
      avatarInitials:
        editingDoctor && f.avatarInitials && f.avatarInitials !== generateInitials(f.name)
          ? f.avatarInitials
          : initials,
    }));
  };

  // ── Save handler ──

  const handleSave = async () => {
    if (!form.name.trim()) {
      setFormError("اسم الطبيب مطلوب");
      return;
    }

    setFormError(null);

    try {
      if (editingDoctor) {
        const req: UpdateDoctorRequest & { id: string } = {
          id: editingDoctor.id,
          name: form.name.trim(),
          specialty: form.specialty.trim() || undefined,
          licenseNumber: form.licenseNumber.trim() || undefined,
          branchId: form.branchId || undefined,
          color: form.color,
          avatarInitials: form.avatarInitials.trim() || undefined,
          compensationType:
            form.compensationType as UpdateDoctorRequest["compensationType"],
          defaultCommissionPercentage:
            form.defaultCommissionPercentage && (form.compensationType === "Percentage" || form.compensationType === "Mixed")
              ? Number(form.defaultCommissionPercentage)
              : undefined,
          compensationNotes:
            form.compensationNotes.trim() || undefined,
          // Always explicit on update: empty selection clears the standing room
          // via the all-zeros GUID convention (omitting would mean "unchanged").
          defaultClinicRoomId:
            form.defaultClinicRoomId || "00000000-0000-0000-0000-000000000000",
        };
        await updateDoctor.mutateAsync(req);
        showToast("success", "تم تحديث بيانات الطبيب بنجاح");
      } else {
        const req: CreateDoctorRequest = {
          name: form.name.trim(),
          specialty: form.specialty.trim() || undefined,
          licenseNumber: form.licenseNumber.trim() || undefined,
          branchId: form.branchId || undefined,
          color: form.color,
          avatarInitials: form.avatarInitials.trim() || undefined,
          compensationType:
            form.compensationType as CreateDoctorRequest["compensationType"],
          defaultCommissionPercentage:
            form.defaultCommissionPercentage && (form.compensationType === "Percentage" || form.compensationType === "Mixed")
              ? Number(form.defaultCommissionPercentage)
              : undefined,
          compensationNotes:
            form.compensationNotes.trim() || undefined,
          defaultClinicRoomId: form.defaultClinicRoomId || undefined,
        };
        await createDoctor.mutateAsync(req);
        showToast("success", "تم إضافة الطبيب بنجاح");
      }
      closeModal();
    } catch (err) {
      setFormError(getErrorMessage(err));
    }
  };

  // ── Toggle status ──

  const handleToggleStatus = (doctor: DoctorDto) => {
    setConfirm({
      open: true,
      title: doctor.isActive ? "تعطيل الطبيب" : "تفعيل الطبيب",
      message: doctor.isActive
        ? `هل أنت متأكد من تعطيل الطبيب "${doctor.name}"؟ لن يمكن حجز مواعيد معه بعد التعطيل.`
        : `هل أنت متأكد من تفعيل الطبيب "${doctor.name}"؟`,
      confirmLabel: doctor.isActive ? "تعطيل" : "تفعيل",
      variant: "warning",
      onConfirm: async () => {
        setConfirm(EMPTY_CONFIRM);
        try {
          await toggleDoctorStatus.mutateAsync(doctor.id);
          showToast(
            "success",
            `تم ${doctor.isActive ? "تعطيل" : "تفعيل"} الطبيب بنجاح`
          );
        } catch (err) {
          showToast("error", getErrorMessage(err));
        }
      },
    });
  };

  // ── Delete ──

  const handleDelete = (doctor: DoctorDto) => {
    setConfirm({
      open: true,
      title: "حذف الطبيب",
      message: `هل أنت متأكد من حذف الطبيب "${doctor.name}"؟ لا يمكن التراجع عن هذا الإجراء. سيتم تعطيل الحساب بدلاً من الحذف إذا كان مرتبطاً ببيانات نشطة.`,
      confirmLabel: "حذف",
      variant: "danger",
      onConfirm: async () => {
        setConfirm(EMPTY_CONFIRM);
        try {
          await deleteDoctor.mutateAsync(doctor.id);
          showToast("success", "تم حذف الطبيب بنجاح");
        } catch (err) {
          showToast("error", getErrorMessage(err));
        }
      },
    });
  };

  // ── Navigate to schedules ──

  const handleManageSchedule = (doctor: DoctorDto) => {
    router.push(`/doctors/${doctor.id}/schedules`);
  };

  const isSaving = createDoctor.isPending || updateDoctor.isPending;

  // ─── Render ─────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-6">
      {/* ── Toast ── */}
      {toast && (
        <div
          className={cn(
            "fixed top-6 left-1/2 -translate-x-1/2 z-[100] flex items-center gap-2 px-5 py-3 rounded-xl shadow-lg text-sm font-medium transition-all animate-in fade-in slide-in-from-top-2",
            toast.type === "success"
              ? "bg-emerald-600 text-white"
              : "bg-red-600 text-white"
          )}
        >
          {toast.type === "success" ? (
            <CheckCircle className="w-4 h-4 flex-shrink-0" />
          ) : (
            <AlertTriangle className="w-4 h-4 flex-shrink-0" />
          )}
          {toast.message}
          <button
            onClick={() => setToast(null)}
            className="mr-2 hover:opacity-70 transition"
          >
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      )}

      {/* ── Header ── */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="text-2xl font-bold text-[#0d2137]">الأطباء</h1>
          <p className="text-sm text-gray-500 mt-1">
            إدارة أطباء العيادة وتخصصاتهم وجداول العمل
          </p>
        </div>
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-[#3d7ab5]/10 flex items-center justify-center">
            <Stethoscope className="w-5 h-5 text-[#3d7ab5]" />
          </div>
          {isAdmin && (
            <button
              onClick={openAddModal}
              className="flex items-center gap-2 bg-[#3d7ab5] text-white px-4 py-2.5 rounded-xl text-sm font-semibold hover:opacity-90 transition shadow-sm"
            >
              <Plus className="w-4 h-4" />
              إضافة طبيب
            </button>
          )}
        </div>
      </div>

      {/* ── Filters Bar ── */}
      <div className="flex flex-wrap gap-3 items-center">
        {/* Status filter pills */}
        <div className="flex bg-gray-100 rounded-xl p-1 gap-0.5">
          {STATUS_OPTIONS.map((opt) => (
            <button
              key={opt.value}
              onClick={() => setStatusFilter(opt.value)}
              className={cn(
                "px-4 py-1.5 rounded-lg text-sm font-medium transition",
                statusFilter === opt.value
                  ? "bg-white text-[#3d7ab5] shadow-sm"
                  : "text-gray-500 hover:text-gray-700"
              )}
            >
              {opt.label}
            </button>
          ))}
        </div>

        {/* Branch filter */}
        <select
          value={branchFilter}
          onChange={(e) => setBranchFilter(e.target.value)}
          className="border border-gray-200 rounded-xl px-3 py-2.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/40 focus:border-[#3d7ab5] transition min-w-[160px]"
        >
          <option value="">جميع الفروع</option>
          {branches?.map((b) => (
            <option key={b.id} value={b.id}>
              {b.name}
            </option>
          ))}
        </select>

        {/* Search input */}
        <div className="relative flex-1 min-w-[240px]">
          <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="بحث بالاسم أو التخصص أو رقم الترخيص..."
            className="w-full border border-gray-200 rounded-xl pr-9 pl-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/40 focus:border-[#3d7ab5] transition bg-white"
          />
        </div>
      </div>

      {/* ── Content ── */}
      {isLoading ? (
        <div className="flex items-center justify-center py-24 gap-3 text-gray-400">
          <Loader2 className="w-6 h-6 animate-spin" />
          <span className="text-sm">جارٍ تحميل الأطباء...</span>
        </div>
      ) : isError ? (
        <div className="flex items-center justify-center py-24">
          <div className="text-center">
            <AlertTriangle className="w-10 h-10 text-red-400 mx-auto mb-3" />
            <p className="text-gray-600 font-medium mb-2">
              {getErrorMessage(queryError)}
            </p>
            <button
              onClick={() => refetch()}
              className="text-sm text-[#3d7ab5] hover:underline font-medium"
            >
              إعادة المحاولة
            </button>
          </div>
        </div>
      ) : filteredDoctors.length === 0 ? (
        <div className="text-center py-24 text-gray-400">
          <Stethoscope className="w-12 h-12 mx-auto mb-3 opacity-40" />
          <p className="font-medium">
            {doctors && doctors.length > 0
              ? "لا توجد نتائج مطابقة للبحث"
              : "لا يوجد أطباء"}
          </p>
          {doctors && doctors.length === 0 && (
            <p className="text-sm mt-1 text-gray-300">
              قم بإضافة طبيب جديد للبدء
            </p>
          )}
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {filteredDoctors.map((doctor) => (
            <DoctorCard
              key={doctor.id}
              doctor={doctor}
              isAdmin={isAdmin}
              onEdit={openEditModal}
              onManageSchedule={handleManageSchedule}
              onToggleStatus={handleToggleStatus}
              onDelete={handleDelete}
              isToggling={
                toggleDoctorStatus.isPending &&
                toggleDoctorStatus.variables === doctor.id
              }
              isDeleting={
                deleteDoctor.isPending &&
                deleteDoctor.variables === doctor.id
              }
            />
          ))}
        </div>
      )}

      {/* ── Add/Edit Modal ── */}
      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          {/* Backdrop */}
          <div
            className="absolute inset-0 bg-black/40 backdrop-blur-sm"
            onClick={closeModal}
          />

          {/* Modal */}
          <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-lg overflow-hidden animate-in fade-in zoom-in-95 max-h-[90vh] flex flex-col">
            {/* Modal Header */}
            <div className="bg-[#0d2137] px-6 py-4 flex items-center justify-between flex-shrink-0">
              <h2 className="text-white font-bold text-lg">
                {editingDoctor ? "تعديل بيانات الطبيب" : "إضافة طبيب جديد"}
              </h2>
              <button
                onClick={closeModal}
                className="text-white/70 hover:text-white transition"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Modal Body */}
            <div className="px-6 py-5 space-y-4 overflow-y-auto flex-1">
              {/* Form Error */}
              {formError && (
                <div className="flex items-center gap-2 text-red-600 text-xs bg-red-50 rounded-lg px-3 py-2">
                  <AlertTriangle className="w-3.5 h-3.5 flex-shrink-0" />
                  {formError}
                </div>
              )}

              {/* Name */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1.5">
                  اسم الطبيب <span className="text-red-500">*</span>
                </label>
                <div className="relative">
                  <UserCircle className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                  <input
                    value={form.name}
                    onChange={(e) => handleNameChange(e.target.value)}
                    placeholder="مثال: د. أحمد محمد"
                    className="w-full border border-gray-200 rounded-xl pr-9 pl-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/40 focus:border-[#3d7ab5] transition"
                    autoFocus
                  />
                </div>
              </div>

              {/* Specialty + License Row */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                {/* Specialty */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    التخصص
                  </label>
                  <select
                    value={form.specialty}
                    onChange={(e) =>
                      setForm((f) => ({ ...f, specialty: e.target.value }))
                    }
                    className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/40 focus:border-[#3d7ab5] transition"
                  >
                    <option value="">اختر التخصص</option>
                    {SPECIALTY_OPTIONS.map((opt) => (
                      <option key={opt.value} value={opt.value}>
                        {opt.label}
                      </option>
                    ))}
                  </select>
                </div>

                {/* License Number */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    رقم الترخيص
                  </label>
                  <div className="relative">
                    <Hash className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                    <input
                      value={form.licenseNumber}
                      onChange={(e) =>
                        setForm((f) => ({
                          ...f,
                          licenseNumber: e.target.value,
                        }))
                      }
                      placeholder="رقم الترخيص المهني"
                      className="w-full border border-gray-200 rounded-xl pr-9 pl-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/40 focus:border-[#3d7ab5] transition"
                      dir="ltr"
                    />
                  </div>
                </div>
              </div>

              {/* Branch */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1.5">
                  الفرع
                </label>
                <div className="relative">
                  <MapPin className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                  <select
                    value={form.branchId}
                    onChange={(e) =>
                      setForm((f) => ({ ...f, branchId: e.target.value }))
                    }
                    className="w-full border border-gray-200 rounded-xl pr-9 pl-3 py-2.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/40 focus:border-[#3d7ab5] transition appearance-none"
                  >
                    <option value="">اختر الفرع</option>
                    {branches?.map((b) => (
                      <option key={b.id} value={b.id}>
                        {b.name}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              {/* Default clinic room — "تعيينات غرف الأطباء": pre-fills the room
                  when calling this doctor's patient from the daily-operations queue */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1.5">
                  الغرفة الافتراضية
                </label>
                <div className="relative">
                  <DoorOpen className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                  <select
                    value={form.defaultClinicRoomId}
                    onChange={(e) =>
                      setForm((f) => ({ ...f, defaultClinicRoomId: e.target.value }))
                    }
                    className="w-full border border-gray-200 rounded-xl pr-9 pl-3 py-2.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/40 focus:border-[#3d7ab5] transition appearance-none"
                  >
                    <option value="">بدون تعيين — اختيار يدوي عند النداء</option>
                    {clinicRooms?.map((r) => (
                      <option key={r.id} value={r.id}>
                        {r.arabicName}
                      </option>
                    ))}
                  </select>
                </div>
                <p className="mt-1 text-[11px] text-gray-400">
                  تُقترح هذه الغرفة تلقائيًا عند نداء مرضى هذا الطبيب من العمليات اليومية.
                </p>
              </div>

              {/* Color Picker */}
              <div>
                <label className="flex items-center gap-1.5 text-sm font-medium text-gray-700 mb-2">
                  <Palette className="w-4 h-4" />
                  اللون المميز
                </label>
                <div className="flex flex-wrap gap-2">
                  {DOCTOR_COLORS.map((color) => (
                    <button
                      key={color}
                      type="button"
                      onClick={() => setForm((f) => ({ ...f, color }))}
                      className={cn(
                        "w-8 h-8 rounded-lg transition-all border-2",
                        form.color === color
                          ? "border-gray-800 scale-110 shadow-md"
                          : "border-transparent hover:scale-105"
                      )}
                      style={{ backgroundColor: color }}
                      title={color}
                    />
                  ))}
                </div>
              </div>

              {/* Avatar Initials */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1.5">
                  الأحرف المختصرة
                </label>
                <div className="flex items-center gap-3">
                  <div
                    className="w-10 h-10 rounded-full flex items-center justify-center text-white font-bold text-sm flex-shrink-0"
                    style={{ backgroundColor: form.color || DOCTOR_COLORS[0] }}
                  >
                    {form.avatarInitials || "د"}
                  </div>
                  <input
                    value={form.avatarInitials}
                    onChange={(e) =>
                      setForm((f) => ({
                        ...f,
                        avatarInitials: e.target.value.slice(0, 3),
                      }))
                    }
                    placeholder="يتم إنشاؤه تلقائياً من الاسم"
                    maxLength={3}
                    className="w-32 border border-gray-200 rounded-xl px-3 py-2.5 text-sm text-center focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/40 focus:border-[#3d7ab5] transition"
                  />
                </div>
              </div>

              {/* ── Compensation Section (collapsible) ── */}
              <div className="border border-gray-100 rounded-xl overflow-hidden">
                <button
                  type="button"
                  onClick={() => setShowCompensation(!showCompensation)}
                  className="w-full flex items-center justify-between px-4 py-3 bg-gray-50 hover:bg-gray-100 transition text-sm font-medium text-gray-700"
                >
                  <span className="flex items-center gap-2">
                    <Banknote className="w-4 h-4 text-[#3d7ab5]" />
                    التعويضات والمكافآت
                  </span>
                  {showCompensation ? (
                    <ChevronUp className="w-4 h-4 text-gray-400" />
                  ) : (
                    <ChevronDown className="w-4 h-4 text-gray-400" />
                  )}
                </button>

                {showCompensation && (
                  <div className="px-4 py-4 space-y-4 bg-white">
                    {/* Compensation Type */}
                    <div>
                      <label className="block text-sm font-medium text-gray-700 mb-1.5">
                        نوع التعويض
                      </label>
                      <select
                        value={form.compensationType}
                        onChange={(e) =>
                          setForm((f) => ({
                            ...f,
                            compensationType: e.target.value,
                          }))
                        }
                        className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/40 focus:border-[#3d7ab5] transition"
                      >
                        {Object.entries(COMPENSATION_LABELS).map(
                          ([value, label]) => (
                            <option key={value} value={value}>
                              {label}
                            </option>
                          )
                        )}
                      </select>
                    </div>

                    {/* Commission Percentage */}
                    {(form.compensationType === "Percentage" ||
                      form.compensationType === "Mixed") && (
                      <div>
                        <label className="block text-sm font-medium text-gray-700 mb-1.5">
                          نسبة العمولة الافتراضية (%)
                        </label>
                        <input
                          type="number"
                          min="0"
                          max="100"
                          step="0.1"
                          value={form.defaultCommissionPercentage}
                          onChange={(e) =>
                            setForm((f) => ({
                              ...f,
                              defaultCommissionPercentage: e.target.value,
                            }))
                          }
                          placeholder="مثال: 25"
                          className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/40 focus:border-[#3d7ab5] transition"
                          dir="ltr"
                        />
                      </div>
                    )}

                    {/* Compensation Notes */}
                    <div>
                      <label className="flex items-center gap-1.5 text-sm font-medium text-gray-700 mb-1.5">
                        <FileText className="w-3.5 h-3.5" />
                        ملاحظات التعويض
                      </label>
                      <textarea
                        value={form.compensationNotes}
                        onChange={(e) =>
                          setForm((f) => ({
                            ...f,
                            compensationNotes: e.target.value,
                          }))
                        }
                        placeholder="ملاحظات إضافية حول التعويضات..."
                        rows={2}
                        className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/40 focus:border-[#3d7ab5] transition resize-none"
                      />
                    </div>
                  </div>
                )}
              </div>
            </div>

            {/* Modal Footer */}
            <div className="px-6 py-4 bg-gray-50 border-t border-gray-100 flex items-center justify-end gap-3 flex-shrink-0">
              <button
                onClick={closeModal}
                className="px-5 py-2.5 rounded-xl text-sm font-medium text-gray-600 hover:bg-gray-100 transition"
              >
                إلغاء
              </button>
              <button
                onClick={handleSave}
                disabled={isSaving}
                className={cn(
                  "flex items-center gap-2 px-6 py-2.5 rounded-xl text-sm font-semibold transition",
                  isSaving
                    ? "bg-gray-100 text-gray-400 cursor-not-allowed"
                    : "bg-[#3d7ab5] text-white hover:opacity-90 shadow-sm"
                )}
              >
                {isSaving && <Loader2 className="w-4 h-4 animate-spin" />}
                {isSaving
                  ? "جارٍ الحفظ..."
                  : editingDoctor
                  ? "حفظ التعديلات"
                  : "إضافة الطبيب"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Confirm Dialog ── */}
      {confirm.open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          {/* Backdrop */}
          <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" />

          {/* Dialog */}
          <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-sm overflow-hidden animate-in fade-in zoom-in-95">
            <div className="px-6 py-6 text-center">
              <div
                className={cn(
                  "w-14 h-14 rounded-full flex items-center justify-center mx-auto mb-4",
                  confirm.variant === "danger"
                    ? "bg-red-100"
                    : "bg-amber-100"
                )}
              >
                <AlertTriangle
                  className={cn(
                    "w-7 h-7",
                    confirm.variant === "danger"
                      ? "text-red-500"
                      : "text-amber-500"
                  )}
                />
              </div>
              <h3 className="text-lg font-bold text-gray-900 mb-2">
                {confirm.title}
              </h3>
              <p className="text-sm text-gray-500 leading-relaxed">
                {confirm.message}
              </p>
            </div>
            <div className="px-6 py-4 bg-gray-50 border-t border-gray-100 flex items-center justify-center gap-3">
              <button
                onClick={() => setConfirm(EMPTY_CONFIRM)}
                className="px-5 py-2.5 rounded-xl text-sm font-medium text-gray-600 hover:bg-gray-100 transition"
              >
                إلغاء
              </button>
              <button
                onClick={confirm.onConfirm}
                className={cn(
                  "flex items-center gap-2 px-6 py-2.5 rounded-xl text-sm font-semibold text-white transition shadow-sm",
                  confirm.variant === "danger"
                    ? "bg-red-600 hover:bg-red-700"
                    : "bg-amber-500 hover:bg-amber-600"
                )}
              >
                {confirm.confirmLabel}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// ─── DoctorCard ─────────────────────────────────────────────────────────────

interface DoctorCardProps {
  doctor: DoctorDto;
  isAdmin: boolean;
  onEdit: (doctor: DoctorDto) => void;
  onManageSchedule: (doctor: DoctorDto) => void;
  onToggleStatus: (doctor: DoctorDto) => void;
  onDelete: (doctor: DoctorDto) => void;
  isToggling: boolean;
  isDeleting: boolean;
}

function DoctorCard({
  doctor,
  isAdmin,
  onEdit,
  onManageSchedule,
  onToggleStatus,
  onDelete,
  isToggling,
  isDeleting,
}: DoctorCardProps) {
  const displaySpecialty = getDisplaySpecialty(doctor.specialty);
  const displayCompensation = getDisplayCompensation(doctor.compensationType);
  const initials = doctor.avatarInitials || "د";
  const color = doctor.color || DOCTOR_COLORS[0];

  return (
    <div
      className={cn(
        "bg-white rounded-2xl border shadow-sm overflow-hidden transition-all hover:shadow-md",
        doctor.isActive !== false
          ? "border-gray-100"
          : "border-gray-200 bg-gray-50/50"
      )}
    >
      {/* Card Header */}
      <div className="px-5 pt-5 pb-3">
        <div className="flex items-start justify-between gap-2">
          <div className="flex items-center gap-3 min-w-0">
            {/* Avatar */}
            <div
              className="w-11 h-11 rounded-full flex items-center justify-center flex-shrink-0 text-white font-bold text-sm shadow-sm"
              style={{ backgroundColor: color }}
            >
              {initials}
            </div>
            <div className="min-w-0">
              <h3 className="font-bold text-gray-900 text-sm truncate">
                {doctor.name}
              </h3>
              <div className="flex items-center gap-2 mt-1 flex-wrap">
                {/* Status badge */}
                <span
                  className={cn(
                    "inline-flex items-center gap-1 text-[11px] font-semibold px-2 py-0.5 rounded-full",
                    doctor.isActive !== false
                      ? "bg-emerald-100 text-emerald-700"
                      : "bg-gray-200 text-gray-500"
                  )}
                >
                  <span
                    className={cn(
                      "w-1.5 h-1.5 rounded-full",
                      doctor.isActive !== false
                        ? "bg-emerald-500"
                        : "bg-gray-400"
                    )}
                  />
                  {doctor.isActive !== false ? "نشط" : "غير نشط"}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Card Body - Details */}
      <div className="px-5 pb-3 space-y-1.5">
        {/* Specialty */}
        {displaySpecialty && (
          <div className="flex items-center gap-2 text-gray-600 text-xs">
            <Stethoscope className="w-3.5 h-3.5 flex-shrink-0 text-gray-400" />
            <span className="truncate">{displaySpecialty}</span>
          </div>
        )}

        {/* Branch */}
        {doctor.branchName && (
          <div className="flex items-center gap-2 text-gray-500 text-xs">
            <MapPin className="w-3.5 h-3.5 flex-shrink-0 text-gray-400" />
            <span className="truncate">{doctor.branchName}</span>
          </div>
        )}

        {/* License Number */}
        {doctor.licenseNumber && (
          <div className="flex items-center gap-2 text-gray-500 text-xs">
            <BadgeCheck className="w-3.5 h-3.5 flex-shrink-0 text-gray-400" />
            <span>ترخيص: </span>
            <span dir="ltr" className="text-gray-600">{doctor.licenseNumber}</span>
          </div>
        )}

        {/* Compensation Badge */}
        {displayCompensation && (
          <div className="flex items-center gap-2 mt-2">
            <span className="inline-flex items-center gap-1 bg-[#3d7ab5]/10 text-[#3d7ab5] text-[11px] font-semibold px-2 py-0.5 rounded-full">
              <Banknote className="w-3 h-3" />
              {displayCompensation}
              {doctor.defaultCommissionPercentage != null &&
                (doctor.compensationType === "Percentage" ||
                  doctor.compensationType === "Mixed") && (
                  <span dir="ltr">({doctor.defaultCommissionPercentage}%)</span>
                )}
            </span>
          </div>
        )}
      </div>

      {/* Schedule Count */}
      {doctor.scheduleCount != null && (
        <div className="px-5 pb-3">
          <div className="flex items-center gap-1.5 text-xs text-gray-500">
            <CalendarClock className="w-3.5 h-3.5 text-[#3d7ab5]" />
            <span>
              <span className="font-semibold text-gray-700">
                {doctor.scheduleCount}
              </span>{" "}
              جدول عمل
            </span>
          </div>
        </div>
      )}

      {/* Card Actions (Admin only) */}
      {isAdmin && (
        <div className="px-5 py-3 border-t border-gray-100 bg-gray-50/50 flex items-center gap-1.5 justify-end flex-wrap">
          {/* Edit */}
          <button
            onClick={() => onEdit(doctor)}
            className="flex items-center gap-1 text-xs text-gray-500 hover:text-[#3d7ab5] font-medium px-2.5 py-1.5 rounded-lg hover:bg-[#3d7ab5]/5 transition"
            title="تعديل"
          >
            <Pencil className="w-3.5 h-3.5" />
            تعديل
          </button>

          {/* Manage Schedule */}
          <button
            onClick={() => onManageSchedule(doctor)}
            className="flex items-center gap-1 text-xs text-gray-500 hover:text-[#3d7ab5] font-medium px-2.5 py-1.5 rounded-lg hover:bg-[#3d7ab5]/5 transition"
            title="إدارة الجدول"
          >
            <CalendarClock className="w-3.5 h-3.5" />
            الجدول
          </button>

          {/* Toggle Status */}
          <button
            onClick={() => onToggleStatus(doctor)}
            disabled={isToggling}
            className={cn(
              "flex items-center gap-1 text-xs font-medium px-2.5 py-1.5 rounded-lg transition",
              isToggling
                ? "text-gray-300 cursor-not-allowed"
                : doctor.isActive !== false
                ? "text-amber-600 hover:text-amber-700 hover:bg-amber-50"
                : "text-emerald-600 hover:text-emerald-700 hover:bg-emerald-50"
            )}
            title={doctor.isActive !== false ? "تعطيل" : "تفعيل"}
          >
            {isToggling ? (
              <Loader2 className="w-3.5 h-3.5 animate-spin" />
            ) : doctor.isActive !== false ? (
              <PowerOff className="w-3.5 h-3.5" />
            ) : (
              <Power className="w-3.5 h-3.5" />
            )}
            {doctor.isActive !== false ? "تعطيل" : "تفعيل"}
          </button>

          {/* Delete */}
          <button
            onClick={() => onDelete(doctor)}
            disabled={isDeleting}
            className={cn(
              "flex items-center gap-1 text-xs font-medium px-2.5 py-1.5 rounded-lg transition",
              isDeleting
                ? "text-gray-300 cursor-not-allowed"
                : "text-red-500 hover:text-red-700 hover:bg-red-50"
            )}
            title="حذف"
          >
            {isDeleting ? (
              <Loader2 className="w-3.5 h-3.5 animate-spin" />
            ) : (
              <Trash2 className="w-3.5 h-3.5" />
            )}
            حذف
          </button>
        </div>
      )}
    </div>
  );
}
