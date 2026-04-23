"use client";
import { useEffect, useState } from "react";
import { Settings, Users, Shield, Save } from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";

type Tab = "clinic" | "users" | "roles";

const TABS: { key: Tab; label: string; icon: typeof Settings }[] = [
  { key: "clinic", label: "بيانات المركز", icon: Settings },
  { key: "users",  label: "المستخدمون",   icon: Users },
  { key: "roles",  label: "الأدوار",      icon: Shield },
];

interface ClinicSettings {
  "clinic.name"?: string;
  "clinic.location"?: string;
  "clinic.phones"?: string;
  "clinic.currency"?: string;
  [key: string]: string | undefined;
}

interface UserRow {
  id: string;
  username: string;
  email?: string;
  role: string;
  isActive: boolean;
  lastLoginAt?: string;
  doctorName?: string;
}

const ROLE_LABELS: Record<string, string> = {
  Admin: "مدير النظام",
  Orthodontist: "أخصائي تقويم",
  GeneralDentist: "طبيب أسنان",
  OralSurgeon: "جراح وجه وفكين",
  Reception: "استقبال",
  Accountant: "محاسب",
  Assistant: "مساعد",
  BranchManager: "مدير فرع",
};

const inputCls = "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal";

// ─── Clinic Info Tab ──────────────────────────────────────────────────────────
function ClinicTab() {
  const [settings, setSettings] = useState<ClinicSettings>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    api.get<ClinicSettings>("/api/settings")
      .then((r) => setSettings(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const handleSave = async () => {
    setSaving(true);
    setSaved(false);
    try {
      const clinicFields = ["clinic.name", "clinic.location", "clinic.phones"] as const;
      await Promise.all(
        clinicFields.map((key) =>
          api.put(`/api/settings/${encodeURIComponent(key)}`, {
            value: settings[key] ?? "",
            category: "clinic",
          })
        )
      );
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch {
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return <div className="animate-pulse space-y-3">{Array.from({ length: 4 }).map((_, i) => <div key={i} className="h-10 bg-gray-100 rounded-lg" />)}</div>;
  }

  return (
    <div className="space-y-4">
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1.5">اسم المركز</label>
        <input
          value={settings["clinic.name"] ?? ""}
          onChange={(e) => setSettings({ ...settings, "clinic.name": e.target.value })}
          className={inputCls}
          placeholder="مركز د. عقلان الكامل لطب وتقويم الأسنان"
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1.5">العنوان</label>
        <input
          value={settings["clinic.location"] ?? ""}
          onChange={(e) => setSettings({ ...settings, "clinic.location": e.target.value })}
          className={inputCls}
          placeholder="تعز، اليمن"
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1.5">أرقام الهاتف</label>
        <input
          value={settings["clinic.phones"] ?? ""}
          onChange={(e) => setSettings({ ...settings, "clinic.phones": e.target.value })}
          className={inputCls}
          placeholder="04-253028، 770XXXXXX"
          dir="ltr"
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1.5">بادئة رقم المريض</label>
        <input
          value={settings["patient.number_prefix"] ?? "GM"}
          onChange={(e) => setSettings({ ...settings, "patient.number_prefix": e.target.value })}
          className={inputCls}
          placeholder="GM"
          dir="ltr"
        />
      </div>

      <div className="flex items-center gap-3 pt-2">
        <button
          onClick={handleSave}
          disabled={saving}
          className="flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
        >
          <Save className="w-4 h-4" />
          {saving ? "جارٍ الحفظ..." : "حفظ الإعدادات"}
        </button>
        {saved && <span className="text-sm text-green-600 font-medium">✓ تم الحفظ</span>}
      </div>
    </div>
  );
}

// ─── Users Tab ────────────────────────────────────────────────────────────────
function UsersTab() {
  const [users, setUsers] = useState<UserRow[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<UserRow[]>("/api/users")
      .then((r) => setUsers(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return <div className="animate-pulse space-y-2">{Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-12 bg-gray-100 rounded-lg" />)}</div>;
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-gray-200">
      <table className="w-full text-sm">
        <thead className="bg-gray-50 border-b border-gray-200">
          <tr>
            {["اسم المستخدم", "الاسم الكامل", "الدور", "آخر دخول", "الحالة"].map((h) => (
              <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500">{h}</th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {users.map((u) => (
            <tr key={u.id} className="hover:bg-gray-50 transition">
              <td className="px-4 py-3 font-mono font-medium text-gray-900">{u.username}</td>
              <td className="px-4 py-3 text-gray-700">{u.doctorName ?? "—"}</td>
              <td className="px-4 py-3">
                <span className="text-xs bg-blue-50 text-blue-700 px-2 py-0.5 rounded-full font-medium">
                  {ROLE_LABELS[u.role] ?? u.role}
                </span>
              </td>
              <td className="px-4 py-3 text-gray-500 text-xs">
                {u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleDateString("ar-YE") : "—"}
              </td>
              <td className="px-4 py-3">
                <span className={cn(
                  "text-xs px-2 py-0.5 rounded-full font-medium",
                  u.isActive ? "bg-green-50 text-green-700" : "bg-gray-100 text-gray-500"
                )}>
                  {u.isActive ? "نشط" : "معطّل"}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ─── Roles Tab ────────────────────────────────────────────────────────────────
function RolesTab() {
  const ROLES = ["Admin", "Orthodontist", "GeneralDentist", "OralSurgeon", "Reception", "Accountant"];
  const PERMISSIONS = [
    { key: "patients.view",        label: "عرض المرضى" },
    { key: "patients.create",      label: "إضافة مريض" },
    { key: "patients.edit",        label: "تعديل مريض" },
    { key: "appointments.view",    label: "عرض المواعيد" },
    { key: "appointments.create",  label: "إضافة موعد" },
    { key: "ortho.view",           label: "عرض التقويم" },
    { key: "ortho.create",         label: "إنشاء حالة تقويمية" },
    { key: "finance.view",         label: "عرض المالية" },
    { key: "finance.create",       label: "تسجيل دفعة" },
    { key: "reports.view",         label: "عرض التقارير" },
    { key: "settings.view",        label: "عرض الإعدادات" },
    { key: "settings.edit",        label: "تعديل الإعدادات" },
  ];

  // Admin has all, define defaults for others
  const ROLE_DEFAULTS: Record<string, string[]> = {
    Admin: PERMISSIONS.map(p => p.key),
    Orthodontist: ["patients.view", "patients.edit", "appointments.view", "appointments.create", "ortho.view", "ortho.create"],
    GeneralDentist: ["patients.view", "patients.edit", "appointments.view", "appointments.create"],
    OralSurgeon: ["patients.view", "patients.edit", "appointments.view", "appointments.create"],
    Reception: ["patients.view", "patients.create", "appointments.view", "appointments.create"],
    Accountant: ["patients.view", "finance.view", "finance.create", "reports.view"],
  };

  return (
    <div className="overflow-x-auto rounded-lg border border-gray-200">
      <table className="w-full text-xs">
        <thead className="bg-gray-50 border-b border-gray-200">
          <tr>
            <th className="text-start px-4 py-3 font-semibold text-gray-700 min-w-[160px]">الصلاحية</th>
            {ROLES.map((r) => (
              <th key={r} className="px-3 py-3 font-semibold text-gray-600 text-center whitespace-nowrap">
                {ROLE_LABELS[r]}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {PERMISSIONS.map(({ key, label }) => (
            <tr key={key} className="hover:bg-gray-50 transition">
              <td className="px-4 py-2.5 text-gray-700 font-medium">{label}</td>
              {ROLES.map((role) => {
                const has = (ROLE_DEFAULTS[role] ?? []).includes(key);
                return (
                  <td key={role} className="px-3 py-2.5 text-center">
                    <span className={cn(
                      "inline-block w-5 h-5 rounded text-center leading-5 font-bold text-xs",
                      has ? "text-green-600" : "text-gray-300"
                    )}>
                      {has ? "✓" : "✕"}
                    </span>
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
      <p className="text-xs text-gray-400 p-3 border-t border-gray-100">
        * يتم إدارة الصلاحيات من قاعدة البيانات (role_permissions) — هذا العرض للمرجع
      </p>
    </div>
  );
}

// ─── Main Page ────────────────────────────────────────────────────────────────
export default function SettingsPage() {
  const [activeTab, setActiveTab] = useState<Tab>("clinic");

  return (
    <div className="space-y-5 max-w-5xl">
      <div>
        <h1 className="text-2xl font-extrabold text-gray-900">الإعدادات</h1>
        <p className="text-sm text-gray-500 mt-0.5">إدارة إعدادات المركز والمستخدمين</p>
      </div>

      <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
        <div className="flex border-b border-gray-100 overflow-x-auto">
          {TABS.map(({ key, label, icon: Icon }) => (
            <button
              key={key}
              onClick={() => setActiveTab(key)}
              className={cn(
                "flex items-center gap-2 px-5 py-3.5 text-sm font-medium whitespace-nowrap border-b-2 transition",
                activeTab === key
                  ? "border-clinic-teal text-clinic-teal"
                  : "border-transparent text-gray-500 hover:text-gray-900"
              )}
            >
              <Icon className="w-4 h-4" />
              {label}
            </button>
          ))}
        </div>

        <div className="p-5">
          {activeTab === "clinic" && <ClinicTab />}
          {activeTab === "users"  && <UsersTab />}
          {activeTab === "roles"  && <RolesTab />}
        </div>
      </div>
    </div>
  );
}
