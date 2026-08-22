import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import {
  allowedSurgeryTransitions,
  canUseSurgery,
  normalizeSurgeryStatus,
  REFERRAL_STATUS_LABELS,
  SURGERY_STATUS_LABELS,
  type HospitalReferral,
  type OperativeReport,
  type PostopRecord,
  type PreopReport,
  type SurgeryCase
} from "@/lib/surgery";
import { colors, radius, spacing } from "@/theme";
import { router, useFocusEffect, useLocalSearchParams } from "expo-router";
import React, { useCallback, useState } from "react";
import { ActivityIndicator, Pressable, RefreshControl, StyleSheet, Text, View } from "react-native";

export default function SurgeryCaseScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ id: string; patientName?: string }>();
  const id = Array.isArray(params.id) ? params.id[0] : params.id;
  const fallbackPatientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const allowed = canUseSurgery(user?.role);

  const [item, setItem] = useState<SurgeryCase | null>(null);
  const [preop, setPreop] = useState<PreopReport | null>(null);
  const [operative, setOperative] = useState<OperativeReport | null>(null);
  const [postop, setPostop] = useState<PostopRecord | null>(null);
  const [referrals, setReferrals] = useState<HospitalReferral[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [statusBusy, setStatusBusy] = useState<string | null>(null);
  const [approving, setApproving] = useState(false);

  const load = useCallback(async () => {
    if (!id || !allowed) {
      setLoading(false);
      return;
    }
    setError(null);
    const results = await Promise.allSettled([
      apiRequest<SurgeryCase>(`/api/surgery-cases/${id}`),
      apiRequest<PreopReport | null>(`/api/surgery-cases/${id}/preop`),
      apiRequest<OperativeReport | null>(`/api/surgery-cases/${id}/operative`),
      apiRequest<PostopRecord | null>(`/api/surgery-cases/${id}/postop`),
      apiRequest<HospitalReferral[]>(`/api/surgery-cases/${id}/referrals`)
    ]);

    const [caseResult, preopResult, operativeResult, postopResult, referralsResult] = results;
    if (caseResult.status === "fulfilled") setItem(caseResult.value);
    else setError(caseResult.reason instanceof Error ? caseResult.reason.message : "تعذر تحميل الحالة الجراحية");
    if (preopResult.status === "fulfilled") setPreop(preopResult.value);
    if (operativeResult.status === "fulfilled") setOperative(operativeResult.value);
    if (postopResult.status === "fulfilled") setPostop(postopResult.value);
    if (referralsResult.status === "fulfilled") setReferrals(referralsResult.value ?? []);
    setLoading(false);
  }, [allowed, id]);

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

  async function updateStatus(nextStatus: string) {
    if (!item || statusBusy) return;
    setStatusBusy(nextStatus);
    setError(null);
    try {
      await apiRequest<{ id: string; status: string }>(`/api/surgery-cases/${item.id}/status`, {
        method: "PUT",
        body: JSON.stringify({ status: nextStatus })
      });
      setItem((current) => (current ? { ...current, status: nextStatus } : current));
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحديث حالة الجراحة");
    } finally {
      setStatusBusy(null);
    }
  }

  async function approveOperative() {
    if (!item || approving) return;
    setApproving(true);
    setError(null);
    try {
      const result = await apiRequest<{ approvedAt?: string | null }>(
        `/api/surgery-cases/${item.id}/operative/approve`,
        { method: "PUT" }
      );
      setOperative((current) => (current ? { ...current, approvedAt: result.approvedAt ?? new Date().toISOString() } : current));
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر اعتماد التقرير الجراحي");
    } finally {
      setApproving(false);
    }
  }

  if (!allowed) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="وحدة جراحة الفم متاحة للأدمن وجراح الفم فقط." />
      </Screen>
    );
  }

  if (loading && !item) {
    return (
      <Screen>
        <ActivityIndicator size="large" color={colors.primary} />
      </Screen>
    );
  }

  if (!item) {
    return (
      <Screen>
        <StateMessage
          title="تعذر فتح الحالة الجراحية"
          message={error ?? "الحالة غير موجودة"}
          action={<PrimaryButton title="إعادة المحاولة" onPress={() => void load()} />}
        />
      </Screen>
    );
  }

  const status = normalizeSurgeryStatus(item.status);
  const transitions = allowedSurgeryTransitions(status);
  const checklistValues = Object.values(preop?.checklist ?? {});
  const checklistDone = checklistValues.filter(Boolean).length;

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      <View>
        <Text style={styles.title}>{item.caseNumber}</Text>
        <Text style={styles.subtitle}>{item.patientName || fallbackPatientName || "الحالة الجراحية"}</Text>
      </View>

      {error ? <StateMessage title="تنبيه" message={error} /> : null}

      <Card>
        <View style={styles.header}>
          <Text style={styles.status}>{SURGERY_STATUS_LABELS[status] ?? item.status}</Text>
          <View style={styles.headerText}>
            <Text style={styles.itemTitle}>{item.surgeryType}</Text>
            <Text style={styles.meta}>{item.teethInvolved ? `الأسنان: ${item.teethInvolved}` : "لا توجد أسنان محددة"}</Text>
          </View>
        </View>
        <Row label="الجراح" value={item.doctorName ? `د. ${item.doctorName}` : "—"} />
        <Row label="تاريخ الإنشاء" value={item.createdAt || "—"} last />
      </Card>

      {transitions.length > 0 ? (
        <>
          <SectionTitle>تغيير حالة الجراحة</SectionTitle>
          <View style={styles.actions}>
            {transitions.map((next) => (
              <StatusAction
                key={next}
                status={next}
                disabled={statusBusy !== null}
                loading={statusBusy === next}
                onPress={() => void updateStatus(next)}
              />
            ))}
          </View>
        </>
      ) : null}

      <SectionTitle>ما قبل الجراحة</SectionTitle>
      <Card>
        <Row label="تاريخ الجراحة" value={preop?.surgeryDate || "غير مسجل"} />
        <Row label="المكان" value={preop?.surgeryLocation || "—"} />
        <Row label="التخدير" value={preop?.anesthesiaType || "—"} />
        <Row label="الموافقة" value={preop?.consentSigned ? "موقعة" : "غير موقعة"} />
        <Row
          label="Checklist"
          value={checklistValues.length > 0 ? `${checklistDone}/${checklistValues.length}` : "غير مسجل"}
          last
        />
        {preop?.requiredTests?.length ? (
          <Text style={styles.body}>الفحوص المطلوبة: {preop.requiredTests.join("، ")}</Text>
        ) : null}
      </Card>
      <PrimaryButton
        title={preop ? "تعديل ما قبل الجراحة" : "إضافة ما قبل الجراحة"}
        onPress={() => router.push({ pathname: "/(app)/surgery-preop", params: { id: item.id, patientName: item.patientName } })}
      />

      <SectionTitle>تقرير الجراحة</SectionTitle>
      <Card>
        {operative ? (
          <>
            <Row label="التاريخ والوقت" value={operative.surgeryDateTime || "—"} />
            <Row label="المدة" value={operative.durationMinutes != null ? `${operative.durationMinutes} دقيقة` : "—"} />
            <Row label="التخدير" value={operative.anesthesiaUsed || "—"} />
            <Row label="النتيجة" value={operative.outcome || "—"} />
            <Row label="الغرز" value={operative.suturesCount != null ? String(operative.suturesCount) : "—"} />
            <Row label="عينة مرضية" value={operative.specimenSent ? "نعم" : "لا"} />
            <Row label="الاعتماد" value={operative.approvedAt ? `معتمد ${operative.approvedAt}` : "غير معتمد"} last />
            {operative.detailedDescription ? <Text style={styles.body}>{operative.detailedDescription}</Text> : null}
            {operative.complications ? <Text style={styles.warning}>مضاعفات: {operative.complications}</Text> : null}
          </>
        ) : (
          <Text style={styles.empty}>لا يوجد تقرير جراحي بعد.</Text>
        )}
      </Card>
      <PrimaryButton
        title={operative ? "تعديل تقرير الجراحة" : "إضافة تقرير الجراحة"}
        onPress={() => router.push({ pathname: "/(app)/surgery-operative", params: { id: item.id, patientName: item.patientName } })}
      />
      {operative && !operative.approvedAt ? (
        <PrimaryButton title="اعتماد تقرير الجراحة" loading={approving} onPress={() => void approveOperative()} />
      ) : null}

      <SectionTitle>ما بعد الجراحة</SectionTitle>
      <Card>
        {postop ? (
          <>
            <Text style={styles.body}>{postop.instructions || "لا توجد تعليمات مسجلة"}</Text>
            <Row label="الأدوية" value={String(postop.prescription?.length ?? 0)} />
            <Row label="المتابعات" value={String(postop.followupSchedule?.length ?? 0)} last />
          </>
        ) : (
          <Text style={styles.empty}>لا يوجد سجل ما بعد الجراحة بعد.</Text>
        )}
      </Card>
      <PrimaryButton
        title={postop ? "تعديل ما بعد الجراحة" : "إضافة ما بعد الجراحة"}
        onPress={() => router.push({ pathname: "/(app)/surgery-postop", params: { id: item.id, patientName: item.patientName } })}
      />

      <SectionTitle>إحالات المستشفيات</SectionTitle>
      <PrimaryButton
        title="إضافة إحالة مستشفى"
        onPress={() => router.push({ pathname: "/(app)/surgery-referral-new", params: { id: item.id, patientName: item.patientName } })}
      />
      {referrals.length === 0 ? <StateMessage title="لا توجد إحالات مرتبطة" /> : null}
      {referrals.map((referral) => (
        <Card key={referral.id}>
          <View style={styles.header}>
            <Text style={styles.referralStatus}>{REFERRAL_STATUS_LABELS[referral.status] ?? referral.status}</Text>
            <View style={styles.headerText}>
              <Text style={styles.itemTitle}>{referral.hospitalName || "مستشفى غير محدد"}</Text>
              <Text style={styles.meta}>{referral.referralDate || referral.createdAt || "—"}</Text>
            </View>
          </View>
          {referral.reason ? <Text style={styles.body}>السبب: {referral.reason}</Text> : null}
          {referral.notes ? <Text style={styles.body}>{referral.notes}</Text> : null}
        </Card>
      ))}
    </Screen>
  );
}

