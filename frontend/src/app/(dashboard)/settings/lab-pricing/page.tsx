"use client";
import { useEffect, useState, useCallback } from "react";
import {
  Plus, X, Search, Trash2, Edit3, Save, DollarSign,
  Loader2, CheckCircle2, AlertTriangle,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import api from "@/lib/api";
import type { Lab, LabWorkType, LabWorkPrice, CreateLabWorkPriceRequest, UpdateLabWorkPriceRequest } from "@/types/lab";

const inputCls = "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue";

export default function LabPricingPage() {
  const [prices, setPrices] = useState<LabWorkPrice[]>([]);
  const [labs, setLabs] = useState<Lab[]>([]);
  const [workTypes, setWorkTypes] = useState<LabWorkType[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [filterLabId, setFilterLabId] = useState<string>("");
  const [searchQuery, setSearchQuery] = useState("");
  const [modalMode, setModalMode] = useState<"create" | "edit">("create");
  const [showModal, setShowModal] = useState(false);
  const [editingId, setEditingId] = useState<string>("");
  const [form, setForm] = useState<CreateLabWorkPriceRequest>({ labId: "", workTypeId: "", unitPrice: 0 });
  const [toast, setToast] = useState<{ message: string; type: "success" | "error" } | null>(null);
  const showToast = useCallback((message: string, type: "success" | "error" = "success") => { setToast({ message, type }); setTimeout(() => setToast(null), 4000); }, []);
  const [confirmDialog, setConfirmDialog] = useState<{ open: boolean; title: string; message: string; onConfirm: () => void; }>({ open: false, title: "", message: "", onConfirm: () => {} });

  const fetchData = async () => {
    try {
      const [pricesRes, labsRes, typesRes] = await Promise.all([
        api.get<{ data: LabWorkPrice[] }>("/api/lab-work-prices"),
        api.get<{ data: Lab[] }>("/api/labs"),
        api.get<{ data: LabWorkType[] }>("/api/lab-work-types"),
      ]);
      setPrices(pricesRes.data.data);
      setLabs(labsRes.data.data);
      setWorkTypes(typesRes.data.data);
    } catch { showToast("فشل تحميل البيانات", "error"); }
    finally { setLoading(false); }
  };

  useEffect(() => { fetchData(); }, []);

  const openCreateModal = () => {
    setModalMode("create");
    setForm({ labId: labs[0]?.id ?? "", workTypeId: workTypes[0]?.id ?? "", unitPrice: 0 });
    setShowModal(true);
  };

  const openEditModal = (p: LabWorkPrice) => {
    setModalMode("edit"); setEditingId(p.id);
    setForm({ labId: p.labId, workTypeId: p.workTypeId, unitPrice: p.unitPrice, urgentSurcharge: p.urgentSurcharge ?? undefined, urgentSurchargeType: p.urgentSurchargeType ?? undefined, estimatedDays: p.estimatedDays ?? undefined, notes: p.notes ?? "" });
    setShowModal(true);
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      if (modalMode === "create") { await api.post("/api/lab-work-prices", form); showToast("تم إضافة السعر بنجاح"); }
      else {
        const u: UpdateLabWorkPriceRequest = { unitPrice: form.unitPrice, urgentSurcharge: form.urgentSurcharge, urgentSurchargeType: form.urgentSurchargeType, estimatedDays: form.estimatedDays, notes: form.notes };
        await api.put(`/api/lab-work-prices/${editingId}`, u); showToast("تم تحديث السعر بنجاح");
      }
      setShowModal(false); fetchData();
    } catch (err: unknown) { showToast((err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ", "error"); }
    finally { setSaving(false); }
  };

  const handleDelete = (p: LabWorkPrice) => {
    setConfirmDialog({ open: true, title: "حذف السعر", message: `هل أنت متأكد من حذف سعر "${p.workTypeName}" للمعمل "${p.labName}"؟`, onConfirm: async () => {
      try { await api.delete(`/api/lab-work-prices/${p.id}`); showToast("تم حذف السعر"); fetchData(); } catch { showToast("حدث خطأ", "error"); }
      setConfirmDialog((prev) => ({ ...prev, open: false }));
    }});
  };

  const filtered = prices.filter((p) => {
    if (filterLabId && p.labId !== filterLabId) return false;
    if (searchQuery) { const q = searchQuery.toLowerCase(); return (p.labName ?? "").toLowerCase().includes(q) || (p.workTypeName ?? "").toLowerCase().includes(q) || (p.workTypeNameAr ?? "").includes(q); }
    return true;
  });

  if (loading) return <div className="animate-pulse space-y-3">{Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-12 bg-gray-100 rounded-lg" />)}</div>;

  return (
    <div className="space-y-5">
      {toast && <div className={cn("fixed top-4 left-1/2 -translate-x-1/2 z-[100] px-4 py-2.5 rounded-lg shadow-lg text-sm font-medium flex items-center gap-2", toast.type === "success" ? "bg-green-600 text-white" : "bg-red-600 text-white")}>{toast.type === "success" ? <CheckCircle2 className="w-4 h-4" /> : <AlertTriangle className="w-4 h-4" />}{toast.message}</div>}
      <div className="flex items-center justify-between">
        <p className="text-sm text-gray-500">{filtered.length} تسعيرة</p>
        <button onClick={openCreateModal} className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"><Plus className="w-4 h-4" /> إضافة تسعيرة</button>
      </div>
      <div className="flex flex-wrap gap-3 items-center">
        <div className="relative flex-1 min-w-[200px]"><Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" /><input type="text" value={searchQuery} onChange={(e) => setSearchQuery(e.target.value)} placeholder="بحث..." className="w-full pr-9 pl-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue" /></div>
        <select value={filterLabId} onChange={(e) => setFilterLabId(e.target.value)} className="px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white"><option value="">جميع المعامل</option>{labs.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}</select>
      </div>
      <div className="overflow-x-auto rounded-lg border border-gray-200">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 border-b border-gray-200"><tr>{["المعمل", "نوع العمل", "السعر", "إضافة مستعجلة", "أيام متوقعة", "إجراءات"].map((h) => (<th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap">{h}</th>))}</tr></thead>
          <tbody className="divide-y divide-gray-100">
            {filtered.length === 0 ? <tr><td colSpan={6} className="px-4 py-8 text-center text-gray-400">لا توجد تسعيرات</td></tr> : filtered.map((p) => (
              <tr key={p.id} className="hover:bg-gray-50 transition">
                <td className="px-4 py-3 font-medium text-gray-900">{p.labName ?? "—"}</td>
                <td className="px-4 py-3 text-gray-700">{p.workTypeNameAr ?? p.workTypeName ?? "—"}</td>
                <td className="px-4 py-3 text-gray-900 font-mono" dir="ltr">{p.unitPrice.toLocaleString()}</td>
                <td className="px-4 py-3 text-gray-500 text-xs">{p.urgentSurcharge != null ? `${p.urgentSurcharge}${p.urgentSurchargeType === "percentage" ? "%" : ""}` : "—"}</td>
                <td className="px-4 py-3 text-gray-500">{p.estimatedDays != null ? `${p.estimatedDays} يوم` : "—"}</td>
                <td className="px-4 py-3"><div className="flex items-center gap-1">
                  <button onClick={() => openEditModal(p)} title="تعديل" className="p-1.5 rounded-lg text-gray-400 hover:text-clinic-blue hover:bg-blue-50 transition"><Edit3 className="w-4 h-4" /></button>
                  <button onClick={() => handleDelete(p)} title="حذف" className="p-1.5 rounded-lg text-gray-400 hover:text-red-600 hover:bg-red-50 transition"><Trash2 className="w-4 h-4" /></button>
                </div></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true">
          <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={() => setShowModal(false)} />
          <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-lg p-6 space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between"><h3 className="text-base font-bold text-gray-900 flex items-center gap-2"><DollarSign className="w-5 h-5 text-clinic-blue" />{modalMode === "create" ? "إضافة تسعيرة" : "تعديل التسعيرة"}</h3><button onClick={() => setShowModal(false)} className="p-1 rounded-lg text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition"><X className="w-5 h-5" /></button></div>
            <div className="space-y-3">
              <div><label className="block text-xs font-medium text-gray-600 mb-1">المعمل <span className="text-red-500">*</span></label><select value={form.labId} onChange={(e) => setForm({ ...form, labId: e.target.value })} disabled={modalMode === "edit"} className={cn(inputCls, modalMode === "edit" && "bg-gray-100")}><option value="">اختر المعمل</option>{labs.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}</select></div>
              <div><label className="block text-xs font-medium text-gray-600 mb-1">نوع العمل <span className="text-red-500">*</span></label><select value={form.workTypeId} onChange={(e) => setForm({ ...form, workTypeId: e.target.value })} disabled={modalMode === "edit"} className={cn(inputCls, modalMode === "edit" && "bg-gray-100")}><option value="">اختر نوع العمل</option>{workTypes.map((w) => <option key={w.id} value={w.id}>{w.nameAr ?? w.name} ({w.name})</option>)}</select></div>
              <div className="grid grid-cols-2 gap-3">
                <div><label className="block text-xs font-medium text-gray-600 mb-1">السعر <span className="text-red-500">*</span></label><input type="number" value={form.unitPrice} onChange={(e) => setForm({ ...form, unitPrice: parseFloat(e.target.value) || 0 })} className={inputCls} min="0" dir="ltr" /></div>
                <div><label className="block text-xs font-medium text-gray-600 mb-1">الأيام المتوقعة</label><input type="number" value={form.estimatedDays ?? ""} onChange={(e) => setForm({ ...form, estimatedDays: e.target.value ? parseInt(e.target.value) : undefined })} className={inputCls} min="0" dir="ltr" /></div>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div><label className="block text-xs font-medium text-gray-600 mb-1">إضافة مستعجلة</label><input type="number" value={form.urgentSurcharge ?? ""} onChange={(e) => setForm({ ...form, urgentSurcharge: e.target.value ? parseFloat(e.target.value) : undefined })} className={inputCls} min="0" dir="ltr" /></div>
                <div><label className="block text-xs font-medium text-gray-600 mb-1">نوع الإضافة</label><select value={form.urgentSurchargeType ?? ""} onChange={(e) => setForm({ ...form, urgentSurchargeType: e.target.value || undefined })} className={inputCls}><option value="">بدون</option><option value="fixed">مبلغ ثابت</option><option value="percentage">نسبة مئوية</option></select></div>
              </div>
              <div><label className="block text-xs font-medium text-gray-600 mb-1">ملاحظات</label><input type="text" value={form.notes ?? ""} onChange={(e) => setForm({ ...form, notes: e.target.value })} className={inputCls} /></div>
            </div>
            <div className="flex items-center justify-end gap-3 pt-2">
              <button onClick={() => setShowModal(false)} className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition">إلغاء</button>
              <button onClick={handleSave} disabled={saving || !form.labId || !form.workTypeId} className="px-4 py-2 text-sm font-medium text-white bg-clinic-blue rounded-lg hover:opacity-90 disabled:opacity-60 transition flex items-center gap-2">{saving && <Loader2 className="w-4 h-4 animate-spin" />}<Save className="w-4 h-4" />{saving ? "جارٍ الحفظ..." : "حفظ"}</button>
            </div>
          </div>
        </div>
      )}
      <ConfirmDialog open={confirmDialog.open} title={confirmDialog.title} message={confirmDialog.message} variant="danger" onConfirm={confirmDialog.onConfirm} onCancel={() => setConfirmDialog((prev) => ({ ...prev, open: false }))} />
    </div>
  );
}
