"use client";

import { useQuery } from "@tanstack/react-query";
import api from "@/lib/api";
import { formatMoney } from "./FinanceHelpers";
import { LoadingSkeleton, EmptyState, tokens } from "./FinanceSharedUI";
import { BookOpen } from "lucide-react";

export type PartyType = "patient" | "doctor" | "supplier" | "lab";

interface PartyStatementEntry {
  date: string;
  createdAt: string;
  documentNumber: string;
  documentType: string;
  description: string;
  debit: number;
  credit: number;
  runningBalance: number;
  branchId: string;
  sourceId: string;
  status: string;
  isReversal: boolean;
}

interface PartyCurrencyStatement {
  currency: string;
  totalDebit: number;
  totalCredit: number;
  closingBalance: number;
  balanceNature: "Debit" | "Credit" | "Settled";
  entries: PartyStatementEntry[];
}

interface PartyAccountStatement {
  partyType: PartyType;
  partyId: string;
  partyName: string;
  currencies: PartyCurrencyStatement[];
}

const natureLabel = {
  Debit: "مدين",
  Credit: "دائن",
  Settled: "مسدد",
} as const;

export function PartyAccountStatementPanel({
  partyType,
  partyId,
}: {
  partyType: PartyType;
  partyId?: string | null;
}) {
  const statement = useQuery({
    queryKey: ["party-account-statement", partyType, partyId],
    queryFn: async () => {
      const { data } = await api.get<PartyAccountStatement>(
        `/api/finance-v3/party-statements/${partyType}/${partyId}`,
      );
      return data;
    },
    enabled: !!partyId,
  });

  if (statement.isLoading) return <LoadingSkeleton rows={5} />;
  if (statement.isError) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-xs text-red-700">
        تعذر تحميل كشف المدين والدائن. تحقق من الصلاحية ثم حاول مجددًا.
      </div>
    );
  }
  if (!statement.data || statement.data.currencies.length === 0)
    return <EmptyState icon={BookOpen} message="لا توجد قيود مالية مرحلة لهذا الحساب" />;

  return (
    <div className="space-y-5" dir="rtl">
      <p className="text-xs" style={{ color: tokens.textSecondary }}>
        كل عملة مستقلة ولا تُجمع مع العملات الأخرى. الرصيد الجاري = المدين − الدائن.
      </p>
      {statement.data.currencies.map((group) => (
        <section key={group.currency} className="space-y-3">
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
            <Summary label="إجمالي المدين" value={formatMoney(group.totalDebit, group.currency)} />
            <Summary label="إجمالي الدائن" value={formatMoney(group.totalCredit, group.currency)} />
            <Summary label="الرصيد" value={formatMoney(group.closingBalance, group.currency)} />
            <Summary label="طبيعة الرصيد" value={natureLabel[group.balanceNature]} />
          </div>

          <div className="overflow-x-auto rounded-lg border" style={{ borderColor: tokens.border }}>
            <table className="w-full min-w-[900px] text-xs">
              <thead>
                <tr style={{ backgroundColor: tokens.cardHover }}>
                  <th className="px-3 py-2 text-right">التاريخ</th>
                  <th className="px-3 py-2 text-right">المستند</th>
                  <th className="px-3 py-2 text-right">البيان</th>
                  <th className="px-3 py-2 text-left">مدين</th>
                  <th className="px-3 py-2 text-left">دائن</th>
                  <th className="px-3 py-2 text-left">الرصيد الجاري</th>
                  <th className="px-3 py-2 text-right">الحالة</th>
                </tr>
              </thead>
              <tbody>
                {group.entries.map((entry) => (
                  <tr key={`${entry.sourceId}-${entry.documentType}-${entry.createdAt}`} className="border-t" style={{ borderColor: tokens.border }}>
                    <td className="px-3 py-2 whitespace-nowrap">{entry.date}</td>
                    <td className="px-3 py-2 font-mono" dir="ltr">{entry.documentNumber}</td>
                    <td className="px-3 py-2">
                      {entry.description}
                      {entry.isReversal && <span className="mr-2 text-red-600">(قيد عكسي)</span>}
                    </td>
                    <td className="px-3 py-2 text-left font-mono" dir="ltr">{entry.debit ? formatMoney(entry.debit, group.currency) : "—"}</td>
                    <td className="px-3 py-2 text-left font-mono" dir="ltr">{entry.credit ? formatMoney(entry.credit, group.currency) : "—"}</td>
                    <td className="px-3 py-2 text-left font-mono font-bold" dir="ltr">{formatMoney(entry.runningBalance, group.currency)}</td>
                    <td className="px-3 py-2">{entry.status}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ))}
    </div>
  );
}

function Summary({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border p-3" style={{ borderColor: tokens.border, backgroundColor: tokens.cardHover }}>
      <p className="text-[10px]" style={{ color: tokens.textTertiary }}>{label}</p>
      <p className="mt-1 text-sm font-bold" style={{ color: tokens.textPrimary }}>{value}</p>
    </div>
  );
}
