import { Fragment, useEffect, useState } from "react";
import { Plus, Search, Edit3, Power, PowerOff, X, Save, Stethoscope, ChevronDown, ChevronUp, ChevronLeft, Trash2 } from "lucide-react";
import Link from "@/lib/nextLinkCompat";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import type { ServiceConsumable } from "@/types/appointment";

// ─── Types ────────────────────────────────────────────────────────────────────

interface InventoryItemOption {
  id: string;
  name: string;
  unit?: string | null;
}

interface ServiceRow {
  id: string;
  arabicName: string;
  englishName: string;
  code: string;
  department?: string;
  category: string;
  description?: string;
  defaultDurationMinutes: number;
  defaultPrice: number;
  requiresDoctor: boolean;
  requiresConsultationFee: boolean;
  showInBooking: boolean;
  showInReception: boolean;
  showInTreatmentPlan: boolean;
  isActive: boolean;
  sortOrder: number;
  /** YOLO-S2: optional hex color for calendar/queue display. */
  color?: string | null;
  // Commission defaults (returned from API)
  defaultMaterialCost: number;
  defaultMaterialCostType: string;
  defaultLabCost: number;
  defaultDoctorCommissionPercentage: number | null;
  commissionBaseRule: string;
  commissionRecognitionMode: string;
}

interface ServiceForm {
  arabicName: string;
  englishName: string;
  code: string;
  department: string;
  category: string;
  description: string;
  defaultDurationMinutes: number;
  defaultPrice: number;
  requiresDoctor: boolean;
  requiresConsultationFee: boolean;
  showInBooking: boolean;
  showInReception: boolean;
  showInTreatmentPlan: boolean;
  sortOrder: number;
  /** YOLO-S2: optional hex color. */
  color: string;
  // Commission defaults
  defaultMaterialCost: number;
  defaultMaterialCostType: string;
  defaultLabCost: number;
  defaultDoctorCommissionPercentage: number | null;
  commissionBaseRule: string;
  commissionRecognitionMode: string;
}

// ─── Constants ────────────────────────────────────────────────────────────────

const CATEGORY_LABELS: Record<string, string> = {
  Consultation: "معاينات",
  Preventive: "وقائي",
  Restorative: "ترميمي",
  Endodontics: "علاج عصب",
  Prosthodontics: "تركيبات",
  Orthodontics: "تقويم",
  Surgery: "جراحة",
  Cosmetic: "تجميل",
  Radiology: "أشعة",
  Other: "أخرى",
};

const CATEGORIES = Object.keys(CATEGORY_LABELS);

const CATEGORY_COLORS: Record<string, string> = {
  Consultation: "bg-blue-50 text-blue-700",
  Preventive: "bg-green-50 text-green-700",
  Restorative: "bg-amber-50 text-amber-700",
  Endodontics: "bg-red-50 text-red-700",
  Prosthodontics: "bg-purple-50 text-purple-700",
  Orthodontics: "bg-pink-50 text-pink-700",
  Surgery: "bg-rose-50 text-rose-700",
  Cosmetic: "bg-fuchsia-50 text-fuchsia-700",
  Radiology: "bg-cyan-50 text-cyan-700",
  Other: "bg-gray-50 text-gray-700",
};

const EMPTY_FORM: ServiceForm = {
  arabicName: "",
  englishName: "",
  code: "",
  department: "",
  category: "Other",
  description: "",
  defaultDurationMinutes: 30,
  defaultPrice: 0,
  requiresDoctor: true,
  requiresConsultationFee: false,
  showInBooking: true,
  showInReception: true,
  showInTreatmentPlan: true,
  sortOrder: 0,
  color: "",
  defaultMaterialCost: 0,
  defaultMaterialCostType: "FixedAmount",
  defaultLabCost: 0,
  defaultDoctorCommissionPercentage: null,
  commissionBaseRule: "AfterDiscountAndCosts",
  commissionRecognitionMode: "OnInvoiceApproval",
};

const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue";

// ─── Badge Component ──────────────────────────────────────────────────────────

