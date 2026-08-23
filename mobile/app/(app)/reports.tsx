import { useSession } from "@/auth/SessionProvider";
import { FormField } from "@/components/forms";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { isoDateLocal } from "@/lib/format";
import {
  appointmentStatusArabic,
  formatReportValue,
  OPERATIONAL_REPORT_OPTIONS,
  type AppointmentAnalyticsReport,
  type CenterSummaryReport,
  type DoctorPerformanceRow,
  type FinancialReport,
  type OperationalReportPage,
  type OperationalReportType
} from "@/lib/reports";
import { colors, radius, spacing } from "@/theme";
import { useFocusEffect } from "expo-router";
import React, { useCallback, useMemo, useState } from "react";
import { ActivityIndicator, Pressable, RefreshControl, StyleSheet, Text, View } from "react-native";

type RangePreset = 7 | 30 | 90;

export default function ReportsScreen() {
  const { can } = useSession();
  const allowed = can("reports.view");
  const today = useMemo(() => isoDateLocal(new Date()), []);
  const [preset, setPreset] = useState<RangePreset>(30);
  const [from, setFrom] = useState(() => dateDaysAgo(29));
  const [to, setTo] = useState(today);
  const [summary, setSummary] = useState<CenterSummaryReport | null>(null);
  const [financial, setFinancial] = useState<FinancialReport | null>(null);
  const [appointments, setAppointments] = useState<AppointmentAnalyticsReport | null>(null);
  const [doctors, setDoctors] = useState<DoctorPerformanceRow[]>([]);
  const [operationalType, setOperationalType] = useState<OperationalReportType>("income");
  const [operational, setOperational] = useState<OperationalReportPage | null>(null);
  const [loading, setLoading] = useState(true);
  const [operationalLoading, setOperationalLoading] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [operationalError, setOperationalError] = useState<string | null>(null);

  const validRange = /^\d{4}-\d{2}-\d{2}$/.test(from) && /^\d{4}-\d{2}-\d{2}$/.test(to) && from <= to;

  const loadManagement = useCallback(async () => {
    if (!allowed || !validRange) {
      setLoading(false);
      return;
    }
    setError(null);
    const query = `from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`;
    const [summaryResult, financialResult, appointmentsResult, doctorsResult] = await Promise.allSettled([
      apiRequest<CenterSummaryReport>(`/api/reports/center-summary?${query}`),
      apiRequest<FinancialReport>(`/api/reports/financial?${query}`),
      apiRequest<AppointmentAnalyticsReport>(`/api/reports/appointment-analytics?${query}`),
      apiRequest<DoctorPerformanceRow[]>(`/api/reports/doctor-performance?${query}`)
    ]);

    if (summaryResult.status === "fulfilled") setSummary(summaryResult.value); else setSummary(null);
    if (financialResult.status === "fulfilled") setFinancial(financialResult.value); else setFinancial(null);
    if (appointmentsResult.status === "fulfilled") setAppointments(appointmentsResult.value); else setAppointments(null);
    if (doctorsResult.status === "fulfilled") setDoctors(doctorsResult.value ?? []); else setDoctors([]);

    const failures = [summaryResult, financialResult, appointmentsResult, doctorsResult]
      .filter((result) => result.status === "rejected") as PromiseRejectedResult[];
    const firstFailure = failures[0];
    if (firstFailure) {
      setError(firstFailure.reason instanceof Error ? firstFailure.reason.message : "تعذر تحميل بعض التقارير");
    }
    setLoading(false);
  }, [allowed, from, to, validRange]);

  const loadOperational = useCallback(async () => {
    if (!allowed || !validRange) return;
    setOperationalLoading(true);
    setOperationalError(null);
    try {
      const query = new URLSearchParams({
        type: operationalType,
        from,
        to,
        page: "1",
        pageSize: "100"
      });
      setOperational(await apiRequest<OperationalReportPage>(`/api/reports/operations/details?${query.toString()}`));
    } catch (err) {
      setOperational(null);
      setOperationalError(err instanceof Error ? err.message : "تعذر تحميل التقرير التشغيلي");
    } finally {
      setOperationalLoading(false);
    }
  }, [allowed, from, operationalType, to, validRange]);

  useFocusEffect(
    useCallback(() => {
      setLoading(true);
      void loadManagement();
      void loadOperational();
    }, [loadManagement, loadOperational])
  );

  function choosePreset(days: RangePreset) {
    setPreset(days);
    setFrom(dateDaysAgo(days - 1));
    setTo(isoDateLocal(new Date()));
  }

  async function applyRange() {
    if (!validRange) return setError("أدخل تاريخ بداية ونهاية بصيغة YYYY-MM-DD، ويجب ألا يكون تاريخ البداية بعد النهاية.");
    setLoading(true);
    await Promise.all([loadManagement(), loadOperational()]);
  }

  async function refresh() {
    setRefreshing(true);
    try { await Promise.all([loadManagement(), loadOperational()]); } finally { setRefreshing(false); }
  }

  if (!allowed) {
    return <Screen><StateMessage title="غير مصرح" message="حسابك لا يملك صلاحية reports.view / ReportsAccess." /></Screen>;
  }

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      <View>
        <Text style={styles.title}>التقارير والإدارة</Text>
        <Text style={styles.subtitle}>مؤشرات المركز والتشغيل والتحصيل بدون خلط العملات</Text>
      </View>

      <View style={styles.presets}>
        {([7, 30, 90] as RangePreset[]).map((days) => (
          <Pressable key={days} onPress={() => choosePreset(days)} style={[styles.preset, preset === days && styles.presetActive]}>
            <Text style={[styles.presetText, preset === days && styles.presetTextActive]}>{days} يوم</Text>
          </Pressable>
        ))}
      </View>
      <Card>
        <View style={styles.form}>
          <FormField label="من YYYY-MM-DD" value={from} onChangeText={(value) => { setPreset(30); setFrom(value); }} />
          <FormField label="إلى YYYY-MM-DD" value={to} onChangeText={(value) => { setPreset(30); setTo(value); }} />
          <PrimaryButton title="تطبيق الفترة" onPress={() => void applyRange()} />
        </View>
      </Card>

      {error ? <StateMessage title="تعذر تحميل بعض المؤشرات" message={error} /> : null}
      {loading ? <ActivityIndicator size="large" color={colors.primary} /> : null}

      {summary ? (
        <>
          <SectionTitle>ملخص المركز</SectionTitle>
          <View style={styles.metrics}>
            <Metric label="إجمالي المرضى" value={summary.totalPatients} />
            <Metric label="مرضى جدد" value={summary.newPatients} />
            <Metric label="المواعيد" value={summary.totalAppointments} />
            <Metric label="مواعيد مكتملة" value={summary.completedAppointments} />
            <Metric label="حالات تقويم نشطة" value={summary.activeOrthoCases} />
            <Metric label="تحصيل YER فقط" value={`${summary.totalRevenue.toLocaleString()} YER`} wide />
          </View>
          <Text style={styles.disclaimer}>قيمة التحصيل في center-summary هي YER فقط حسب عقد الخادم. تفصيل كل العملات يظهر أدناه.</Text>
        </>
      ) : null}

      {financial ? (
        <>
          <SectionTitle>الملخص المالي حسب العملة</SectionTitle>
          {financial.totalsByCurrency.length === 0 ? <StateMessage title="لا توجد حركة مالية في الفترة" /> : null}
          {financial.totalsByCurrency.map((row) => (
            <Card key={row.currency}>
              <Text style={styles.currencyTitle}>{row.currency}</Text>
              <Row label="المقبوض" value={money(row.collected, row.currency)} />
              <Row label="مصروفات تشغيلية" value={money(row.expenses, row.currency)} />
              <Row label="مرتجعات" value={money(row.refunds, row.currency)} />
              <Row label="مدفوعات موردين" value={money(row.supplierPayments, row.currency)} />
              <Row label="سلف رواتب" value={money(row.salaryAdvances, row.currency)} />
              <Row label="الصافي" value={money(row.net, row.currency)} strong last />
            </Card>
          ))}
          <Text style={styles.disclaimer}>لا يتم جمع YER وSAR وUSD في رقم واحد؛ كل عملة معروضة مستقلة كما يعيدها الـBackend.</Text>
        </>
      ) : null}

      {appointments ? (
        <>
          <SectionTitle>تحليل المواعيد</SectionTitle>
          <View style={styles.metrics}>
            <Metric label="الإجمالي" value={appointments.totalAppointments} />
            <Metric label="المتوسط/يوم" value={appointments.averagePerDay} />
            <Metric label="نسبة الإنجاز" value={`${appointments.completionRate}%`} />
            <Metric label="عدم الحضور" value={`${appointments.noShowRate}%`} />
            <Metric label="الإلغاء" value={`${appointments.cancellationRate}%`} wide />
          </View>
          {appointments.statusDistribution.length ? (
            <Card>
              {appointments.statusDistribution.map((entry, index) => (
                <Row key={entry.status} label={appointmentStatusArabic(entry.status)} value={String(entry.count)} last={index === appointments.statusDistribution.length - 1} />
              ))}
            </Card>
          ) : null}
          {appointments.peakHours.length ? (
            <Card>
              <Text style={styles.cardTitle}>أكثر ساعات المواعيد</Text>
              {appointments.peakHours.slice().sort((a, b) => b.count - a.count).slice(0, 5).map((entry, index, list) => (
                <Row key={entry.hour} label={entry.label} value={String(entry.count)} last={index === list.length - 1} />
              ))}
            </Card>
          ) : null}
        </>
      ) : null}

      {doctors.length ? (
        <>
          <SectionTitle>أداء الأطباء</SectionTitle>
          {doctors.slice().sort((a, b) => b.completedCount - a.completedCount).map((doctor) => (
            <Card key={doctor.doctorId}>
              <Text style={styles.cardTitle}>{doctor.name}</Text>
              <Text style={styles.meta}>{doctor.specialty || "—"}</Text>
              <Row label="المواعيد" value={String(doctor.appointmentCount)} />
              <Row label="المكتملة" value={String(doctor.completedCount)} />
              <Row label="علاجات عامة" value={String(doctor.treatmentsCount)} />
              <Row label="حالات تقويم نشطة" value={String(doctor.orthoCasesCount)} />
              <Row label="تحصيل منسوب للطبيب (YER فقط)" value={money(doctor.revenue, "YER")} last />
            </Card>
          ))}
        </>
      ) : null}

      <SectionTitle>التقارير التشغيلية التفصيلية</SectionTitle>
      <View style={styles.reportTypes}>
        {OPERATIONAL_REPORT_OPTIONS.map((option) => (
          <Pressable
            key={option.value}
            onPress={() => setOperationalType(option.value)}
            style={[styles.reportType, operationalType === option.value && styles.reportTypeActive]}
          >
            <Text style={[styles.reportTypeText, operationalType === option.value && styles.reportTypeTextActive]}>{option.label}</Text>
          </Pressable>
        ))}
      </View>
      <PrimaryButton title="تحميل التقرير المختار" loading={operationalLoading} onPress={() => void loadOperational()} />
      {operationalError ? <StateMessage title="تعذر تحميل التقرير" message={operationalError} /> : null}
      {operationalLoading ? <ActivityIndicator color={colors.primary} /> : null}
      {operational ? <OperationalReport report={operational} /> : null}
    </Screen>
  );
}

