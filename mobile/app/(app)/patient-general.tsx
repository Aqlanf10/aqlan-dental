import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import {
  canUseGeneralDentistry,
  PRIMARY_FDI_TEETH,
  PERMANENT_FDI_TEETH,
  TREATMENT_PLAN_PRIORITY_LABELS,
  TREATMENT_PLAN_STATUS_LABELS,
  toothConditionLabel,
  type DentalChart,
  type GeneralTreatment,
  type GeneralTreatmentPlanItem,
  type PerioRecord,
  type ToothCondition
} from "@/lib/general";
import { colors, radius, spacing } from "@/theme";
import { router, useFocusEffect, useLocalSearchParams } from "expo-router";
import React, { useCallback, useMemo, useState } from "react";
import {
  ActivityIndicator,
  Pressable,
  RefreshControl,
  StyleSheet,
  Text,
  View
} from "react-native";

type Dentition = "permanent" | "primary";

export default function PatientGeneralDentistryScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ patientId: string; patientName?: string }>();
  const patientId = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const allowed = canUseGeneralDentistry(user?.role);

  const [dentition, setDentition] = useState<Dentition>("permanent");
  const [chart, setChart] = useState<DentalChart | null>(null);
  const [treatments, setTreatments] = useState<GeneralTreatment[]>([]);
  const [plans, setPlans] = useState<GeneralTreatmentPlanItem[]>([]);
  const [perio, setPerio] = useState<PerioRecord[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [statusBusyId, setStatusBusyId] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!patientId || !allowed) {
      setLoading(false);
      return;
    }

    setError(null);
    const results = await Promise.allSettled([
      apiRequest<DentalChart>(`/api/dental-chart/${patientId}`),
      apiRequest<GeneralTreatment[]>(`/api/general-treatments/${patientId}`),
      apiRequest<GeneralTreatmentPlanItem[]>(`/api/general/treatment-plans/${patientId}`),
      apiRequest<PerioRecord[]>(`/api/general/perio/${patientId}`)
    ]);

    const errors: string[] = [];
    const [chartResult, treatmentsResult, plansResult, perioResult] = results;

    if (chartResult.status === "fulfilled") setChart(chartResult.value);
    else errors.push("مخطط الأسنان");

    if (treatmentsResult.status === "fulfilled") setTreatments(treatmentsResult.value ?? []);
    else errors.push("سجل العلاجات");

    if (plansResult.status === "fulfilled") setPlans(plansResult.value ?? []);
    else errors.push("خطة العلاج");

    if (perioResult.status === "fulfilled") setPerio(perioResult.value ?? []);
    else errors.push("سجل اللثة");

    if (errors.length > 0) {
      setError(`تعذر تحميل: ${errors.join("، ")}. بقية الأقسام المعروضة ما زالت صالحة.`);
    }
    setLoading(false);
  }, [allowed, patientId]);

  useFocusEffect(
    useCallback(() => {
      setLoading(true);
      void load();
    }, [load])
  );

  const toothMap = useMemo(
    () => new Map((chart?.teeth ?? []).map((item) => [item.toothNumber, item])),
    [chart]
  );

  async function refresh() {
    setRefreshing(true);
    try {
      await load();
    } finally {
      setRefreshing(false);
    }
  }

  async function updatePlanStatus(item: GeneralTreatmentPlanItem, status: string) {
    if (statusBusyId) return;
    setStatusBusyId(item.id);
    setError(null);
    try {
      const updated = await apiRequest<GeneralTreatmentPlanItem>(
        `/api/general/treatment-plans/${item.id}/status`,
        {
          method: "PATCH",
          body: JSON.stringify({ status })
        }
      );
      setPlans((current) => current.map((plan) => (plan.id === item.id ? updated : plan)));
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحديث حالة خطة العلاج");
    } finally {
      setStatusBusyId(null);
    }
  }

  function openTooth(toothNumber: string) {
    router.push({
      pathname: "/(app)/general-tooth",
      params: { patientId, patientName, toothNumber }
    });
  }

  if (!allowed) {
    return (
      <Screen>
        <StateMessage
          title="غير مصرح"
          message="وحدة الأسنان العامة متاحة للأدمن وطبيب الأسنان العام فقط، مطابقةً لصلاحية GeneralAccess في الخادم."
        />
      </Screen>
    );
  }

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      <View>
        <Text style={styles.title}>الأسنان العامة</Text>
        <Text style={styles.subtitle}>{patientName || "ملف المريض"}</Text>
      </View>

      {loading ? <ActivityIndicator size="large" color={colors.primary} /> : null}
      {error ? <StateMessage title="تنبيه تحميل" message={error} /> : null}

      <SectionTitle>مخطط الأسنان FDI</SectionTitle>
      <View style={styles.toggleRow}>
        <ToggleButton
          label="دائم"
          selected={dentition === "permanent"}
          onPress={() => setDentition("permanent")}
        />
        <ToggleButton
          label="لبني"
          selected={dentition === "primary"}
          onPress={() => setDentition("primary")}
        />
      </View>

      <Card>
        <Text style={styles.chartHint}>اضغط على السن لتسجيل الحالة والأسطح والعلاج والملاحظات.</Text>
        {dentition === "permanent" ? (
          <>
            <Quadrant title="الفك العلوي — يمين" teeth={PERMANENT_FDI_TEETH.slice(0, 8)} map={toothMap} onPress={openTooth} />
            <Quadrant title="الفك العلوي — يسار" teeth={PERMANENT_FDI_TEETH.slice(8, 16)} map={toothMap} onPress={openTooth} />
            <View style={styles.archDivider} />
            <Quadrant title="الفك السفلي — يمين" teeth={PERMANENT_FDI_TEETH.slice(16, 24)} map={toothMap} onPress={openTooth} />
            <Quadrant title="الفك السفلي — يسار" teeth={PERMANENT_FDI_TEETH.slice(24, 32)} map={toothMap} onPress={openTooth} />
          </>
        ) : (
          <>
            <Quadrant title="اللبني العلوي — يمين" teeth={PRIMARY_FDI_TEETH.slice(0, 5)} map={toothMap} onPress={openTooth} />
            <Quadrant title="اللبني العلوي — يسار" teeth={PRIMARY_FDI_TEETH.slice(5, 10)} map={toothMap} onPress={openTooth} />
            <View style={styles.archDivider} />
            <Quadrant title="اللبني السفلي — يمين" teeth={PRIMARY_FDI_TEETH.slice(10, 15)} map={toothMap} onPress={openTooth} />
            <Quadrant title="اللبني السفلي — يسار" teeth={PRIMARY_FDI_TEETH.slice(15, 20)} map={toothMap} onPress={openTooth} />
          </>
        )}
        {chart?.chartDate ? <Text style={styles.meta}>تاريخ المخطط: {chart.chartDate}</Text> : null}
      </Card>

      <SectionTitle>خطة العلاج</SectionTitle>
      <PrimaryButton
        title="إضافة عنصر لخطة العلاج"
        onPress={() =>
          router.push({
            pathname: "/(app)/general-plan-new",
            params: { patientId, patientName }
          })
        }
      />
      {plans.length === 0 && !loading ? <StateMessage title="لا توجد خطة علاج مسجلة" /> : null}
      {plans.slice(0, 12).map((item) => (
        <Card key={item.id}>
          <View style={styles.cardHeader}>
            <Text style={styles.badge}>{TREATMENT_PLAN_STATUS_LABELS[item.status] ?? item.status}</Text>
            <View style={styles.cardHeaderText}>
              <Text style={styles.itemTitle}>{item.treatment}</Text>
              <Text style={styles.meta}>
                {item.toothNumber ? `سن ${item.toothNumber} • ` : ""}
                أولوية {TREATMENT_PLAN_PRIORITY_LABELS[item.priority] ?? item.priority}
              </Text>
            </View>
          </View>
          {item.notes ? <Text style={styles.body}>{item.notes}</Text> : null}
          {item.estimatedCost != null ? (
            <Text style={styles.moneyWarning}>قيمة تقديرية مسجلة: {item.estimatedCost} — العملة غير مرفقة من API الحالي.</Text>
          ) : null}
          {item.doctorName ? <Text style={styles.meta}>د. {item.doctorName}</Text> : null}
          {item.status === "planned" ? (
            <View style={styles.actionRow}>
              <MiniAction title="بدء التنفيذ" disabled={statusBusyId !== null} onPress={() => void updatePlanStatus(item, "in_progress")} />
              <MiniAction title="إلغاء" danger disabled={statusBusyId !== null} onPress={() => void updatePlanStatus(item, "cancelled")} />
            </View>
          ) : null}
          {item.status === "in_progress" ? (
            <View style={styles.actionRow}>
              <MiniAction title="إكمال" disabled={statusBusyId !== null} onPress={() => void updatePlanStatus(item, "completed")} />
              <MiniAction title="إلغاء" danger disabled={statusBusyId !== null} onPress={() => void updatePlanStatus(item, "cancelled")} />
            </View>
          ) : null}
        </Card>
      ))}

      <SectionTitle>العلاجات المنفذة</SectionTitle>
      <PrimaryButton
        title="تسجيل علاج عام"
        onPress={() =>
          router.push({
            pathname: "/(app)/general-treatment-new",
            params: { patientId, patientName }
          })
        }
      />
      {treatments.length === 0 && !loading ? <StateMessage title="لا توجد علاجات عامة مسجلة" /> : null}
      {treatments.slice(0, 12).map((item) => (
        <Card key={item.id}>
          <Text style={styles.itemTitle}>{item.treatmentType}</Text>
          <Text style={styles.meta}>
            {item.createdAt}{item.toothNumber ? ` • سن ${item.toothNumber}` : ""}{item.doctorName ? ` • د. ${item.doctorName}` : ""}
          </Text>
          {item.materialUsed ? <Row label="المادة" value={item.materialUsed} /> : null}
          {item.anesthesiaType ? <Row label="التخدير" value={item.anesthesiaType} /> : null}
          {item.notes ? <Text style={styles.body}>{item.notes}</Text> : null}
          {item.cost != null ? (
            <Text style={styles.moneyWarning}>قيمة مالية مسجلة: {item.cost} — لا أفترض عملتها لأن الـAPI لا يعيد العملة.</Text>
          ) : null}
        </Card>
      ))}

      <SectionTitle>سجل اللثة</SectionTitle>
      <PrimaryButton
        title="إضافة قياس لثوي"
        onPress={() =>
          router.push({
            pathname: "/(app)/general-perio-new",
            params: { patientId, patientName }
          })
        }
      />
      {perio.length === 0 && !loading ? <StateMessage title="لا توجد قياسات لثوية مسجلة" /> : null}
      {perio.slice(0, 12).map((item) => (
        <Card key={item.id}>
          <Text style={styles.itemTitle}>سن {item.toothNumber}</Text>
          <Text style={styles.meta}>{item.createdAt}{item.doctorName ? ` • د. ${item.doctorName}` : ""}</Text>
          <Row label="Probing depth" value={`${item.probingDepth} mm`} />
          <Row label="Clinical attachment" value={`${item.clinicalAttachment} mm`} />
          <Row label="Bleeding" value={item.bleedingOnProbing ? "نعم" : "لا"} />
          <Row label="Plaque / Gingival" value={`${item.plaqueIndex} / ${item.gingivalIndex}`} />
          <Row label="Furcation / Mobility" value={`${item.furcation} / ${item.mobility}`} />
          {item.notes ? <Text style={styles.body}>{item.notes}</Text> : null}
        </Card>
      ))}
    </Screen>
  );
}

