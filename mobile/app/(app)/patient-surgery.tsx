import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import {
  canUseSurgery,
  normalizeSurgeryStatus,
  SURGERY_STATUS_LABELS,
  type SurgeryCaseListResponse
} from "@/lib/surgery";
import { colors, spacing } from "@/theme";
import { router, useFocusEffect, useLocalSearchParams } from "expo-router";
import React, { useCallback, useState } from "react";
import { ActivityIndicator, Pressable, RefreshControl, StyleSheet, Text, View } from "react-native";

export default function PatientSurgeryScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ patientId: string; patientName?: string }>();
  const patientId = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const allowed = canUseSurgery(user?.role);

  const [result, setResult] = useState<SurgeryCaseListResponse>({ data: [], total: 0, page: 1, pageSize: 100 });
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!patientId || !allowed) {
      setLoading(false);
      return;
    }
    setError(null);
    try {
      setResult(
        await apiRequest<SurgeryCaseListResponse>(
          `/api/surgery-cases?patientId=${patientId}&page=1&pageSize=100`
        )
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحميل الحالات الجراحية");
    } finally {
      setLoading(false);
    }
  }, [allowed, patientId]);

  useFocusEffect(
    useCallback(() => {
      setLoading(true);
      void load();
    }, [load])
  );

  async function refresh() {
    setRefreshing(true);
    try {
      await load();
    } finally {
      setRefreshing(false);
    }
  }

  if (!allowed) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="وحدة جراحة الفم متاحة للأدمن وجراح الفم فقط." />
      </Screen>
    );
  }

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      <View>
        <Text style={styles.title}>جراحة الفم</Text>
        <Text style={styles.subtitle}>{patientName || "حالات المريض"}</Text>
      </View>

      <PrimaryButton
        title="إنشاء حالة جراحية"
        onPress={() =>
          router.push({ pathname: "/(app)/surgery-new", params: { patientId, patientName } })
        }
      />

      {loading ? <ActivityIndicator size="large" color={colors.primary} /> : null}
      {error ? (
        <StateMessage
          title="تعذر تحميل الحالات"
          message={error}
          action={<PrimaryButton title="إعادة المحاولة" onPress={() => void load()} />}
        />
      ) : null}
      {!loading && !error && result.data.length === 0 ? (
        <StateMessage title="لا توجد حالة جراحية مسجلة لهذا المريض" />
      ) : null}

      {result.data.map((item) => {
        const status = normalizeSurgeryStatus(item.status);
        return (
          <Pressable
            key={item.id}
            onPress={() =>
              router.push({
                pathname: "/(app)/surgery-case",
                params: { id: item.id, patientName: item.patientName }
              })
            }
          >
            <Card>
              <View style={styles.header}>
                <Text style={styles.status}>{SURGERY_STATUS_LABELS[status] ?? item.status}</Text>
                <View style={styles.headerText}>
                  <Text style={styles.caseNumber}>{item.caseNumber}</Text>
                  <Text style={styles.type}>{item.surgeryType}</Text>
                </View>
              </View>
              <Row label="الأسنان" value={item.teethInvolved || "—"} />
              <Row label="الطبيب" value={item.doctorName ? `د. ${item.doctorName}` : "—"} />
              <Row label="تاريخ الإنشاء" value={item.createdAt || "—"} last />
            </Card>
          </Pressable>
        );
      })}
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
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  header: { flexDirection: "row", alignItems: "flex-start", justifyContent: "space-between", gap: spacing.sm },
  headerText: { flex: 1 },
  caseNumber: { color: colors.text, fontSize: 18, fontWeight: "800", textAlign: "right" },
  type: { color: colors.muted, marginTop: 4, textAlign: "right" },
  status: {
    color: colors.primary,
    backgroundColor: colors.primarySoft,
    paddingHorizontal: spacing.sm,
    paddingVertical: 5,
    borderRadius: 999,
    fontWeight: "800",
    fontSize: 12
  },
  row: {
    minHeight: 42,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    gap: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border
  },
  label: { color: colors.muted, textAlign: "right" },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" }
});
