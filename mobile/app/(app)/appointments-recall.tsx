import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { colors, radius, spacing } from "@/theme";
import { router, useFocusEffect } from "expo-router";
import React, { useCallback, useState } from "react";
import { ActivityIndicator, Pressable, RefreshControl, StyleSheet, Text, View } from "react-native";

type RecallCandidate = {
  patientId: string;
  patientName: string;
  patientNumber: string;
  phone?: string | null;
  missedCount: number;
  lastMissedDate: string;
};

type RecallResponse = {
  items: RecallCandidate[];
  totalCount: number;
  windowDays: number;
};

const WINDOWS = [7, 14, 30, 60, 90];

export default function AppointmentsRecallScreen() {
  const [windowDays, setWindowDays] = useState(30);
  const [data, setData] = useState<RecallResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      setData(
        await apiRequest<RecallResponse>(
          `/api/appointments/recall-candidates?windowDays=${windowDays}`
        )
      );
    } catch (err) {
      setData(null);
      setError(err instanceof Error ? err.message : "تعذر تحميل قائمة الاستدعاء");
    } finally {
      setLoading(false);
    }
  }, [windowDays]);

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

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      <View>
        <Text style={styles.title}>قائمة الاستدعاء</Text>
        <Text style={styles.subtitle}>
          مرضى تغيبوا وليس لديهم موعد قادم
          {data ? ` • ${data.totalCount} مريض` : ""}
        </Text>
      </View>

      <View style={styles.windows}>
        {WINDOWS.map((days) => (
          <Pressable
            key={days}
            onPress={() => setWindowDays(days)}
            style={[styles.window, windowDays === days && styles.windowActive]}
          >
            <Text style={[styles.windowText, windowDays === days && styles.windowTextActive]}>
              {days} يوم
            </Text>
          </Pressable>
        ))}
      </View>

      {loading ? <ActivityIndicator size="large" color={colors.primary} /> : null}
      {error ? (
        <StateMessage
          title="تعذر تحميل قائمة الاستدعاء"
          message={error}
          action={<PrimaryButton title="إعادة المحاولة" onPress={() => void load()} />}
        />
      ) : null}

      {!loading && data?.items.length === 0 ? (
        <StateMessage title="لا يوجد مرضى بحاجة لإعادة حجز خلال هذه الفترة" />
      ) : null}

      {data?.items.map((item) => (
        <Card key={item.patientId}>
          <Pressable
            onPress={() =>
              router.push({ pathname: "/(app)/patients/[id]", params: { id: item.patientId } })
            }
          >
            <View style={styles.header}>
              <View style={[styles.badge, item.missedCount >= 2 && styles.badgeDanger]}>
                <Text style={[styles.badgeText, item.missedCount >= 2 && styles.badgeTextDanger]}>
                  غياب × {item.missedCount}
                </Text>
              </View>
              <View style={styles.patientBlock}>
                <Text style={styles.patient}>{item.patientName}</Text>
                <Text style={styles.meta}>{item.patientNumber}</Text>
              </View>
            </View>
            {item.phone ? <Text style={styles.meta}>الهاتف: {item.phone}</Text> : null}
            <Text style={styles.meta}>آخر غياب: {item.lastMissedDate}</Text>
          </Pressable>
          <View style={{ marginTop: spacing.md }}>
            <PrimaryButton
              title="حجز موعد جديد"
              onPress={() =>
                router.push({
                  pathname: "/(app)/appointments-new",
                  params: { patientId: item.patientId, patientName: item.patientName }
                })
              }
            />
          </View>
        </Card>
      ))}
    </Screen>
  );
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.muted, marginTop: 4, textAlign: "right", lineHeight: 21 },
  windows: { flexDirection: "row-reverse", flexWrap: "wrap", gap: spacing.xs },
  window: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 999,
    paddingHorizontal: spacing.sm,
    paddingVertical: 8,
    backgroundColor: colors.surface
  },
  windowActive: { borderColor: colors.warning, backgroundColor: colors.warningSoft },
  windowText: { color: colors.text, fontWeight: "700" },
  windowTextActive: { color: colors.warning },
  header: { flexDirection: "row", justifyContent: "space-between", gap: spacing.sm },
  patientBlock: { flex: 1 },
  patient: { color: colors.text, fontSize: 17, fontWeight: "800", textAlign: "right" },
  meta: { color: colors.muted, marginTop: 4, textAlign: "right" },
  badge: {
    alignSelf: "flex-start",
    backgroundColor: colors.warningSoft,
    borderRadius: radius.sm,
    paddingHorizontal: spacing.sm,
    paddingVertical: 6
  },
  badgeDanger: { backgroundColor: colors.dangerSoft },
  badgeText: { color: colors.warning, fontSize: 12, fontWeight: "800" },
  badgeTextDanger: { color: colors.danger }
});
