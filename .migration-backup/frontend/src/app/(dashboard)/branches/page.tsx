"use client";

import { useState, useEffect, useCallback } from "react";
import {
  Building2,
  Plus,
  Search,
  Pencil,
  Trash2,
  Power,
  PowerOff,
  Loader2,
  AlertTriangle,
  MapPin,
  Phone,
  Users,
  UserRound,
  CheckCircle,
  X,
  Crown,
} from "lucide-react";
import { cn } from "@/lib/utils";
import {
  useBranches,
  useCreateBranch,
  useUpdateBranch,
  useToggleBranchStatus,
  useDeleteBranch,
  type BranchDto,
  type CreateBranchRequest,
  type UpdateBranchRequest,
} from "@/hooks/useBranches";
import { useAuthStore } from "@/stores/authStore";

// ─── Status Filter Options ──────────────────────────────────────────────────

type StatusFilter = "all" | "active" | "inactive";

const STATUS_OPTIONS: { value: StatusFilter; label: string }[] = [
  { value: "all", label: "الكل" },
  { value: "active", label: "نشط" },
  { value: "inactive", label: "غير نشط" },
];

// ─── Branch Form State ──────────────────────────────────────────────────────

interface BranchFormData {
  name: string;
  address: string;
  phone: string;
  isMain: boolean;
}

const EMPTY_FORM: BranchFormData = {
  name: "",
  address: "",
  phone: "",
  isMain: false,
};

// ─── Confirmation Dialog ────────────────────────────────────────────────────

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

// ─── Page Component ─────────────────────────────────────────────────────────

