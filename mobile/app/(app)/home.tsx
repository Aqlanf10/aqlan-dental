import { useSession } from "@/auth/SessionProvider";
import { useClinicBranding } from "@/brand";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { formatYemeniRial } from "@/lib/format";
import type { DashboardAlerts, DashboardStats } from "@/lib/types";
import { colors, radius, shadow, spacing } from "@/theme";
import { router } from "expo-router";
import { StatusBar } from "expo-status-bar";
import React, { useCallback, useEffect, useState } from "react";
import { Image, Pressable, RefreshControl, StyleSheet, Text, View } from "react-native";

export default function DashboardScreen() {
  const { user, can } = useSession();
  const brand = useClinicBranding();
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
      <StatusBar style="dark" />
      <View style={styles.hero}>
        <View pointerEvents="none" style={styles.heroOrb} />
        <View style={styles.heroTop}>
          <View style={styles.logoTile}>
            <Image source={require("../../assets/logo-white.png")} resizeMode="contain" style={styles.logo} />
          </View>
          <View style={styles.heroCopy}>
            <Text style={styles.heroEyebrow}>مساحة عمل المركز</Text>
            <Text accessibilityRole="header" style={styles.greeting}>مرحباً، {user?.doctorName || user?.username}</Text>
            <View style={styles.rolePill}><Text style={styles.role}>{roleLabel(user?.role)}</Text></View>
          </View>
        </View>
        <View style={styles.heroDivider} />
        <Text numberOfLines={2} style={styles.clinicName}>{brand.clinicName}</Text>
        <Text style={styles.clinicLocation}>{brand.address}</Text>
      </View>

      <View style={styles.statusStrip}>
        <View style={styles.liveDot} />
        <Text style={styles.statusText}>متصل بنظام العيادة · اسحب لأسفل لتحديث البيانات</Text>
      </View>

      <SectionTitle>الوصول السريع</SectionTitle>
      <View style={styles.actionsGrid}>
        <QuickAction badge="ي" title="تشغيل اليوم" subtitle="الحضور والطابور" onPress={() => router.push("/(app)/journey")} emphasized />
        <QuickAction badge="م" title="المرضى" subtitle="بحث وملفات" onPress={() => router.push("/(app)/patients")} />
        <QuickAction badge="ع" title="المواعيد" subtitle="جدول اليوم" onPress={() => router.push("/(app)/appointments")} />
        <QuickAction badge="ر" title="الرسائل" subtitle="التواصل الداخلي" onPress={() => router.push("/(app)/messages")} />
      </View>

      <SectionTitle>مؤشرات اليوم</SectionTitle>
      {error && !stats ? <StateMessage title="تعذر تحميل لوحة التحكم" message={error} /> : null}

      {stats ? (
        <View style={styles.grid}>
          <Metric title="مواعيد اليوم" value={stats.appointmentsToday} tone="blue" />
          <Metric title="وصلوا اليوم" value={stats.todayArrivedCount} tone="green" />
          <Metric title="في الانتظار" value={stats.queueWaitingCount} tone="orange" />
          <Metric title="إجمالي المرضى" value={stats.totalPatients} />
          <Metric title="حالات التقويم" value={stats.activeOrthoCases} />
          <Metric title="أعمال المعمل" value={stats.pendingLabOrders} tone="orange" />
          {canSeeFinance ? <Metric title="إيراد الشهر" value={formatYemeniRial(stats.totalRevenueMTD)} wide tone="green" /> : null}
        </View>
      ) : !error ? <Card><Text style={styles.muted}>جارٍ تحميل مؤشرات المركز…</Text></Card> : null}

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

      {isAdmin || canViewReports ? (
        <>
          <SectionTitle>الإدارة</SectionTitle>
          <View style={styles.adminActions}>
            {canViewReports ? <PrimaryButton title="التقارير ولوحة الإدارة" variant="secondary" onPress={() => router.push("/(app)/reports")} /> : null}
            {isAdmin ? <PrimaryButton title="المخزون والمواد" variant="secondary" onPress={() => router.push("/(app)/inventory")} /> : null}
          </View>
        </>
      ) : null}

      <PrimaryButton title="عرض الإشعارات" variant="accent" onPress={() => router.push("/(app)/notifications")} />
    </Screen>
  );
}

function roleLabel(role?: string): string {
  const labels: Record<string, string> = {
    Admin: "مدير النظام", Orthodontist: "أخصائي تقويم", GeneralDentist: "طبيب أسنان",
    OralSurgeon: "جراح فم ووجه وفكين", Reception: "الاستقبال", Accountant: "المحاسبة",
    Assistant: "مساعد العيادة", BranchManager: "مدير فرع"
  };
  return role ? labels[role] || role : "فريق المركز";
}

function QuickAction({ badge, title, subtitle, onPress, emphasized = false }: { badge: string; title: string; subtitle: string; onPress: () => void; emphasized?: boolean }) {
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={`${title}، ${subtitle}`} onPress={onPress} style={({ pressed }) => [styles.action, emphasized && styles.actionEmphasized, pressed && styles.pressed]}>
      <View style={[styles.actionBadge, emphasized && styles.actionBadgeEmphasized]}><Text style={[styles.actionBadgeText, emphasized && styles.actionBadgeTextEmphasized]}>{badge}</Text></View>
      <Text style={[styles.actionTitle, emphasized && styles.actionTitleEmphasized]}>{title}</Text>
      <Text style={[styles.actionSubtitle, emphasized && styles.actionSubtitleEmphasized]}>{subtitle}</Text>
    </Pressable>
  );
}

