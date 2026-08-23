import { useSession } from "@/auth/SessionProvider";
import { useClinicBranding } from "@/brand";
import { Card, PageHeader, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { colors, radius, shadow, spacing } from "@/theme";
import { markRuntimeAction } from "@/lib/runtimeDiagnostics";
import Constants from "expo-constants";
import { router } from "expo-router";
import React, { useRef, useState } from "react";
import { Image, StyleSheet, Text, View } from "react-native";

export default function AccountScreen() {
  const { user, permissions, signOut, reload } = useSession();
  const brand = useClinicBranding();
  const [loading, setLoading] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const logoutRef = useRef(false);

  async function logout() {
    if (logoutRef.current) return;
    logoutRef.current = true;
    setLoading(true);
    setError(null);
    markRuntimeAction("تسجيل الخروج");
    try {
      await signOut();
      router.replace("/sign-in");
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تسجيل الخروج");
    } finally {
      logoutRef.current = false;
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
      <PageHeader title="حسابي" eyebrow="الجلسة والأمان" subtitle="بيانات الحساب الفعلية والصلاحيات القادمة من الخادم." />
      {error ? <StateMessage title="تعذر تنفيذ العملية" message={error} /> : null}
      <View style={styles.profileCard}>
        <View style={styles.profileLogo}><Image source={require("../../../assets/logo.png")} resizeMode="contain" style={styles.logo} /></View>
        <View style={styles.profileCopy}>
          <Text style={styles.profileName}>{user?.doctorName || user?.username || "مستخدم المركز"}</Text>
          <Text style={styles.profileRole}>{roleLabel(user?.role)}</Text>
          <Text numberOfLines={2} style={styles.clinic}>{brand.clinicName}</Text>
        </View>
      </View>

      <SectionTitle>بيانات الحساب</SectionTitle>
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
      <View style={styles.actions}>
        <PrimaryButton title="تغيير كلمة المرور" variant="secondary" onPress={() => { markRuntimeAction("فتح تغيير كلمة المرور"); router.push("/change-password"); }} />
        <PrimaryButton title="تحديث الجلسة والصلاحيات" variant="secondary" loading={refreshing} disabled={refreshing} onPress={() => void refreshSession()} />
        <PrimaryButton title="الإعدادات وحالة أسعار الصرف" variant="secondary" onPress={() => { markRuntimeAction("فتح الإعدادات"); router.push("/(app)/settings"); }} />
        <PrimaryButton title="تشخيص الاتصال ونسخة التطبيق" variant="secondary" onPress={() => { markRuntimeAction("فتح التشخيص"); router.push("/(app)/diagnostics"); }} />
      </View>

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
      <PrimaryButton title="تسجيل الخروج من هذا الجهاز" variant="danger" loading={loading} disabled={loading} onPress={() => void logout()} />

      <Text style={styles.version}>
        Aqlan Dental Pro Mobile · {Constants.expoConfig?.version ?? "0.1.0"}
      </Text>
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

function Row({ label, value, last = false }: { label: string; value: string; last?: boolean }) {
  return <View style={[styles.row, last && { borderBottomWidth: 0 }]}><Text style={styles.value}>{value}</Text><Text style={styles.label}>{label}</Text></View>;
}

const styles = StyleSheet.create({
  profileCard: { flexDirection: "row-reverse", alignItems: "center", gap: spacing.md, backgroundColor: colors.primary, borderRadius: radius.lg, padding: spacing.md, ...shadow.floating },
  profileLogo: { width: 78, height: 68, borderRadius: radius.md, backgroundColor: colors.white, alignItems: "center", justifyContent: "center" },
  logo: { width: 68, height: 54 },
  profileCopy: { flex: 1, alignItems: "flex-end", gap: 3 },
  profileName: { color: colors.white, fontSize: 18, fontWeight: "900", textAlign: "right" },
  profileRole: { color: colors.accent, fontSize: 12, fontWeight: "800" },
  clinic: { color: "rgba(255,255,255,0.60)", fontSize: 10, lineHeight: 16, textAlign: "right" },
  row: { minHeight: 48, borderBottomWidth: 1, borderBottomColor: colors.border, flexDirection: "row", justifyContent: "space-between", alignItems: "center", gap: spacing.md },
  label: { color: colors.muted, textAlign: "right", flexShrink: 1 },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" },
  permissions: { flexDirection: "row-reverse", flexWrap: "wrap", gap: spacing.xs },
  permission: { color: colors.primary, backgroundColor: colors.primarySoft, borderRadius: 999, paddingHorizontal: spacing.sm, paddingVertical: 6, fontSize: 11, fontWeight: "700" },
  admin: { color: colors.primary, textAlign: "right", fontWeight: "700" },
  muted: { color: colors.muted, textAlign: "right" },
  actions: { gap: spacing.sm },
  version: { color: colors.muted, textAlign: "center", fontSize: 12, paddingVertical: spacing.md }
});
