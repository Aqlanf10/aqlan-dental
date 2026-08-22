import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { colors, radius, spacing } from "@/theme";
import Constants from "expo-constants";
import { router } from "expo-router";
import React, { useState } from "react";
import { StyleSheet, Text, View } from "react-native";

export default function AccountScreen() {
  const { user, permissions, signOut, reload } = useSession();
  const [loading, setLoading] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function logout() {
    setLoading(true);
    setError(null);
    try {
      await signOut();
      router.replace("/sign-in");
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تسجيل الخروج");
    } finally {
      setLoading(false);
    }
  }

  async function refreshSession() {
    setRefreshing(true);
    setError(null);
    try { await reload(); } catch (err) { setError(err instanceof Error ? err.message : "تعذر تحديث الجلسة"); } finally { setRefreshing(false); }
  }

  return (
    <Screen>
      <SectionTitle>الحساب</SectionTitle>
      {error ? <StateMessage title="تعذر تنفيذ العملية" message={error} /> : null}
      <Card>
        <Row label="المستخدم" value={user?.username ?? "—"} />
        <Row label="الدور" value={user?.role ?? "—"} />
        <Row label="الطبيب" value={user?.doctorName ?? "—"} />
        <Row label="البريد" value={user?.email ?? "—"} />
        <Row label="الفرع" value={user?.branchId ?? "غير محدد"} />
        <Row label="الحساب" value={user?.isActive ? "نشط" : "غير نشط"} />
        <Row label="تغيير إلزامي لكلمة المرور" value={user?.mustChangePassword ? "نعم" : "لا"} />
        <Row label="عدد الصلاحيات" value={`${permissions.length}`} last />
      </Card>

      <SectionTitle>الأمان</SectionTitle>
      <PrimaryButton title="تغيير كلمة المرور" onPress={() => router.push("/change-password")} />
      <PrimaryButton title="تحديث الجلسة والصلاحيات" loading={refreshing} disabled={refreshing} onPress={() => void refreshSession()} />
      <PrimaryButton title="الإعدادات وحالة أسعار الصرف" onPress={() => router.push("/(app)/settings")} />

      <SectionTitle>الصلاحيات الفعلية</SectionTitle>
      {user?.role === "Admin" ? (
        <Card><Text style={styles.admin}>Admin يملك جميع الصلاحيات حسب SessionProvider.</Text></Card>
      ) : permissions.length ? (
        <View style={styles.permissions}>
          {permissions.slice().sort().map((permission) => <Text key={permission} style={styles.permission}>{permission}</Text>)}
        </View>
      ) : (
        <Card><Text style={styles.muted}>لم يرجع الخادم صلاحيات صريحة لهذا الحساب.</Text></Card>
      )}

      <SectionTitle>الجلسة</SectionTitle>
      <PrimaryButton title="تسجيل الخروج من هذا الجهاز" loading={loading} disabled={loading} onPress={() => void logout()} />

      <Text style={styles.version}>
        Aqlan Dental Pro Mobile · {Constants.expoConfig?.version ?? "0.1.0"}
      </Text>
    </Screen>
  );
}

function Row({ label, value, last = false }: { label: string; value: string; last?: boolean }) {
  return <View style={[styles.row, last && { borderBottomWidth: 0 }]}><Text style={styles.value}>{value}</Text><Text style={styles.label}>{label}</Text></View>;
}

const styles = StyleSheet.create({
  row: { minHeight: 48, borderBottomWidth: 1, borderBottomColor: colors.border, flexDirection: "row", justifyContent: "space-between", alignItems: "center", gap: spacing.md },
  label: { color: colors.muted, textAlign: "right", flexShrink: 1 },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" },
  permissions: { flexDirection: "row-reverse", flexWrap: "wrap", gap: spacing.xs },
  permission: { color: colors.primary, backgroundColor: colors.primarySoft, borderRadius: 999, paddingHorizontal: spacing.sm, paddingVertical: 6, fontSize: 11, fontWeight: "700" },
  admin: { color: colors.primary, textAlign: "right", fontWeight: "700" },
  muted: { color: colors.muted, textAlign: "right" },
  version: { color: colors.muted, textAlign: "center", fontSize: 12, paddingVertical: spacing.md }
});
