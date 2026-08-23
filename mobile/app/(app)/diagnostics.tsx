import { useSession } from "@/auth/SessionProvider";
import { Card, PageHeader, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest, apiServerOrigin, checkApiHealth, type ApiHealth } from "@/lib/api";
import type { StaffUser, UserPermissions } from "@/lib/types";
import { normalizePermissions, normalizeStaffUser } from "@/lib/session";
import { colors, spacing } from "@/theme";
import Constants from "expo-constants";
import { useFocusEffect } from "expo-router";
import React, { useCallback, useState } from "react";
import { ActivityIndicator, StyleSheet, Text, View } from "react-native";

type CheckState = "idle" | "success" | "failed";

export default function DiagnosticsScreen() {
  const { user } = useSession();
  const [health, setHealth] = useState<ApiHealth | null>(null);
  const [sessionCheck, setSessionCheck] = useState<CheckState>("idle");
  const [permissionsCheck, setPermissionsCheck] = useState<CheckState>("idle");
  const [permissionsCount, setPermissionsCount] = useState<number | null>(null);
  const [checkedAt, setCheckedAt] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const runChecks = useCallback(async () => {
    setLoading(true);
    setError(null);

    const [healthResult, sessionResult, permissionResult] = await Promise.allSettled([
      checkApiHealth(),
      apiRequest<unknown>("/api/auth/me"),
      apiRequest<unknown>("/api/auth/me/permissions")
    ] as const);

    if (healthResult.status === "fulfilled") setHealth(healthResult.value);
    else {
      setHealth(null);
      setError(readableError(healthResult.reason, "تعذر الوصول إلى الخادم"));
    }

    setSessionCheck(sessionResult.status === "fulfilled" && normalizeStaffUser(sessionResult.value) ? "success" : "failed");
    setPermissionsCheck(permissionResult.status === "fulfilled" ? "success" : "failed");
    setPermissionsCount(
      permissionResult.status === "fulfilled"
        ? normalizePermissions(permissionResult.value).permissions.length
        : null
    );
    setCheckedAt(new Date().toLocaleString("ar-YE"));
    setLoading(false);
  }, []);

  useFocusEffect(useCallback(() => { void runChecks(); }, [runChecks]));

  return (
    <Screen>
      <PageHeader title="تشخيص التطبيق" eyebrow="أداة دعم آمنة" subtitle="فحص النسخة واتصال الهاتف بالخادم والجلسة الفعلية دون كشف الرموز السرية." />

      {error ? <StateMessage title="فشل فحص الاتصال" message={error} /> : null}
      {loading ? <ActivityIndicator accessibilityLabel="جارٍ فحص التطبيق" size="large" color={colors.primary} /> : null}

      <SectionTitle>نسخة التطبيق</SectionTitle>
      <Card>
        <Row label="الإصدار" value={Constants.expoConfig?.version ?? "غير محدد"} />
        <Row label="رقم بناء Android" value={String(Constants.expoConfig?.android?.versionCode ?? "غير محدد")} />
        <Row label="نوع التشغيل" value={__DEV__ ? "تطوير — يحتاج Metro" : "Release مستقل"} danger={__DEV__} />
        <Row label="عنوان الخادم" value={safeServerOrigin()} last />
      </Card>

      <SectionTitle>الخادم</SectionTitle>
      <Card>
        <Row label="الحالة" value={health ? healthLabel(health.status) : "غير متصل"} danger={!health} />
        <Row label="زمن الاستجابة" value={health ? `${health.latencyMs} ms` : "—"} danger={!health} />
        <Row label="إصدار الخادم" value={health?.version || "غير معلن"} />
        <Row label="وقت الخادم" value={health?.timestamp || "—"} />
        <Row label="آخر فحص من الهاتف" value={checkedAt || "لم يُفحص بعد"} last />
      </Card>

      <SectionTitle>الجلسة والصلاحيات</SectionTitle>
      <Card>
        <Row label="المستخدم الحالي" value={user?.username || "—"} />
        <Row label="التحقق من الجلسة" value={checkLabel(sessionCheck)} danger={sessionCheck === "failed"} />
        <Row label="تحميل الصلاحيات" value={checkLabel(permissionsCheck)} danger={permissionsCheck === "failed"} />
        <Row label="عدد الصلاحيات من الخادم" value={permissionsCount === null ? "—" : String(permissionsCount)} last />
      </Card>

      <PrimaryButton
        title="إعادة فحص الاتصال والجلسة"
        variant="accent"
        loading={loading}
        disabled={loading}
        accessibilityHint="يعيد اختبار الخادم والجلسة والصلاحيات من هذا الهاتف"
        onPress={() => void runChecks()}
      />
      <Text style={styles.note}>لا تعرض هذه الصفحة كلمات المرور أو الرموز السرية، ولا تغيّر أي بيانات في النظام.</Text>
    </Screen>
  );
}

function safeServerOrigin(): string {
  try {
    return apiServerOrigin();
  } catch (err) {
    return readableError(err, "غير مضبوط");
  }
}

function readableError(error: unknown, fallback: string): string {
  return error instanceof Error && error.message ? error.message : fallback;
}

function healthLabel(status: string): string {
  return status.toLowerCase() === "healthy" ? "متصل ويعمل" : status;
}

function checkLabel(state: CheckState): string {
  if (state === "success") return "ناجح";
  if (state === "failed") return "فشل";
  return "لم يُفحص";
}

function Row({ label, value, danger = false, last = false }: { label: string; value: string; danger?: boolean; last?: boolean }) {
  return (
    <View style={[styles.row, last && styles.lastRow]}>
      <Text selectable style={[styles.value, danger && styles.danger]}>{value}</Text>
      <Text style={styles.label}>{label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  row: { minHeight: 48, borderBottomWidth: 1, borderBottomColor: colors.border, flexDirection: "row", justifyContent: "space-between", alignItems: "center", gap: spacing.md },
  lastRow: { borderBottomWidth: 0 },
  label: { color: colors.muted, textAlign: "right", flexShrink: 1 },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" },
  danger: { color: colors.danger },
  note: { color: colors.muted, fontSize: 12, lineHeight: 20, textAlign: "right" }
});
