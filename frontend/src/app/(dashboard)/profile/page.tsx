"use client";

import { useEffect, useState, useCallback } from "react";
import { useAuthStore } from "@/stores/authStore";
import { useChangePassword } from "@/hooks/useSettings";
import api from "@/lib/api";
import type { UserDto } from "@/types/auth";
import { User, Lock, Save, Eye, EyeOff } from "lucide-react";

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

export default function ProfilePage() {
  const { user: authUser } = useAuthStore();
  const [profileUser, setProfileUser] = useState<UserDto | null>(null);
  const [loading, setLoading] = useState(true);

  // Change password form
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showCurrent, setShowCurrent] = useState(false);
  const [showNew, setShowNew] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [passwordError, setPasswordError] = useState("");
  const [passwordSuccess, setPasswordSuccess] = useState("");

  const changePassword = useChangePassword();

  // Fetch fresh user data
  useEffect(() => {
    const loadProfile = async () => {
      setLoading(true);
      try {
        const { data } = await api.get<UserDto>("/api/auth/me");
        setProfileUser(data);
      } catch {
        // fallback to auth store user
        setProfileUser(authUser);
      } finally {
        setLoading(false);
      }
    };
    loadProfile();
  }, [authUser]);

  const handleChangePassword = useCallback(async () => {
    setPasswordError("");
    setPasswordSuccess("");

    if (!currentPassword || !newPassword || !confirmPassword) {
      setPasswordError("جميع الحقول مطلوبة");
      return;
    }
    if (newPassword.length < 6) {
      setPasswordError("كلمة المرور الجديدة يجب أن تكون 6 أحرف على الأقل");
      return;
    }
    if (newPassword !== confirmPassword) {
      setPasswordError("كلمة المرور الجديدة غير متطابقة");
      return;
    }

    try {
      await changePassword.mutateAsync({
        currentPassword,
        newPassword,
      });
      setPasswordSuccess("تم تغيير كلمة المرور بنجاح");
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
    } catch {
      setPasswordError("فشل تغيير كلمة المرور. تحقق من كلمة المرور الحالية.");
    }
  }, [currentPassword, newPassword, confirmPassword, changePassword]);

  const displayUser = profileUser ?? authUser;

  if (loading) {
    return (
      <div className="p-6 flex items-center justify-center min-h-[60vh]">
        <div className="animate-spin w-8 h-8 border-4 border-clinic-teal border-t-transparent rounded-full" />
      </div>
    );
  }

  if (!displayUser) {
    return (
      <div className="p-6 text-center text-gray-500">
        لم يتم العثور على بيانات المستخدم
      </div>
    );
  }

  return (
    <div className="p-4 md:p-6 max-w-3xl mx-auto space-y-6">
      {/* Page Title */}
      <div className="flex items-center gap-3">
        <div className="w-10 h-10 rounded-xl bg-clinic-teal/10 flex items-center justify-center">
          <User className="w-5 h-5 text-clinic-teal" />
        </div>
        <h1 className="text-xl font-bold text-gray-900">حسابي</h1>
      </div>

      {/* Avatar + Name Section */}
      <div className="bg-white rounded-2xl border border-gray-200 p-6">
        <div className="flex items-center gap-4">
          <div
            className="w-16 h-16 rounded-full flex items-center justify-center text-white text-2xl font-bold flex-shrink-0"
            style={{ backgroundColor: displayUser.doctorColor ?? "#0E7490" }}
          >
            {displayUser.doctorInitials ?? displayUser.username.charAt(0).toUpperCase()}
          </div>
          <div>
            <h2 className="text-lg font-bold text-gray-900">
              {displayUser.doctorName ?? displayUser.username}
            </h2>
            <p className="text-sm text-gray-500">
              {ROLE_LABELS[displayUser.role] ?? displayUser.role}
            </p>
          </div>
        </div>
      </div>

      {/* User Info Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        {/* Username */}
        <div className="bg-white rounded-2xl border border-gray-200 p-4">
          <div className="text-xs text-gray-400 mb-1">اسم المستخدم</div>
          <div className="text-sm font-semibold text-gray-900">{displayUser.username}</div>
        </div>

        {/* Role */}
        <div className="bg-white rounded-2xl border border-gray-200 p-4">
          <div className="text-xs text-gray-400 mb-1">الدور</div>
          <div className="text-sm font-semibold text-gray-900">
            {ROLE_LABELS[displayUser.role] ?? displayUser.role}
          </div>
        </div>

        {/* Email */}
        <div className="bg-white rounded-2xl border border-gray-200 p-4">
          <div className="text-xs text-gray-400 mb-1">البريد الإلكتروني</div>
          <div className="text-sm font-semibold text-gray-900">
            {displayUser.email ?? "غير محدد"}
          </div>
        </div>

        {/* Doctor Name */}
        <div className="bg-white rounded-2xl border border-gray-200 p-4">
          <div className="text-xs text-gray-400 mb-1">اسم الطبيب</div>
          <div className="text-sm font-semibold text-gray-900">
            {displayUser.doctorName ?? "غير محدد"}
          </div>
        </div>

        {/* Last Login */}
        <div className="bg-white rounded-2xl border border-gray-200 p-4 sm:col-span-2">
          <div className="text-xs text-gray-400 mb-1">آخر تسجيل دخول</div>
          <div className="text-sm font-semibold text-gray-900">
            {displayUser.lastLogin ?? "غير متوفر"}
          </div>
        </div>
      </div>

      {/* Change Password Section */}
      <div className="bg-white rounded-2xl border border-gray-200 p-6">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-9 h-9 rounded-lg bg-amber-50 flex items-center justify-center">
            <Lock className="w-4 h-4 text-amber-600" />
          </div>
          <h3 className="text-base font-bold text-gray-900">تغيير كلمة المرور</h3>
        </div>

        <div className="space-y-4">
          {/* Current Password */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              كلمة المرور الحالية
            </label>
            <div className="relative">
              <input
                type={showCurrent ? "text" : "password"}
                value={currentPassword}
                onChange={(e) => setCurrentPassword(e.target.value)}
                className="w-full rounded-lg border border-gray-300 px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-clinic-teal/30 focus:border-clinic-teal"
                placeholder="أدخل كلمة المرور الحالية"
                dir="ltr"
              />
              <button
                type="button"
                onClick={() => setShowCurrent(!showCurrent)}
                className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
              >
                {showCurrent ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
            </div>
          </div>

          {/* New Password */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              كلمة المرور الجديدة
            </label>
            <div className="relative">
              <input
                type={showNew ? "text" : "password"}
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                className="w-full rounded-lg border border-gray-300 px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-clinic-teal/30 focus:border-clinic-teal"
                placeholder="أدخل كلمة المرور الجديدة"
                dir="ltr"
              />
              <button
                type="button"
                onClick={() => setShowNew(!showNew)}
                className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
              >
                {showNew ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
            </div>
          </div>

          {/* Confirm Password */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              تأكيد كلمة المرور الجديدة
            </label>
            <div className="relative">
              <input
                type={showConfirm ? "text" : "password"}
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                className="w-full rounded-lg border border-gray-300 px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-clinic-teal/30 focus:border-clinic-teal"
                placeholder="أعد إدخال كلمة المرور الجديدة"
                dir="ltr"
              />
              <button
                type="button"
                onClick={() => setShowConfirm(!showConfirm)}
                className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
              >
                {showConfirm ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
            </div>
          </div>

          {/* Error/Success Messages */}
          {passwordError && (
            <div className="text-sm text-red-600 bg-red-50 rounded-lg p-3">
              {passwordError}
            </div>
          )}
          {passwordSuccess && (
            <div className="text-sm text-green-600 bg-green-50 rounded-lg p-3">
              {passwordSuccess}
            </div>
          )}

          {/* Submit Button */}
          <button
            onClick={handleChangePassword}
            disabled={changePassword.isPending}
            className="flex items-center gap-2 px-5 py-2.5 bg-clinic-teal text-white rounded-lg text-sm font-medium hover:bg-clinic-teal/90 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {changePassword.isPending ? (
              <div className="animate-spin w-4 h-4 border-2 border-white border-t-transparent rounded-full" />
            ) : (
              <Save className="w-4 h-4" />
            )}
            <span>حفظ كلمة المرور</span>
          </button>
        </div>
      </div>
    </div>
  );
}
