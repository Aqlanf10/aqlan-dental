"use client";
// Sprint 11A — extracted from the former monolithic settings/page.tsx.
// Behavior unchanged: same UI, same API calls, same state management.

import { useEffect, useState } from "react";
import {
  AlertTriangle, CheckCircle2, Loader2, Save,
} from "lucide-react";
import { cn } from "@/lib/utils";
import api from "@/lib/api";
import { extractErrorMessage } from "@/lib/errors";
import {
  usePermissions,
  useUpdateRolePermissions,
  type PermissionDto,
} from "@/hooks/useUsers";
import { ALL_ROLES, ROLE_LABELS } from "./_shared";

// ─── Roles Tab (Complete Rewrite) ────────────────────────────────────────────
export function RolesTab() {
  const { data: permissions, isLoading: permissionsLoading } = usePermissions();
  const [selectedRole, setSelectedRole] = useState<string>("Admin");
  const [rolePermissionsMap, setRolePermissionsMap] = useState<Record<string, Set<string>>>({});
  const [loadingRoles, setLoadingRoles] = useState<Set<string>>(new Set());
  const [saving, setSaving] = useState(false);
  const [toast, setToast] = useState<{ message: string; type: "success" | "error" } | null>(null);
  const [showAdminWarning, setShowAdminWarning] = useState(false);

  const updateRolePermissions = useUpdateRolePermissions();

  // Load permissions for all roles
  useEffect(() => {
    const loadRolePermissions = async () => {
      const roles = ALL_ROLES;
      for (const role of roles) {
        setLoadingRoles((prev) => new Set(prev).add(role));
        try {
          const { data } = await api.get<{ role: string; permissions: string[] }>(`/api/roles/${role}/permissions`);
          setRolePermissionsMap((prev) => ({ ...prev, [role]: new Set(data.permissions) }));
        } catch {
          setRolePermissionsMap((prev) => ({ ...prev, [role]: new Set() }));
        } finally {
          setLoadingRoles((prev) => {
            const next = new Set(prev);
            next.delete(role);
            return next;
          });
        }
      }
    };
    loadRolePermissions();
  }, []);

  const togglePermission = (role: string, permissionKey: string) => {
    setRolePermissionsMap((prev) => {
      const current = new Set(prev[role] ?? []);
      if (current.has(permissionKey)) {
        current.delete(permissionKey);
      } else {
        current.add(permissionKey);
      }
      return { ...prev, [role]: current };
    });
  };

  const handleSave = async () => {
    if (selectedRole === "Admin") {
      setShowAdminWarning(true);
      return;
    }
    await savePermissions(selectedRole);
  };

  const savePermissions = async (role: string) => {
    const permissions = Array.from(rolePermissionsMap[role] ?? []);
    setSaving(true);
    updateRolePermissions.mutate(
      { role, permissions },
      {
        onSuccess: () => {
          setToast({ message: "تم حفظ الصلاحيات بنجاح", type: "success" });
          setTimeout(() => setToast(null), 3000);
        },
        onError: (err) => {
          setToast({ message: extractErrorMessage(err, "حدث خطأ أثناء الحفظ"), type: "error" });
          setTimeout(() => setToast(null), 3000);
        },
        onSettled: () => setSaving(false),
      }
    );
  };

  if (permissionsLoading || loadingRoles.size > 0) {
    return <div className="animate-pulse space-y-3">{Array.from({ length: 4 }).map((_, i) => <div key={i} className="h-10 bg-gray-100 rounded-lg" />)}</div>;
  }

  // Group permissions by resource
  const permList = permissions ?? [];
  const groupedPermissions = permList.reduce<Record<string, PermissionDto[]>>((acc, p) => {
    if (!acc[p.resource]) acc[p.resource] = [];
    acc[p.resource].push(p);
    return acc;
  }, {});

  const RESOURCE_LABELS: Record<string, string> = {
    patients: "المرضى",
    ortho: "التقويم",
    general_dentistry: "طب الأسنان العام",
    surgery: "الجراحة",
    appointments: "المواعيد",
    finance: "المدفوعات",
    reports: "التقارير",
    users: "المستخدمون",
    settings: "الإعدادات",
    ai: "الذكاء الاصطناعي",
    user_management: "إدارة المستخدمين",
    password_reset_requests: "طلبات إعادة تعيين كلمة المرور",
    impersonation: "الانتحال",
    daily_operations: "التشغيل اليومي",
    booking_requests: "طلبات الحجز",
    clinic_queue: "الانتظار",
    clinic_display: "شاشة النداء",
    patient_journey: "رحلة المرضى",
    visits: "الزيارات",
    checkout: "جاهز للدفع",
    invoices: "الفواتير",
    rooms: "الغرف / الكراسي",
    // Legacy PascalCase keys (fallback)
    Patients: "المرضى",
    Appointments: "المواعيد",
    Ortho: "التقويم",
    Finance: "المالية",
    Reports: "التقارير",
    Settings: "الإعدادات",
    Users: "المستخدمون",
    Doctors: "الأطباء",
  };

  const ACTION_LABELS: Record<string, string> = {
    view: "عرض",
    create: "إنشاء",
    edit: "تعديل",
    delete: "حذف",
    export: "تصدير",
    approve: "اعتماد",
    // Legacy PascalCase keys (fallback)
    View: "عرض",
    Create: "إنشاء",
    Edit: "تعديل",
    Delete: "حذف",
    Export: "تصدير",
    Approve: "موافقة",
  };

  // "invoices" is intentionally absent: it is a seeded resource that no API
  // guard and no screen consults. The resource that actually governs invoices
  // is "finance.invoices", listed under the finance group below.
  const PERMISSION_GROUPS = [
    {
      title: "التشغيل اليومي",
      resources: ["daily_operations", "booking_requests", "clinic_queue", "clinic_display", "patient_journey", "visits", "checkout", "rooms"],
    },
    {
      title: "العيادة",
      resources: ["patients", "appointments", "finance", "reports"],
    },
    {
      title: "المالية",
      resources: [
        "finance.dashboard", "finance.invoices", "finance.payments", "finance.receipts",
        "finance.expenses", "finance.contracts", "finance.commissions", "finance.treasuries",
        "finance.cashier_session", "finance.reports", "finance.patient_balance",
        "finance.account_statement",
      ],
    },
    {
      title: "المعمل",
      resources: ["lab_orders", "labs", "lab_work_types", "lab_work_prices", "lab_payables", "lab_reports"],
    },
    {
      title: "التخصصات",
      resources: ["ortho", "general_dentistry", "surgery"],
    },
    {
      title: "النظام",
      resources: ["users", "user_management", "settings", "ai", "password_reset_requests", "impersonation"],
    },
  ];

  return (
    <div className="space-y-4">
      {/* Toast */}
      {toast && (
        <div
          className={cn(
            "fixed top-4 left-1/2 -translate-x-1/2 z-[100] px-4 py-2.5 rounded-lg shadow-lg text-sm font-medium flex items-center gap-2 animate-in fade-in",
            toast.type === "success" ? "bg-green-600 text-white" : "bg-red-600 text-white"
          )}
        >
          {toast.type === "success" ? <CheckCircle2 className="w-4 h-4" /> : <AlertTriangle className="w-4 h-4" />}
          {toast.message}
        </div>
      )}

      {/* Role selector */}
      <div className="flex items-center gap-3 flex-wrap">
        {ALL_ROLES.map((role) => (
          <button
            key={role}
            onClick={() => setSelectedRole(role)}
            className={cn(
              "px-3 py-1.5 text-sm font-medium rounded-lg border transition",
              selectedRole === role
                ? "bg-clinic-blue text-white border-clinic-blue"
                : "bg-white text-gray-700 border-gray-300 hover:border-clinic-blue"
            )}
          >
            {ROLE_LABELS[role] ?? role}
          </button>
        ))}
      </div>

      {/* Admin warning */}
      {selectedRole === "Admin" && (
        <div className="text-xs px-3 py-2 rounded-lg bg-amber-50 text-amber-800 border border-amber-200 flex items-start gap-2">
          <AlertTriangle className="w-4 h-4 mt-0.5 flex-shrink-0" />
          <span>مدير النظام يمتلك جميع الصلاحيات ولا يمكن تعديلها.</span>
        </div>
      )}

      {/* Permission Matrix — grouped by permission groups */}
      <div className="space-y-4">
        {PERMISSION_GROUPS.map((group) => {
          // Collect permissions for this group's resources
          const groupResources = group.resources.filter((r) => groupedPermissions[r]);
          if (groupResources.length === 0) return null;

          return (
            <div key={group.title} className="overflow-x-auto rounded-lg border border-gray-200">
              <div className="bg-gray-50 px-4 py-2.5 border-b border-gray-200">
                <h3 className="text-sm font-bold text-gray-800">{group.title}</h3>
              </div>
              <table className="w-full text-xs">
                <thead className="bg-gray-50/50 border-b border-gray-200">
                  <tr>
                    <th className="text-start px-4 py-2.5 font-semibold text-gray-700 min-w-[120px]">المورد</th>
                    <th className="text-start px-4 py-2.5 font-semibold text-gray-700 min-w-[80px]">الإجراء</th>
                    <th className="text-center px-4 py-2.5 font-semibold text-gray-600">الصلاحية</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {groupResources.map((resource) =>
                    (groupedPermissions[resource] ?? []).map((p, idx) => {
                      const isChecked = rolePermissionsMap[selectedRole]?.has(p.key) ?? false;
                      const isAdmin = selectedRole === "Admin";
                      return (
                        <tr key={p.key} className="hover:bg-gray-50 transition">
                          <td className="px-4 py-2.5 text-gray-700 font-medium">
                            {idx === 0 ? (RESOURCE_LABELS[resource] ?? resource) : ""}
                          </td>
                          <td className="px-4 py-2.5 text-gray-600">
                            {ACTION_LABELS[p.action] ?? p.action}
                          </td>
                          <td className="px-4 py-2.5 text-center">
                            <input
                              type="checkbox"
                              checked={isAdmin || isChecked}
                              disabled={isAdmin}
                              onChange={() => togglePermission(selectedRole, p.key)}
                              className="w-4 h-4 rounded border-gray-300 text-clinic-blue focus:ring-clinic-blue cursor-pointer disabled:cursor-not-allowed"
                            />
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          );
        })}

        {/* Show any ungrouped resources */}
        {(() => {
          const groupedResourceKeys = new Set(PERMISSION_GROUPS.flatMap((g) => g.resources));
          const ungroupedResources = Object.keys(groupedPermissions).filter((r) => !groupedResourceKeys.has(r));
          if (ungroupedResources.length === 0) return null;
          return (
            <div className="overflow-x-auto rounded-lg border border-gray-200">
              <div className="bg-gray-50 px-4 py-2.5 border-b border-gray-200">
                <h3 className="text-sm font-bold text-gray-800">أخرى</h3>
              </div>
              <table className="w-full text-xs">
                <thead className="bg-gray-50/50 border-b border-gray-200">
                  <tr>
                    <th className="text-start px-4 py-2.5 font-semibold text-gray-700 min-w-[120px]">المورد</th>
                    <th className="text-start px-4 py-2.5 font-semibold text-gray-700 min-w-[80px]">الإجراء</th>
                    <th className="text-center px-4 py-2.5 font-semibold text-gray-600">الصلاحية</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {ungroupedResources.map((resource) =>
                    (groupedPermissions[resource] ?? []).map((p, idx) => {
                      const isChecked = rolePermissionsMap[selectedRole]?.has(p.key) ?? false;
                      const isAdmin = selectedRole === "Admin";
                      return (
                        <tr key={p.key} className="hover:bg-gray-50 transition">
                          <td className="px-4 py-2.5 text-gray-700 font-medium">
                            {idx === 0 ? (RESOURCE_LABELS[resource] ?? resource) : ""}
                          </td>
                          <td className="px-4 py-2.5 text-gray-600">
                            {ACTION_LABELS[p.action] ?? p.action}
                          </td>
                          <td className="px-4 py-2.5 text-center">
                            <input
                              type="checkbox"
                              checked={isAdmin || isChecked}
                              disabled={isAdmin}
                              onChange={() => togglePermission(selectedRole, p.key)}
                              className="w-4 h-4 rounded border-gray-300 text-clinic-blue focus:ring-clinic-blue cursor-pointer disabled:cursor-not-allowed"
                            />
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          );
        })()}
      </div>

      {/* Save button */}
      {selectedRole !== "Admin" && (
        <div className="flex items-center gap-3 pt-2">
          <button
            onClick={handleSave}
            disabled={saving}
            className="flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition"
          >
            {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
            {saving ? "جارٍ الحفظ..." : "حفظ الصلاحيات"}
          </button>
        </div>
      )}

      {/* Admin warning dialog */}
      {showAdminWarning && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true">
          <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={() => setShowAdminWarning(false)} />
          <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-md p-6 space-y-4">
            <div className="flex items-start gap-4">
              <div className="flex-shrink-0 w-10 h-10 rounded-full flex items-center justify-center bg-amber-100">
                <AlertTriangle className="w-5 h-5 text-amber-600" />
              </div>
              <div>
                <h3 className="text-base font-bold text-gray-900">تحذير: تعديل صلاحيات المدير</h3>
                <p className="text-sm text-gray-600 mt-1">
                  تعديل صلاحيات مدير النظام قد يؤثر على قدرة المدير على إدارة النظام. هل أنت متأكد؟
                </p>
                <div className="flex items-center justify-end gap-3 pt-4">
                  <button
                    onClick={() => setShowAdminWarning(false)}
                    className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition"
                  >
                    إلغاء
                  </button>
                  <button
                    onClick={async () => {
                      setShowAdminWarning(false);
                      await savePermissions("Admin");
                    }}
                    className="px-4 py-2 text-sm font-medium text-white bg-amber-600 rounded-lg hover:bg-amber-700 transition"
                  >
                    متابعة بالحفظ
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