function StatusAction({
  status,
  onPress,
  disabled,
  loading
}: {
  status: string;
  onPress: () => void;
  disabled: boolean;
  loading: boolean;
}) {
  const danger = status === "cancelled";
  return (
    <Pressable
      disabled={disabled}
      onPress={onPress}
      style={[styles.statusAction, danger && styles.statusActionDanger, disabled && styles.disabled]}
    >
      {loading ? (
        <ActivityIndicator color={danger ? colors.danger : colors.primary} />
      ) : (
        <Text style={[styles.statusActionText, danger && styles.statusActionDangerText]}>
          {SURGERY_STATUS_LABELS[status] ?? status}
        </Text>
      )}
    </Pressable>
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
  itemTitle: { color: colors.text, fontSize: 17, fontWeight: "800", textAlign: "right" },
  meta: { color: colors.muted, marginTop: 4, textAlign: "right", fontSize: 12 },
  status: {
    color: colors.primary,
    backgroundColor: colors.primarySoft,
    paddingHorizontal: spacing.sm,
    paddingVertical: 5,
    borderRadius: 999,
    fontWeight: "800",
    fontSize: 12
  },
  referralStatus: {
    color: colors.warning,
    backgroundColor: colors.warningSoft,
    paddingHorizontal: spacing.sm,
    paddingVertical: 5,
    borderRadius: 999,
    fontWeight: "800",
    fontSize: 12
  },
  actions: { flexDirection: "row-reverse", flexWrap: "wrap", gap: spacing.sm },
  statusAction: {
    minWidth: 120,
    minHeight: 44,
    alignItems: "center",
    justifyContent: "center",
    borderWidth: 1,
    borderColor: colors.primary,
    borderRadius: radius.sm,
    backgroundColor: colors.primarySoft,
    paddingHorizontal: spacing.md
  },
  statusActionDanger: { borderColor: colors.danger, backgroundColor: colors.dangerSoft },
  statusActionText: { color: colors.primary, fontWeight: "800" },
  statusActionDangerText: { color: colors.danger },
  disabled: { opacity: 0.5 },
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
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" },
  body: { color: colors.text, marginTop: spacing.sm, textAlign: "right", lineHeight: 22 },
  warning: { color: colors.warning, marginTop: spacing.sm, textAlign: "right", lineHeight: 22 },
  empty: { color: colors.muted, textAlign: "right" }
});
