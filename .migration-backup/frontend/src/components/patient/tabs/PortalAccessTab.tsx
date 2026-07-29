"use client";

import { useEffect, useState, useCallback } from "react";
import {
  KeyRound, UserCircle, Shield, Clock, Copy, Check, Plus,
  RotateCcw, ExternalLink, AlertTriangle, Loader2,
} from "lucide-react";
import api from "@/lib/api";
import { extractErrorMessage } from "@/lib/errors";
import { cn, formatArabicDate } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { useAuthStore } from "@/stores/authStore";
import type { PatientPortalCredentials, CreatePortalAccountResponse, PatientPasswordResetResponse } from "@/types/patientPortal";

interface PortalAccessTabProps {
  patientId: string;
  patientNumber: string;
}

export function PortalAccessTab({ patientId, patientNumber }: PortalAccessTabProps) {
  const { user } = useAuthStore();
  const [creds, setCreds] = useState<PatientPortalCredentials | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");
  const [creating, setCreating] = useState(false);
  const [resetting, setResetting] = useState(false);
  const [showTempPassword, setShowTempPassword] = useState(false);
  const [tempPassword, setTempPassword] = useState<string | null>(null);
  const [tempUsername, setTempUsername] = useState<string | null>(null);
  const [copiedField, setCopiedField] = useState<string | null>(null);

  const isAdminOrReception = user?.role === "Admin" || user?.role === "Reception";
  const PORTAL_URL = typeof window !== "undefined" ? `${window.location.origin}/portal/login` : "/portal/login";

  const fetchCredentials = useCallback(async () => {
    setLoading(true);
    setLoadError("");
    try {
      const { data } = await api.get<PatientPortalCredentials>(`/api/portal/credentials/${patientId}`);
      setCreds(data);
    } catch (error: unknown) {
      setCreds(null);
      setLoadError(
        extractErrorMessage(error, "تعذر تحميل حالة حساب بوابة المريض — تحقق من الاتصال وحاول مجدداً")
      );
    } finally {
      setLoading(false);
    }
  }, [patientId]);

  useEffect(() => {
    fetchCredentials();
  }, [fetchCredentials]);

  const copyToClipboard = async (text: string, field: string) => {
    try {
      await navigator.clipboard.writeText(text);
      setCopiedField(field);
      toast.success("تم النسخ");
      setTimeout(() => setCopiedField(null), 2000);
    } catch {
      toast.error("فشل النسخ");
    }
  };

  const handleCreateAccount = async () => {
    setCreating(true);
    try {
      const { data } = await api.post<CreatePortalAccountResponse>(`/api/portal/credentials/${patientId}/create`);
      if (data.alreadyExists) {
        toast.info("الحساب موجود بالفعل");
        await fetchCredentials();
      } else {
        setTempPassword(data.temporaryPassword ?? null);
        setTempUsername(data.username);
        setShowTempPassword(true);
        toast.success("تم إنشاء حساب البوابة بنجاح");
        await fetchCredentials();
      }
    } catch (error: unknown) {
      toast.error(extractErrorMessage(error, "فشل إنشاء حساب البوابة"));
    } finally {
      setCreating(false);
    }
  };

  const handleResetPassword = async () => {
    setResetting(true);
    try {
      const { data } = await api.post<PatientPasswordResetResponse>(`/api/portal/credentials/${patientId}/reset-password`);
      setTempPassword(data.temporaryPassword);
      setTempUsername(data.username);
      setShowTempPassword(true);
      toast.success("تم إعادة تعيين كلمة المرور بنجاح");
      await fetchCredentials();
    } catch (error: unknown) {
      toast.error(extractErrorMessage(error, "فشل إعادة تعيين كلمة المرور"));
    } finally {
      setResetting(false);
    }
  };

  const handleClosePasswordDialog = () => {
    setShowTempPassword(false);
    setTempPassword(null);
    setTempUsername(null);
  };

  if (loading) {
    return (
      <div className="space-y-3 animate-pulse">
        <div className="h-8 bg-gray-100 rounded-lg w-1/3" />
        <div className="h-20 bg-gray-100 rounded-lg" />
        <div className="h-10 bg-gray-100 rounded-lg w-1/2" />
      </div>
    );
  }

  if (loadError) {
    return (
      <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-6 text-center">
        <AlertTriangle className="w-10 h-10 mx-auto mb-3 text-red-400" />
        <p className="text-sm font-semibold text-red-700">{loadError}</p>
        <p className="text-xs text-red-500 mt-1">
          لم نعتبر فشل الطلب «لا يوجد حساب»، ولم نعرض إجراءات إنشاء أو إعادة تعيين قبل معرفة الحالة الحقيقية.
        </p>
        <button
          type="button"
          onClick={fetchCredentials}
          className="mt-4 px-4 py-2 text-sm font-semibold rounded-lg bg-white border border-red-200 text-red-700 hover:bg-red-100 transition"
        >
          إعادة المحاولة
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      {/* Account Status Card */}
      <div
        className="rounded-xl overflow-hidden bg-white border border-[#e8f0f9] shadow-sm"
      >
        {/* Header */}
        <div
          className="px-5 py-3 flex items-center gap-2 bg-[#0d2137] text-white"
        >
          <KeyRound className="w-4 h-4 text-orange-500" />
          <span className="font-bold text-sm">بوابة المريض</span>
        </div>

        {/* Status Section */}
        <div className="p-5 space-y-4">
          {/* Account exists or not */}
          {creds?.hasPortalAccount ? (
            <>
              {/* Status badge */}
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <div
                    className={cn("w-2.5 h-2.5 rounded-full", creds.accountActive ? "bg-green-500" : "bg-red-500")}
                  />
                  <span className="text-sm font-semibold text-[#0d2137]">
                    {creds.accountActive ? "الحساب نشط" : "الحساب معطّل"}
                  </span>
                </div>
                <span
                  className={cn(
                    "text-xs px-2.5 py-1 rounded-full font-semibold",
                    creds.accountActive
                      ? "bg-green-500/10 text-green-500"
                      : "bg-red-500/10 text-red-500"
                  )}
                >
                  {creds.accountActive ? "نشط" : "معطّل"}
                </span>
              </div>

              {/* Username */}
              <div className="flex items-center justify-between p-3 rounded-lg bg-[#f8fafc]">
                <div className="flex items-center gap-2">
                  <UserCircle className="w-4 h-4 text-[#3d7ab5]" />
                  <span className="text-sm text-slate-500">اسم المستخدم</span>
                </div>
                <div className="flex items-center gap-2">
                  <span className="font-mono text-sm font-bold text-[#0d2137]">
                    {creds.username}
                  </span>
                  <button
                    onClick={() => copyToClipboard(creds.username, "username")}
                    className="p-1 rounded hover:bg-gray-200 transition"
                    title="نسخ اسم المستخدم"
                  >
                    {copiedField === "username" ? (
                      <Check className="w-3.5 h-3.5 text-green-500" />
                    ) : (
                      <Copy className="w-3.5 h-3.5 text-[#94a3b8]" />
                    )}
                  </button>
                </div>
              </div>

              {/* Must change password */}
              {creds.mustChangePassword && (
                <div
                  className="flex items-center gap-2 p-3 rounded-lg"
                  style={{ background: "#f5922e10", border: "1px solid #f5922e30" }}
                >
                  <Shield className="w-4 h-4 text-orange-500" />
                  <span className="text-xs font-medium text-orange-500">
                    يجب تغيير كلمة المرور عند أول تسجيل دخول
                  </span>
                </div>
              )}

              {/* Last login */}
              <div className="flex items-center justify-between p-3 rounded-lg bg-[#f8fafc]">
                <div className="flex items-center gap-2">
                  <Clock className="w-4 h-4 text-[#94a3b8]" />
                  <span className="text-sm text-slate-500">آخر تسجيل دخول</span>
                </div>
                <span className={cn("text-sm", creds.lastLogin ? "text-[#0d2137]" : "text-[#94a3b8]")}>
                  {creds.lastLogin ? formatArabicDate(creds.lastLogin) : "لم يسجل الدخول بعد"}
                </span>
              </div>

              {/* Action buttons — Admin/Reception only */}
              {isAdminOrReception && (
                <div className="flex flex-wrap gap-2 pt-2 border-t border-[#e8f0f9]">
                  <button
                    onClick={handleResetPassword}
                    disabled={resetting}
                    className={cn("flex items-center gap-1.5 px-3 py-2 text-xs font-semibold rounded-lg transition text-white", resetting ? "bg-slate-400" : "bg-orange-500")}
                  >
                    {resetting ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <RotateCcw className="w-3.5 h-3.5" />}
                    إعادة تعيين كلمة المرور
                  </button>
                  <button
                    onClick={() => copyToClipboard(creds.username, "username-btn")}
                    className="flex items-center gap-1.5 px-3 py-2 text-xs font-semibold rounded-lg transition border-[1.5px] border-[#3d7ab5] text-[#3d7ab5] bg-white"
                  >
                    {copiedField === "username-btn" ? (
                      <Check className="w-3.5 h-3.5 text-green-500" />
                    ) : (
                      <Copy className="w-3.5 h-3.5" />
                    )}
                    نسخ اسم المستخدم
                  </button>
                  <button
                    onClick={() => copyToClipboard(PORTAL_URL, "portal-url")}
                    className="flex items-center gap-1.5 px-3 py-2 text-xs font-semibold rounded-lg transition border-[1.5px] border-[#dce8f5] text-slate-500 bg-white"
                  >
                    {copiedField === "portal-url" ? (
                      <Check className="w-3.5 h-3.5 text-green-500" />
                    ) : (
                      <ExternalLink className="w-3.5 h-3.5" />
                    )}
                    نسخ رابط البوابة
                  </button>
                </div>
              )}
            </>
          ) : (
            <>
              {/* No account */}
              <div className="text-center py-6">
                <KeyRound className="w-12 h-12 mx-auto mb-3 text-[#dce8f5]" />
                <p className="text-sm font-medium text-[#0d2137]">
                  لا يوجد حساب بوابة لهذا المريض
                </p>
                <p className="text-xs mt-1 text-[#94a3b8]">
                  يمكن إنشاء حساب بوابة ليتمكن المريض من الدخول وعرض بياناته
                </p>
              </div>

              {/* Create button — Admin/Reception only */}
              {isAdminOrReception && (
                <div className="flex justify-center pt-2 border-t border-[#e8f0f9]">
                  <button
                    onClick={handleCreateAccount}
                    disabled={creating}
                    className={cn("flex items-center gap-2 px-5 py-2.5 text-sm font-bold rounded-lg transition text-white", creating ? "bg-slate-400" : "bg-[#3d7ab5]")}
                  >
                    {creating ? <Loader2 className="w-4 h-4 animate-spin" /> : <Plus className="w-4 h-4" />}
                    إنشاء حساب بوابة
                  </button>
                </div>
              )}
            </>
          )}
        </div>
      </div>

      {/* Quick info for all staff */}
      <div
        className="rounded-xl p-4 bg-[#f8fafc] border border-[#e8f0f9]"
      >
        <div className="flex items-center gap-2 mb-2">
          <Shield className="w-4 h-4 text-[#3d7ab5]" />
          <span className="text-sm font-semibold text-[#0d2137]">معلومات البوابة</span>
        </div>
        <div className="space-y-1.5 text-xs text-slate-500">
          <p>• رقم الملف <span className="font-mono font-bold text-[#0d2137]">{patientNumber}</span> يُستخدم كاسم مستخدم افتراضي</p>
          <p>• المريض يجب تغيير كلمة المرور عند أول دخول</p>
          <p>• الأطباء يستطيعون عرض حالة الحساب فقط</p>
          <p>• المدير والاستقبال يستطيعون إنشاء/إعادة تعيين الحساب</p>
        </div>
      </div>

      {/* Temporary Password Dialog — shown once after create/reset */}
      {showTempPassword && tempPassword && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
          <div
            className="bg-white rounded-2xl w-full max-w-md shadow-2xl overflow-hidden"
           
          >
            {/* Header */}
            <div className="px-6 py-4 bg-[#0d2137]">
              <div className="flex items-center gap-2">
                <KeyRound className="w-5 h-5 text-orange-500" />
                <h3 className="text-white font-bold">بيانات الدخول للبوابة</h3>
              </div>
            </div>

            <div className="p-6 space-y-4">
              {/* Warning */}
              <div
                className="flex items-start gap-2 p-3 rounded-lg"
                style={{ background: "#ef444410", border: "1px solid #ef444430" }}
              >
                <AlertTriangle className="w-5 h-5 flex-shrink-0 mt-0.5 text-red-500" />
                <div>
                  <p className="text-sm font-bold text-red-500">
                    تحذير مهم
                  </p>
                  <p className="text-xs mt-0.5 text-red-600">
                    كلمة المرور المؤقتة ستظهر مرة واحدة فقط! احفظها الآن قبل الإغلاق.
                  </p>
                </div>
              </div>

              {/* Username */}
              <div className="p-3 rounded-lg bg-[#f8fafc]">
                <p className="text-xs mb-1 text-slate-500">اسم المستخدم</p>
                <div className="flex items-center justify-between">
                  <span className="font-mono text-base font-bold text-[#0d2137]">
                    {tempUsername}
                  </span>
                  <button
                    onClick={() => copyToClipboard(tempUsername ?? "", "dialog-username")}
                    className="p-1.5 rounded hover:bg-gray-200 transition"
                    title="نسخ"
                  >
                    {copiedField === "dialog-username" ? (
                      <Check className="w-4 h-4 text-green-500" />
                    ) : (
                      <Copy className="w-4 h-4 text-[#3d7ab5]" />
                    )}
                  </button>
                </div>
              </div>

              {/* Temporary Password */}
              <div className="p-3 rounded-lg" style={{ background: "#f5922e08", border: "1px solid #f5922e20" }}>
                <p className="text-xs mb-1 text-orange-500">كلمة المرور المؤقتة</p>
                <div className="flex items-center justify-between">
                  <span className="font-mono text-base font-bold text-[#0d2137]" dir="ltr">
                    {tempPassword}
                  </span>
                  <div className="flex items-center gap-1">
                    <button
                      onClick={() => copyToClipboard(tempPassword, "dialog-password")}
                      className="p-1.5 rounded hover:bg-gray-200 transition"
                      title="نسخ كلمة المرور"
                    >
                      {copiedField === "dialog-password" ? (
                        <Check className="w-4 h-4 text-green-500" />
                      ) : (
                        <Copy className="w-4 h-4 text-orange-500" />
                      )}
                    </button>
                  </div>
                </div>
              </div>

              {/* Portal URL */}
              <div className="p-3 rounded-lg bg-[#f8fafc]">
                <p className="text-xs mb-1 text-slate-500">رابط بوابة المريض</p>
                <div className="flex items-center justify-between">
                  <span className="text-xs font-mono break-all text-[#3d7ab5]" dir="ltr">
                    {PORTAL_URL}
                  </span>
                  <button
                    onClick={() => copyToClipboard(PORTAL_URL, "dialog-url")}
                    className="p-1.5 rounded hover:bg-gray-200 transition flex-shrink-0 ms-2"
                    title="نسخ الرابط"
                  >
                    {copiedField === "dialog-url" ? (
                      <Check className="w-4 h-4 text-green-500" />
                    ) : (
                      <Copy className="w-4 h-4 text-[#94a3b8]" />
                    )}
                  </button>
                </div>
              </div>

              {/* Copy all info */}
              <button
                onClick={() => {
                  const info = `بيانات دخول بوابة المريض\nاسم المستخدم: ${tempUsername}\nكلمة المرور المؤقتة: ${tempPassword}\nرابط البوابة: ${PORTAL_URL}`;
                  copyToClipboard(info, "dialog-all");
                }}
                className={cn("w-full flex items-center justify-center gap-2 py-2.5 rounded-lg text-sm font-semibold transition text-white", copiedField === "dialog-all" ? "bg-green-500" : "bg-[#3d7ab5]")}
              >
                {copiedField === "dialog-all" ? (
                  <>
                    <Check className="w-4 h-4" />
                    تم النسخ
                  </>
                ) : (
                  <>
                    <Copy className="w-4 h-4" />
                    نسخ كل بيانات الدخول
                  </>
                )}
              </button>

              {/* Close button */}
              <button
                onClick={handleClosePasswordDialog}
                className="w-full py-2.5 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
              >
                إغلاق — لن أتمكن من رؤية كلمة المرور مرة أخرى
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
