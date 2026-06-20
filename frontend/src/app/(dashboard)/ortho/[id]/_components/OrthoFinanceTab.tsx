"use client";

import Link from "next/link";
import { formatYemeniRiyal } from "@/lib/utils";
import { financeV3ContractsUrl } from "@/lib/financeRoutes";
import { useOrthoOverview } from "@/hooks/useOrtho";

/**
 * Finance tab — kept as a separate component (FE-20) for parity with the other
 * tab extracts even though it's small. It uses `useOrthoOverview` directly
 * (React Query cache is shared with the page shell, so no extra request).
 */
export function OrthoFinanceTab({
  caseId,
  patientId,
}: {
  caseId: string;
  patientId: string;
}) {
  const { data: overview } = useOrthoOverview(caseId);
  return (
    <div className="grid gap-5 md:grid-cols-3">
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <p className="text-sm text-gray-500">إجمالي العقد</p>
        <p className="mt-2 text-2xl font-bold text-gray-900">
          {formatYemeniRiyal(overview?.contractTotal ?? 0)}
        </p>
      </div>
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <p className="text-sm text-gray-500">المدفوع</p>
        <p className="mt-2 text-2xl font-bold text-green-600">
          {formatYemeniRiyal(overview?.contractPaid ?? 0)}
        </p>
      </div>
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <p className="text-sm text-gray-500">المتبقي</p>
        <p className="mt-2 text-2xl font-bold text-red-600">
          {formatYemeniRiyal(overview?.contractRemaining ?? 0)}
        </p>
      </div>
      <div className="rounded-lg border border-gray-200 bg-white p-5 md:col-span-3">
        <div className="flex flex-wrap gap-3">
          {overview?.contractId && (
            <Link
              href={financeV3ContractsUrl(patientId)}
              className="rounded-lg bg-clinic-blue px-4 py-2 text-sm font-medium text-white"
            >
              فتح العقد
            </Link>
          )}
          <Link
            href={financeV3ContractsUrl(patientId, { relatedCaseId: caseId })}
            className="rounded-lg border border-gray-200 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
          >
            إنشاء عقد تقويم
          </Link>
          <Link
            href={financeV3ContractsUrl(patientId)}
            className="rounded-lg border border-gray-200 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
          >
            مالية المريض
          </Link>
        </div>
      </div>
    </div>
  );
}
