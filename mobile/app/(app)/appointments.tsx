import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { appointmentStatusLabel, isoDateLocal } from "@/lib/format";
import type { Appointment } from "@/lib/types";
import { colors, radius, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useCallback, useEffect, useMemo, useState } from "react";
import { ActivityIndicator, Pressable, RefreshControl, StyleSheet, Text, View } from "react-native";

export default function AppointmentsScreen() {
  const params = useLocalSearchParams<{ patientId?: string; patientName?: string }>();
  const patientId = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;

  const [date, setDate] = useState(() => new Date());
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const dateText = useMemo(() => isoDateLocal(date), [date]);

  const load = useCallback(async () => {
    setError(null);
    try {
      const query = new URLSearchParams({ from: dateText, to: dateText });
      if (patientId) query.set("patientId", patientId);
      const result = await apiRequest<Appointment[]>(`/api/appointments?${query.toString()}`);
      setAppointments(result ?? []);
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحميل المواعيد");
    } finally {
      setLoading(false);
    }
  }, [dateText, patientId]);

  useEffect(() => {
    setLoading(true);
    void load();
  }, [load]);

  function moveDay(days: number) {
    setDate((current) => {
      const next = new Date(current);
      next.setDate(current.getDate() + days);
      return next;
    });
  }

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
      {patientName ? <Text style={styles.filter}>مواعيد: {patientName}</Text> : null}

      <PrimaryButton
        title="حجز موعد جديد"
        onPress={() =>
          router.push({
            pathname: "/(app)/appointments-new",
            params: patientId
              ? { patientId, patientName: patientName ?? "", date: dateText }
              : { date: dateText }
          })
        }
      />

      <View style={styles.dateBar}>
        <Pressable onPress={() => moveDay(-1)} style={styles.dateButton}>
          <Text style={styles.dateButtonText}>اليوم السابق</Text>
        </Pressable>
        <View style={styles.dateCenter}>
          <Text style={styles.date}>{dateText}</Text>
          <Pressable onPress={() => setDate(new Date())}>
            <Text style={styles.today}>اليوم</Text>
          </Pressable>
        </View>
        <Pressable onPress={() => moveDay(1)} style={styles.dateButton}>
          <Text style={styles.dateButtonText}>اليوم التالي</Text>
        </Pressable>
      </View>

      {loading ? (
        <ActivityIndicator size="large" color={colors.primary} />
      ) : error && appointments.length === 0 ? (
        <StateMessage
          title="تعذر تحميل المواعيد"
          message={error}
          action={<PrimaryButton title="إعادة المحاولة" onPress={() => void load()} />}
        />
      ) : appointments.length === 0 ? (
        <Card>
          <Text style={styles.empty}>لا توجد مواعيد في هذا اليوم.</Text>
        </Card>
      ) : (
        appointments.map((item) => (
          <Card key={item.id}>
            <View style={styles.appointmentHeader}>
              <Text style={styles.status}>{appointmentStatusLabel(item.status)}</Text>
              <View style={{ flex: 1 }}>
                <Text style={styles.patient}>{item.patientName}</Text>
                <Text style={styles.meta}>{item.patientNumber}</Text>
              </View>
            </View>
            <Text style={styles.time}>
              {item.startTime} – {item.endTime}
            </Text>
            <Text style={styles.meta}>د. {item.doctorName}</Text>
            {item.appointmentType ? <Text style={styles.meta}>{item.appointmentType}</Text> : null}
            {item.roomName ? <Text style={styles.meta}>الغرفة: {item.roomName}</Text> : null}
          </Card>
        ))
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  filter: {
    color: colors.primary,
    backgroundColor: colors.primarySoft,
    borderRadius: radius.sm,
    padding: spacing.sm,
    textAlign: "right",
    fontWeight: "700"
  },
  dateBar: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    gap: spacing.sm
  },
  dateButton: {
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.surface,
    borderRadius: radius.sm,
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.sm
  },
  dateButtonText: { color: colors.primary, fontSize: 12, fontWeight: "700" },
  dateCenter: { alignItems: "center" },
  date: { color: colors.text, fontWeight: "800" },
  today: { color: colors.primary, fontSize: 12, marginTop: 4 },
  appointmentHeader: {
    flexDirection: "row",
    alignItems: "flex-start",
    justifyContent: "space-between",
    gap: spacing.md
  },
  patient: { color: colors.text, fontSize: 17, fontWeight: "800", textAlign: "right" },
  status: {
    color: colors.primary,
    backgroundColor: colors.primarySoft,
    borderRadius: 999,
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
    fontSize: 12,
    fontWeight: "700"
  },
  time: {
    color: colors.text,
    fontSize: 16,
    fontWeight: "700",
    marginTop: spacing.md,
    textAlign: "right"
  },
  meta: { color: colors.muted, marginTop: 4, textAlign: "right" },
  empty: { color: colors.muted, textAlign: "center" }
});
