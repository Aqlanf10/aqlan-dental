"use client";

import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { AlertTriangle, Archive, CalendarDays, Loader2 } from "lucide-react";
import api from "@/lib/api";

interface LegacyAppointment {
  id: string;
  appointmentAt?: string | null;
  archiveType?: string | null;
  description?: string | null;
  notes?: string | null;
}

interface LegacyTreatment {
  id: string;
  treatmentDate?: string | null;
  documentType?: string | null;
  serviceName?: string | null;
  description?: string | null;
  lineTotal: number;
  discountAmount: number;
  doctorName?: string | null;
  isOrthodonticService: boolean;
}

interface LegacyFinancialEntry {
  id: string;
  entryDate?: string | null;
  accountName?: string | null;
  description?: string | null;
  debitAmount: number;
  creditAmount: number;
}

interface LegacyLinkedRecord {
  id: string;
  sourceTable: string;
  classification: string;
  legacyTypeId?: number | null;
  dateValue01?: string | null;
  numberValue01?: number | null;
  accountName?: string | null;
}

interface LegacyArchiveResponse {
  appointments: LegacyAppointment[];
  treatments: LegacyTreatment[];
  financialEntries: LegacyFinancialEntry[];
  linkedRecords: LegacyLinkedRecord[];
  summary: {
    appointmentCards: number;
    treatmentLines: number;
    treatmentValue: number;
    financialEntryLines: number;
    debitTotal: number;
    creditTotal: number;
    unclassifiedLinkedRecords: number;
  };
}

const amount = (value: number) =>
  new Intl.NumberFormat("ar-YE", { maximumFractionDigits: 2 }).format(value);

const dateText = (value?: string | null) =>
  value ? new Date(value).toLocaleString("ar-YE") : "-";

export function LegacyArchiveTab({ patientId }: { patientId: string }) {
  const [data, setData] = useState<LegacyArchiveResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    setLoading(true);
    setError(false);
    api.get<LegacyArchiveResponse>(`/api/patients/${patientId}/legacy-archive`)
      .then((response) => setData(response.data))
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }, [patientId]);

  if (loading) {
    return <div className="flex justify-center py-12"><Loader2 className="h-6 w-6 animate-spin text-[#3d7ab5]" /></div>;
  }

  if (error || !data) {
    return <div className="py-12 text-center text-red-600">تعذر تحميل الأرشيف القديم حالياً</div>;
  }

  return (
    <div className="space-y-5" dir="rtl">
      <div className="flex gap-3 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
        <AlertTriangle className="mt-0.5 h-4 w-4 flex-shrink-0" />
        <span>بيانات الأرشيف المالي أدناه للمراجعة والمطابقة فقط، ولا تدخل تلقائياً في رصيد المريض الحالي أو المدفوعات.</span>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <SummaryCard label="مواعيد قديمة" value={String(data.summary.appointmentCards)} />
        <SummaryCard label="علاجات مؤرشفة" value={String(data.summary.treatmentLines)} />
        <SummaryCard label="قيمة خدمات قديمة" value={amount(data.summary.treatmentValue)} />
        <SummaryCard label="قيود مالية مرجعية" value={String(data.summary.financialEntryLines)} />
      </div>

      <section className="overflow-hidden rounded-lg border border-[#e8f0f9] bg-white">
        <SectionTitle icon={<CalendarDays className="h-4 w-4 text-[#3d7ab5]" />} text="بطاقات المواعيد القديمة" />
        {data.appointments.length === 0 ? (
          <Empty text="لا توجد مواعيد قديمة مؤرشفة لهذا المريض" />
        ) : (
          <Table headers={["التاريخ", "النوع", "البيان", "الملاحظات"]} rows={data.appointments.map((item) => [
            dateText(item.appointmentAt),
            item.archiveType ?? "موعد قديم",
            item.description ?? "-",
            item.notes ?? "-",
          ])} />
        )}
      </section>

      <section className="overflow-hidden rounded-lg border border-[#e8f0f9] bg-white">
        <SectionTitle icon={<Archive className="h-4 w-4 text-[#3d7ab5]" />} text="سجل العلاج القديم" />
        {data.treatments.length === 0 ? (
          <Empty text="لا توجد خدمات علاجية مؤرشفة" />
        ) : (
          <Table headers={["التاريخ", "الخدمة", "المستند", "القيمة"]} rows={data.treatments.map((item) => [
            dateText(item.treatmentDate),
            `${item.serviceName ?? item.description ?? "-"}${item.isOrthodonticService ? " (تقويم)" : ""}`,
            item.documentType ?? "-",
            amount(item.lineTotal),
          ])} />
        )}
      </section>

      <section className="overflow-hidden rounded-lg border border-[#e8f0f9] bg-white">
        <SectionTitle text="القيود المالية القديمة - مرجعية" />
        {data.financialEntries.length === 0 ? (
          <Empty text="لا توجد قيود مالية مرتبطة بهذا الملف" />
        ) : (
          <Table headers={["التاريخ", "الحساب", "مدين", "دائن"]} rows={data.financialEntries.map((item) => [
            dateText(item.entryDate),
            item.accountName ?? item.description ?? "-",
            amount(item.debitAmount),
            amount(item.creditAmount),
          ])} />
        )}
      </section>

      {data.linkedRecords.length > 0 && (
        <section className="overflow-hidden rounded-lg border border-[#e8f0f9] bg-white">
          <SectionTitle text="سجلات قديمة مرتبطة قيد التصنيف" />
          <p className="border-b border-[#e8f0f9] px-4 py-2 text-xs text-slate-500">
            محفوظة من النظام السابق للرجوع إليها فقط، وليست دفعة أو موعداً أو علاجاً فعالاً.
          </p>
          <Table headers={["المصدر", "التصنيف", "التاريخ", "الحساب", "القيمة"]} rows={data.linkedRecords.map((item) => [
            item.sourceTable,
            item.classification,
            dateText(item.dateValue01),
            item.accountName ?? "-",
            item.numberValue01 == null ? "-" : amount(item.numberValue01),
          ])} />
        </section>
      )}
    </div>
  );
}

function SummaryCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-[#e8f0f9] bg-white p-4">
      <div className="text-xs text-slate-500">{label}</div>
      <div className="mt-1 text-xl font-bold text-[#0d2137]">{value}</div>
    </div>
  );
}

function SectionTitle({ icon, text }: { icon?: ReactNode; text: string }) {
  return <h3 className="flex items-center gap-2 border-b border-[#e8f0f9] px-4 py-3 font-bold text-[#0d2137]">{icon}{text}</h3>;
}

function Empty({ text }: { text: string }) {
  return <p className="px-4 py-8 text-center text-sm text-slate-400">{text}</p>;
}

function Table({ headers, rows }: { headers: string[]; rows: string[][] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead className="bg-[#f7fafd] text-slate-500">
          <tr>{headers.map((header) => <th key={header} className="px-4 py-2 text-right">{header}</th>)}</tr>
        </thead>
        <tbody>
          {rows.map((row, index) => (
            <tr key={index} className="border-t border-slate-100">
              {row.map((value, cell) => <td key={cell} className="px-4 py-2">{value}</td>)}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
