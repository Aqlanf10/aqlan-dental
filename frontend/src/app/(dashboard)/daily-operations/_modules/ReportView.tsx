"use client";

import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";
import {
  Wallet, CreditCard, Landmark, Calculator, AlertTriangle, CheckCircle,
  Loader2, RefreshCw, Sparkles, TrendingUp, DollarSign
} from "lucide-react";
import { NAVY, BLUE, ORANGE, fmtRial, fmtDate } from "../_lib/constants";
import { CashierSession } from "../_lib/hooks";

interface DoctorCommissionStats {
  doctorId: string;
  doctorName: string;
  casesCount: number;
  totalServiceValue: number;
  commissionPercentage: number;
  commissionDue: number;
  commissionPaid: number;
  commissionRemaining: number;
}

export default function ReportView() {
  const queryClient = useQueryClient();

  // 1. Fetch Active Cashier Session Details
  const { data: session, isLoading: sessionLoading, refetch: refetchSession } = useQuery<CashierSession | null>({
    queryKey: ["daily-ops", "active-cashier-session-reconcile"],
    queryFn: async () => {
      try {
        const { data } = await api.get<{ hasActiveSession: boolean; id?: string } & CashierSession>("/api/cashier-sessions/active");
        if (!data?.hasActiveSession || !data.id) return null;
        return data;
      } catch {
        return null;
      }
    },
    staleTime: 10_000,
  });

  // 2. Fetch Doctor Commissions Stats
  const { data: commissions = [], isLoading: commissionsLoading, refetch: refetchCommissions } = useQuery<DoctorCommissionStats[]>({
    queryKey: ["daily-ops", "doctor-commissions-stats"],
    queryFn: async () => {
      const { data } = await api.get("/api/finance-v3/doctor-commissions");
      return data;
    },
    staleTime: 30_000,
  });

  // 3. Cashier Session Reconciliation input states
  const [actualCash, setActualCash] = useState("");
  const [actualCard, setActualCard] = useState("");
  const [actualBank, setActualBank] = useState("");
  const [closeNotes, setCloseNotes] = useState("");

  // Initialize actual balances when expected ones load
  useEffect(() => {
    if (session) {
      setActualCash(String(session.expectedClosingCash || 0));
      setActualCard(String(session.expectedClosingCard || 0));
      setActualBank(String(session.expectedClosingBank || 0));
    }
  }, [session]);

  // Calculations
  const expectedCash = session?.expectedClosingCash ?? 0;
  const expectedCard = session?.expectedClosingCard ?? 0;
  const expectedBank = session?.expectedClosingBank ?? 0;
  const expectedTotal = expectedCash + expectedCard + expectedBank;

  const actualCashNum = parseFloat(actualCash) || 0;
  const actualCardNum = parseFloat(actualCard) || 0;
  const actualBankNum = parseFloat(actualBank) || 0;
  const actualTotal = actualCashNum + actualCardNum + actualBankNum;

  const difference = actualTotal - expectedTotal;

  // 4. Close session mutation
  const closeSessionMutation = useMutation({
    mutationFn: async () => {
      const { data } = await api.post("/api/cashier-sessions/close", {
        actualClosingCash: actualCashNum,
        actualClosingCard: actualCardNum,
        actualClosingBank: actualBankNum,
        notes: closeNotes.trim() || undefined
      });
      return data;
    },
    onSuccess: () => {
      toast.success("تم إقفال الصندوق والوردية بنجاح");
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });
      queryClient.invalidateQueries({ queryKey: ["daily-ops", "active-cashier-session"] });
      queryClient.invalidateQueries({ queryKey: ["daily-ops", "active-cashier-session-reconcile"] });
      setCloseNotes("");
    },
    onError: (err: unknown) => {
      let errorMsg = "فشل إقفال الوردية";
      if (err && typeof err === "object" && "response" in err) {
        const resp = (err as { response?: { data?: { message?: string } } }).response;
        if (resp?.data?.message) errorMsg = resp.data.message;
      }
      toast.error(errorMsg);
    }
  });

  const handleCloseSession = () => {
    if (!session) return;
    if (Math.abs(difference) > 0) {
      const confirmMsg = `تنبيه: يوجد فارق ${difference > 0 ? "فائض" : "عجز"} بقيمة ${fmtRial(Math.abs(difference))}. هل أنت متأكد من رغبتك في إقفال الوردية بهذا الفارق؟`;
      if (!window.confirm(confirmMsg)) return;
    } else {
      if (!window.confirm("هل أنت متأكد من رغبتك في إقفال الوردية وصندوق الاستقبال؟")) return;
    }
    closeSessionMutation.mutate();
  };

  const handleRefresh = () => {
    refetchSession();
    refetchCommissions();
    toast.success("تم تحديث البيانات المالية");
  };

  const isClosingLoading = closeSessionMutation.isPending;

  return (
    <div className="flex-1 flex flex-col overflow-hidden p-4 space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between bg-white p-3 rounded-2xl border border-gray-100 shadow-sm">
        <div>
          <h2 className="text-sm font-extrabold" style={{ color: NAVY }}>التقرير المالي اليومي وتسوية الصندوق</h2>
          <p className="text-[11px] text-gray-400 mt-0.5 font-medium">متابعة تسوية خزانة الاستقبال وعمولات أطباء العيادة</p>
        </div>
        <button
          onClick={handleRefresh}
          className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100 border text-gray-500 border-gray-200 transition"
          title="تحديث البيانات"
        >
          <RefreshCw className="w-4 h-4" />
        </button>
      </div>

      <div className="flex-1 grid grid-cols-1 xl:grid-cols-3 gap-4 overflow-y-auto min-h-0">
        
        {/* Left reconciliation area (Col 1) */}
        <div className="xl:col-span-1 flex flex-col space-y-4">
          
          {/* Active Session card */}
          <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-4 flex flex-col space-y-3">
            <div className="flex items-center gap-2 pb-2 border-b border-gray-100">
              <div className="w-8 h-8 rounded-lg bg-orange-50 flex items-center justify-center text-orange-600">
                <Wallet className="w-4 h-4" />
              </div>
              <div>
                <div className="text-xs font-bold" style={{ color: NAVY }}>وردية الصندوق النشطة</div>
                {session ? (
                  <div className="text-[10px] text-gray-400 font-sans mt-0.5">رقم الوردية: {session.sessionNumber}</div>
                ) : (
                  <div className="text-[10px] text-red-500 font-bold mt-0.5">صندوق الاستقبال مغلق حالياً</div>
                )}
              </div>
            </div>

            {session ? (
              <div className="space-y-3.5">
                <div className="grid grid-cols-2 gap-2 text-xs">
                  <div className="bg-gray-50 p-2 rounded-lg">
                    <span className="text-[10px] text-gray-400 block font-semibold">تاريخ الفتح</span>
                    <span className="font-bold text-gray-700 font-sans">{new Date(session.openedAt).toLocaleDateString("ar-YE")}</span>
                  </div>
                  <div className="bg-gray-50 p-2 rounded-lg">
                    <span className="text-[10px] text-gray-400 block font-semibold">وقت الفتح</span>
                    <span className="font-bold text-gray-700 font-sans">
                      {new Date(session.openedAt).toLocaleTimeString("ar-YE", { hour: "2-digit", minute: "2-digit" })}
                    </span>
                  </div>
                  <div className="bg-gray-50 p-2 rounded-lg col-span-2">
                    <span className="text-[10px] text-gray-400 block font-semibold">صاحب الوردية (الكاشير)</span>
                    <span className="font-bold text-gray-700">{session.cashierName || "—"}</span>
                  </div>
                </div>

                <div className="p-3 rounded-xl border border-gray-100 space-y-2">
                  <div className="text-xs font-bold text-gray-500">الأرصدة المتوقعة بالصندوق (النظام)</div>
                  
                  <div className="flex items-center justify-between text-xs font-medium">
                    <span className="flex items-center gap-1 text-gray-500"><Wallet className="w-3.5 h-3.5 text-orange-500" /> كاش (عهدة + تحصيل)</span>
                    <span className="font-bold text-gray-800 font-sans">{fmtRial(expectedCash)}</span>
                  </div>
                  <div className="flex items-center justify-between text-xs font-medium">
                    <span className="flex items-center gap-1 text-gray-500"><CreditCard className="w-3.5 h-3.5 text-blue-500" /> شبكة / بطاقات</span>
                    <span className="font-bold text-gray-800 font-sans">{fmtRial(expectedCard)}</span>
                  </div>
                  <div className="flex items-center justify-between text-xs font-medium">
                    <span className="flex items-center gap-1 text-gray-500"><Landmark className="w-3.5 h-3.5 text-emerald-500" /> تحويلات بنكية</span>
                    <span className="font-bold text-gray-800 font-sans">{fmtRial(expectedBank)}</span>
                  </div>
                  
                  <div className="h-px bg-gray-100 my-2" />
                  
                  <div className="flex items-center justify-between text-xs font-bold text-gray-900">
                    <span>إجمالي الإيراد المتوقع</span>
                    <span className="font-sans text-sm text-[#1a3a5c]">{fmtRial(expectedTotal)}</span>
                  </div>
                </div>

                {/* Actual inputs reconciliation form */}
                <div className="p-3.5 rounded-xl bg-slate-50 border border-slate-100 space-y-3">
                  <div className="text-xs font-bold text-slate-800">إدخال الجرد الفعلي والتحقق</div>
                  
                  <div>
                    <label className="text-[10px] text-gray-500 block mb-1 font-semibold">النقد الفعلي بالدرج (الخزينة) *</label>
                    <input
                      type="number"
                      value={actualCash}
                      onChange={e => setActualCash(e.target.value)}
                      placeholder="0"
                      className="w-full text-xs font-bold font-sans rounded-lg border border-gray-200 px-3 py-1.5 outline-none focus:border-blue-500"
                    />
                  </div>
                  <div>
                    <label className="text-[10px] text-gray-500 block mb-1 font-semibold">نقاط البيع الفعلية (الشبكة) *</label>
                    <input
                      type="number"
                      value={actualCard}
                      onChange={e => setActualCard(e.target.value)}
                      placeholder="0"
                      className="w-full text-xs font-bold font-sans rounded-lg border border-gray-200 px-3 py-1.5 outline-none focus:border-blue-500"
                    />
                  </div>
                  <div>
                    <label className="text-[10px] text-gray-500 block mb-1 font-semibold">التحويلات البنكية الفعلية *</label>
                    <input
                      type="number"
                      value={actualBank}
                      onChange={e => setActualBank(e.target.value)}
                      placeholder="0"
                      className="w-full text-xs font-bold font-sans rounded-lg border border-gray-200 px-3 py-1.5 outline-none focus:border-blue-500"
                    />
                  </div>
                  <div>
                    <label className="text-[10px] text-gray-500 block mb-1 font-semibold">ملاحظات الإقفال</label>
                    <textarea
                      value={closeNotes}
                      onChange={e => setCloseNotes(e.target.value)}
                      placeholder="ملاحظات ترحيل الفارق أو أي تعليق..."
                      rows={2}
                      className="w-full text-xs rounded-lg border border-gray-200 px-3 py-1.5 outline-none focus:border-blue-500"
                    />
                  </div>

                  {/* Dynamic calculation result */}
                  <div className="p-3 rounded-lg flex flex-col space-y-1.5 text-xs font-semibold"
                    style={{
                      background: difference === 0 ? "#f0fdf4" : difference > 0 ? "#eff6ff" : "#fef2f2",
                      color: difference === 0 ? "#16a34a" : difference > 0 ? "#2563eb" : "#dc2626",
                      border: `1px solid ${difference === 0 ? "#bbf7d0" : difference > 0 ? "#bfdbfe" : "#fecaca"}`
                    }}>
                    <div className="flex items-center justify-between">
                      <span>إجمالي الفعلي المدخل</span>
                      <span className="font-bold font-sans">{fmtRial(actualTotal)}</span>
                    </div>
                    <div className="flex items-center justify-between font-extrabold">
                      <span>الفارق (عجز / فائض)</span>
                      <span className="font-sans flex items-center gap-1">
                        {difference === 0 ? "متطابق" : difference > 0 ? `فائض ${fmtRial(difference)}` : `عجز ${fmtRial(Math.abs(difference))}`}
                      </span>
                    </div>
                  </div>

                  <button
                    onClick={handleCloseSession}
                    disabled={isClosingLoading}
                    className="w-full py-2.5 rounded-xl text-xs font-bold text-white bg-red-600 hover:bg-red-700 transition flex items-center justify-center gap-2"
                  >
                    {isClosingLoading ? <Loader2 className="w-4 h-4 animate-spin" /> : <CheckCircle className="w-4 h-4" />}
                    إقفال الوردية الحالية
                  </button>
                </div>
              </div>
            ) : (
              <div className="text-center py-8 text-gray-400">
                <AlertTriangle className="w-10 h-10 mx-auto mb-2 text-amber-500 opacity-60" />
                <div className="text-xs font-bold">يرجى فتح صندوق الكاشير لبدء الوردية وتسجيل الحساب الفعلي.</div>
              </div>
            )}
          </div>
        </div>

        {/* Right doctor commissions area (Col 2 & 3) */}
        <div className="xl:col-span-2 flex flex-col space-y-4">
          <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-4 flex-1 flex flex-col overflow-hidden">
            <div className="flex items-center gap-2 pb-2 border-b border-gray-100 flex-shrink-0">
              <div className="w-8 h-8 rounded-lg bg-emerald-50 flex items-center justify-center text-emerald-600">
                <Sparkles className="w-4 h-4" />
              </div>
              <div>
                <h3 className="text-xs font-bold" style={{ color: NAVY }}>تقرير عمولات الأطباء</h3>
                <p className="text-[10px] text-gray-400 font-medium">صافي عمولة الطبيب = (المحصل الفعلي - تكاليف المعمل - تكاليف المواد) * نسبة الطبيب%</p>
              </div>
            </div>

            <div className="flex-1 overflow-auto mt-3">
              {commissionsLoading ? (
                <div className="flex items-center justify-center py-20">
                  <Loader2 className="w-8 h-8 animate-spin" style={{ color: BLUE }} />
                </div>
              ) : commissions.length === 0 ? (
                <div className="text-center py-20 text-gray-400">
                  <TrendingUp className="w-12 h-12 mx-auto mb-3 opacity-20" />
                  <p className="text-xs font-bold">لا توجد عمولات مسجلة لهذه الفترة</p>
                </div>
              ) : (
                <table className="w-full text-right" style={{ borderCollapse: "separate", borderSpacing: 0 }}>
                  <thead>
                    <tr className="text-[10px] font-bold text-gray-400 uppercase tracking-wider bg-gray-50/50">
                      <th className="py-2.5 px-3 border-b">الطبيب</th>
                      <th className="py-2.5 px-3 border-b text-center">الزيارات</th>
                      <th className="py-2.5 px-3 border-b">إجمالي العمل</th>
                      <th className="py-2.5 px-3 border-b text-center">النسبة</th>
                      <th className="py-2.5 px-3 border-b">العمولة المستحقة</th>
                      <th className="py-2.5 px-3 border-b">العمولة المدفوعة</th>
                      <th className="py-2.5 px-3 border-b">المتبقي للطبيب</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {commissions.map((doc) => (
                      <tr key={doc.doctorId} className="hover:bg-gray-50/30 text-xs">
                        <td className="py-3 px-3 font-bold" style={{ color: NAVY }}>{doc.doctorName}</td>
                        <td className="py-3 px-3 text-center font-sans font-semibold text-gray-600">{doc.casesCount}</td>
                        <td className="py-3 px-3 font-sans font-bold text-gray-800">{fmtRial(doc.totalServiceValue)}</td>
                        <td className="py-3 px-3 text-center font-sans font-semibold text-purple-700 bg-purple-50 rounded-lg">{doc.commissionPercentage}%</td>
                        <td className="py-3 px-3 font-sans font-bold text-indigo-600">{fmtRial(doc.commissionDue)}</td>
                        <td className="py-3 px-3 font-sans font-bold text-emerald-600">{fmtRial(doc.commissionPaid)}</td>
                        <td className="py-3 px-3">
                          <span className="font-sans font-bold px-2 py-1 rounded text-xs"
                            style={{
                              background: doc.commissionRemaining > 0 ? "#fef2f2" : "#f0fdf4",
                              color: doc.commissionRemaining > 0 ? "#ef4444" : "#16a34a"
                            }}>
                            {fmtRial(doc.commissionRemaining)}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        </div>

      </div>
    </div>
  );
}
