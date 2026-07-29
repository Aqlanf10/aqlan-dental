import { useEffect, useState } from "react";
import { CreditCard, TrendingUp, TrendingDown, FileText, Receipt } from "lucide-react";
import portalApi from "@/lib/portalApi";
import type { PatientFinancialSummary } from "@/types/patientPortal";
import { formatYemeniRiyal } from "@/lib/utils";

export default function PortalFinancePage() {
  const [finance, setFinance] = useState<PatientFinancialSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    portalApi.get<PatientFinancialSummary>("/api/portal/finance")
      .then((r) => setFinance(r.data))
      .catch((err) => setError(err?.response?.data?.message || "حدث خطأ في تحميل البيانات المالية"))
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="pb-24">
      {/* Header */}
      <div className="bg-white border-b border-gray-100 px-5 pt-10 pb-4">
        <h1 className="text-xl font-extrabold text-gray-900">الحساب المالي</h1>
      </div>

      <div className="px-4 mt-4 space-y-4">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-2xl p-4 text-center">
            {error}
          </div>
        )}
        {loading ? (
          <div className="space-y-4 animate-pulse">
            <div className="bg-white rounded-2xl h-32" />
            <div className="bg-white rounded-2xl h-64" />
          </div>
        ) : (
          <>
            {/* Summary Cards */}
            <div className="grid grid-cols-3 gap-3">
              <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-3">
                <div className="flex items-center gap-1.5 mb-2">
                  <div className="w-6 h-6 bg-blue-100 rounded-lg flex items-center justify-center">
                    <Receipt className="w-3 h-3 text-blue-600" />
                  </div>
                </div>
                <p className="text-[10px] text-gray-500 mb-0.5">إجمالي التكلفة</p>
                <p className="text-sm font-bold text-gray-900">{formatYemeniRiyal(finance?.totalAmount ?? 0)}</p>
              </div>
              <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-3">
                <div className="flex items-center gap-1.5 mb-2">
                  <div className="w-6 h-6 bg-green-100 rounded-lg flex items-center justify-center">
                    <TrendingUp className="w-3 h-3 text-green-600" />
                  </div>
                </div>
                <p className="text-[10px] text-gray-500 mb-0.5">المدفوع</p>
                <p className="text-sm font-bold text-green-700">{formatYemeniRiyal(finance?.totalPaid ?? 0)}</p>
              </div>
              <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-3">
                <div className="flex items-center gap-1.5 mb-2">
                  <div className="w-6 h-6 bg-red-100 rounded-lg flex items-center justify-center">
                    <TrendingDown className="w-3 h-3 text-red-600" />
                  </div>
                </div>
                <p className="text-[10px] text-gray-500 mb-0.5">المتبقي</p>
                <p className="text-sm font-bold text-red-700">{formatYemeniRiyal(finance?.totalOutstanding ?? 0)}</p>
              </div>
            </div>

            {/* Contracts */}
            {finance?.contracts && finance.contracts.length > 0 && (
              <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
                <h3 className="font-bold text-gray-900 mb-3 flex items-center gap-2">
                  <FileText className="w-4 h-4 text-clinic-blue" />
                  العقود النشطة ({finance.activeContracts})
                </h3>
                <div className="space-y-3">
                  {finance.contracts.map((c) => (
                    <div key={c.id} className="border border-gray-100 rounded-xl p-3">
                      <div className="flex items-center justify-between mb-2">
                        <span className="text-sm font-medium text-gray-900">
                          {c.specialty || "عقد علاج"}
                        </span>
                        {c.startDate && (
                          <span className="text-[10px] text-gray-400">{c.startDate}</span>
                        )}
                      </div>
                      <div className="grid grid-cols-3 gap-2 text-center">
                        <div>
                          <p className="text-[10px] text-gray-400">الإجمالي</p>
                          <p className="text-xs font-bold text-gray-700">{formatYemeniRiyal(c.totalAmount)}</p>
                        </div>
                        <div>
                          <p className="text-[10px] text-gray-400">المدفوع</p>
                          <p className="text-xs font-bold text-green-600">{formatYemeniRiyal(c.paidAmount)}</p>
                        </div>
                        <div>
                          <p className="text-[10px] text-gray-400">المتبقي</p>
                          <p className="text-xs font-bold text-red-600">{formatYemeniRiyal(c.remainingAmount)}</p>
                        </div>
                      </div>
                      {/* Progress bar */}
                      <div className="mt-2 h-1.5 bg-gray-100 rounded-full overflow-hidden">
                        <div
                          className="h-full bg-green-500 rounded-full transition-all"
                          style={{ width: c.totalAmount > 0 ? `${(c.paidAmount / c.totalAmount) * 100}%` : "0%" }}
                        />
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* No contracts message */}
            {(!finance?.contracts || finance.contracts.length === 0) && (
              <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-6 text-center">
                <FileText className="w-10 h-10 text-gray-300 mx-auto mb-2" />
                <p className="text-sm text-gray-500">لا توجد عقود نشطة</p>
              </div>
            )}

            {/* Recent Payments */}
            <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
              <h3 className="font-bold text-gray-900 mb-3">سجل المدفوعات</h3>
              {!finance?.recentPayments || finance.recentPayments.length === 0 ? (
                <div className="text-center py-8">
                  <CreditCard className="w-10 h-10 text-gray-300 mx-auto mb-2" />
                  <p className="text-sm text-gray-500">لا توجد مدفوعات</p>
                </div>
              ) : (
                <div className="space-y-2">
                  {finance.recentPayments.map((p) => (
                    <div key={p.id} className="flex items-center justify-between py-2.5 border-b border-gray-50 last:border-0">
                      <div>
                        <p className="text-sm font-bold text-gray-900">{formatYemeniRiyal(p.amount)}</p>
                        <div className="flex items-center gap-2 mt-0.5">
                          <p className="text-xs text-gray-400">{p.createdAt}</p>
                          {p.serviceDescription && (
                            <p className="text-xs text-gray-500">{p.serviceDescription}</p>
                          )}
                        </div>
                      </div>
                      <div className="text-left">
                        <span className="text-xs bg-gray-100 text-gray-600 px-2 py-0.5 rounded-full">
                          {p.paymentMethod}
                        </span>
                        {p.receiptNumber && (
                          <p className="text-[10px] text-gray-400 mt-0.5 font-mono">{p.receiptNumber}</p>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* Currency Note */}
            <p className="text-[10px] text-gray-400 text-center">جميع المبالغ بالريال اليمني (ر.ي)</p>
          </>
        )}
      </div>
    </div>
  );
}
