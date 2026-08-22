import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { formatYemeniRial, isoDateLocal } from "@/lib/format";
import {
  canClinicalJourney,
  canReceptionJourney,
  journeyActionLabel,
  journeyStatusLabel,
  type TodayJourneyItem
} from "@/lib/journey";
import { OPERATIONAL_PERMISSION } from "@/lib/permissionContract";
import { colors, radius, spacing } from "@/theme";
import { router, useFocusEffect } from "expo-router";
import React, { useCallback, useMemo, useState } from "react";
import { Alert, Pressable, RefreshControl, StyleSheet, Text, View } from "react-native";

type FilterKey = "all" | "queue" | "clinic" | "checkout" | "done";
type BusyAction = { id: string; action: string } | null;

const FILTERS: { key: FilterKey; label: string }[] = [
  { key: "all", label: "الكل" },
  { key: "queue", label: "الانتظار" },
  { key: "clinic", label: "داخل العيادة" },
  { key: "checkout", label: "الحساب" },
  { key: "done", label: "مكتمل" }
];

export default function JourneyScreen() {
  const { user, can } = useSession();
  const canEditAppointments = can(OPERATIONAL_PERMISSION.appointments.edit);
  const canCreateQueue = can(OPERATIONAL_PERMISSION.clinicQueue.create);
  const canEditQueue = can(OPERATIONAL_PERMISSION.clinicQueue.edit);
  const canEditVisits = can(OPERATIONAL_PERMISSION.visits.edit);
  const [items, setItems] = useState<TodayJourneyItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [filter, setFilter] = useState<FilterKey>("all");
  const [busy, setBusy] = useState<BusyAction>(null);
  const date = isoDateLocal(new Date());

  const load = useCallback(async () => {
    setError(null);
    try {
      const result = await apiRequest<TodayJourneyItem[]>(
        `/api/patient-journey/today?date=${encodeURIComponent(date)}`
      );
      setItems(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحميل تشغيل اليوم");
    }
  }, [date]);

  useFocusEffect(useCallback(() => { void load(); }, [load]));

  const visibleItems = useMemo(() => items.filter((item) => {
    if (filter === "all") return true;
    if (filter === "queue") return item.queueStatus === "Waiting" || item.queueStatus === "Called";
    if (filter === "clinic") {
      return ["InRoom", "InProgress"].includes(item.appointmentStatus) ||
        ["InRoom", "InProgress"].includes(item.queueStatus ?? "");
    }
    if (filter === "checkout") return item.checkoutStatus === "ReadyForCheckout";
    if (filter === "done") return item.appointmentStatus === "Completed" || item.checkoutStatus === "CheckedOut";
    return true;
  }), [filter, items]);

  async function refresh() {
    setRefreshing(true);
    try { await load(); } finally { setRefreshing(false); }
  }

  async function mutate(item: TodayJourneyItem, action: string, path: string, init: RequestInit) {
    const key = item.visitId ?? item.appointmentId ?? item.queueItemId ?? item.patientId;
    setBusy({ id: key, action });
    setError(null);
    try {
      await apiRequest<unknown>(path, init);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تنفيذ الإجراء");
    } finally {
      setBusy(null);
    }
  }

  function confirmAppointment(item: TodayJourneyItem) {
    if (!canEditAppointments) return setError("مفتاح appointments.edit غير مفعّل لهذا الحساب.");
    if (!item.appointmentId) return;
    void mutate(item, "confirm", `/api/appointments/${item.appointmentId}/status`, {
      method: "PUT",
      body: JSON.stringify({ status: "Confirmed" })
    });
  }

  function intake(item: TodayJourneyItem) {
    if (!item.appointmentId) return;
    void mutate(item, "intake", `/api/patient-journey/${item.appointmentId}/intake`, {
      method: "POST",
      body: JSON.stringify({})
    });
  }

  function sendToQueue(item: TodayJourneyItem) {
    if (!canCreateQueue) return setError("مفتاح clinic_queue.create غير مفعّل لهذا الحساب.");
    if (!item.appointmentId) return;
    void mutate(item, "queue", `/api/patient-journey/${item.appointmentId}/send-to-queue`, {
      method: "POST",
      body: JSON.stringify({})
    });
  }

  function callPatient(item: TodayJourneyItem) {
    if (!canEditQueue) return setError("مفتاح clinic_queue.edit غير مفعّل لهذا الحساب.");
    if (!item.queueItemId) return;
    void mutate(item, "call", `/api/clinic-queue/${item.queueItemId}/call`, {
      method: "POST",
      body: JSON.stringify(item.roomName ? { roomName: item.roomName } : {})
    });
  }

  function enterRoom(item: TodayJourneyItem) {
    if (!canEditQueue) return setError("مفتاح clinic_queue.edit غير مفعّل لهذا الحساب.");
    if (!item.queueItemId) return;
    void mutate(item, "room", `/api/clinic-queue/${item.queueItemId}/enter-room`, { method: "POST" });
  }

  function startVisit(item: TodayJourneyItem) {
    if (!item.appointmentId) return;
    void mutate(item, "start", `/api/patient-journey/${item.appointmentId}/start-visit`, {
      method: "POST",
      body: JSON.stringify({})
    });
  }

  function checkout(item: TodayJourneyItem) {
    const id = item.visitId ?? item.appointmentId;
    if (!id) return;
    Alert.alert("إنهاء رحلة المريض", "تأكيد إنهاء checkout لهذه الزيارة؟", [
      { text: "إلغاء", style: "cancel" },
      {
        text: "تأكيد",
        onPress: () => void mutate(item, "checkout", `/api/patient-journey/${id}/checkout`, {
          method: "POST",
          body: JSON.stringify({})
        })
      }
    ]);
  }

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      <View>
        <Text style={styles.title}>تشغيل اليوم</Text>
        <Text style={styles.subtitle}>{date}</Text>
      </View>

      <View style={styles.filters}>
        {FILTERS.map((entry) => (
          <Pressable key={entry.key} onPress={() => setFilter(entry.key)} style={[styles.filter, filter === entry.key && styles.filterActive]}>
            <Text style={[styles.filterText, filter === entry.key && styles.filterTextActive]}>{entry.label}</Text>
          </Pressable>
        ))}
      </View>

      {error ? <StateMessage title="تعذر إكمال تشغيل اليوم" message={error} /> : null}

      <SectionTitle>المرضى ({visibleItems.length})</SectionTitle>
      {visibleItems.length === 0 ? (
        <StateMessage title="لا توجد حالات في هذا القسم" message="اسحب للأسفل لتحديث القائمة." />
      ) : visibleItems.map((item) => (
        <JourneyCard
          key={`${item.appointmentId ?? item.queueItemId ?? item.visitId ?? item.patientId}`}
          item={item}
          userRole={user?.role ?? ""}
          busy={busy}
          canEditAppointments={canEditAppointments}
          canCreateQueue={canCreateQueue}
          canEditQueue={canEditQueue}
          canEditVisits={canEditVisits}
          onConfirm={() => confirmAppointment(item)}
          onIntake={() => intake(item)}
          onQueue={() => sendToQueue(item)}
          onCall={() => callPatient(item)}
          onEnterRoom={() => enterRoom(item)}
          onStart={() => startVisit(item)}
          onCheckout={() => checkout(item)}
        />
      ))}
    </Screen>
  );
}

