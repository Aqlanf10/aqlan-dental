import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { appointmentStatusLabel, isoDateLocal } from "@/lib/format";
import type { Appointment } from "@/lib/types";
import { colors, radius, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useCallback, useEffect, useMemo, useState } from "react";
import { ActivityIndicator, Alert, Pressable, RefreshControl, StyleSheet, Text, View } from "react-native";

const STATUS_FILTERS = [
  { value: "all", label: "الكل" },
  { value: "Scheduled", label: "مجدول" },
  { value: "Confirmed", label: "مؤكد" },
  { value: "Arrived", label: "وصل" },
  { value: "Waiting", label: "انتظار" },
  { value: "Completed", label: "مكتمل" },
  { value: "NoShow", label: "لم يحضر" },
  { value: "Cancelled", label: "ملغي" }
];

type BusyAction = { id: string; action: string } | null;

export default function AppointmentsScreen() {
  const params = useLocalSearchParams<{ patientId?: string; patientName?: string }>();
  const patientId = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;

  const [date, setDate] = useState(() => new Date());
  const [statusFilter, setStatusFilter] = useState("all");
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState<BusyAction>(null);

  const dateText = useMemo(() => isoDateLocal(date), [date]);

  const load = useCallback(async () => {
    setError(null);
    try {
      const query = new URLSearchParams({ from: dateText, to: dateText });
      if (patientId) query.set("patientId", patientId);
      if (statusFilter !== "all") query.set("status", statusFilter);
      const result = await apiRequest<Appointment[]>(`/api/appointments?${query.toString()}`);
      setAppointments(result ?? []);
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحميل المواعيد");
    } finally {
      setLoading(false);
    }
  }, [dateText, patientId, statusFilter]);

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

  async function changeStatus(item: Appointment, status: string) {
    if (busy) return;
    setBusy({ id: item.id, action: status });
    setError(null);
    setNotice(null);
    try {
      await apiRequest<Appointment>(`/api/appointments/${item.id}/status`, {
        method: "PUT",
        body: JSON.stringify({ status })
      });
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تغيير حالة الموعد");
    } finally {
      setBusy(null);
    }
  }

  async function sendReminder(item: Appointment, channel: "whatsapp" | "email") {
    if (busy) return;
    setBusy({ id: item.id, action: channel });
    setError(null);
    setNotice(null);
    try {
      const path =
        channel === "email"
          ? `/api/appointments/${item.id}/send-email-reminder`
          : `/api/appointments/${item.id}/send-reminder`;
      const response = await apiRequest<{ message?: string }>(path, { method: "POST" });
      setNotice(
        response.message ||
          (channel === "email" ? "تم إرسال تذكير البريد الإلكتروني." : "تم إرسال تذكير واتساب.")
      );
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : channel === "email"
            ? "تعذر إرسال تذكير البريد"
            : "تعذر إرسال تذكير واتساب"
      );
    } finally {
      setBusy(null);
    }
  }

  function confirmDangerousStatus(item: Appointment, status: "Cancelled" | "NoShow") {
    const label = status === "Cancelled" ? "إلغاء الموعد" : "تسجيل عدم حضور";
    Alert.alert(label, `تأكيد الإجراء للموعد الخاص بـ ${item.patientName}؟`, [
      { text: "رجوع", style: "cancel" },
      {
        text: "تأكيد",
        style: status === "Cancelled" ? "destructive" : "default",
        onPress: () => void changeStatus(item, status)
      }
    ]);
  }

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      {patientName ? <Text style={styles.filterBanner}>مواعيد: {patientName}</Text> : null}

      <View style={styles.topActions}>
        <View style={{ flex: 1 }}>
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
        </View>
        {!patientId ? (
          <Pressable
            onPress={() => router.push("/(app)/appointments-recall")}
            style={styles.recallButton}
          >
            <Text style={styles.recallButtonText}>الاستدعاء</Text>
          </Pressable>
        ) : null}
      </View>

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

      <View style={styles.statusFilters}>
        {STATUS_FILTERS.map((entry) => (
          <Pressable
            key={entry.value}
            onPress={() => setStatusFilter(entry.value)}
            style={[styles.statusFilter, statusFilter === entry.value && styles.statusFilterActive]}
          >
            <Text style={[styles.statusFilterText, statusFilter === entry.value && styles.statusFilterTextActive]}>
              {entry.label}
            </Text>
          </Pressable>
        ))}
      </View>

      {notice ? <StateMessage title="تم الإجراء" message={notice} /> : null}
      {error ? <StateMessage title="تعذر إكمال إجراء المواعيد" message={error} /> : null}

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
          <Text style={styles.empty}>لا توجد مواعيد مطابقة في هذا اليوم.</Text>
        </Card>
      ) : (
        appointments.map((item) => {
          const actionBusy = busy?.id === item.id;
          const reminderEligible = ["Scheduled", "Confirmed"].includes(item.status);
          return (
            <Card key={item.id}>
              <Pressable
                onPress={() =>
                  router.push({ pathname: "/(app)/patients/[id]", params: { id: item.patientId } })
                }
              >
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
              </Pressable>

              <View style={styles.actionArea}>
                {item.status === "Scheduled" ? (
                  <PrimaryButton
                    title="تأكيد الموعد"
                    onPress={() => void changeStatus(item, "Confirmed")}
                    loading={actionBusy && busy?.action === "Confirmed"}
                    disabled={actionBusy}
                  />
                ) : null}

                {reminderEligible ? (
                  <View style={styles.secondaryActions}>
                    <Pressable
                      disabled={actionBusy}
                      onPress={() => void sendReminder(item, "whatsapp")}
                      style={[styles.secondaryButton, styles.reminderButton]}
                    >
                      <Text style={styles.reminderButtonText}>
                        {actionBusy && busy?.action === "whatsapp" ? "جارٍ الإرسال…" : "تذكير واتساب"}
                      </Text>
                    </Pressable>
                    <Pressable
                      disabled={actionBusy}
                      onPress={() => void sendReminder(item, "email")}
                      style={[styles.secondaryButton, styles.reminderButton]}
                    >
                      <Text style={styles.reminderButtonText}>
                        {actionBusy && busy?.action === "email" ? "جارٍ الإرسال…" : "تذكير بريد"}
                      </Text>
                    </Pressable>
                  </View>
                ) : null}

                {reminderEligible ? (
                  <View style={styles.secondaryActions}>
                    <Pressable
                      disabled={actionBusy}
                      onPress={() => confirmDangerousStatus(item, "NoShow")}
                      style={styles.secondaryButton}
                    >
                      <Text style={styles.secondaryButtonText}>لم يحضر</Text>
                    </Pressable>
                    <Pressable
                      disabled={actionBusy}
                      onPress={() => confirmDangerousStatus(item, "Cancelled")}
                      style={[styles.secondaryButton, styles.dangerButton]}
                    >
                      <Text style={styles.dangerButtonText}>إلغاء الموعد</Text>
                    </Pressable>
                  </View>
                ) : null}
              </View>
            </Card>
          );
        })
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  filterBanner: {
    color: colors.primary,
    backgroundColor: colors.primarySoft,
    borderRadius: radius.sm,
    padding: spacing.sm,
    textAlign: "right",
    fontWeight: "700"
  },
  topActions: { flexDirection: "row-reverse", gap: spacing.sm, alignItems: "stretch" },
  recallButton: {
    minWidth: 92,
    alignItems: "center",
    justifyContent: "center",
    borderWidth: 1,
    borderColor: colors.warning,
    borderRadius: radius.sm,
    backgroundColor: colors.warningSoft,
    paddingHorizontal: spacing.sm
  },
  recallButtonText: { color: colors.warning, fontWeight: "800" },
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
  statusFilters: { flexDirection: "row-reverse", flexWrap: "wrap", gap: spacing.xs },
  statusFilter: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 999,
    paddingHorizontal: spacing.sm,
    paddingVertical: 7,
    backgroundColor: colors.surface
  },
  statusFilterActive: { borderColor: colors.primary, backgroundColor: colors.primarySoft },
  statusFilterText: { color: colors.text, fontSize: 12, fontWeight: "700" },
  statusFilterTextActive: { color: colors.primary },
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
  actionArea: { marginTop: spacing.md, gap: spacing.sm },
  secondaryActions: { flexDirection: "row-reverse", gap: spacing.sm },
  secondaryButton: {
    flex: 1,
    minHeight: 42,
    alignItems: "center",
    justifyContent: "center",
    borderWidth: 1,
    borderColor: colors.warning,
    borderRadius: radius.sm,
    backgroundColor: colors.warningSoft,
    paddingHorizontal: spacing.xs
  },
  secondaryButtonText: { color: colors.warning, fontWeight: "800", fontSize: 12 },
  reminderButton: { borderColor: colors.primary, backgroundColor: colors.primarySoft },
  reminderButtonText: { color: colors.primary, fontWeight: "800", fontSize: 12 },
  dangerButton: { borderColor: colors.danger, backgroundColor: colors.dangerSoft },
  dangerButtonText: { color: colors.danger, fontWeight: "800", fontSize: 12 },
  empty: { color: colors.muted, textAlign: "center" }
});
