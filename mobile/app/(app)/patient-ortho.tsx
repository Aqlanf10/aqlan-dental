import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { canUseOrthodontics, ORTHO_STATUS_LABELS, type OrthoCase } from "@/lib/ortho";
import { colors, radius, spacing } from "@/theme";
import { router, useFocusEffect, useLocalSearchParams } from "expo-router";
import React, { useCallback, useState } from "react";
import { ActivityIndicator, Pressable, RefreshControl, StyleSheet, Text, View } from "react-native";

export default function PatientOrthoScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ patientId: string; patientName?: string }>();
  const patientId = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const [cases, setCases] = useState<OrthoCase[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const allowed = canUseOrthodontics(user?.role);

  const load = useCallback(async () => {
    if (!patientId || !allowed) {
      setLoading(false);
      return;
    }
    setError(null);
    try {
      const result = await apiRequest<OrthoCase[]>(
        `/api/ortho-cases?patientId=${patientId}&page=1&pageSize=100`
      );
      setCases(result ?? []);
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحميل حالات التقويم");
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
        <StateMessage title="غير مصرح" message="وحدة التقويم متاحة للأدمن وأخصائي التقويم فقط." />
      </Screen>
    );
  }

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      <View>
        <Text style={styles.title}>تقويم الأسنان</Text>
        <Text style={styles.subtitle}>{patientName || "حالات المريض"}</Text>
      </View>

      {loading ? <ActivityIndicator size="large" color={colors.primary} /> : null}
      {error ? (
        <StateMessage
          title="تعذر تحميل حالات التقويم"
          message={error}
          action={<PrimaryButton title="إعادة المحاولة" onPress={() => void load()} />}
        />
      ) : null}

      {!loading && !error && cases.length === 0 ? (
        <StateMessage title="لا توجد حالة تقويم مسجلة لهذا المريض" />
      ) : null}

      {cases.map((item) => (
        <Pressable
          key={item.id}
          onPress={() =>
            router.push({
              pathname: "/(app)/ortho-case",
              params: { id: item.id, patientName: item.patientName }
            })
          }
        >
          <Card>
            <View style={styles.header}>
              <View style={styles.statusBadge}>
                <Text style={styles.statusText}>{ORTHO_STATUS_LABELS[item.status] ?? item.status}</Text>
              </View>
              <View style={styles.caseBlock}>
                <Text style={styles.caseNumber}>{item.caseNumber}</Text>
                <Text style={styles.meta}>{item.doctorName ? `د. ${item.doctorName}` : "—"}</Text>
              </View>
            </View>

            <View style={styles.progressTrack}>
              <View style={[styles.progressFill, { width: `${Math.max(0, Math.min(100, item.stagePercentage))}%` }]} />
            </View>
            <Text style={styles.progressText}>التقدم: {item.stagePercentage}%</Text>

            <Row label="المرحلة الحالية" value={item.currentStage || "—"} />
            <Row label="الجهاز" value={item.applianceType || "—"} />
            <Row label="تاريخ البدء" value={item.startDate || "—"} />
            <Row
              label="المدة المتوقعة"
              value={item.expectedDurationMonths ? `${item.expectedDurationMonths} شهر` : "—"}
              last
            />
          </Card>
        </Pressable>
      ))}
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
  header: { flexDirection: "row", justifyContent: "space-between", gap: spacing.sm },
  caseBlock: { flex: 1 },
  caseNumber: { color: colors.text, fontSize: 18, fontWeight: "800", textAlign: "right" },
  meta: { color: colors.muted, marginTop: 4, textAlign: "right" },
  statusBadge: {
    alignSelf: "flex-start",
    paddingHorizontal: spacing.sm,
    paddingVertical: 6,
    borderRadius: 999,
    backgroundColor: colors.primarySoft
  },
  statusText: { color: colors.primary, fontWeight: "800", fontSize: 12 },
  progressTrack: {
    height: 8,
    backgroundColor: colors.border,
    borderRadius: radius.sm,
    overflow: "hidden",
    marginTop: spacing.md
  },
  progressFill: { height: "100%", backgroundColor: colors.primary },
  progressText: { color: colors.muted, marginTop: 5, textAlign: "right", fontSize: 12 },
  row: {
    minHeight: 44,
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