function JourneyCard({
  item,
  userRole,
  busy,
  canEditAppointments,
  canCreateQueue,
  canEditQueue,
  canEditVisits,
  onConfirm,
  onIntake,
  onQueue,
  onCall,
  onEnterRoom,
  onStart,
  onCheckout
}: {
  item: TodayJourneyItem;
  userRole: string;
  busy: BusyAction;
  canEditAppointments: boolean;
  canCreateQueue: boolean;
  canEditQueue: boolean;
  canEditVisits: boolean;
  onConfirm: () => void;
  onIntake: () => void;
  onQueue: () => void;
  onCall: () => void;
  onEnterRoom: () => void;
  onStart: () => void;
  onCheckout: () => void;
}) {
  const fakeUser = { role: userRole } as Parameters<typeof canReceptionJourney>[0];
  const reception = canReceptionJourney(fakeUser);
  const clinical = canClinicalJourney(fakeUser);
  const itemKey = item.visitId ?? item.appointmentId ?? item.queueItemId ?? item.patientId;
  const loading = busy?.id === itemKey;
  const blockedForPayment = item.paymentBeforeEntryRequired === true;

  return (
    <Card>
      <Pressable onPress={() => router.push({ pathname: "/(app)/patients/[id]", params: { id: item.patientId } })}>
        <View style={styles.cardHeader}>
          <View style={styles.statusChip}>
            <Text style={styles.statusText}>{journeyStatusLabel(item.checkoutStatus ?? item.queueStatus ?? item.appointmentStatus)}</Text>
          </View>
          <View style={styles.patientBlock}>
            <Text style={styles.patientName}>{item.patientName}</Text>
            <Text style={styles.patientMeta}>{[item.patientNumber, item.appointmentTime, item.doctorName].filter(Boolean).join(" • ")}</Text>
          </View>
        </View>
      </Pressable>

      <View style={styles.rowWrap}>
        {item.serviceName ? <Info label="الخدمة" value={item.serviceName} /> : null}
        {item.roomName ? <Info label="الغرفة" value={item.roomName} /> : null}
        {item.appointmentType ? <Info label="النوع" value={item.appointmentType} /> : null}
        <Info label="الإجراء التالي" value={journeyActionLabel(item.nextAction)} />
      </View>

      {item.hasActiveOrthoCase ? (
        <View style={styles.orthoBox}>
          <Text style={styles.orthoTitle}>حالة تقويم نشطة</Text>
          <Text style={styles.orthoText}>{[item.orthoCaseNumber, item.orthoCurrentStage].filter(Boolean).join(" • ") || "—"}</Text>
        </View>
      ) : null}

      {item.amountDueReference != null ? <Text style={styles.amount}>المبلغ المرجعي: {formatYemeniRial(item.amountDueReference)}</Text> : null}

      {blockedForPayment ? (
        <Text style={styles.paymentWarning}>{item.financialEntryReason || "يلزم تسوية متطلب مالي قبل دخول المريض."}</Text>
      ) : null}

      <View style={styles.actions}>
        {reception && canEditAppointments && item.appointmentId && item.appointmentStatus === "Scheduled" ? (
          <PrimaryButton title="تأكيد الموعد" onPress={onConfirm} loading={loading && busy?.action === "confirm"} />
        ) : null}
        {reception && item.appointmentId && item.nextAction === "Intake" ? (
          <PrimaryButton title="تسجيل الوصول" onPress={onIntake} loading={loading && busy?.action === "intake"} />
        ) : null}
        {reception && canCreateQueue && item.appointmentId && item.nextAction === "SendToQueue" && !blockedForPayment ? (
          <PrimaryButton title="إضافة للانتظار" onPress={onQueue} loading={loading && busy?.action === "queue"} />
        ) : null}
        {reception && canEditQueue && item.queueItemId && item.nextAction === "CallPatient" ? (
          <PrimaryButton title="نداء المريض" onPress={onCall} loading={loading && busy?.action === "call"} />
        ) : null}
        {reception && canEditQueue && item.queueItemId && item.nextAction === "EnterRoom" ? (
          <PrimaryButton title="دخول الغرفة" onPress={onEnterRoom} loading={loading && busy?.action === "room"} />
        ) : null}
        {clinical && item.appointmentId && item.nextAction === "StartVisit" && !blockedForPayment ? (
          <PrimaryButton title="بدء الزيارة" onPress={onStart} loading={loading && busy?.action === "start"} />
        ) : null}
        {clinical && canEditVisits && item.visitId && (item.nextAction === "Handoff" || item.nextAction === "InProgress") ? (
          <PrimaryButton
            title="تسليم الزيارة للاستقبال"
            onPress={() => router.push({
              pathname: "/(app)/journey-handoff",
              params: { visitId: item.visitId!, patientId: item.patientId, patientName: item.patientName }
            })}
          />
        ) : null}
        {reception && item.checkoutStatus === "ReadyForCheckout" ? (
          <PrimaryButton title="إنهاء الحساب والخروج" onPress={onCheckout} loading={loading && busy?.action === "checkout"} />
        ) : null}
      </View>
    </Card>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return <View style={styles.info}><Text style={styles.infoLabel}>{label}</Text><Text style={styles.infoValue}>{value}</Text></View>;
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 26, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.muted, marginTop: 4, textAlign: "right" },
  filters: { flexDirection: "row-reverse", flexWrap: "wrap", gap: spacing.xs },
  filter: { borderWidth: 1, borderColor: colors.border, backgroundColor: colors.surface, borderRadius: radius.sm, paddingHorizontal: spacing.sm, paddingVertical: 8 },
  filterActive: { backgroundColor: colors.primary, borderColor: colors.primary },
  filterText: { color: colors.text, fontWeight: "700" },
  filterTextActive: { color: "#fff" },
  cardHeader: { flexDirection: "row", justifyContent: "space-between", gap: spacing.sm },
  patientBlock: { flex: 1 },
  patientName: { color: colors.text, fontSize: 18, fontWeight: "800", textAlign: "right" },
  patientMeta: { color: colors.muted, marginTop: 4, textAlign: "right", lineHeight: 20 },
  statusChip: { alignSelf: "flex-start", backgroundColor: colors.primarySoft, borderRadius: 999, paddingHorizontal: 10, paddingVertical: 5 },
  statusText: { color: colors.primary, fontWeight: "800", fontSize: 12 },
  rowWrap: { marginTop: spacing.md, gap: spacing.xs },
  info: { flexDirection: "row", justifyContent: "space-between", gap: spacing.sm },
  infoLabel: { color: colors.muted },
  infoValue: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" },
  orthoBox: { marginTop: spacing.md, padding: spacing.sm, borderRadius: radius.sm, backgroundColor: colors.primarySoft },
  orthoTitle: { color: colors.primary, fontWeight: "800", textAlign: "right" },
  orthoText: { color: colors.text, marginTop: 3, textAlign: "right" },
  amount: { color: colors.text, marginTop: spacing.sm, textAlign: "right", fontWeight: "700" },
  paymentWarning: { color: colors.danger, backgroundColor: colors.dangerSoft, padding: spacing.sm, borderRadius: radius.sm, marginTop: spacing.sm, textAlign: "right", lineHeight: 21 },
  actions: { marginTop: spacing.md, gap: spacing.sm }
});