function OperationalReport({ report }: { report: OperationalReportPage }) {
  return (
    <View style={styles.reportBlock}>
      <Card>
        <Text style={styles.cardTitle}>{report.title}</Text>
        <Text style={styles.meta}>{report.fromDate} — {report.toDate} • {report.totalRows} صف</Text>
        {report.summary.map((item, index) => (
          <Row
            key={`${item.label}-${index}`}
            label={item.label}
            value={formatReportValue(item.value, undefined, item.currency)}
            last={index === report.summary.length - 1}
          />
        ))}
      </Card>

      {report.rows.length === 0 ? <StateMessage title="لا توجد نتائج لهذا التقرير ضمن الفترة" /> : null}
      {report.rows.map((row, rowIndex) => (
        <Card key={rowIndex}>
          {report.columns.map((column, columnIndex) => {
            const currency = column.kind === "money"
              ? inferRowCurrency(row, column.key)
              : null;
            return (
              <Row
                key={column.key}
                label={column.label}
                value={formatReportValue(row[column.key], column.kind, currency)}
                last={columnIndex === report.columns.length - 1}
              />
            );
          })}
        </Card>
      ))}
      {report.totalPages > 1 ? <Text style={styles.disclaimer}>يعرض الهاتف أول {report.pageSize} صف من {report.totalRows}. التصدير الكامل يبقى متاحًا في النظام الرئيسي.</Text> : null}
    </View>
  );
}

