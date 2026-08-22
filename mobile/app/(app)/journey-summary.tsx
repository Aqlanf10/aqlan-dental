import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { formatYemeniRial } from "@/lib/format";
import { journeyActionLabel, journeyStatusLabel } from "@/lib/journey";
import type { DailyJourneySummary } from "@/lib/journeySummary";
import { colors, radius, spacing } from "@/theme";
import { router, useFocusEffect, useLocalSearchParams } from "expo-router";
import React, { useCallback, useState } from "react";
import { ActivityIndicator, RefreshControl, StyleSheet, Text, View } from "react-native";

export default function JourneySummaryScreen() {
  const params = useLocalSearchParams<{ patientId: string }>();
  const patientId = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const [summary, setSummary] = useState<DailyJourneySummary | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async () => {
    if (!patientId) return;
    setError(null);
    try {
      setSummary(
        await apiRequest<DailyJourneySummary>(`/api/patient-journey/${patientId}/daily-summary`)
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحميل ملخص رحلة المريض");
    } finally {
      setLoading(false);
    }
  }, [patientId]);

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

  if (loading && !summary) {
    return (
      <Screen>
        <ActivityIndicator size="large" color={colors.primary} />
      </Screen>
    );
  }

  if (!summary) {
    return (
      <Screen>
        <StateMessage
          title="تعذر فتح ملخص رحلة المريض"
          message={error ?? "لا توجد بيانات"}
          action={<PrimaryButton title="إعادة المحاولة" onPress={() => void load()} />}
        />
      </Screen>
    );
  }

  return (
    <Screen
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}
    >
      <View>
        <Text style={styles.title}>{summary.patient.fullName}</Text>
        <Text style={styles.subtitle}>{summary.patient.patientNumber}</Text>
      </View>

      {error ? <StateMessage title="تعذر تحديث بعض البيانات" message={error} /> : null}

      <Card>
        <Row label="مرحلة الرحلة" value={journeyStatusLabel(summary.journeyStep)} />
        <Row label="الإجراء التالي" value={journeyActionLabel(summary.nextAction)} last />
      </Card>

      {summary.medicalAlerts.length > 0 ? (
        <>
          <SectionTitle>تنبيهات طبية</SectionTitle>
          {summary.medicalAlerts.map((alert, index) => (
            <View
              key={`${alert.type}-${index}`}
              style={[
                styles.alert,
                alert.severity === "danger" ? styles.alertDanger : styles.alertWarning
              ]}
            >
              <Text style={styles.alertTitle}>{alert.label}</Text>
              <Text style={styles.alertText}>{alert.value}</Text>
            </View>
          ))}
        </>
      ) : null}

      <SectionTitle>موعد اليوم</SectionTitle>
      {summary.todayAppointment ? (
        <Card>
          <Row label="الوقت" value={summary.todayAppointment.startTime} />
          <Row label="الحالة" value={journeyStatusLabel(summary.todayAppointment.status)} />
          <Row label="الطبيب" value={summary.todayAppointment.doctorName || "—"} />
          <Row label="الغرفة" value={summary.todayAppointment.roomName || "—"} />
          <Row label="النوع" value={summary.todayAppointment.appointmentType || "—"} last />
        </Card>
      ) : (
        <StateMessage title="لا يوجد موعد اليوم" />
      )}

      {summary.queueStatus ? (
        <>
          <SectionTitle>الطابور</SectionTitle>
          <Card>
            <Row label="الحالة" value={journeyStatusLabel(summary.queueStatus.status)} />
            <Row label="الغرفة" value={summary.queueStatus.roomName || "—"} last />
          </Card>
        </>
      ) : null}

      {summary.todayVisit ? (
        <>
          <SectionTitle>زيارة اليوم</SectionTitle>
          <Card>
            <Row label="الشكوى" value={summary.todayVisit.chiefComplaint || "—"} />
            <Row label="التشخيص" value={summary.todayVisit.diagnosis || "—"} />
            <Row label="العلاج" value={summary.todayVisit.treatmentDone || "—"} />
            <Row label="خطة الزيارة القادمة" value={summary.todayVisit.nextVisitPlan || "—"} />
            <Row
              label="حالة الحساب"
              value={journeyStatusLabel(summary.todayVisit.checkoutStatus)}
              last
            />
          </Card>
        </>
      ) : null}

      {summary.financeSummary ? (
        <>
          <SectionTitle>الملخص المالي</SectionTitle>
          <Card>
            {summary.financeSummary.totalTreatmentCost != null ? (
              <Row
                label="إجمالي العلاج"
                value={formatYemeniRial(summary.financeSummary.totalTreatmentCost)}
              />
            ) : null}
            {summary.financeSummary.totalPaid != null ? (
              <Row
                label="إجمالي المدفوع"
                value={formatYemeniRial(summary.financeSummary.totalPaid)}
              />
            ) : null}
            <Row
              label="الرصيد المستحق"
              value={formatYemeniRial(summary.financeSummary.outstandingBalance)}
            />
            <Row
              label="المتأخر"
              value={formatYemeniRial(summary.financeSummary.overdueAmount)}
            />
            <Row label="فواتير غير مدفوعة" value={String(summary.unpaidInvoicesCount)} last />
          </Card>
        </>
      ) : null}

      {summary.activeOrthoCase ? (
        <>
          <SectionTitle>التقويم</SectionTitle>
          <Card>
            <Row label="رقم الحالة" value={summary.activeOrthoCase.caseNumber || "—"} />
            <Row label="المرحلة" value={summary.activeOrthoCase.currentStage || "—"} />
            <Row label="الجهاز" value={summary.activeOrthoCase.applianceType || "—"} />
            <Row
              label="التقدم"
              value={
                summary.activeOrthoCase.stagePercentage != null
                  ? `${summary.activeOrthoCase.stagePercentage}%`
                  : "—"
              }
              last
            />
          </Card>
        </>
      ) : null}

      {summary.recentVisits.length > 0 ? (
        <>
          <SectionTitle>آخر الزيارات</SectionTitle>
          <Card>
            {summary.recentVisits.slice(0, 5).map((visit, index) => (
              <View key={visit.id} style={[styles.visitRow, index === Math.min(4, summary.recentVisits.length - 1) && styles.last]}>
                <Text style={styles.visitDate}>{visit.visitDate}</Text>
                <View style={{ flex: 1 }}>
                  <Text style={styles.visitTitle}>{visit.treatmentDone || visit.diagnosis || visit.visitType || "زيارة"}</Text>
                  {visit.chiefComplaint ? <Text style={styles.visitSub}>{visit.chiefComplaint}</Text> : null}
                </View>
              </View>
            ))}
          </Card>
        </>
      ) : null}

      {summary.timeline.length > 0 ? (
        <>
          <SectionTitle>الخط الزمني</SectionTitle>
          <Card>
            {summary.timeline.slice(0, 10).map((event, index) => (
              <View key={`${event.date}-${index}`} style={[styles.timelineRow, index === Math.min(9, summary.timeline.length - 1) && styles.last]}>
                <Text style={styles.timelineDate}>{event.date}</Text>
                <View style={{ flex: 1 }}>
                  <Text style={styles.timelineTitle}>{event.title}</Text>
                  <Text style={styles.timelineSub}>{event.sub}</Text>
                </View>
              </View>
            ))}
          </Card>
        </>
      ) : null}

      <PrimaryButton
        title="فتح ملف المريض الكامل"
        onPress={() =>
          router.push({ pathname: "/(app)/patients/[id]", params: { id: summary.patient.id } })
        }
      />
    </Screen>
  );
}

