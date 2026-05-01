"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { FileSignature } from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";

interface ContractDto {
  id: string;
  contractNumber?: string;
  totalAmount: number;
  paidAmount: number;
  status: string;
  createdAt: string;
  description?: string;
}

const CONTRACT_STATUS_LABELS: Record<string, string> = {
  active: "نشط",
  Active: "نشط",
  completed: "مكتمل",
  Completed: "مكتمل",
  cancelled: "ملغى",
  Cancelled: "ملغى",
  draft: "مسودة",
  Draft: "مسودة",
  overdue: "متأخر",
  Overdue: "متأخر",
};

interface ContractsTabProps {
  patientId: string;
}

export function ContractsTab({ patientId }: ContractsTabProps) {
  const [contracts, setContracts] = useState<ContractDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<ContractDto[]>(`/api/contracts?patientId=${patientId}`)
      .then((r) => setContracts(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  if (loading) {
    return (
      <div className="space-y-2 animate-pulse">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-16 bg-gray-100 rounded-lg" />
        ))}
      </div>
    );
  }

  if (contracts.length === 0) {
    return (
      <div className="text-center py-12 text-gray-400" dir="rtl">
        <FileSignature className="w-10 h-10 mx-auto mb-2 opacity-30" />
        <p className="text-sm">لا توجد عقود مسجّلة</p>
      </div>
    );
  }

  return (
    <div className="space-y-2" dir="rtl">
      {contracts.map((c) => (
        <Link key={c.id} href={`/finance/contracts/${c.id}`}
          className="flex items-center justify-between p-3 bg-white border border-gray-100 rounded-lg hover:border-gray-200 transition"
        >
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-blue-50 flex items-center justify-center flex-shrink-0">
              <FileSignature className="w-5 h-5 text-blue-500" />
            </div>
            <div>
              <div className="flex items-center gap-2 flex-wrap">
                <span className="text-sm font-medium text-gray-900">
                  {c.contractNumber ?? c.id.slice(0, 8)}
                </span>
                <span className={cn(
                  "text-xs px-1.5 py-0.5 rounded-full font-medium",
                  c.status === "active" || c.status === "Active" ? "bg-green-50 text-green-700" :
                  c.status === "overdue" || c.status === "Overdue" ? "bg-red-50 text-red-700" :
                  "bg-gray-100 text-gray-500"
                )}>
                  {CONTRACT_STATUS_LABELS[c.status] ?? c.status}
                </span>
              </div>
              <p className="text-xs text-gray-400">{formatArabicDate(c.createdAt)}</p>
              {c.description && <p className="text-xs text-gray-500 line-clamp-1">{c.description}</p>}
            </div>
          </div>
          <div className="text-left flex-shrink-0">
            <p className="text-sm font-semibold text-gray-900">{c.totalAmount.toLocaleString()}</p>
            <p className="text-xs text-gray-400">مدفوع: {c.paidAmount.toLocaleString()}</p>
          </div>
        </Link>
      ))}
    </div>
  );
}
