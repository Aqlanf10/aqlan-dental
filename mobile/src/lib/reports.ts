export type CenterSummaryReport = {
  fromDate: string;
  toDate: string;
  totalPatients: number;
  newPatients: number;
  totalAppointments: number;
  completedAppointments: number;
  activeOrthoCases: number;
  totalRevenue: number;
};

export type FinancialCurrencyTotal = {
  currency: string;
  collected: number;
  expenses: number;
  refunds: number;
  supplierPayments: number;
  salaryAdvances: number;
  net: number;
};

export type FinancialReport = {
  fromDate: string;
  toDate: string;
  totalsByCurrency: FinancialCurrencyTotal[];
  daily: Array<{ date: string; currency: string; total: number; count: number }>;
  bySpecialty: Array<{ specialty: string; currency: string; total: number; count: number }>;
  byMethod: Array<{ method: string; currency: string; total: number }>;
};

export type AppointmentAnalyticsReport = {
  fromDate: string;
  toDate: string;
  totalAppointments: number;
  statusDistribution: Array<{ status: string; count: number }>;
  peakHours: Array<{ hour: number; label: string; count: number }>;
  averagePerDay: number;
  noShowRate: number;
  cancellationRate: number;
  completionRate: number;
  byType: Array<{ type: string; count: number }>;
};

export type DoctorPerformanceRow = {
  doctorId: string;
  name: string;
  color?: string | null;
  specialty?: string | null;
  appointmentCount: number;
  completedCount: number;
  orthoCasesCount: number;
  treatmentsCount: number;
  revenue: number;
};

export type OperationalReportColumn = {
  key: string;
  label: string;
  kind: string;
};

export type OperationalReportSummary = {
  label: string;
  value: string | number | boolean | null;
  currency?: string | null;
};

export type OperationalReportPage = {
  title: string;
  fromDate: string;
  toDate: string;
  columns: OperationalReportColumn[];
  rows: Array<Record<string, unknown>>;
  summary: OperationalReportSummary[];
  totalRows: number;
  page: number;
  pageSize: number;
  totalPages: number;
};

export type OperationalReportType =
  | "new-patients"
  | "treated-patients"
  | "income"
  | "outstanding-balances"
  | "treatment-progress"
  | "returning-patients"
  | "ortho-cases";

export const OPERATIONAL_REPORT_OPTIONS: Array<{ value: OperationalReportType; label: string }> = [
  { value: "new-patients", label: "المرضى الجدد" },
  { value: "treated-patients", label: "المرضى المعالجون" },
  { value: "income", label: "الدخل والتحصيل التفصيلي" },
  { value: "outstanding-balances", label: "الأرصدة المستحقة" },
  { value: "treatment-progress", label: "تقدم العلاج" },
  { value: "returning-patients", label: "المرضى العائدون" },
  { value: "ortho-cases", label: "حالات التقويم" }
];

export function formatReportValue(value: unknown, kind?: string, currency?: string | null): string {
  if (value === null || value === undefined || value === "") return "—";
  if (typeof value === "boolean") return value ? "نعم" : "لا";
  if (typeof value === "number") {
    const formatted = value.toLocaleString("ar-YE", { maximumFractionDigits: 2 });
    return kind === "percent" ? `${formatted}%` : currency ? `${formatted} ${currency}` : formatted;
  }
  return String(value);
}

export function appointmentStatusArabic(status: string): string {
  const map: Record<string, string> = {
    Scheduled: "مجدول",
    Confirmed: "مؤكد",
    Arrived: "وصل",
    Waiting: "في الانتظار",
    Called: "تم النداء",
    InRoom: "داخل الغرفة",
    InProgress: "قيد العلاج",
    Completed: "مكتمل",
    Cancelled: "ملغي",
    NoShow: "لم يحضر"
  };
  return map[status] ?? status;
}
