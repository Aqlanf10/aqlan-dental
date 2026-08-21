import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, SectionTitle } from "@/components/ui";
import { colors, spacing } from "@/theme";
import Constants from "expo-constants";
import { router } from "expo-router";
import React, { useState } from "react";
import { StyleSheet, Text, View } from "react-native";

export default function AccountScreen() {
  const { user, permissions, signOut } = useSession();
  const [loading, setLoading] = useState(false);

  async function logout() {
    setLoading(true);
    try {
      await signOut();
      router.replace("/sign-in");
    } finally {
      setLoading(false);
    }
  }

  return (
    <Screen>
      <SectionTitle>الحساب</SectionTitle>
      <Card>
        <Row label="المستخدم" value={user?.username ?? "—"} />
        <Row label="الدور" value={user?.role ?? "—"} />
        <Row label="الطبيب" value={user?.doctorName ?? "—"} />
        <Row label="الصلاحيات" value={`${permissions.length}`} last />
      </Card>

      <PrimaryButton title="تسجيل الخروج" loading={loading} onPress={() => void logout()} />

      <Text style={styles.version}>
        Aqlan Dental Pro Mobile · {Constants.expoConfig?.version ?? "0.1.0"}
      </Text>
    </Screen>
  );
}

function Row({ label, value, last = false }: { label: string; value: string; last?: boolean }) {
  return (
    <View style={[styles.row, last && { borderBottomWidth: 0 }]}>
      <Text style={styles.value}>{value}</Text>
      <Text style={styles.label}>{label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  row: { minHeight: 48, borderBottomWidth: 1, borderBottomColor: colors.border, flexDirection: "row", justifyContent: "space-between", alignItems: "center", gap: spacing.md },
  label: { color: colors.muted },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" },
  version: { color: colors.muted, textAlign: "center", fontSize: 12 }
});