export default function BranchesPage() {
  const { user } = useAuthStore();
  const isAdmin = user?.role === "Admin";

  // Filters
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("all");
  const [search, setSearch] = useState("");

  // Modal
  const [showModal, setShowModal] = useState(false);
  const [editingBranch, setEditingBranch] = useState<BranchDto | null>(null);
  const [form, setForm] = useState<BranchFormData>(EMPTY_FORM);
  const [formError, setFormError] = useState<string | null>(null);

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
  const queryStatus = statusFilter === "all" ? undefined : statusFilter;
  const {
    data: branches,
    isLoading,
    isError,
    error: queryError,
    refetch,
  } = useBranches(queryStatus);

  const createBranch = useCreateBranch();
  const updateBranch = useUpdateBranch();
  const toggleBranchStatus = useToggleBranchStatus();
  const deleteBranch = useDeleteBranch();

  // ── Helpers ──

  const showToast = useCallback(
    (type: "success" | "error", message: string) => {
      setToast({ type, message });
    },
    []
  );

  const getErrorMessage = (err: unknown): string => {
    if (err && typeof err === "object" && "response" in err) {
      const resp = (err as { response?: { data?: { message?: string } } })
        .response;
      if (resp?.data?.message) return resp.data.message;
    }
    if (err instanceof Error) return err.message;
    return "حدث خطأ غير متوقع";
  };

  // ── Filtered branches ──

  const filteredBranches = (branches ?? []).filter((b) => {
    if (!search.trim()) return true;
    const q = search.trim().toLowerCase();
    return (
      b.name.toLowerCase().includes(q) ||
      (b.address ?? "").toLowerCase().includes(q) ||
      (b.phone ?? "").toLowerCase().includes(q)
    );
  });

  // ── Modal handlers ──

  const openAddModal = () => {
    setEditingBranch(null);
    setForm(EMPTY_FORM);
    setFormError(null);
    setShowModal(true);
  };

  const openEditModal = (branch: BranchDto) => {
    setEditingBranch(branch);
    setForm({
      name: branch.name,
      address: branch.address ?? "",
      phone: branch.phone ?? "",
      isMain: branch.isMain,
    });
    setFormError(null);
    setShowModal(true);
  };

  const closeModal = () => {
    setShowModal(false);
    setEditingBranch(null);
    setForm(EMPTY_FORM);
    setFormError(null);
  };

  const handleSave = async () => {
    if (!form.name.trim()) {
      setFormError("اسم الفرع مطلوب");
      return;
    }

    setFormError(null);

    try {
      if (editingBranch) {
        const req: UpdateBranchRequest = {
          name: form.name.trim(),
          address: form.address.trim() || undefined,
          phone: form.phone.trim() || undefined,
          isMain: form.isMain,
        };
        await updateBranch.mutateAsync({ id: editingBranch.id, ...req });
        showToast("success", "تم تحديث الفرع بنجاح");
      } else {
        const req: CreateBranchRequest = {
          name: form.name.trim(),
          address: form.address.trim() || undefined,
          phone: form.phone.trim() || undefined,
          isMain: form.isMain,
        };
        await createBranch.mutateAsync(req);
        showToast("success", "تم إضافة الفرع بنجاح");
      }
      closeModal();
    } catch (err) {
      setFormError(getErrorMessage(err));
    }
  };

  // ── Toggle status ──

  const handleToggleStatus = (branch: BranchDto) => {
    setConfirm({
      open: true,
      title: branch.isActive ? "تعطيل الفرع" : "تفعيل الفرع",
      message: branch.isActive
        ? `هل أنت متأكد من تعطيل فرع "${branch.name}"؟ لن يكون بإمكان المرضى حجز مواعيد في هذا الفرع.`
        : `هل أنت متأكد من تفعيل فرع "${branch.name}"؟`,
      confirmLabel: branch.isActive ? "تعطيل" : "تفعيل",
      variant: "warning",
      onConfirm: async () => {
        setConfirm(EMPTY_CONFIRM);
        try {
          await toggleBranchStatus.mutateAsync(branch.id);
          showToast(
            "success",
            `تم ${branch.isActive ? "تعطيل" : "تفعيل"} الفرع بنجاح`
          );
        } catch (err) {
          showToast("error", getErrorMessage(err));
        }
      },
    });
  };

  // ── Delete ──

  const handleDelete = (branch: BranchDto) => {
    setConfirm({
      open: true,
      title: "حذف الفرع",
      message: `هل أنت متأكد من حذف فرع "${branch.name}"؟ لا يمكن التراجع عن هذا الإجراء.`,
      confirmLabel: "حذف",
      variant: "danger",
      onConfirm: async () => {
        setConfirm(EMPTY_CONFIRM);
        try {
          await deleteBranch.mutateAsync(branch.id);
          showToast("success", "تم حذف الفرع بنجاح");
        } catch (err) {
          showToast("error", getErrorMessage(err));
        }
      },
    });
  };

  const isSaving =
    createBranch.isPending || updateBranch.isPending;

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
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-[#0d2137]">الفروع</h1>
          <p className="text-sm text-gray-500 mt-1">
            إدارة فروع العيادة والمعلومات الأساسية لكل فرع
          </p>
        </div>
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-[#3d7ab5]/10 flex items-center justify-center">
            <Building2 className="w-5 h-5 text-[#3d7ab5]" />
          </div>
          {isAdmin && (
            <button
              onClick={openAddModal}
              className="flex items-center gap-2 bg-[#3d7ab5] text-white px-4 py-2.5 rounded-xl text-sm font-semibold hover:opacity-90 transition shadow-sm"
            >
              <Plus className="w-4 h-4" />
              إضافة فرع
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

        {/* Search input */}
        <div className="relative flex-1 min-w-[240px]">
          <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="بحث بالاسم أو العنوان أو الهاتف..."
            className="w-full border border-gray-200 rounded-xl pr-9 pl-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/40 focus:border-[#3d7ab5] transition bg-white"
          />
        </div>
      </div>

      {/* ── Content ── */}
      {isLoading ? (
        <div className="flex items-center justify-center py-24 gap-3 text-gray-400">
          <Loader2 className="w-6 h-6 animate-spin" />
          <span className="text-sm">جارٍ تحميل الفروع...</span>
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
      ) : filteredBranches.length === 0 ? (
        <div className="text-center py-24 text-gray-400">
          <Building2 className="w-12 h-12 mx-auto mb-3 opacity-40" />
          <p className="font-medium">
            {branches && branches.length > 0
              ? "لا توجد نتائج مطابقة للبحث"
              : "لا توجد فروع"}
          </p>
          {branches && branches.length === 0 && (
            <p className="text-sm mt-1 text-gray-300">
              قم بإضافة فرع جديد للبدء
            </p>
          )}
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {filteredBranches.map((branch) => (
            <BranchCard
              key={branch.id}
              branch={branch}
              isAdmin={isAdmin}
              onEdit={openEditModal}
              onToggleStatus={handleToggleStatus}
              onDelete={handleDelete}
              isToggling={
                toggleBranchStatus.isPending &&
                toggleBranchStatus.variables === branch.id
              }
              isDeleting={
                deleteBranch.isPending &&
                deleteBranch.variables === branch.id
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
          <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-md overflow-hidden animate-in fade-in zoom-in-95">
            {/* Modal Header */}
            <div className="bg-[#0d2137] px-6 py-4 flex items-center justify-between">
              <h2 className="text-white font-bold text-lg">
                {editingBranch ? "تعديل الفرع" : "إضافة فرع جديد"}
              </h2>
              <button
                onClick={closeModal}
                className="text-white/70 hover:text-white transition"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Modal Body */}
            <div className="px-6 py-5 space-y-4">
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
                  اسم الفرع <span className="text-red-500">*</span>
                </label>
                <input
                  value={form.name}
                  onChange={(e) =>
                    setForm((f) => ({ ...f, name: e.target.value }))
                  }
                  placeholder="مثال: فرع المركز"
                  className="w-full border border-gray-200 rounded-xl px-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/40 focus:border-[#3d7ab5] transition"
                  autoFocus
                />
              </div>

              {/* Address */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1.5">
                  العنوان
                </label>
                <input
                  value={form.address}
                  onChange={(e) =>
                    setForm((f) => ({ ...f, address: e.target.value }))
                  }
                  placeholder="مثال: شارع الزبيري"
                  className="w-full border border-gray-200 rounded-xl px-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/40 focus:border-[#3d7ab5] transition"
                />
              </div>

              {/* Phone */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1.5">
                  الهاتف
                </label>
                <input
                  value={form.phone}
                  onChange={(e) =>
                    setForm((f) => ({ ...f, phone: e.target.value }))
                  }
                  placeholder="مثال: 01-234567"
                  className="w-full border border-gray-200 rounded-xl px-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/40 focus:border-[#3d7ab5] transition"
                  dir="ltr"
                />
              </div>

              {/* IsMain Toggle */}
              <div className="flex items-center justify-between bg-gray-50 rounded-xl px-4 py-3">
                <div>
                  <label className="text-sm font-medium text-gray-700">
                    الفرع الرئيسي
                  </label>
                  <p className="text-xs text-gray-400 mt-0.5">
                    يمكن تحديد فرع واحد فقط كفرع رئيسي
                  </p>
                </div>
                <button
                  type="button"
                  role="switch"
                  aria-checked={form.isMain}
                  onClick={() =>
                    setForm((f) => ({ ...f, isMain: !f.isMain }))
                  }
                  className={cn(
                    "relative inline-flex h-6 w-11 items-center rounded-full transition-colors",
                    form.isMain ? "bg-amber-500" : "bg-gray-300"
                  )}
                >
                  <span
                    className={cn(
                      "inline-block h-4 w-4 rounded-full bg-white transition-transform shadow-sm",
                      form.isMain ? "-translate-x-6" : "-translate-x-1"
                    )}
                  />
                </button>
              </div>
            </div>

            {/* Modal Footer */}
            <div className="px-6 py-4 bg-gray-50 border-t border-gray-100 flex items-center justify-end gap-3">
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
                  : editingBranch
                  ? "حفظ التعديلات"
                  : "إضافة الفرع"}
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

// ─── BranchCard ─────────────────────────────────────────────────────────────

interface BranchCardProps {
  branch: BranchDto;
  isAdmin: boolean;
  onEdit: (branch: BranchDto) => void;
  onToggleStatus: (branch: BranchDto) => void;
  onDelete: (branch: BranchDto) => void;
  isToggling: boolean;
  isDeleting: boolean;
}

function BranchCard({
  branch,
  isAdmin,
  onEdit,
  onToggleStatus,
  onDelete,
  isToggling,
  isDeleting,
}: BranchCardProps) {
  return (
    <div
      className={cn(
        "bg-white rounded-2xl border shadow-sm overflow-hidden transition-all hover:shadow-md",
        branch.isActive
          ? "border-gray-100"
          : "border-gray-200 bg-gray-50/50"
      )}
    >
      {/* Card Header */}
      <div className="px-5 pt-5 pb-3">
        <div className="flex items-start justify-between gap-2">
          <div className="flex items-center gap-3 min-w-0">
            <div
              className={cn(
                "w-10 h-10 rounded-xl flex items-center justify-center flex-shrink-0",
                branch.isActive
                  ? "bg-[#3d7ab5]/10"
                  : "bg-gray-100"
              )}
            >
              <Building2
                className={cn(
                  "w-5 h-5",
                  branch.isActive ? "text-[#3d7ab5]" : "text-gray-400"
                )}
              />
            </div>
            <div className="min-w-0">
              <h3 className="font-bold text-gray-900 text-sm truncate">
                {branch.name}
              </h3>
              <div className="flex items-center gap-2 mt-1 flex-wrap">
                {branch.isMain && (
                  <span className="inline-flex items-center gap-1 bg-amber-100 text-amber-700 text-[11px] font-semibold px-2 py-0.5 rounded-full">
                    <Crown className="w-3 h-3" />
                    الفرع الرئيسي
                  </span>
                )}
                <span
                  className={cn(
                    "inline-flex items-center gap-1 text-[11px] font-semibold px-2 py-0.5 rounded-full",
                    branch.isActive
                      ? "bg-emerald-100 text-emerald-700"
                      : "bg-gray-200 text-gray-500"
                  )}
                >
                  <span
                    className={cn(
                      "w-1.5 h-1.5 rounded-full",
                      branch.isActive ? "bg-emerald-500" : "bg-gray-400"
                    )}
                  />
                  {branch.isActive ? "نشط" : "غير نشط"}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Card Body - Details */}
      <div className="px-5 pb-3 space-y-2">
        {branch.address && (
          <div className="flex items-center gap-2 text-gray-500 text-xs">
            <MapPin className="w-3.5 h-3.5 flex-shrink-0 text-gray-400" />
            <span className="truncate">{branch.address}</span>
          </div>
        )}
        {branch.phone && (
          <div className="flex items-center gap-2 text-gray-500 text-xs" dir="ltr">
            <Phone className="w-3.5 h-3.5 flex-shrink-0 text-gray-400" />
            <span className="text-right">{branch.phone}</span>
          </div>
        )}
      </div>

      {/* Stats */}
      <div className="px-5 pb-4">
        <div className="flex items-center gap-4 text-xs">
          <div className="flex items-center gap-1.5 text-gray-500">
            <UserRound className="w-3.5 h-3.5 text-[#3d7ab5]" />
            <span>
              <span className="font-semibold text-gray-700">
                {branch.doctorsCount ?? 0}
              </span>{" "}
              أطباء
            </span>
          </div>
          <div className="flex items-center gap-1.5 text-gray-500">
            <Users className="w-3.5 h-3.5 text-[#3d7ab5]" />
            <span>
              <span className="font-semibold text-gray-700">
                {branch.patientsCount ?? 0}
              </span>{" "}
              مرضى
            </span>
          </div>
        </div>
      </div>

      {/* Card Actions (Admin only) */}
      {isAdmin && (
        <div className="px-5 py-3 border-t border-gray-100 bg-gray-50/50 flex items-center gap-1.5 justify-end">
          <button
            onClick={() => onEdit(branch)}
            className="flex items-center gap-1 text-xs text-gray-500 hover:text-[#3d7ab5] font-medium px-2.5 py-1.5 rounded-lg hover:bg-[#3d7ab5]/5 transition"
            title="تعديل"
          >
            <Pencil className="w-3.5 h-3.5" />
            تعديل
          </button>

          <button
            onClick={() => onToggleStatus(branch)}
            disabled={isToggling}
            className={cn(
              "flex items-center gap-1 text-xs font-medium px-2.5 py-1.5 rounded-lg transition",
              isToggling
                ? "text-gray-300 cursor-not-allowed"
                : branch.isActive
                ? "text-amber-600 hover:text-amber-700 hover:bg-amber-50"
                : "text-emerald-600 hover:text-emerald-700 hover:bg-emerald-50"
            )}
            title={branch.isActive ? "تعطيل" : "تفعيل"}
          >
            {isToggling ? (
              <Loader2 className="w-3.5 h-3.5 animate-spin" />
            ) : branch.isActive ? (
              <PowerOff className="w-3.5 h-3.5" />
            ) : (
              <Power className="w-3.5 h-3.5" />
            )}
            {branch.isActive ? "تعطيل" : "تفعيل"}
          </button>

          <button
            onClick={() => onDelete(branch)}
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
