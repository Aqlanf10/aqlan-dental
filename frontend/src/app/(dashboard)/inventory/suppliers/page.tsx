"use client";

import { useState, type FormEvent } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import {
  Truck, Phone, Mail, Plus, Edit2, Trash2, Search,
} from "lucide-react";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";
import { TableSkeleton } from "@/components/ui/skeleton";
import { ErrorBoundary } from "@/components/shared/ErrorBoundary";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import { cn } from "@/lib/utils";
import type { Supplier, CreateSupplierRequest, PaginatedResponse } from "@/types/inventory";

/* ─── Supplier Form Modal ──────────────────────────────────────────────────── */
function SupplierFormModal({
  supplier,
  onClose,
}: {
  supplier: Supplier | null;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const isEdit = supplier !== null;

  const [form, setForm] = useState<CreateSupplierRequest>({
    name: supplier?.name ?? "",
    contactPerson: supplier?.contactPerson ?? "",
    phone: supplier?.phone ?? "",
    email: supplier?.email ?? "",
    address: supplier?.address ?? "",
    notes: supplier?.notes ?? "",
  });

  const mutation = useMutation({
    mutationFn: async (data: CreateSupplierRequest) => {
      if (isEdit) {
        await api.put(`/api/suppliers/${supplier!.id}`, data);
      } else {
        await api.post("/api/suppliers", data);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["suppliers"] });
      toast.success(isEdit ? "تم تحديث المورد" : "تم إضافة المورد");
      onClose();
    },
    onError: () => toast.error(isEdit ? "فشل التحديث" : "فشل الإضافة"),
  });

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    mutation.mutate({
      ...form,
      contactPerson: form.contactPerson || undefined,
      phone: form.phone || undefined,
      email: form.email || undefined,
      address: form.address || undefined,
      notes: form.notes || undefined,
    });
  };

  const set = <K extends keyof CreateSupplierRequest>(k: K, v: CreateSupplierRequest[K]) =>
    setForm((f) => ({ ...f, [k]: v }));

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-xl w-full max-w-lg mx-4 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between p-6 border-b sticky top-0 bg-white z-10">
          <h2 className="text-lg font-bold text-gray-900">
            {isEdit ? "تعديل المورد" : "إضافة مورد جديد"}
          </h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            ×
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">اسم المورد *</label>
            <input
              required
              value={form.name}
              onChange={(e) => set("name", e.target.value)}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
            />
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">جهة الاتصال</label>
              <input
                value={form.contactPerson ?? ""}
                onChange={(e) => set("contactPerson", e.target.value)}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
              />
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">الهاتف</label>
              <input
                value={form.phone ?? ""}
                onChange={(e) => set("phone", e.target.value)}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
              />
            </div>
          </div>

          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">البريد الإلكتروني</label>
            <input
              type="email"
              value={form.email ?? ""}
              onChange={(e) => set("email", e.target.value)}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
            />
          </div>

          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">العنوان</label>
            <input
              value={form.address ?? ""}
              onChange={(e) => set("address", e.target.value)}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
            />
          </div>

          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">ملاحظات</label>
            <textarea
              value={form.notes ?? ""}
              onChange={(e) => set("notes", e.target.value)}
              rows={2}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
            />
          </div>

          <div className="flex gap-3 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 border border-gray-200 text-gray-600 py-2.5 rounded-lg text-sm font-medium hover:bg-gray-50 transition-colors"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={mutation.isPending}
              className="flex-1 bg-cyan-700 text-white py-2.5 rounded-lg text-sm font-medium hover:bg-cyan-800 transition-colors disabled:opacity-50"
            >
              {mutation.isPending ? "جارٍ الحفظ..." : isEdit ? "حفظ التعديلات" : "إضافة"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

/* ─── Main Suppliers Page ──────────────────────────────────────────────────── */
export default function SuppliersPage() {
  const [search, setSearch] = useState("");
  const [showForm, setShowForm] = useState(false);
  const [editSupplier, setEditSupplier] = useState<Supplier | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Supplier | null>(null);

  const queryClient = useQueryClient();

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ["suppliers"],
    queryFn: async () => {
      const res = await api.get<PaginatedResponse<Supplier>>("/api/suppliers?pageSize=100");
      return res.data.data;
    },
  });

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => api.delete(`/api/suppliers/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["suppliers"] });
      toast.success("تم حذف المورد");
      setDeleteTarget(null);
    },
    onError: () => toast.error("فشل حذف المورد"),
  });

  const suppliers = (data ?? []).filter(
    (s) =>
      search.trim() === "" ||
      s.name.toLowerCase().includes(search.toLowerCase()) ||
      (s.phone ?? "").includes(search) ||
      (s.contactPerson ?? "").toLowerCase().includes(search.toLowerCase())
  );

  return (
    <ErrorBoundary>
      <div className="space-y-6">
        {/* Navigation Tabs */}
        <div className="flex gap-1 bg-gray-100 p-1 rounded-lg w-fit">
          <Link
            href="/inventory"
            className="px-4 py-2 text-sm font-medium rounded-md text-gray-500 hover:text-gray-700 transition-colors"
          >
            المخزون
          </Link>
          <span className="px-4 py-2 text-sm font-medium rounded-md bg-white shadow-sm text-gray-900">
            الموردين
          </span>
          <Link
            href="/inventory/purchases"
            className="px-4 py-2 text-sm font-medium rounded-md text-gray-500 hover:text-gray-700 transition-colors"
          >
            أوامر الشراء
          </Link>
        </div>

        {/* Header */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-clinic-blue-50 flex items-center justify-center">
              <Truck className="w-5 h-5 text-clinic-blue" />
            </div>
            <div>
              <h1 className="text-2xl font-bold text-gray-900">الموردين</h1>
              <p className="text-sm text-gray-500">
                إدارة الموردين ومعلومات الاتصال
              </p>
            </div>
          </div>
          <button
            onClick={() => {
              setEditSupplier(null);
              setShowForm(true);
            }}
            className="flex items-center gap-2 bg-cyan-700 text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-cyan-800 transition-colors"
          >
            <Plus className="w-4 h-4" />
            إضافة مورد
          </button>
        </div>

        {/* Search */}
        <div className="flex flex-wrap gap-3 items-center">
          <div className="relative flex-1 min-w-52">
            <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="بحث بالاسم أو الهاتف أو جهة الاتصال..."
              className="w-full border border-gray-200 rounded-lg pr-9 pl-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
            />
          </div>
        </div>

        {/* Error State */}
        {error && (
          <div className="bg-red-50 border border-red-200 rounded-xl p-6 text-center">
            <p className="text-red-700 font-medium">فشل تحميل الموردين</p>
            <button
              onClick={() => refetch()}
              className="mt-2 text-sm text-red-600 hover:text-red-800 underline"
            >
              إعادة المحاولة
            </button>
          </div>
        )}

        {/* Table */}
        {!error && (
          <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">
            {isLoading ? (
              <div className="p-6">
                <TableSkeleton rows={6} cols={6} />
              </div>
            ) : suppliers.length === 0 ? (
              <div className="flex flex-col items-center justify-center py-16 text-gray-400">
                <Truck className="w-10 h-10 mb-3" />
                <p className="font-medium">لا يوجد موردين</p>
                <p className="text-sm mt-1">أضف مورد جديد للبدء</p>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead className="bg-gray-50 border-b border-gray-100">
                    <tr>
                      {[
                        "الاسم",
                        "جهة الاتصال",
                        "الهاتف",
                        "البريد",
                        "أوامر الشراء",
                        "إجراءات",
                      ].map((h) => (
                        <th
                          key={h}
                          className="text-right px-4 py-3 font-medium text-gray-500 text-xs"
                        >
                          {h}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-50">
                    {suppliers.map((supplier) => (
                      <tr
                        key={supplier.id}
                        className={cn(
                          "hover:bg-gray-50 transition-colors",
                          !supplier.isActive && "opacity-50"
                        )}
                      >
                        <td className="px-4 py-3">
                          <div className="flex items-center gap-2">
                            <div className="w-8 h-8 rounded-lg bg-clinic-blue-50 flex items-center justify-center flex-shrink-0">
                              <Truck className="w-4 h-4 text-clinic-blue" />
                            </div>
                            <span className="font-medium text-gray-900">
                              {supplier.name}
                            </span>
                            {!supplier.isActive && (
                              <span className="text-[10px] bg-gray-100 text-gray-500 px-1.5 py-0.5 rounded-full">
                                غير نشط
                              </span>
                            )}
                          </div>
                        </td>
                        <td className="px-4 py-3 text-gray-600">
                          {supplier.contactPerson || "—"}
                        </td>
                        <td className="px-4 py-3">
                          {supplier.phone ? (
                            <a
                              href={`tel:${supplier.phone}`}
                              className="flex items-center gap-1 text-clinic-blue hover:underline"
                            >
                              <Phone className="w-3 h-3" />
                              {supplier.phone}
                            </a>
                          ) : (
                            "—"
                          )}
                        </td>
                        <td className="px-4 py-3">
                          {supplier.email ? (
                            <a
                              href={`mailto:${supplier.email}`}
                              className="flex items-center gap-1 text-clinic-blue hover:underline"
                            >
                              <Mail className="w-3 h-3" />
                              {supplier.email}
                            </a>
                          ) : (
                            "—"
                          )}
                        </td>
                        <td className="px-4 py-3 text-gray-700 font-medium">
                          {supplier.purchaseOrderCount ?? 0}
                        </td>
                        <td className="px-4 py-3">
                          <div className="flex items-center gap-2 justify-end">
                            <button
                              onClick={() => {
                                setEditSupplier(supplier);
                                setShowForm(true);
                              }}
                              className="text-xs text-gray-500 hover:text-gray-700 font-medium flex items-center gap-0.5"
                            >
                              <Edit2 className="w-3 h-3" />
                              تعديل
                            </button>
                            <button
                              onClick={() => setDeleteTarget(supplier)}
                              className="text-xs text-red-500 hover:text-red-700 font-medium flex items-center gap-0.5"
                            >
                              <Trash2 className="w-3 h-3" />
                              حذف
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}

        {/* Summary */}
        {!error && !isLoading && suppliers.length > 0 && (
          <div className="text-sm text-gray-500 text-center">
            إجمالي الموردين: {suppliers.length} مورد
          </div>
        )}
      </div>

      {/* Create / Edit Modal */}
      {showForm && (
        <SupplierFormModal
          supplier={editSupplier}
          onClose={() => {
            setShowForm(false);
            setEditSupplier(null);
          }}
        />
      )}

      {/* Delete Confirmation */}
      <ConfirmDialog
        open={!!deleteTarget}
        title="حذف المورد"
        message={`هل أنت متأكد من حذف "${deleteTarget?.name}"؟ لا يمكن التراجع عن هذا الإجراء.`}
        confirmLabel="حذف"
        variant="danger"
        onConfirm={() => deleteTarget && deleteMutation.mutate(deleteTarget.id)}
        onCancel={() => setDeleteTarget(null)}
      />
    </ErrorBoundary>
  );
}
