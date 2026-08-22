import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import {
  canUseOrthodontics,
  ORTHO_STATUS_LABELS,
  STAGE_STATUS_LABELS,
  type OrthoCase,
  type OrthoVisit
} from "@/lib/ortho";
import { colors, radius, spacing } from "@/theme";
import { router, useFocusEffect, useLocalSearchParams } from "expo-router";
import React, { useCallback, useState } from "react";
import { ActivityIndicator, RefreshControl, StyleSheet, Text, View } from "react-native";

export default function OrthoCaseScreen() {
  const { user, can } = useSession();
  const params = useLocalSearchParams<{ id: string; patientName?: string }>();
  const id = Array.isArray(params.id) ? params.id[0] : params.id;
  const [orthoCase, setOrthoCase] = useState<OrthoCase | null>(null);
  const [visits, setVisits] = useState<OrthoVisit[]>([]);
  const [visitError, setVisitError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

  const allowed = canUseOrthodontics(user?.role);
  const canReadLab = can("lab_orders.view");

  const load = useCallback(async () => {
    if (!id || !allowed) {
      setLoading(false);
      return;
    }
    setError(null);
    setVisitError(null);

    const [caseResult, visitsResult] = await Promise.allSettled([
      apiRequest<OrthoCase>(`/api/ortho-cases/${id}`),
      apiRequest<OrthoVisit[]>(`/api/ortho-cases/${id}/visits`)
    ]);

    if (caseResult.status === "fulfilled") {
      setOrthoCase(caseResult.value);
    } else {
      setOrthoCase(null);
      setError(caseResult.reason instanceof Error ? caseResult.reason.message : "تعذر تحميل حالة التقويم");
    }

    if (visitsResult.status === "fulfilled") {
      setVisits(visitsResult.value);
    } else {
      setVisits([]);
      setVisitError(visitsResult.reason instanceof Error ? visitsResult.reason.message : "تعذر تحميل زيارات التقويم");
    }
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

  if (!allowed) {
    return <Screen><StateMessage title="غير مصرح" message="هذه الوحدة مخصصة لأخصائي التقويم والإدارة." /></Screen>;
  }

  if (loading && !orthoCase) {
    return <Screen><ActivityIndicator size="large" color={colors.primary} /></Screen>;
  }

  if (!orthoCase) {
    return (
      <Screen>
        <StateMessage title="تعذر فتح حالة التقويم" message={error ?? "الحالة غير موجودة"} action={<PrimaryButton title="إعادة المحاولة" onPress={() => void load()} />} />
      </Screen>
    );
  }

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      <View>
        <Text style={styles.title}>{orthoCase.caseNumber}</Text>
        <Text style={styles.subtitle}>{orthoCase.patientName}</Text>
      </View>

      <Card>
        <Row label="الحالة" value={ORTHO_STATUS_LABELS[orthoCase.status] ?? orthoCase.status} />
        <Row label="الطبيب" value={orthoCase.doctorName || "—"} />
        <Row label="الجهاز" value={orthoCase.applianceType || "—"} />
        <Row label="تاريخ البدء" value={orthoCase.startDate || "—"} />
        <Row label="المرحلة الحالية" value={orthoCase.currentStage || "—"} />
        <Row label="التقدم" value={`${orthoCase.stagePercentage}%`} />
        <Row label="المدة المتوقعة" value={orthoCase.expectedDurationMonths ? `${orthoCase.expectedDurationMonths} شهر` : "—"} last={!orthoCase.totalFee} />
        {orthoCase.totalFee != null ? <Row label="القيمة المسجلة للحالة" value={orthoCase.totalFee.toLocaleString()} last /> : null}
      </Card>

      <View style={styles.progressTrack}>
        <View style={[styles.progressFill, { width: `${Math.max(0, Math.min(100, orthoCase.stagePercentage))}%` }]} />
      </View>

      {orthoCase.status.toLowerCase() === "active" ? (
        <PrimaryButton
          title="تسجيل زيارة تقويمية"
          onPress={() => router.push({
            pathname: "/(app)/ortho-visit-new",
            params: { caseId: orthoCase.id, patientName: orthoCase.patientName, currentStage: orthoCase.currentStage ?? "", doctorId: orthoCase.doctorId ?? "" }
          })}
        />
      ) : null}

      <PrimaryButton
        title="صور وأشعة هذه الحالة"
        onPress={() => router.push({
          pathname: "/(app)/patient-media",
          params: { patientId: orthoCase.patientId, patientName: orthoCase.patientName, orthoCaseId: orthoCase.id }
        })}
      />

      {canReadLab ? (
        <PrimaryButton
          title="طلبات المعمل لهذه الحالة"
          onPress={() => router.push({
            pathname: "/(app)/patient-lab",
            params: { patientId: orthoCase.patientId, patientName: orthoCase.patientName, orthoCaseId: orthoCase.id }
          })}
        />
      ) : null}

      <SectionTitle>مراحل العلاج</SectionTitle>
      {orthoCase.stages?.length ? (
        orthoCase.stages.map((stage) => (
          <Card key={stage.id}>
            <View style={styles.stageHeader}>
              <View style={styles.stageBadge}><Text style={styles.stageBadgeText}>{STAGE_STATUS_LABELS[stage.status] ?? stage.status}</Text></View>
              <View style={{ flex: 1 }}>
                <Text style={styles.stageName}>{stage.stageOrder}. {stage.stageName}</Text>
                {stage.targetDurationMonths ? <Text style={styles.meta}>المدة المستهدفة: {stage.targetDurationMonths} شهر</Text> : null}
              </View>
            </View>
            {stage.startedAt ? <Text style={styles.meta}>بدأت: {stage.startedAt}</Text> : null}
            {stage.completedAt ? <Text style={styles.meta}>اكتملت: {stage.completedAt}</Text> : null}
            {stage.notes ? <Text style={styles.notes}>{stage.notes}</Text> : null}
          </Card>
        ))
      ) : <StateMessage title="لا توجد مراحل علاج مسجلة" />}

      {orthoCase.extractionDecisionValue || orthoCase.retentionPlan ? (
        <>
          <SectionTitle>قرارات الخطة</SectionTitle>
          <Card>
            {orthoCase.extractionDecisionValue ? <Row label="قرار الخلع" value={orthoCase.extractionDecisionValue} /> : null}
            {orthoCase.retentionPlan ? <Row label="خطة التثبيت" value={orthoCase.retentionPlan} last /> : null}
          </Card>
        </>
      ) : null}

      <SectionTitle>زيارات التقويم</SectionTitle>
      {visitError ? <StateMessage title="تعذر تحميل زيارات التقويم" message={visitError} /> : visits.length === 0 ? <StateMessage title="لا توجد زيارات تقويمية مسجلة" /> : visits.map((visit) => (
        <Card key={visit.id}>
          <View style={styles.visitHeader}><Text style={styles.visitDate}>{visit.visitDate}</Text><Text style={styles.visitNumber}>زيارة #{visit.visitNumber}</Text></View>
          {visit.currentStage ? <Row label="المرحلة" value={visit.currentStage} /> : null}
          {visit.wireUpper ? <Row label="السلك العلوي" value={visit.wireUpper} /> : null}
          {visit.wireLower ? <Row label="السلك السفلي" value={visit.wireLower} /> : null}
          {visit.elasticsType ? <Row label="المطاط" value={visit.elasticsType} /> : null}
          {visit.currentOverjet != null ? <Row label="Overjet" value={`${visit.currentOverjet} mm`} /> : null}
          {visit.currentOverbite != null ? <Row label="Overbite" value={`${visit.currentOverbite} mm`} /> : null}
          {visit.clinicalNotes ? <Text style={styles.notes}>{visit.clinicalNotes}</Text> : null}
          {visit.nextAppointmentDate ? <Text style={styles.next}>الموعد القادم: {visit.nextAppointmentDate}{visit.nextAppointmentType ? ` • ${visit.nextAppointmentType}` : ""}</Text> : null}
        </Card>
      ))}
    </Screen>
  );
}

function Row({ label, value, last = false }: { label: string; value: string; last?: boolean }) {
  return <View style={[styles.row, last && { borderBottomWidth: 0 }]}><Text style={styles.value}>{value}</Text><Text style={styles.label}>{label}</Text></View>;
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  row: { minHeight: 44, flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.border },
  label: { color: colors.muted, textAlign: "right" },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" },
  progressTrack: { height: 10, backgroundColor: colors.border, borderRadius: radius.sm, overflow: "hidden" },
  progressFill: { height: "100%", backgroundColor: colors.primary },
  stageHeader: { flexDirection: "row", gap: spacing.sm },
  stageBadge: { alignSelf: "flex-start", borderRadius: 999, paddingHorizontal: spacing.sm, paddingVertical: 5, backgroundColor: colors.primarySoft },
  stageBadgeText: { color: colors.primary, fontSize: 11, fontWeight: "800" },
  stageName: { color: colors.text, fontWeight: "800", textAlign: "right" },
  meta: { color: colors.muted, marginTop: 4, textAlign: "right" },
  notes: { color: colors.text, backgroundColor: colors.background, borderRadius: radius.sm, padding: spacing.sm, marginTop: spacing.sm, textAlign: "right", lineHeight: 22 },
  visitHeader: { flexDirection: "row", justifyContent: "space-between", gap: spacing.sm },
  visitDate: { color: colors.primary, fontWeight: "800" },
  visitNumber: { color: colors.text, fontWeight: "800" },
  next: { color: colors.primary, marginTop: spacing.sm, textAlign: "right", fontWeight: "700" }
});
