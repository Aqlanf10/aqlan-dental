"use client";
import { useEffect, useState } from "react";
import { User, Phone, MessageCircle, MapPin, Calendar, Shield, Save, CheckCircle, KeyRound } from "lucide-react";
import portalApi from "@/lib/portalApi";
import { usePatientAuthStore } from "@/stores/patientAuthStore";
import type { PatientPortalProfile } from "@/types/patientPortal";
import { cn } from "@/lib/utils";
import Link from "next/link";

export default function PortalProfilePage() {
  const { profile: storeProfile } = usePatientAuthStore();
  const [profile, setProfile] = useState<PatientPortalProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  // Editable fields
  const [editPhone, setEditPhone] = useState("");
  const [editWhatsapp, setEditWhatsapp] = useState("");
  const [editAddress, setEditAddress] = useState("");
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editSuccess, setEditSuccess] = useState("");
  const [editError, setEditError] = useState("");

  useEffect(() => {
    portalApi.get<PatientPortalProfile>("/api/portal/profile")
      .then((r) => {
        setProfile(r.data);
        setEditPhone(r.data.phone || "");
        setEditWhatsapp(r.data.whatsapp || "");
        setEditAddress(r.data.address || "");
      })
      .catch((err) => setLoadError(err?.response?.data?.message || "حدث خطأ في تحميل البيانات"))
      .finally(() => setLoading(false));
  }, []);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setEditError("");
    setEditSuccess("");
    try {
      const { data } = await portalApi.put<PatientPortalProfile>("/api/portal/profile", {
        phone: editPhone || undefined,
        whatsapp: editWhatsapp || undefined,
        address: editAddress || undefined,
      });
      setProfile(data);
      setEditing(false);
      setEditSuccess("تم تحديث البيانات بنجاح");
      setTimeout(() => setEditSuccess(""), 3000);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setEditError(msg || "حدث خطأ في تحديث البيانات");
    } finally {
      setSaving(false);
    }
  };

  const GENDER_LABELS: Record<string, string> = { Male: "ذكر", Female: "أنثى" };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="w-10 h-10 border-4 border-clinic-blue border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  return (
    <div className="pb-24">
      {/* Header */}
      <div className="clinic-gradient px-5 pt-10 pb-6 text-white">
        <div className="flex items-center gap-4">
          <div className="w-16 h-16 bg-white/15 rounded-2xl flex items-center justify-center backdrop-blur-sm border border-white/10">
            <User className="w-8 h-8 text-white/80" />
          </div>
          <div>
            <h1 className="text-lg font-extrabold">{profile?.fullName || storeProfile?.fullName || "المريض"}</h1>
            <p className="text-white/70 text-xs">رقم المريض: {profile?.patientNumber || "—"}</p>
          </div>
        </div>
      </div>

      <div className="px-4 -mt-4 space-y-4">
        {/* Load Error */}
        {loadError && (
          <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-2xl p-4 text-center">
            {loadError}
          </div>
        )}
        {/* Success/Error Messages */}
        {editSuccess && (
          <div className="text-sm text-green-700 bg-green-50 border border-green-200 rounded-xl p-3 flex items-center gap-2">
            <CheckCircle className="w-4 h-4" /> {editSuccess}
          </div>
        )}
        {editError && (
          <div className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-xl p-3">{editError}</div>
        )}

        {/* Personal Info */}
        <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
          <h3 className="font-bold text-gray-900 mb-3">البيانات الشخصية</h3>
          <div className="space-y-3 text-sm">
            <div className="flex justify-between items-center">
              <span className="text-gray-500 flex items-center gap-2">
                <User className="w-4 h-4 text-gray-400" /> الاسم
              </span>
              <span className="font-medium text-gray-900">{profile?.fullName || "—"}</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-gray-500 flex items-center gap-2">
                <Calendar className="w-4 h-4 text-gray-400" /> تاريخ الميلاد
              </span>
              <span className="text-gray-900">{profile?.dateOfBirth || "—"}</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-gray-500">العمر</span>
              <span className="text-gray-900">{profile?.age ? `${profile.age} سنة` : "—"}</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-gray-500">الجنس</span>
              <span className="text-gray-900">{profile?.gender ? GENDER_LABELS[profile.gender] || profile.gender : "—"}</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-gray-500">رقم المريض</span>
              <span className="font-mono text-gray-900">{profile?.patientNumber || "—"}</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-gray-500">الطبيب المعالج</span>
              <span className="text-gray-900">{profile?.primaryDoctorName || "—"}</span>
            </div>
          </div>
        </div>

        {/* Contact Info (Editable) */}
        <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
          <div className="flex items-center justify-between mb-3">
            <h3 className="font-bold text-gray-900">بيانات الاتصال</h3>
            {!editing ? (
              <button
                onClick={() => setEditing(true)}
                className="text-xs text-clinic-blue hover:underline flex items-center gap-1"
              >
                <Save className="w-3 h-3" /> تعديل
              </button>
            ) : null}
          </div>

          {editing ? (
            <form onSubmit={handleSave} className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1 flex items-center gap-1.5">
                  <Phone className="w-3.5 h-3.5" /> رقم الهاتف
                </label>
                <input
                  type="tel"
                  value={editPhone}
                  onChange={(e) => setEditPhone(e.target.value)}
                  placeholder="770123456"
                  dir="ltr"
                  className="w-full px-3 py-2 text-sm rounded-lg border border-gray-300 focus:ring-2 focus:ring-clinic-blue focus:outline-none text-left"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1 flex items-center gap-1.5">
                  <MessageCircle className="w-3.5 h-3.5" /> واتساب
                </label>
                <input
                  type="tel"
                  value={editWhatsapp}
                  onChange={(e) => setEditWhatsapp(e.target.value)}
                  placeholder="770123456"
                  dir="ltr"
                  className="w-full px-3 py-2 text-sm rounded-lg border border-gray-300 focus:ring-2 focus:ring-clinic-blue focus:outline-none text-left"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1 flex items-center gap-1.5">
                  <MapPin className="w-3.5 h-3.5" /> العنوان
                </label>
                <input
                  type="text"
                  value={editAddress}
                  onChange={(e) => setEditAddress(e.target.value)}
                  placeholder="العنوان..."
                  className="w-full px-3 py-2 text-sm rounded-lg border border-gray-300 focus:ring-2 focus:ring-clinic-blue focus:outline-none"
                />
              </div>
              {editError && (
                <div className="text-xs text-red-600 bg-red-50 border border-red-200 rounded-lg p-2">{editError}</div>
              )}
              <div className="flex gap-2">
                <button
                  type="submit"
                  disabled={saving}
                  className={cn(
                    "flex-1 py-2 text-sm font-semibold rounded-lg transition",
                    "bg-clinic-blue text-white hover:bg-clinic-navy-700 disabled:opacity-60"
                  )}
                >
                  {saving ? "جارٍ الحفظ..." : "حفظ"}
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setEditing(false);
                    setEditError("");
                    setEditPhone(profile?.phone || "");
                    setEditWhatsapp(profile?.whatsapp || "");
                    setEditAddress(profile?.address || "");
                  }}
                  className="flex-1 py-2 text-sm font-medium rounded-lg bg-gray-100 text-gray-600 hover:bg-gray-200 transition"
                >
                  إلغاء
                </button>
              </div>
            </form>
          ) : (
            <div className="space-y-3 text-sm">
              <div className="flex justify-between items-center">
                <span className="text-gray-500 flex items-center gap-2">
                  <Phone className="w-4 h-4 text-gray-400" /> الهاتف
                </span>
                <span className="font-mono text-gray-900" dir="ltr">{profile?.phone || "—"}</span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-gray-500 flex items-center gap-2">
                  <MessageCircle className="w-4 h-4 text-gray-400" /> واتساب
                </span>
                <span className="font-mono text-gray-900" dir="ltr">{profile?.whatsapp || "—"}</span>
              </div>
              <div className="flex justify-between items-start">
                <span className="text-gray-500 flex items-center gap-2">
                  <MapPin className="w-4 h-4 text-gray-400" /> العنوان
                </span>
                <span className="text-gray-900 text-left max-w-[60%]">{profile?.address || "—"}</span>
              </div>
            </div>
          )}
        </div>

        {/* Account Status */}
        <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
          <h3 className="font-bold text-gray-900 mb-3 flex items-center gap-2">
            <Shield className="w-4 h-4 text-clinic-blue" />
            حالة الحساب
          </h3>
          <div className="space-y-2 text-sm">
            <div className="flex justify-between items-center">
              <span className="text-gray-500">حالة الحساب</span>
              <span className={cn(
                "text-xs px-2 py-0.5 rounded-full font-medium",
                profile?.accountStatus === "active" ? "bg-green-100 text-green-700" : "bg-amber-100 text-amber-700"
              )}>
                {profile?.accountStatus === "active" ? "مفعل" : "قيد التفعيل"}
              </span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-gray-500">آخر تسجيل دخول</span>
              <span className="text-gray-900">{profile?.lastLogin || "—"}</span>
            </div>
          </div>
          <div className="mt-3 pt-3 border-t border-gray-100">
            <Link
              href="/portal/change-password"
              className="flex items-center gap-2 text-sm text-clinic-blue hover:text-clinic-navy-700 font-medium transition"
            >
              <KeyRound className="w-4 h-4" />
              تغيير كلمة المرور
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