function Metric({ title, value, wide = false, tone = "navy" }: { title: string; value: string | number; wide?: boolean; tone?: "navy" | "blue" | "green" | "orange" }) {
  return <View style={[styles.metric, wide && styles.metricWide]}><View style={[styles.metricAccent, styles[`metric_${tone}`]]} /><Text style={[styles.metricValue, styles[`metricText_${tone}`]]}>{value}</Text><Text style={styles.metricTitle}>{title}</Text></View>;
}

function AlertRow({ label, value, last = false }: { label: string; value: number; last?: boolean }) {
  return <View style={[styles.alertRow, last && { borderBottomWidth: 0 }]}><Text style={[styles.alertValue, value > 0 && styles.alertActive]}>{value}</Text><Text style={styles.alertLabel}>{label}</Text></View>;
}

const styles = StyleSheet.create({
  hero: { position: "relative", overflow: "hidden", backgroundColor: colors.primary, borderRadius: radius.lg, padding: spacing.lg, ...shadow.floating },
  heroOrb: { position: "absolute", top: -72, left: -46, width: 190, height: 190, borderRadius: 95, backgroundColor: "rgba(61,122,181,0.28)" },
  heroTop: { flexDirection: "row-reverse", alignItems: "center", gap: spacing.md },
  logoTile: { width: 82, height: 72, alignItems: "center", justifyContent: "center" },
  logo: { width: 80, height: 62 },
  heroCopy: { flex: 1, alignItems: "flex-end", gap: spacing.xxs },
  heroEyebrow: { color: colors.accent, fontSize: 11, fontWeight: "900" },
  greeting: { color: colors.white, fontSize: 22, lineHeight: 30, fontWeight: "900", textAlign: "right" },
  rolePill: { backgroundColor: "rgba(255,255,255,0.12)", borderRadius: radius.pill, paddingHorizontal: spacing.sm, paddingVertical: 5 },
  role: { color: "rgba(255,255,255,0.84)", fontSize: 11, fontWeight: "800", textAlign: "right" },
  heroDivider: { height: 1, backgroundColor: "rgba(255,255,255,0.12)", marginVertical: spacing.md },
  clinicName: { color: colors.white, fontSize: 13, lineHeight: 21, fontWeight: "800", textAlign: "right" },
  clinicLocation: { color: "rgba(255,255,255,0.58)", fontSize: 11, marginTop: 3, textAlign: "right" },
  statusStrip: { flexDirection: "row-reverse", alignItems: "center", justifyContent: "center", gap: spacing.xs, backgroundColor: colors.successSoft, borderRadius: radius.pill, paddingHorizontal: spacing.md, paddingVertical: spacing.xs },
  liveDot: { width: 8, height: 8, borderRadius: 4, backgroundColor: colors.success },
  statusText: { color: colors.success, fontSize: 11, fontWeight: "800", textAlign: "center" },
  actionsGrid: { flexDirection: "row-reverse", flexWrap: "wrap", gap: spacing.sm },
  action: { width: "48%", minHeight: 124, backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border, borderRadius: radius.md, padding: spacing.md, alignItems: "flex-end", ...shadow.card },
  actionEmphasized: { backgroundColor: colors.primary, borderColor: colors.primary },
  actionBadge: { width: 36, height: 36, borderRadius: 12, backgroundColor: colors.primarySoft, alignItems: "center", justifyContent: "center", marginBottom: spacing.sm },
  actionBadgeEmphasized: { backgroundColor: colors.accent },
  actionBadgeText: { color: colors.primary, fontSize: 15, fontWeight: "900" },
  actionBadgeTextEmphasized: { color: colors.white },
  actionTitle: { color: colors.text, fontSize: 16, fontWeight: "900", textAlign: "right" },
  actionTitleEmphasized: { color: colors.white },
  actionSubtitle: { color: colors.muted, fontSize: 11, marginTop: 3, textAlign: "right" },
  actionSubtitleEmphasized: { color: "rgba(255,255,255,0.62)" },
  pressed: { opacity: 0.82, transform: [{ scale: 0.985 }] },
  grid: { flexDirection: "row-reverse", flexWrap: "wrap", gap: spacing.sm },
  metric: { position: "relative", overflow: "hidden", width: "48%", minHeight: 108, backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border, borderRadius: radius.md, padding: spacing.md, justifyContent: "center", ...shadow.card },
  metricAccent: { position: "absolute", top: 0, right: 0, bottom: 0, width: 5 },
  metric_navy: { backgroundColor: colors.primary },
  metric_blue: { backgroundColor: colors.secondary },
  metric_green: { backgroundColor: colors.success },
  metric_orange: { backgroundColor: colors.accent },
  metricWide: { width: "100%" },
  metricValue: { color: colors.primary, fontSize: 25, fontWeight: "900", textAlign: "right" },
  metricText_navy: { color: colors.primary },
  metricText_blue: { color: colors.secondary },
  metricText_green: { color: colors.success },
  metricText_orange: { color: colors.accentDark },
  metricTitle: { color: colors.muted, marginTop: spacing.xs, textAlign: "right" },
  alertRow: { minHeight: 48, flexDirection: "row", alignItems: "center", justifyContent: "space-between", borderBottomWidth: 1, borderBottomColor: colors.border },
  alertValue: { color: colors.muted, fontWeight: "800" },
  alertActive: { color: colors.danger },
  alertLabel: { color: colors.text, textAlign: "right" },
  muted: { color: colors.muted, textAlign: "right" },
  adminActions: { gap: spacing.sm }
});
