/**
 * OverrideDialog — manager-approval dialog shown when a patient has overdue
 * installments and the user wants to bypass the block (manager must log in).
 *
 * Extracted from `_components/Modals.tsx` (CLEANUP-1). No behavior changes —
 * pure file extraction. Arabic RTL preserved.
 */

"use client";

import { useState } from "react";
import { AlertTriangle, Loader2 } from "lucide-react";
import { inputCls, fmtRial } from "../../_lib/constants";
import axios from "axios";
import { ModalShell } from "./ModalShell";

export function OverrideDialog({
  open,
  onClose,
  patientName,
  overdueAmount,
  onConfirm,
}: {
  open: boolean;
  onClose: () => void;
  patientName: string;
  overdueAmount: number;
  onConfirm: (managerUsername: string) => void;
}) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const handleVerify = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!username.trim() || !password) return;
    setLoading(true);
    setError("");
    try {
      const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "";
      const { data } = await axios.post(`${API_URL}/api/auth/login`, {
        username: username.trim(),
        password,
      });

      const userRole = data?.user?.role;
      if (userRole === "Admin" || userRole === "BranchManager") {
        onConfirm(username.trim());
        setUsername("");
        setPassword("");
      } else {
        setError("عذراً، هذا الحساب لا يملك صلاحية تجاوز (يجب أن يكون مدير فرع أو مسؤول)");
      }
    } catch (err: unknown) {
      let errorMsg = "فشل التحقق من اسم المستخدم أو كلمة المرور";
      if (err && typeof err === "object" && "response" in err) {
        const resp = (err as { response?: { data?: { message?: string } } }).response;
        if (resp?.data?.message) errorMsg = resp.data.message;
      }
      setError(errorMsg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <ModalShell open={open} onClose={onClose} title="موافقة المدير المطلوبة (تجاوز متأخرات)" icon={AlertTriangle} iconColor="#ef4444">
      <div className="mb-4 p-3.5 rounded-xl border border-red-100 bg-red-50 text-red-800 text-xs">
        <div className="font-extrabold text-sm mb-1">تنبيه وجود متأخرات مالية!</div>
        لا يمكن تسجيل وصول أو إدخال المريض <span className="font-extrabold">{patientName}</span> للانتظار نظراً لوجود أقساط متأخرة بقيمة <span className="font-extrabold">{fmtRial(overdueAmount)}</span>.
        لتجاوز هذا المنع، يجب إدخال اسم مستخدم وكلمة مرور لمدير الفرع أو المسؤول.
      </div>

      <form onSubmit={handleVerify} className="space-y-3.5">
        <div>
          <label className="text-xs font-semibold block mb-1 text-gray-700">اسم مستخدم المدير</label>
          <input
            type="text"
            value={username}
            onChange={e => setUsername(e.target.value)}
            placeholder="Username"
            className={inputCls()}
            required
            autoFocus
          />
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1 text-gray-700">كلمة المرور</label>
          <input
            type="password"
            value={password}
            onChange={e => setPassword(e.target.value)}
            placeholder="••••••••"
            className={inputCls()}
            required
          />
        </div>

        {error && (
          <div className="text-xs font-semibold text-red-600 bg-red-50 p-2 rounded-lg border border-red-200">
            {error}
          </div>
        )}

        <div className="flex gap-2 pt-2">
          <button type="button" onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold bg-gray-100 text-gray-600">
            إلغاء
          </button>
          <button
            type="submit"
            disabled={loading || !username.trim() || !password}
            className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white bg-red-600 hover:bg-red-700 flex items-center justify-center gap-2 disabled:opacity-50"
          >
            {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : null}
            تأكيد التجاوز
          </button>
        </div>
      </form>
    </ModalShell>
  );
}
