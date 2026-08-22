import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { fullPatientName } from "@/lib/format";
import { canUseGeneralDentistry } from "@/lib/general";
import { canFinanceJourney } from "@/lib/journey";
import { canUseOrthodontics } from "@/lib/ortho";
import { canAccessClinicalRecords } from "@/lib/roles";
import { canUseSurgery } from "@/lib/surgery";
import type { ConversationDetail, PatientProfile } from "@/lib/types";
import { colors, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useCallback, useEffect, useState } from "react";
import { ActivityIndicator, StyleSheet, Text, View } from "react-native";

export default function PatientDetailsScreen() {
  const { user, can } = useSession();
  const params = useLocalSearchParams<{ id: string }>();
  const id = Array.isArray(params.id) ? params.id[0] : params.id;
  const [patient, setPatient] = useState<PatientProfile | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [communicationError, setCommunicationError] = useState<string | null>(null);
  const [messagingAction, setMessagingAction] = useState<"internal" | "patient" | null>(null);
  const [loading, setLoading] = useState(true);
  // MOBILE-02 safety: reception receives an operational profile that intentionally omits
  // occupation/referralSource. Until the API exposes a partial operational update contract,
  // only Admin may use the full-profile PUT editor so hidden values cannot be erased.
  const canEdit = user?.role === "Admin";
  const canReadClinical = canAccessClinicalRecords(user);
  const canReadFinance = canFinanceJourney(user);
  const canReadOrtho = canUseOrthodontics(user?.role);
  const canReadGeneral = canUseGeneralDentistry(user?.role);
  const canReadSurgery = canUseSurgery(user?.role);
  const canReadLab = can("lab_orders.view");

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setError(null);
    try {
      setPatient(await apiRequest<PatientProfile>(`/api/patients/${id}`));
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحميل ملف المريض");
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => { void load(); }, [load]);

  async function openPatientConversation(kind: "internal" | "patient") {
    if (!patient || messagingAction) return;
    setMessagingAction(kind);
    setCommunicationError(null);
    try {
      const path = kind === "patient"
        ? `/api/messages/conversations/patient/${patient.id}`
        : `/api/messages/internal-patient/${patient.id}`;
      const conversation = await apiRequest<ConversationDetail>(path, { method: "POST" });
      router.push({ pathname: "/(app)/message-detail", params: { id: conversation.id } });
    } catch (err) {
      setCommunicationError(err instanceof Error ? err.message : "تعذر فتح المحادثة المرتبطة بالمريض");
    } finally {
      setMessagingAction(null);
    }
  }

  if (loading) return <Screen><ActivityIndicator size="large" color={colors.primary} /></Screen>;
  if (!patient) {
    return <Screen><StateMessage title="تعذر فتح ملف المريض" message={error ?? "المريض غير موجود"} action={<PrimaryButton title="إعادة المحاولة" onPress={() => void load()} />} /></Screen>;
  }

  const patientName = fullPatientName(patient);

  return (
    <Screen>
      <View><Text style={styles.name}>{patientName}</Text><Text style={styles.number}>{patient.patientNumber}</Text></View>
      {patient.isLimitedView ? <Text style={styles.notice}>هذا عرض سريري محدود وفق صلاحيات حسابك.</Text> : null}

      <SectionTitle>المعلومات الأساسية</SectionTitle>
      <Card>
        <Row label="العمر" value={patient.age ? `${patient.age} سنة` : "—"} />
        <Row label="الجنس" value={patient.gender || "—"} />
        <Row label="الطبيب" value={patient.primaryDoctorName || "—"} />
        <Row label="الفرع" value={patient.branchName || "—"} />
        {patient.phone ? <Row label="الهاتف" value={patient.phone} /> : null}
        {patient.whatsApp ? <Row label="واتساب" value={patient.whatsApp} /> : null}
        {patient.address ? <Row label="العنوان" value={patient.address} last /> : null}
      </Card>

      {patient.medicalHistory ? (
        <><SectionTitle>التاريخ الطبي</SectionTitle><Card>
          <Row label="أمراض مزمنة" value={patient.medicalHistory.chronicDiseases || "لا يوجد مسجل"} />
          <Row label="أدوية حالية" value={patient.medicalHistory.currentMedications || "لا يوجد مسجل"} />
          <Row label="حساسية أدوية" value={patient.medicalHistory.drugAllergies || "لا يوجد مسجل"} last />
        </Card></>
      ) : null}

      {patient.dentalHistory?.chiefComplaint ? (
        <><SectionTitle>الشكوى الرئيسية</SectionTitle><Card><Text style={styles.paragraph}>{patient.dentalHistory.chiefComplaint}</Text></Card></>
      ) : null}

      <SectionTitle>إجراءات سريعة</SectionTitle>
      {communicationError ? <StateMessage title="تعذر فتح المحادثة" message={communicationError} /> : null}
      <PrimaryButton title="ملخص رحلة المريض اليوم" onPress={() => router.push({ pathname: "/(app)/journey-summary", params: { patientId: patient.id } })} />
      {canReadClinical ? (
        <>
          <PrimaryButton title="السجل السريري والزيارات" onPress={() => router.push({ pathname: "/(app)/visits", params: { patientId: patient.id, patientName } })} />
          <PrimaryButton title="الصور والأشعة" onPress={() => router.push({ pathname: "/(app)/patient-media", params: { patientId: patient.id, patientName } })} />
          <PrimaryButton title="المستندات والوصفات والإحالات" onPress={() => router.push({ pathname: "/(app)/patient-records", params: { patientId: patient.id, patientName } })} />
        </>
      ) : null}
      {canReadGeneral ? <PrimaryButton title="الأسنان العامة وFDI" onPress={() => router.push({ pathname: "/(app)/patient-general", params: { patientId: patient.id, patientName } })} /> : null}
      {canReadOrtho ? <PrimaryButton title="تقويم الأسنان" onPress={() => router.push({ pathname: "/(app)/patient-ortho", params: { patientId: patient.id, patientName } })} /> : null}
      {canReadSurgery ? <PrimaryButton title="جراحة الفم" onPress={() => router.push({ pathname: "/(app)/patient-surgery", params: { patientId: patient.id, patientName } })} /> : null}
      {canReadLab ? <PrimaryButton title="طلبات المعمل" onPress={() => router.push({ pathname: "/(app)/patient-lab", params: { patientId: patient.id, patientName } })} /> : null}
      {canReadFinance ? <PrimaryButton title="مالية المريض" onPress={() => router.push({ pathname: "/(app)/patient-finance", params: { patientId: patient.id, patientName } })} /> : null}
      <PrimaryButton title="حجز موعد جديد" onPress={() => router.push({ pathname: "/(app)/appointments-new", params: { patientId: patient.id, patientName } })} />
      <PrimaryButton title="عرض مواعيد المريض" onPress={() => router.push({ pathname: "/(app)/appointments", params: { patientId: patient.id, patientName } })} />
      <PrimaryButton title="محادثة داخلية حول المريض" loading={messagingAction === "internal"} disabled={messagingAction !== null} onPress={() => void openPatientConversation("internal")} />
      <PrimaryButton title="مراسلة المريض — مرئية للمريض" loading={messagingAction === "patient"} disabled={messagingAction !== null} onPress={() => void openPatientConversation("patient")} />
      {canEdit && !patient.isLimitedView ? <PrimaryButton title="تعديل بيانات المريض" onPress={() => router.push({ pathname: "/(app)/patients/edit", params: { id: patient.id } })} /> : null}
    </Screen>
  );
}

function Row({ label, value, last = false }: { label: string; value: string; last?: boolean }) {
  return <View style={[styles.row, last && { borderBottomWidth: 0 }]}><Text style={styles.value}>{value}</Text><Text style={styles.label}>{label}</Text></View>;
}

const styles = StyleSheet.create({
  name: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  number: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  notice: { color: colors.warning, backgroundColor: colors.warningSoft, padding: spacing.sm, borderRadius: 10, textAlign: "right" },
  row: { minHeight: 48, borderBottomWidth: 1, borderBottomColor: colors.border, flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: spacing.md },
  label: { color: colors.muted, textAlign: "right" },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" },
  paragraph: { color: colors.text, textAlign: "right", lineHeight: 24 }
});