function Row({ label, value, last = false }: { label: string; value: string; last?: boolean }) {
  return (
    <View style={[styles.row, last && styles.last]}>
      <Text style={styles.value}>{value}</Text>
      <Text style={styles.label}>{label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  row: {
    minHeight: 48,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    gap: spacing.md
  },
  last: { borderBottomWidth: 0 },
  label: { color: colors.muted, textAlign: "right" },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" },
  alert: { padding: spacing.md, borderRadius: radius.sm, borderWidth: 1 },
  alertDanger: { backgroundColor: colors.dangerSoft, borderColor: "#fecaca" },
  alertWarning: { backgroundColor: colors.warningSoft, borderColor: "#fde68a" },
  alertTitle: { color: colors.text, fontWeight: "800", textAlign: "right" },
  alertText: { color: colors.text, marginTop: 4, textAlign: "right", lineHeight: 22 },
  visitRow: {
    flexDirection: "row",
    gap: spacing.sm,
    paddingVertical: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border
  },
  visitDate: { color: colors.primary, fontWeight: "700", minWidth: 86 },
  visitTitle: { color: colors.text, fontWeight: "700", textAlign: "right" },
  visitSub: { color: colors.muted, marginTop: 3, textAlign: "right" },
  timelineRow: {
    flexDirection: "row",
    gap: spacing.sm,
    paddingVertical: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border
  },
  timelineDate: { color: colors.muted, fontSize: 12, minWidth: 90 },
  timelineTitle: { color: colors.text, fontWeight: "700", textAlign: "right" },
  timelineSub: { color: colors.muted, marginTop: 2, textAlign: "right" }
});
