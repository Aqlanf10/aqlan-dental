import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { formatYemeniRial } from "@/lib/format";
import type { DashboardAlerts, DashboardStats } from "@/lib/types";
import { colors, radius, spacing } from "@/theme";
import { router } from "expo-router";
import React, { useCallback, useEffect, useState } from "react";
import { RefreshControl, StyleSheet, Text, View } from "react-native";

export default function DashboardScreen() {
  const { user, can } = useSession();
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [alerts, setAlerts] = useState<DashboardAlerts | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async () => {
    setError(null);
    try {
      const [statsResponse, alertsResponse] = await Promise.all([
        apiRequest<DashboardStats>("/api/dashboard/stats"),
        apiRequest<DashboardAlerts>("/api/dashboard/alerts")
      ]);
      setStats(statsResponse);
      setAlerts(alertsResponse);
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحميل لوحة التحكم");
    }
  }, []);

  useEffect(() => { void load(); }, [load]);

  async function refresh() {
    setRefreshing(true);
    try { await load(); } finally { setRefreshing(false); }
  }

  const canSeeFinance = user?.role === "Admin" || user?.role === "Accountant" || user?.role === "Reception";
  const isAdmin = user?.role === "Admin";
  const canViewReports = can("reports.view");

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      <View>
        <Text style={styles.greeting}>مرحباً، {user?.doctorName || user?.username}</Text>
        <Text style={styles.role}>{user?.role}</Text>
      </View>

      <SectionTitle>تشغيل العيادة</SectionTitle>
      <PrimaryButton title="فتح تشغيل اليوم" onPress={() => router.push("/(app)/journey")} />

      {isAdmin || canViewReports ? (
        <>
          <SectionTitle>الإدارة</SectionTitle>
          {canViewReports ? <PrimaryButton title="التقارير والإدارة" onPress={() => router.push("/(app)/reports")} /> : null}
          {isAdmin ? <PrimaryButton title="إدارة المخزون" onPress={() => router.push("/(app)/inventory")} /> : null}
        </>
      ) : null}

      <SectionTitle>نظرة سريعة</SectionTitle>
      {error && !stats ? <StateMessage title="تعذر تحميل لوحة التحكم" message={error} /> : null}

      {stats ? (
        <View style={styles.grid}>
          <Metric title="مواعيد اليوم" value={stats.appointmentsToday} />
          <Metric title="وصلوا اليوم" value={stats.todayArrivedCount} />
          <Metric title="في الانتظار" value={stats.queueWaitingCount} />
          <Metric title="إجمالي المرضى" value={stats.totalPatients} />
          <Metric title="حالات التقويم" value={stats.activeOrthoCases} />
          <Metric title="أعمال المعمل" value={stats.pendingLabOrders} />
          {canSeeFinance ? <Metric title="إيراد الشهر" value={formatYemeniRial(stats.totalRevenueMTD)} wide /> : null}
        </View>
      ) : null}

      <SectionTitle>يحتاج انتباهك</SectionTitle>
      {alerts ? (
        <Card>
          <AlertRow label="تراكيب متأخرة" value={alerts.overdueLabOrdersCount} />
          <AlertRow label="غياب اليوم" value={alerts.todayNoShowCount} />
          <AlertRow label="انتظار طويل" value={alerts.longWaitingCount} />
          <AlertRow label="غير مؤكدة غداً" value={alerts.unconfirmedTomorrowCount} />
          <AlertRow label="مرشحون للاستدعاء" value={alerts.recallCandidatesCount} last />
        </Card>
      ) : (
        <Card><Text style={styles.muted}>جارٍ تحميل التنبيهات…</Text></Card>
      )}

      <SectionTitle>التواصل</SectionTitle>
      <PrimaryButton title="فتح الإشعارات" onPress={() => router.push("/(app)/notifications")} />
    </Screen>
  );
}

function Metric({ title, value, wide = false }: { title: string; value: string | number; wide?: boolean }) {
  return <View style={[styles.metric, wide && styles.metricWide]}><Text style={styles.metricValue}>{value}</Text><Text style={styles.metricTitle}>{title}</Text></View>;
}

function AlertRow({ label, value, last = false }: { label: string; value: number; last?: boolean }) {
  return <View style={[styles.alertRow, last && { borderBottomWidth: 0 }]}><Text style={[styles.alertValue, value > 0 && styles.alertActive]}>{value}</Text><Text style={styles.alertLabel}>{label}</Text></View>;
}

const styles = StyleSheet.create({
  greeting: { color: colors.text, fontSize: 24, fontWeight: "800", textAlign: "right" },
  role: { color: colors.muted, fontSize: 13, marginTop: 4, textAlign: "right" },
  grid: { flexDirection: "row-reverse", flexWrap: "wrap", gap: spacing.sm },
  metric: { width: "48%", minHeight: 104, backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border, borderRadius: radius.md, padding: spacing.md, justifyContent: "center" },
  metricWide: { width: "100%" },
  metricValue: { color: colors.primary, fontSize: 24, fontWeight: "800", textAlign: "right" },
  metricTitle: { color: colors.muted, marginTop: spacing.xs, textAlign: "right" },
  alertRow: { minHeight: 48, flexDirection: "row", alignItems: "center", justifyContent: "space-between", borderBottomWidth: 1, borderBottomColor: colors.border },
  alertValue: { color: colors.muted, fontWeight: "800" },
  alertActive: { color: colors.danger },
  alertLabel: { color: colors.text, textAlign: "right" },
  muted: { color: colors.muted, textAlign: "right" }
});