function inferRowCurrency(row: Record<string, unknown>, key: string): string | null {
  const candidate = key === "appliedAmount" ? row.accountCurrency : row.currency;
  return typeof candidate === "string" && candidate.trim() ? candidate.toUpperCase() : null;
}

function Metric({ label, value, wide = false }: { label: string; value: string | number; wide?: boolean }) {
  return <View style={[styles.metric, wide && styles.metricWide]}><Text style={styles.metricValue}>{value}</Text><Text style={styles.metricLabel}>{label}</Text></View>;
}

function Row({ label, value, strong = false, last = false }: { label: string; value: string; strong?: boolean; last?: boolean }) {
  return <View style={[styles.row, last && { borderBottomWidth: 0 }]}><Text style={[styles.rowValue, strong && styles.strong]}>{value}</Text><Text style={styles.rowLabel}>{label}</Text></View>;
}

function dateDaysAgo(days: number): string {
  const date = new Date();
  date.setDate(date.getDate() - days);
  return isoDateLocal(date);
}

function money(value: number, currency: string): string {
  return `${value.toLocaleString("ar-YE", { maximumFractionDigits: 2 })} ${currency}`;
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 26, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.muted, marginTop: 4, textAlign: "right" },
  presets: { flexDirection: "row-reverse", gap: spacing.sm },
  preset: { flex: 1, borderWidth: 1, borderColor: colors.border, borderRadius: radius.sm, paddingVertical: spacing.sm, alignItems: "center", backgroundColor: colors.surface },
  presetActive: { borderColor: colors.primary, backgroundColor: colors.primarySoft },
  presetText: { color: colors.text, fontWeight: "700" },
  presetTextActive: { color: colors.primary },
  form: { gap: spacing.md },
  metrics: { flexDirection: "row-reverse", flexWrap: "wrap", gap: spacing.sm },
  metric: { width: "48%", minHeight: 90, borderWidth: 1, borderColor: colors.border, borderRadius: radius.md, padding: spacing.md, backgroundColor: colors.surface, justifyContent: "center" },
  metricWide: { width: "100%" },
  metricValue: { color: colors.primary, fontSize: 21, fontWeight: "900", textAlign: "right" },
  metricLabel: { color: colors.muted, marginTop: 4, textAlign: "right" },
  disclaimer: { color: colors.muted, fontSize: 12, lineHeight: 20, textAlign: "right" },
  currencyTitle: { color: colors.primary, fontSize: 20, fontWeight: "900", textAlign: "right", marginBottom: spacing.sm },
  cardTitle: { color: colors.text, fontSize: 17, fontWeight: "800", textAlign: "right" },
  meta: { color: colors.muted, marginTop: 4, marginBottom: spacing.sm, textAlign: "right", fontSize: 12 },
  row: { minHeight: 43, flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.border },
  rowLabel: { color: colors.muted, textAlign: "right", flexShrink: 1 },
  rowValue: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" },
  strong: { color: colors.primary, fontWeight: "900" },
  reportTypes: { flexDirection: "row-reverse", flexWrap: "wrap", gap: spacing.xs },
  reportType: { borderWidth: 1, borderColor: colors.border, borderRadius: 999, paddingHorizontal: spacing.sm, paddingVertical: 7, backgroundColor: colors.surface },
  reportTypeActive: { borderColor: colors.primary, backgroundColor: colors.primarySoft },
  reportTypeText: { color: colors.muted, fontWeight: "700", fontSize: 12 },
  reportTypeTextActive: { color: colors.primary },
  reportBlock: { gap: spacing.sm }
});