function Quadrant({
  title,
  teeth,
  map,
  onPress
}: {
  title: string;
  teeth: readonly string[];
  map: Map<string, ToothCondition>;
  onPress: (toothNumber: string) => void;
}) {
  return (
    <View style={styles.quadrant}>
      <Text style={styles.quadrantTitle}>{title}</Text>
      <View style={styles.teethRow}>
        {teeth.map((toothNumber) => {
          const item = map.get(toothNumber);
          const recorded = Boolean(item && (item.condition || item.treatmentDone || item.notes || item.surfacesAffected));
          return (
            <Pressable
              key={toothNumber}
              accessibilityRole="button"
              accessibilityLabel={`سن ${toothNumber}: ${toothConditionLabel(item?.condition)}`}
              onPress={() => onPress(toothNumber)}
              style={({ pressed }) => [
                styles.tooth,
                recorded && styles.toothRecorded,
                pressed && styles.pressed
              ]}
            >
              <Text style={[styles.toothNumber, recorded && styles.toothNumberRecorded]}>{toothNumber}</Text>
              <Text style={[styles.toothMark, recorded && styles.toothMarkRecorded]}>{recorded ? "●" : "○"}</Text>
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}

function ToggleButton({ label, selected, onPress }: { label: string; selected: boolean; onPress: () => void }) {
  return (
    <Pressable onPress={onPress} style={[styles.toggle, selected && styles.toggleSelected]}>
      <Text style={[styles.toggleText, selected && styles.toggleTextSelected]}>{label}</Text>
    </Pressable>
  );
}

function MiniAction({
  title,
  onPress,
  danger = false,
  disabled = false
}: {
  title: string;
  onPress: () => void;
  danger?: boolean;
  disabled?: boolean;
}) {
  return (
    <Pressable
      disabled={disabled}
      onPress={onPress}
      style={[styles.miniAction, danger && styles.miniDanger, disabled && styles.disabled]}
    >
      <Text style={[styles.miniActionText, danger && styles.miniDangerText]}>{title}</Text>
    </Pressable>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.row}>
      <Text style={styles.rowValue}>{value}</Text>
      <Text style={styles.rowLabel}>{label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  toggleRow: { flexDirection: "row-reverse", gap: spacing.sm },
  toggle: {
    flex: 1,
    minHeight: 44,
    alignItems: "center",
    justifyContent: "center",
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.sm,
    backgroundColor: colors.surface
  },
  toggleSelected: { borderColor: colors.primary, backgroundColor: colors.primarySoft },
  toggleText: { color: colors.muted, fontWeight: "700" },
  toggleTextSelected: { color: colors.primary },
  chartHint: { color: colors.muted, textAlign: "right", lineHeight: 21 },
  quadrant: { marginTop: spacing.md, gap: spacing.xs },
  quadrantTitle: { color: colors.text, textAlign: "right", fontWeight: "700" },
  teethRow: { flexDirection: "row", flexWrap: "wrap", gap: spacing.xs, justifyContent: "flex-end" },
  tooth: {
    width: 44,
    minHeight: 52,
    alignItems: "center",
    justifyContent: "center",
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.sm,
    backgroundColor: colors.surface
  },
  toothRecorded: { borderColor: colors.primary, backgroundColor: colors.primarySoft },
  toothNumber: { color: colors.text, fontWeight: "800" },
  toothNumberRecorded: { color: colors.primary },
  toothMark: { color: colors.muted, fontSize: 12, marginTop: 2 },
  toothMarkRecorded: { color: colors.primary },
  archDivider: { height: 2, backgroundColor: colors.border, marginTop: spacing.md },
  pressed: { opacity: 0.72 },
  cardHeader: { flexDirection: "row", alignItems: "flex-start", justifyContent: "space-between", gap: spacing.sm },
  cardHeaderText: { flex: 1 },
  badge: {
    color: colors.primary,
    backgroundColor: colors.primarySoft,
    borderRadius: 999,
    paddingHorizontal: spacing.sm,
    paddingVertical: 5,
    fontWeight: "800",
    fontSize: 12
  },
  itemTitle: { color: colors.text, fontSize: 17, fontWeight: "800", textAlign: "right" },
  meta: { color: colors.muted, marginTop: 5, textAlign: "right", fontSize: 12 },
  body: { color: colors.text, marginTop: spacing.sm, textAlign: "right", lineHeight: 22 },
  moneyWarning: { color: colors.warning, marginTop: spacing.sm, textAlign: "right", lineHeight: 20, fontSize: 12 },
  actionRow: { flexDirection: "row-reverse", gap: spacing.sm, marginTop: spacing.md },
  miniAction: {
    flex: 1,
    minHeight: 40,
    alignItems: "center",
    justifyContent: "center",
    borderRadius: radius.sm,
    borderWidth: 1,
    borderColor: colors.primary,
    backgroundColor: colors.primarySoft,
    paddingHorizontal: spacing.sm
  },
  miniDanger: { borderColor: colors.danger, backgroundColor: colors.dangerSoft },
  miniActionText: { color: colors.primary, fontWeight: "800" },
  miniDangerText: { color: colors.danger },
  disabled: { opacity: 0.5 },
  row: {
    minHeight: 38,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    gap: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border
  },
  rowLabel: { color: colors.muted, textAlign: "right" },
  rowValue: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" }
});