function Badge({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <span className={cn("text-xs px-1.5 py-0.5 rounded font-medium whitespace-nowrap", className)}>
      {children}
    </span>
  );
}

// ─── Main Page ────────────────────────────────────────────────────────────────

export default function ServicesSettingsPage() {
  const [services, setServices] = useState<ServiceRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchText, setSearchText] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("");
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<ServiceForm>(EMPTY_FORM);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");
  const [togglingId, setTogglingId] = useState<string | null>(null);
  const [showCommission, setShowCommission] = useState(false);

  // YOLO-S2: per-row expandable consumables management panel state.
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [consumablesByService, setConsumablesByService] = useState<Record<string, ServiceConsumable[]>>({});
  const [consumablesLoading, setConsumablesLoading] = useState<string | null>(null);
  const [inventoryItems, setInventoryItems] = useState<InventoryItemOption[]>([]);
  const [newConsumable, setNewConsumable] = useState<{ itemId: string; quantity: number; notes: string }>({ itemId: "", quantity: 1, notes: "" });
  const [consumableError, setConsumableError] = useState("");
  const [consumableSaving, setConsumableSaving] = useState(false);

  const load = () => {
    setLoading(true);
    api
      .get<ServiceRow[]>("/api/settings/services", {
        params: { search: searchText || undefined, category: categoryFilter || undefined },
      })
      .then((r) => setServices(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(load, [searchText, categoryFilter]);

  const handleOpenAdd = () => {
    setForm(EMPTY_FORM);
    setEditingId(null);
    setFormError("");
    setShowCommission(false);
    setShowForm(true);
  };

  const handleOpenEdit = async (s: ServiceRow) => {
    setForm({
      arabicName: s.arabicName,
      englishName: s.englishName,
      code: s.code,
      department: s.department ?? "",
      category: s.category,
      description: s.description ?? "",
      defaultDurationMinutes: s.defaultDurationMinutes,
      defaultPrice: Number(s.defaultPrice),
      requiresDoctor: s.requiresDoctor,
      requiresConsultationFee: s.requiresConsultationFee,
      showInBooking: s.showInBooking,
      showInReception: s.showInReception,
      showInTreatmentPlan: s.showInTreatmentPlan,
      sortOrder: s.sortOrder,
      color: s.color ?? "",
      // Commission defaults — now returned directly from the services API
      defaultMaterialCost: s.defaultMaterialCost ?? 0,
      defaultMaterialCostType: s.defaultMaterialCostType ?? "FixedAmount",
      defaultLabCost: s.defaultLabCost ?? 0,
      defaultDoctorCommissionPercentage: s.defaultDoctorCommissionPercentage ?? null,
      commissionBaseRule: s.commissionBaseRule ?? "AfterDiscountAndCosts",
      commissionRecognitionMode: s.commissionRecognitionMode ?? "OnInvoiceApproval",
    });
    setEditingId(s.id);
    setFormError("");
    setShowForm(true);
  };

  const handleCloseForm = () => {
    setShowForm(false);
    setEditingId(null);
    setForm(EMPTY_FORM);
    setShowCommission(false);
    setFormError("");
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.arabicName.trim()) {
      setFormError("اسم الخدمة بالعربية مطلوب");
      return;
    }
    if (!form.code.trim()) {
      setFormError("كود الخدمة مطلوب");
      return;
    }

    setSaving(true);
    setFormError("");

    try {
      const payload = {
        arabicName: form.arabicName,
        englishName: form.englishName || null,
        code: form.code,
        department: form.department || null,
        category: form.category,
        description: form.description || null,
        defaultDurationMinutes: form.defaultDurationMinutes,
        defaultPrice: form.defaultPrice,
        requiresDoctor: form.requiresDoctor,
        requiresConsultationFee: form.requiresConsultationFee,
        showInBooking: form.showInBooking,
        showInReception: form.showInReception,
        showInTreatmentPlan: form.showInTreatmentPlan,
        sortOrder: form.sortOrder,
        color: form.color || null,
        // Commission defaults — now sent directly with the service payload
        defaultMaterialCost: form.defaultMaterialCost,
        defaultMaterialCostType: form.defaultMaterialCostType,
        defaultLabCost: form.defaultLabCost,
        defaultDoctorCommissionPercentage: form.defaultDoctorCommissionPercentage,
        commissionBaseRule: form.commissionBaseRule,
        commissionRecognitionMode: form.commissionRecognitionMode,
      };

      if (editingId) {
        await api.put(`/api/settings/services/${editingId}`, payload);
      } else {
        await api.post("/api/settings/services", payload);
      }

      handleCloseForm();
      load();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setFormError(msg ?? "حدث خطأ أثناء الحفظ");
    } finally {
      setSaving(false);
    }
  };

  const handleToggle = async (id: string, isActive: boolean) => {
    setTogglingId(id);
    try {
      if (isActive) {
        await api.patch(`/api/settings/services/${id}/deactivate`);
      } else {
        await api.patch(`/api/settings/services/${id}/activate`);
      }
      load();
    } catch {
    } finally {
      setTogglingId(null);
    }
  };

  // ─── YOLO-S2: ServiceConsumable (service ↔ inventory item) management ────────

  const loadInventoryItems = async () => {
    if (inventoryItems.length > 0) return;
    try {
      const r = await api.get<InventoryItemOption[]>("/api/inventory", { params: { activeOnly: true } });
      setInventoryItems(r.data ?? []);
    } catch {
      // best-effort — the panel just won't show the dropdown options
    }
  };

  const loadConsumables = async (serviceId: string) => {
    setConsumablesLoading(serviceId);
    try {
      const r = await api.get<ServiceConsumable[]>("/api/service-consumables", { params: { serviceId } });
      setConsumablesByService((prev) => ({ ...prev, [serviceId]: r.data ?? [] }));
    } catch {
      setConsumablesByService((prev) => ({ ...prev, [serviceId]: [] }));
    } finally {
      setConsumablesLoading(null);
    }
  };

  const handleToggleExpand = async (serviceId: string) => {
    if (expandedId === serviceId) {
      setExpandedId(null);
      return;
    }
    setExpandedId(serviceId);
    setConsumableError("");
    setNewConsumable({ itemId: "", quantity: 1, notes: "" });
    await loadInventoryItems();
    await loadConsumables(serviceId);
  };

  const handleAddConsumable = async (serviceId: string) => {
    if (!newConsumable.itemId) {
      setConsumableError("اختر المادة أولاً");
      return;
    }
    if (newConsumable.quantity < 1) {
      setConsumableError("الكمية يجب أن تكون 1 على الأقل");
      return;
    }
    setConsumableSaving(true);
    setConsumableError("");
    try {
      await api.post("/api/service-consumables", {
        clinicServiceId: serviceId,
        inventoryItemId: newConsumable.itemId,
        quantity: newConsumable.quantity,
        notes: newConsumable.notes || null,
      });
      setNewConsumable({ itemId: "", quantity: 1, notes: "" });
      await loadConsumables(serviceId);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setConsumableError(msg ?? "فشل إضافة المادة");
    } finally {
      setConsumableSaving(false);
    }
  };

  const handleDeleteConsumable = async (serviceId: string, consumableId: string) => {
    if (!confirm("هل أنت متأكد من حذف هذه المادة من الخدمة؟")) return;
    try {
      await api.delete(`/api/service-consumables/${consumableId}`);
      await loadConsumables(serviceId);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      alert(msg ?? "فشل حذف المادة");
    }
  };

  // ─── Render ──────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-5 max-w-6xl">
      {/* Header */}
      <div className="flex items-center gap-3">
        <Link
          href="/settings"
          className="text-gray-400 hover:text-gray-700 transition text-sm"
        >
          الإعدادات
        </Link>
        <span className="text-gray-300">/</span>
        <h1 className="text-2xl font-extrabold text-gray-900">خدمات العيادة</h1>
      </div>
      <p className="text-sm text-gray-500 -mt-3">إدارة كتالوج الخدمات والأسعار والأعلام</p>

      {/* Toolbar */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center gap-3">
        <div className="relative flex-1 w-full sm:max-w-xs">
          <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
          <input
            value={searchText}
            onChange={(e) => setSearchText(e.target.value)}
            placeholder="بحث بالاسم أو الكود..."
            className={cn(inputCls, "pr-9")}
          />
        </div>
        <select
          value={categoryFilter}
          onChange={(e) => setCategoryFilter(e.target.value)}
          className={inputCls + " w-full sm:w-auto"}
        >
          <option value="">كل التصنيفات</option>
          {CATEGORIES.map((c) => (
            <option key={c} value={c}>
              {CATEGORY_LABELS[c]}
            </option>
          ))}
        </select>
        <button
          onClick={handleOpenAdd}
          className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <Plus className="w-4 h-4" />
          خدمة جديدة
        </button>
      </div>

      {/* Form Modal */}
      {showForm && (
        <form
          onSubmit={handleSubmit}
          className="bg-gray-50 rounded-xl border border-gray-200 p-5 space-y-4"
        >
          <div className="flex items-center justify-between">
            <p className="text-sm font-semibold text-gray-800">
              {editingId ? "تعديل الخدمة" : "إضافة خدمة جديدة"}
            </p>
            <button
              type="button"
              onClick={handleCloseForm}
              className="text-gray-400 hover:text-gray-700 transition"
            >
              <X className="w-5 h-5" />
            </button>
          </div>

          {formError && (
            <p className="text-xs text-red-600 bg-red-50 px-3 py-2 rounded-lg">{formError}</p>
          )}

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
            {/* Arabic Name */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                الاسم بالعربية <span className="text-red-500">*</span>
              </label>
              <input
                value={form.arabicName}
                onChange={(e) => setForm({ ...form, arabicName: e.target.value })}
                className={inputCls}
                placeholder="تنظيف الأسنان"
              />
            </div>
            {/* English Name */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                الاسم بالإنجليزية
              </label>
              <input
                value={form.englishName}
                onChange={(e) => setForm({ ...form, englishName: e.target.value })}
                className={inputCls}
                placeholder="Teeth Cleaning"
                dir="ltr"
              />
            </div>
            {/* Code */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                الكود <span className="text-red-500">*</span>
              </label>
              <input
                value={form.code}
                onChange={(e) => setForm({ ...form, code: e.target.value })}
                className={inputCls}
                placeholder="SRV-001"
                dir="ltr"
              />
            </div>
            {/* Department */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">القسم</label>
              <input
                value={form.department}
                onChange={(e) => setForm({ ...form, department: e.target.value })}
                className={inputCls}
                placeholder="قسم العلاج"
              />
            </div>
            {/* Category */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">التصنيف</label>
              <select
                value={form.category}
                onChange={(e) => setForm({ ...form, category: e.target.value })}
                className={inputCls}
              >
                {CATEGORIES.map((c) => (
                  <option key={c} value={c}>
                    {CATEGORY_LABELS[c]}
                  </option>
                ))}
              </select>
            </div>
            {/* Duration */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                المدة (دقيقة)
              </label>
              <input
                type="number"
                min={5}
                max={480}
                value={form.defaultDurationMinutes}
                onChange={(e) =>
                  setForm({ ...form, defaultDurationMinutes: parseInt(e.target.value) || 30 })
                }
                className={inputCls}
                dir="ltr"
              />
            </div>
            {/* Default Price */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                السعر الافتراضي
              </label>
              <input
                type="number"
                min={0}
                step={100}
                value={form.defaultPrice}
                onChange={(e) =>
                  setForm({ ...form, defaultPrice: parseFloat(e.target.value) || 0 })
                }
                className={inputCls}
                dir="ltr"
              />
            </div>
            {/* Sort Order */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">ترتيب العرض</label>
              <input
                type="number"
                min={0}
                value={form.sortOrder}
                onChange={(e) =>
                  setForm({ ...form, sortOrder: parseInt(e.target.value) || 0 })
                }
                className={inputCls}
                dir="ltr"
              />
            </div>
            {/* Description */}
            <div className="md:col-span-2 lg:col-span-1">
              <label className="block text-xs font-medium text-gray-600 mb-1">الوصف</label>
              <input
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
                className={inputCls}
                placeholder="وصف مختصر للخدمة"
              />
            </div>
            {/* Color (YOLO-S2) */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                اللون (لعرض التقويم/الطابور)
              </label>
              <div className="flex items-center gap-2">
                <input
                  type="color"
                  value={form.color || "#3b82f6"}
                  onChange={(e) => setForm({ ...form, color: e.target.value })}
                  className="w-10 h-9 p-1 rounded-lg border border-gray-300 cursor-pointer bg-white"
                  aria-label="منتقي اللون للخدمة"
                />
                <input
                  value={form.color}
                  onChange={(e) => setForm({ ...form, color: e.target.value })}
                  className={cn(inputCls, "w-28 font-mono text-xs")}
                  dir="ltr"
                  placeholder="#3b82f6"
                />
                {form.color && (
                  <button
                    type="button"
                    onClick={() => setForm({ ...form, color: "" })}
                    className="text-xs text-gray-400 hover:text-red-600 transition px-1"
                    title="مسح اللون"
                  >
                    <X className="w-4 h-4" />
                  </button>
                )}
              </div>
            </div>
          </div>

          {/* Toggles */}
          <div className="flex flex-wrap gap-3 pt-1">
            {([
              { key: "showInBooking" as const, label: "يظهر في الحجز" },
              { key: "showInReception" as const, label: "يظهر في الاستقبال" },
              { key: "showInTreatmentPlan" as const, label: "يظهر في خطة العلاج" },
              { key: "requiresDoctor" as const, label: "يحتاج طبيب" },
              { key: "requiresConsultationFee" as const, label: "يحتاج رسوم معاينة" },
            ] as const).map(({ key, label }) => (
              <label
                key={key}
                className="flex items-center gap-2 text-xs font-medium text-gray-700 cursor-pointer select-none"
              >
                <input
                  type="checkbox"
                  checked={form[key]}
                  onChange={(e) => setForm({ ...form, [key]: e.target.checked })}
                  className="w-4 h-4 rounded border-gray-300 text-clinic-blue focus:ring-clinic-blue"
                />
                {label}
              </label>
            ))}
          </div>

          {/* Commission Defaults — collapsible, available for both create and edit */}
          <div className="border border-gray-200 rounded-lg overflow-hidden">
            <button
              type="button"
              onClick={() => setShowCommission((v) => !v)}
              className="w-full flex items-center justify-between px-4 py-2.5 bg-gray-50 hover:bg-gray-100 transition text-sm"
            >
              <span className="font-medium text-gray-700">إعدادات العمولة الافتراضية</span>
              {showCommission ? <ChevronUp className="w-4 h-4 text-gray-400" /> : <ChevronDown className="w-4 h-4 text-gray-400" />}
            </button>
            {showCommission && (
              <div className="p-4 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">تكلفة المواد الافتراضية</label>
                  <input
                    type="number"
                    min={0}
                    step="0.01"
                    value={form.defaultMaterialCost}
                    onChange={(e) => setForm({ ...form, defaultMaterialCost: parseFloat(e.target.value) || 0 })}
                    className={inputCls}
                    dir="ltr"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">نوع تكلفة المواد</label>
                  <select
                    value={form.defaultMaterialCostType}
                    onChange={(e) => setForm({ ...form, defaultMaterialCostType: e.target.value })}
                    className={inputCls}
                  >
                    <option value="FixedAmount">مبلغ ثابت</option>
                    <option value="PercentageOfServicePrice">نسبة من سعر الخدمة</option>
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">تكلفة المعمل الافتراضية</label>
                  <input
                    type="number"
                    min={0}
                    step="0.01"
                    value={form.defaultLabCost}
                    onChange={(e) => setForm({ ...form, defaultLabCost: parseFloat(e.target.value) || 0 })}
                    className={inputCls}
                    dir="ltr"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">نسبة عمولة الطبيب الافتراضية (%)</label>
                  <input
                    type="number"
                    min={0}
                    max={100}
                    step="0.5"
                    value={form.defaultDoctorCommissionPercentage ?? ""}
                    placeholder="غير محدد"
                    onChange={(e) => setForm({ ...form, defaultDoctorCommissionPercentage: e.target.value === "" ? null : parseFloat(e.target.value) })}
                    className={inputCls}
                    dir="ltr"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">أساس احتساب العمولة</label>
                  <select
                    value={form.commissionBaseRule}
                    onChange={(e) => setForm({ ...form, commissionBaseRule: e.target.value })}
                    className={inputCls}
                  >
                    <option value="GrossAmount">الإجمالي الكلي</option>
                    <option value="AfterDiscount">بعد الخصم</option>
                    <option value="AfterDiscountAndCosts">بعد الخصم والتكاليف</option>
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">وقت احتساب العمولة</label>
                  <select
                    value={form.commissionRecognitionMode}
                    onChange={(e) => setForm({ ...form, commissionRecognitionMode: e.target.value })}
                    className={inputCls}
                  >
                    <option value="OnInvoiceApproval">عند إصدار الفاتورة</option>
                    <option value="OnPaymentCollection">عند تحصيل الدفعة</option>
                  </select>
                </div>
              </div>
            )}
          </div>

          {/* Submit */}
          <div className="flex justify-end gap-2 pt-1">
            <button
              type="button"
              onClick={handleCloseForm}
              className="px-4 py-2 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={saving}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition"
            >
              <Save className="w-4 h-4" />
              {saving ? "جارٍ الحفظ..." : editingId ? "تحديث الخدمة" : "إضافة الخدمة"}
            </button>
          </div>
        </form>
      )}

      {/* Loading */}
      {loading && (
        <div className="animate-pulse space-y-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="h-12 bg-gray-100 rounded-lg" />
          ))}
        </div>
      )}

      {/* Empty State */}
      {!loading && services.length === 0 && (
        <div className="text-center py-16">
          <Stethoscope className="w-12 h-12 text-gray-300 mx-auto mb-3" />
          <p className="text-gray-500 font-medium">لا توجد خدمات</p>
          <p className="text-sm text-gray-400 mt-1">
            أضف أول خدمة بالضغط على زر &quot;خدمة جديدة&quot;
          </p>
        </div>
      )}

      {/* Table */}
      {!loading && services.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-gray-200">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                {[
                  "الاسم",
                  "الكود",
                  "التصنيف",
                  "المدة",
                  "السعر الافتراضي",
                  "اللون",
                  "العمولة",
                  "الحالة",
                  "إجراءات",
                ].map((h) => (
                  <th
                    key={h}
                    className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap"
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {services.map((s) => (
                <Fragment key={s.id}>
                  <tr className="hover:bg-gray-50 transition">
                  <td className="px-4 py-3">
                    <div className="flex items-start gap-2">
                      {/* YOLO-S2: color swatch (optional) */}
                      {s.color && (
                        <span
                          className="w-3 h-3 rounded-full mt-1 flex-shrink-0 border border-gray-200"
                          style={{ backgroundColor: s.color }}
                          title={`لون الخدمة: ${s.color}`}
                        />
                      )}
                      <div>
                        <div className="font-medium text-gray-900">{s.arabicName}</div>
                        {s.englishName && (
                          <div className="text-xs text-gray-400 mt-0.5" dir="ltr">
                            {s.englishName}
                          </div>
                        )}
                        {/* Visibility badges */}
                        <div className="flex flex-wrap gap-1 mt-1.5">
                          {s.showInBooking && (
                            <Badge className="bg-emerald-50 text-emerald-600">حجز</Badge>
                          )}
                          {s.showInReception && (
                            <Badge className="bg-sky-50 text-sky-600">استقبال</Badge>
                          )}
                          {s.showInTreatmentPlan && (
                            <Badge className="bg-violet-50 text-violet-600">خطة علاج</Badge>
                          )}
                          {s.requiresDoctor && (
                            <Badge className="bg-orange-50 text-orange-600">طبيب</Badge>
                          )}
                          {s.requiresConsultationFee && (
                            <Badge className="bg-rose-50 text-rose-600">رسوم معاينة</Badge>
                          )}
                        </div>
                      </div>
                    </div>
                  </td>
                  <td className="px-4 py-3 font-mono text-xs text-gray-600" dir="ltr">
                    {s.code}
                  </td>
                  <td className="px-4 py-3">
                    <span
                      className={cn(
                        "text-xs px-2 py-0.5 rounded-full font-medium",
                        CATEGORY_COLORS[s.category] ?? "bg-gray-50 text-gray-700"
                      )}
                    >
                      {CATEGORY_LABELS[s.category] ?? s.category}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-gray-700 whitespace-nowrap">
                    {s.defaultDurationMinutes} دقيقة
                  </td>
                  <td className="px-4 py-3 text-gray-700 whitespace-nowrap">
                    {Number(s.defaultPrice).toLocaleString("ar-YE")} ر.ي
                  </td>
                  <td className="px-4 py-3">
                    {s.color ? (
                      <div className="flex items-center gap-1.5">
                        <span
                          className="w-5 h-5 rounded-md border border-gray-200"
                          style={{ backgroundColor: s.color }}
                        />
                        <span className="text-xs font-mono text-gray-500" dir="ltr">{s.color}</span>
                      </div>
                    ) : (
                      <span className="text-xs text-gray-400">—</span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-gray-700 whitespace-nowrap">
                    {s.defaultDoctorCommissionPercentage != null ? (
                      <div className="text-xs">
                        <span className="font-medium">{s.defaultDoctorCommissionPercentage}%</span>
                        {s.defaultLabCost > 0 && (
                          <span className="text-gray-400 mr-1">· معمل {Number(s.defaultLabCost).toLocaleString("ar-YE")}</span>
                        )}
                        {s.defaultMaterialCost > 0 && (
                          <span className="text-gray-400 mr-1">· مواد {Number(s.defaultMaterialCost).toLocaleString("ar-YE")}</span>
                        )}
                      </div>
                    ) : (
                      <span className="text-xs text-gray-400">—</span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <span
                      className={cn(
                        "text-xs px-2 py-0.5 rounded-full font-medium",
                        s.isActive
                          ? "bg-green-50 text-green-700"
                          : "bg-gray-100 text-gray-500"
                      )}
                    >
                      {s.isActive ? "نشط" : "معطّل"}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-1">
                      <button
                        onClick={() => handleToggleExpand(s.id)}
                        title={expandedId === s.id ? "إخفاء المواد المستهلكة" : "عرض المواد المستهلكة"}
                        className={cn(
                          "text-gray-400 hover:text-clinic-blue transition p-1",
                          expandedId === s.id && "text-clinic-blue"
                        )}
                      >
                        {expandedId === s.id ? (
                          <ChevronUp className="w-4 h-4" />
                        ) : (
                          <ChevronLeft className="w-4 h-4" />
                        )}
                      </button>
                      <button
                        onClick={() => handleOpenEdit(s)}
                        title="تعديل الخدمة"
                        className="text-gray-400 hover:text-clinic-blue transition p-1"
                      >
                        <Edit3 className="w-4 h-4" />
                      </button>
                      <button
                        onClick={() => handleToggle(s.id, s.isActive)}
                        disabled={togglingId === s.id}
                        title={s.isActive ? "تعطيل الخدمة" : "تفعيل الخدمة"}
                        className="text-gray-400 hover:text-gray-700 transition p-1 disabled:opacity-50"
                      >
                        {s.isActive ? (
                          <PowerOff className="w-4 h-4" />
                        ) : (
                          <Power className="w-4 h-4 text-green-600" />
                        )}
                      </button>
                    </div>
                  </td>
                </tr>
                {/* YOLO-S2: Expandable ServiceConsumable management panel */}
                {expandedId === s.id && (
                  <tr className="bg-gray-50/60">
                    <td colSpan={9} className="px-6 py-4">
                      <div className="border border-gray-200 rounded-lg bg-white p-4 space-y-3">
                        <div className="flex items-center justify-between">
                          <h4 className="text-sm font-bold text-gray-800">
                            المواد المستهلكة لكل جلسة — <span className="text-clinic-blue">{s.arabicName}</span>
                          </h4>
                          <span className="text-xs text-gray-400">
                            {(consumablesByService[s.id] ?? []).length} مادة
                          </span>
                        </div>
                        <p className="text-xs text-gray-500">
                          تُستخدم هذه القائمة كتالوجًا فقط (لا تخصم تلقائيًا من المخزون عند إكمال الموعد).
                        </p>

                        {consumableError && (
                          <p className="text-xs text-red-600 bg-red-50 px-3 py-2 rounded-lg">{consumableError}</p>
                        )}

                        {/* Existing consumables list */}
                        {consumablesLoading === s.id ? (
                          <div className="text-xs text-gray-400 py-3 text-center">جارٍ التحميل...</div>
                        ) : (consumablesByService[s.id] ?? []).length === 0 ? (
                          <div className="text-xs text-gray-400 py-3 text-center bg-gray-50 rounded-lg">
                            لا توجد مواد مرتبطة بهذه الخدمة بعد
                          </div>
                        ) : (
                          <ul className="divide-y divide-gray-100 border border-gray-200 rounded-lg">
                            {(consumablesByService[s.id] ?? []).map((c) => (
                              <li key={c.id} className="flex items-center justify-between gap-3 px-3 py-2">
                                <div className="flex-1 min-w-0">
                                  <div className="text-sm font-medium text-gray-800 truncate">
                                    {c.inventoryItemName}
                                    {c.inventoryItemUnit && (
                                      <span className="text-xs text-gray-400 mr-2">({c.inventoryItemUnit})</span>
                                    )}
                                  </div>
                                  <div className="text-xs text-gray-500 mt-0.5">
                                    الكمية: <span className="font-mono">{c.quantity}</span>
                                    {c.notes && <span className="mr-2">· {c.notes}</span>}
                                  </div>
                                </div>
                                <button
                                  onClick={() => handleDeleteConsumable(s.id, c.id)}
                                  title="حذف المادة من الخدمة"
                                  className="text-gray-400 hover:text-red-600 transition p-1"
                                >
                                  <Trash2 className="w-4 h-4" />
                                </button>
                              </li>
                            ))}
                          </ul>
                        )}

                        {/* Add new consumable */}
                        <div className="grid grid-cols-1 md:grid-cols-12 gap-2 pt-1">
                          <div className="md:col-span-6">
                            <label className="block text-xs font-medium text-gray-600 mb-1">المادة</label>
                            <select
                              value={newConsumable.itemId}
                              onChange={(e) => setNewConsumable((p) => ({ ...p, itemId: e.target.value }))}
                              className={inputCls}
                            >
                              <option value="">اختر المادة...</option>
                              {inventoryItems.map((it) => (
                                <option key={it.id} value={it.id}>
                                  {it.name}{it.unit ? ` (${it.unit})` : ""}
                                </option>
                              ))}
                            </select>
                          </div>
                          <div className="md:col-span-2">
                            <label className="block text-xs font-medium text-gray-600 mb-1">الكمية</label>
                            <input
                              type="number"
                              min={1}
                              value={newConsumable.quantity}
                              onChange={(e) => setNewConsumable((p) => ({ ...p, quantity: parseInt(e.target.value) || 1 }))}
                              className={inputCls}
                              dir="ltr"
                            />
                          </div>
                          <div className="md:col-span-3">
                            <label className="block text-xs font-medium text-gray-600 mb-1">ملاحظات (اختياري)</label>
                            <input
                              value={newConsumable.notes}
                              onChange={(e) => setNewConsumable((p) => ({ ...p, notes: e.target.value }))}
                              className={inputCls}
                              placeholder="مثال: فقط للحالات المعقدة"
                            />
                          </div>
                          <div className="md:col-span-1 flex items-end">
                            <button
                              type="button"
                              onClick={() => handleAddConsumable(s.id)}
                              disabled={consumableSaving}
                              className="w-full flex items-center justify-center gap-1 px-2 py-2 text-xs font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition"
                              title="إضافة المادة"
                            >
                              <Plus className="w-3.5 h-3.5" />
                            </button>
                          </div>
                        </div>
                      </div>
                    </td>
                  </tr>
                )}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
