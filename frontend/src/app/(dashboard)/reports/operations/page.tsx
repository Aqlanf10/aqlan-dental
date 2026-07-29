"use client";

import { useState } from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import {
  Activity,
  ArrowRight,
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  ClipboardList,
  Download,
  Loader2,
  RefreshCw,
  RotateCcw,
  Search,
  Users,
  Wallet,
} from "lucide-react";
import api, { downloadBlob } from "@/lib/api";
import { localDateString } from "@/lib/utils";
import { toast } from "@/stores/toastStore";

type ReportType =
  | "new-patients"
  | "treated-patients"
  | "income"
  | "outstanding-balances"
  | "treatment-progress"
  | "returning-patients"
  | "ortho-cases";

interface ReportColumn {
  key: string;
  label: string;
  kind: "text" | "date" | "number" | "money" | "currency" | "percent" | "boolean";
}

interface ReportSummary {
  label: string;
  value: string | number;
  currency?: string | null;
}

interface OperationalReport {
  title: string;
  fromDate: string;
  toDate: string;
  columns: ReportColumn[];
  rows: Record<string, unknown>[];
  summary: ReportSummary[];
  totalRows: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

const REPORTS: {
  type: ReportType;
  label: string;
  description: string;
  icon: typeof Users;
}[] = [
  { type: "new-patients", label: "المرضى الجدد", description: "المسجلون خلال الفترة", icon: Users },
  { type: "treated-patients", label: "من تم علاجهم", description: "تفاصيل الزيارات والعلاج", icon: Activity },
  { type: "income", label: "الدخل والتحصيل", description: "كل سند بعملته الأصلية", icon: Wallet },
  { type: "outstanding-balances", label: "الأرصدة المتبقية", description: "المطالب والمدفوع والمتبقي", icon: Wallet },
  { type: "treatment-progress", label: "تقدم العلاج", description: "المكتمل والخطوات المتبقية", icon: ClipboardList },
  { type: "returning-patients", label: "العائدون بعد انقطاع", description: "مدة الغياب وغرض العودة", icon: RotateCcw },
  { type: "ortho-cases", label: "حالات التقويم", description: "المرحلة والزيارات والرصيد", icon: Activity },
];

type Preset = "today" | "month" | "year";

function getPresetRange(preset: Preset): { from: string; to: string } {
  const today = localDateString();
  const date = new Date(`${today}T12:00:00`);
  if (preset === "today") return { from: today, to: today };
  if (preset === "month") {
    return {
      from: `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-01`,
      to: today,
    };
  }
  return { from: `${date.getFullYear()}-01-01`, to: today };
}

function formatCell(value: unknown, column: ReportColumn): string {
  if (value === null || value === undefined || value === "") return "—";
  if (column.kind === "boolean") return value ? "نعم" : "لا";
  if (column.kind === "percent") return `${Number(value).toLocaleString("ar-YE", { maximumFractionDigits: 1 })}%`;
  if (column.kind === "money" || column.kind === "number") {
    return Number(value).toLocaleString("ar-YE", { maximumFractionDigits: 2 });
  }
  return String(value);
}

function downloadFile(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = filename;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

export default function DetailedOperationsReportsPage() {
  const initialRange = getPresetRange("month");
  const [reportType, setReportType] = useState<ReportType>("new-patients");
  const [from, setFrom] = useState(initialRange.from);
  const [to, setTo] = useState(initialRange.to);
  const [absenceDays, setAbsenceDays] = useState(90);
  const [page, setPage] = useState(1);
  const [exporting, setExporting] = useState(false);

  const reportQuery = useQuery<OperationalReport>({
    queryKey: ["operational-report", reportType, from, to, absenceDays, page],
    queryFn: async () => {
      const response = await api.get<OperationalReport>("/api/reports/operations/details", {
        params: { type: reportType, from, to, absenceDays, page, pageSize: 50 },
      });
      return response.data;
    },
    enabled: Boolean(from && to && from <= to),
  });

  const chooseReport = (type: ReportType) => {
    setReportType(type);
    setPage(1);
  };

  const applyPreset = (preset: Preset) => {
    const range = getPresetRange(preset);
    setFrom(range.from);
    setTo(range.to);
    setPage(1);
  };

  const handleExport = async () => {
    setExporting(true);
    try {
      const blob = await downloadBlob("/api/reports/operations/export", {
        type: reportType,
        from,
        to,
        absenceDays,
      });
      downloadFile(blob, `${reportType}_${from}_${to}.csv`);
      toast.success("تم تجهيز ملف التقرير");
    } catch {
      toast.error("تعذر تصدير التقرير. حاول مرة أخرى.");
    } finally {
      setExporting(false);
    }
  };

  const data = reportQuery.data;
  const invalidRange = Boolean(from && to && from > to);

  return (
    <div className="max-w-[1500px] space-y-5" dir="rtl">
      <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <Link href="/reports" className="inline-flex items-center gap-1 text-sm text-clinic-blue hover:underline">
            <ArrowRight className="h-4 w-4" />
            العودة إلى ملخص التقارير
          </Link>
          <h1 className="mt-2 text-2xl font-extrabold text-gray-900">التقارير التفصيلية</h1>
          <p className="mt-1 text-sm text-gray-500">
            اختر نوع التقرير والفترة، ثم راجع الصفوف الفعلية أو صدّرها إلى CSV.
          </p>
        </div>
        <button
          type="button"
          onClick={handleExport}
          disabled={!data || data.totalRows === 0 || exporting || invalidRange}
          className="inline-flex items-center justify-center gap-2 rounded-lg bg-emerald-600 px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-emerald-700 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {exporting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Download className="h-4 w-4" />}
          تصدير CSV
        </button>
      </header>

      <section className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {REPORTS.map((report) => {
          const Icon = report.icon;
          const active = reportType === report.type;
          return (
            <button
              key={report.type}
              type="button"
              onClick={() => chooseReport(report.type)}
              aria-pressed={active}
              className={`rounded-xl border p-4 text-start transition ${
                active
                  ? "border-clinic-blue bg-clinic-blue-50 shadow-sm"
                  : "border-gray-200 bg-white hover:border-clinic-blue-200 hover:bg-gray-50"
              }`}
            >
              <Icon className={`mb-3 h-5 w-5 ${active ? "text-clinic-blue" : "text-gray-400"}`} />
              <p className={`font-bold ${active ? "text-clinic-blue" : "text-gray-900"}`}>{report.label}</p>
              <p className="mt-1 text-xs text-gray-500">{report.description}</p>
            </button>
          );
        })}
      </section>

      <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-end gap-3">
          <div className="flex gap-2" role="group" aria-label="فترات سريعة">
            {([
              ["today", "اليوم"],
              ["month", "هذا الشهر"],
              ["year", "هذه السنة"],
            ] as const).map(([preset, label]) => (
              <button
                key={preset}
                type="button"
                onClick={() => applyPreset(preset)}
                className="rounded-lg border border-gray-200 px-3 py-2 text-sm text-gray-700 hover:border-clinic-blue hover:text-clinic-blue"
              >
                {label}
              </button>
            ))}
          </div>

          <label className="text-xs font-medium text-gray-600">
            من
            <input
              type="date"
              value={from}
              onChange={(event) => {
                setFrom(event.target.value);
                setPage(1);
              }}
              className="mt-1 block rounded-lg border border-gray-200 px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-medium text-gray-600">
            إلى
            <input
              type="date"
              value={to}
              onChange={(event) => {
                setTo(event.target.value);
                setPage(1);
              }}
              className="mt-1 block rounded-lg border border-gray-200 px-3 py-2 text-sm"
            />
          </label>

          {reportType === "returning-patients" ? (
            <label className="text-xs font-medium text-gray-600">
              أقل مدة انقطاع
              <div className="mt-1 flex items-center gap-2">
                <input
                  type="number"
                  min={1}
                  max={3650}
                  value={absenceDays}
                  onChange={(event) => {
                    setAbsenceDays(Math.max(1, Number(event.target.value) || 1));
                    setPage(1);
                  }}
                  className="w-28 rounded-lg border border-gray-200 px-3 py-2 text-sm"
                />
                <span className="text-sm text-gray-500">يومًا</span>
              </div>
            </label>
          ) : null}

          <button
            type="button"
            onClick={() => reportQuery.refetch()}
            disabled={reportQuery.isFetching || invalidRange}
            className="inline-flex items-center gap-2 rounded-lg border border-gray-200 px-3 py-2 text-sm text-gray-700 hover:bg-gray-50 disabled:opacity-50"
          >
            <RefreshCw className={`h-4 w-4 ${reportQuery.isFetching ? "animate-spin" : ""}`} />
            تحديث
          </button>
        </div>
        {invalidRange ? <p className="mt-3 text-sm text-red-600">تاريخ البداية يجب أن يسبق تاريخ النهاية.</p> : null}
      </section>

      {data?.summary.length ? (
        <section className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
          {data.summary.map((item) => (
            <div key={`${item.label}-${item.currency ?? ""}`} className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
              <p className="text-xs font-medium text-gray-500">{item.label}</p>
              <p className="mt-2 text-2xl font-extrabold text-gray-900 font-mono" dir="ltr">
                {typeof item.value === "number"
                  ? item.value.toLocaleString("ar-YE", { maximumFractionDigits: 2 })
                  : item.value}
                {item.currency ? ` ${item.currency}` : ""}
              </p>
            </div>
          ))}
        </section>
      ) : null}

      <section className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
        <div className="flex items-center justify-between border-b border-gray-100 px-4 py-3">
          <div>
            <h2 className="font-bold text-gray-900">{data?.title ?? "تفاصيل التقرير"}</h2>
            <p className="mt-0.5 text-xs text-gray-500">
              {data ? `${data.totalRows.toLocaleString("ar-YE")} سجل` : "يتم تحميل البيانات"}
            </p>
          </div>
          <CalendarDays className="h-5 w-5 text-gray-300" />
        </div>

        {reportQuery.isLoading ? (
          <div className="flex min-h-64 items-center justify-center text-gray-500">
            <Loader2 className="ms-2 h-5 w-5 animate-spin" />
            جارٍ تجهيز التقرير...
          </div>
        ) : reportQuery.isError ? (
          <div className="flex min-h-64 flex-col items-center justify-center gap-3 p-6 text-center">
            <p className="font-semibold text-red-600">تعذر تحميل التقرير</p>
            <p className="text-sm text-gray-500">تحقق من الاتصال ثم أعد المحاولة.</p>
            <button
              type="button"
              onClick={() => reportQuery.refetch()}
              className="rounded-lg bg-clinic-blue px-4 py-2 text-sm font-semibold text-white"
            >
              إعادة المحاولة
            </button>
          </div>
        ) : data && data.rows.length > 0 ? (
          <>
            <div className="overflow-x-auto">
              <table className="min-w-full text-sm">
                <thead className="bg-gray-50 text-xs text-gray-500">
                  <tr>
                    {data.columns.map((column) => (
                      <th key={column.key} scope="col" className="whitespace-nowrap px-4 py-3 text-start font-semibold">
                        {column.label}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {data.rows.map((row, rowIndex) => (
                    <tr
                      key={[
                        row.visitId ?? row.paymentId ?? row.caseId ?? row.patientId ?? "row",
                        row.currency ?? "",
                        rowIndex,
                      ].join("-")}
                      className="hover:bg-gray-50"
                    >
                      {data.columns.map((column) => {
                        const content = formatCell(row[column.key], column);
                        const isNumber = ["money", "number", "percent"].includes(column.kind);
                        return (
                          <td
                            key={column.key}
                            className={`max-w-sm whitespace-nowrap px-4 py-3 text-gray-700 ${isNumber ? "font-mono" : ""}`}
                            dir={isNumber ? "ltr" : undefined}
                          >
                            {column.key === "patientName" && row.patientId ? (
                              <Link href={`/patients/${row.patientId}`} className="font-semibold text-clinic-blue hover:underline">
                                {content}
                              </Link>
                            ) : (
                              content
                            )}
                          </td>
                        );
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="flex flex-col gap-3 border-t border-gray-100 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
              <p className="text-xs text-gray-500">
                صفحة {data.page.toLocaleString("ar-YE")} من {data.totalPages.toLocaleString("ar-YE")}
              </p>
              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={() => setPage((current) => Math.max(1, current - 1))}
                  disabled={data.page <= 1 || reportQuery.isFetching}
                  aria-label="الصفحة السابقة"
                  className="rounded-lg border border-gray-200 p-2 text-gray-600 hover:bg-gray-50 disabled:opacity-40"
                >
                  <ChevronRight className="h-4 w-4" />
                </button>
                <button
                  type="button"
                  onClick={() => setPage((current) => Math.min(data.totalPages, current + 1))}
                  disabled={data.page >= data.totalPages || reportQuery.isFetching}
                  aria-label="الصفحة التالية"
                  className="rounded-lg border border-gray-200 p-2 text-gray-600 hover:bg-gray-50 disabled:opacity-40"
                >
                  <ChevronLeft className="h-4 w-4" />
                </button>
              </div>
            </div>
          </>
        ) : (
          <div className="flex min-h-64 flex-col items-center justify-center gap-2 p-6 text-center">
            <Search className="h-9 w-9 text-gray-200" />
            <p className="font-semibold text-gray-700">لا توجد نتائج في هذه الفترة</p>
            <p className="text-sm text-gray-500">جرّب فترة أخرى أو نوع تقرير مختلف.</p>
          </div>
        )}
      </section>
    </div>
  );
}
