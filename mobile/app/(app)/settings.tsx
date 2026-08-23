import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { normalizeExchangeRates, normalizeStringRecord, type ExchangeRatesView } from "@/lib/settings";
import { colors, spacing } from "@/theme";
import { useFocusEffect } from "expo-router";
import React, { useCallback, useState } from "react";
import { ActivityIndicator, RefreshControl, StyleSheet, Text, View } from "react-native";

export default function SettingsScreen() {
  const { user, can, reload } = useSession();
  const isAdmin = user?.role === "Admin";
  const canReadFinanceSettings = can("reports.view");
  const [rates, setRates] = useState<ExchangeRatesView | null>(null);
  const [finance, setFinance] = useState<Record<string, string | null> | null>(null);
  const [general, setGeneral] = useState<Record<string, string> | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sessionRefreshing, setSessionRefreshing] = useState(false);

  const load = useCallback(async () => {
    setError(null);
    const results = await Promise.allSettled([
      apiRequest<unknown>("/api/settings/exchange-rates"),
      canReadFinanceSettings
        ? apiRequest<unknown>("/api/settings/finance")
        : Promise.resolve(null),
      isAdmin
        ? apiRequest<unknown>("/api/settings")
        : Promise.resolve(null)
    ] as const);
    const [ratesResult, financeResult, generalResult] = results;
    if (ratesResult.status === "fulfilled") setRates(normalizeExchangeRates(ratesResult.value)); else setRates(null);
    if (canReadFinanceSettings) {
      setFinance(financeResult.status === "fulfilled" ? normalizeStringRecord(financeResult.value, true) : null);
    } else setFinance(null);
    if (isAdmin) {
      setGeneral(generalResult.status === "fulfilled" ? normalizeStringRecord(generalResult.value) as Record<string, string> | null : null);
    } else setGeneral(null);
    const rejected = results.find((result) => result.status === "rejected") as PromiseRejectedResult | undefined;
    if (rejected) setError(rejected.reason instanceof Error ? rejected.reason.message : "تعذر تحميل بعض الإعدادات");
    setLoading(false);
  }, [canReadFinanceSettings, isAdmin]);

  useFocusEffect(useCallback(() => { setLoading(true); void load(); }, [load]));

  async function refresh() {
    setRefreshing(true);
    try { await load(); } finally { setRefreshing(false); }
  }

  async function refreshSession() {
    setSessionRefreshing(true);
    try { await reload(); } finally { setSessionRefreshing(false); }
  }

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      <View><Text style={styles.title}>الإعدادات والحالة</Text><Text style={styles.subtitle}>إعدادات آمنة للعرض ومعلومات جلسة المستخدم</Text></View>
      {error ? <StateMessage title="تعذر تحميل بعض الإعدادات" message={error} /> : null}
      {loading ? <ActivityIndicator size="large" color={colors.primary} /> : null}

      <SectionTitle>الجلسة</SectionTitle>
      <Card>
        <Row label="المستخدم" value={user?.username || "—"} />
        <Row label="الدور" value={user?.role || "—"} />
        <Row label="الفرع" value={user?.branchId || "غير محدد"} />
        <Row label="البريد" value={user?.email || "—"} last />
      </Card>
      <PrimaryButton title="تحديث الجلسة والصلاحيات" loading={sessionRefreshing} onPress={() => void refreshSession()} />

      <SectionTitle>أسعار الصرف المعتمدة</SectionTitle>
      {rates ? (
        <Card>
          <Row label="السوق" value={rates.marketLabel || rates.market} />
          <Row label="آخر مراجعة" value={rates.updatedOn || "غير محدد"} />
          <Row label="عمر الأسعار" value={`${rates.ageInDays} يوم`} />
          <Row label="الحالة" value={rates.isStale ? "تحتاج مراجعة" : "محدّثة ضمن المدة"} danger={rates.isStale} />
          {Object.entries(rates.ratesToYer).map(([currency, value], index, entries) => (
            <Row key={currency} label={`1 ${currency}`} value={`${Number(value).toLocaleString()} YER`} last={index === entries.length - 1} />
          ))}
        </Card>
      ) : !loading ? <StateMessage title="تعذر تحميل أسعار الصرف" /> : null}

      {canReadFinanceSettings ? (
        <>
          <SectionTitle>إعدادات المالية — قراءة</SectionTitle>
          {finance ? (
            <Card>
              {Object.entries(finance).map(([key, value], index, entries) => (
                <Row key={key} label={friendlyFinanceKey(key)} value={value || "—"} last={index === entries.length - 1} />
              ))}
            </Card>
          ) : !loading ? <StateMessage title="إعدادات المالية غير متاحة الآن" /> : null}
        </>
      ) : null}

      {isAdmin ? (
        <>
          <SectionTitle>الإعدادات العامة — Admin</SectionTitle>
          <Card>
            <Row label="عدد المفاتيح الآمنة المعروضة" value={String(Object.keys(general ?? {}).length)} />
            <Row label="الأسرار" value="لا يعيدها هذا الـAPI" last />
          </Card>
          <Text style={styles.note}>يعرض الهاتف حالة الإعدادات فقط. المفاتيح الحساسة مثل AI وSMTP مستبعدة من الخادم، ولا يوجد محرر عام للمفاتيح في الموبايل لتجنب تغييرات تشغيلية غير مقصودة.</Text>
        </>
      ) : null}
    </Screen>
  );
}

function friendlyFinanceKey(key: string): string {
  const names: Record<string, string> = {
    "finance.max_discount_percentage": "أقصى خصم %",
    "finance.commission.default_doctor_percentage": "نسبة الطبيب الافتراضية %",
    "finance.exchange_rate.sar_to_yer": "SAR → YER",
    "finance.exchange_rate.usd_to_yer": "USD → YER",
    "finance.receipt.show_lead_doctor": "إظهار الطبيب في السند"
  };
  return names[key] ?? key.replace(/^finance\./, "");
}

function Row({ label, value, danger = false, last = false }: { label: string; value: string; danger?: boolean; last?: boolean }) {
  return <View style={[styles.row, last && { borderBottomWidth: 0 }]}><Text style={[styles.value, danger && { color: colors.danger }]}>{value}</Text><Text style={styles.label}>{label}</Text></View>;
}
const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.muted, marginTop: 4, textAlign: "right" },
  row: { minHeight: 44, flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.border },
  label: { color: colors.muted, textAlign: "right", flexShrink: 1 },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" },
  note: { color: colors.muted, fontSize: 12, lineHeight: 20, textAlign: "right" }
});
