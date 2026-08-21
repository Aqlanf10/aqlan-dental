import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { fullPatientName } from "@/lib/format";
import type { PatientProfile } from "@/lib/types";
import { colors, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useCallback, useEffect, useState } from "react";
import { ActivityIndicator, StyleSheet, Text, View } from "react-native";

export default function PatientDetailsScreen() {
  const params = useLocalSearchParams<{ id: string }>();
  const id = Array.isArray(params.id) ? params.id[0] : params.id;
  const [patient, setPatient] = useState<PatientProfile | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

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

  useEffect(() => {
    void load();
  }, [load]);

  if (loading) {
    return (
      <Screen>
        <ActivityIndicator size="large" color={colors.primary} />
      </Screen>
    );
  }

  if (!patient) {
    return (
      <Screen>
        <StateMessage
          title="تعذر فتح ملف المريض"
          message={error ?? "المريض غير موجود"}
          action={<PrimaryButton title="إعادة المحاولة" onPress={() => void load()} />}
        />
      </Screen>
    );
  }

  return (
    <Screen>
      <View>
        <Text style={styles.name}>{fullPatientName(patient)}</Text>
        <Text style={styles.number}>{patient.patientNumber}</Text>
      </View>

      {patient.isLimitedView ? (
        <Text style={styles.notice}>
          هذا عرض سريري محدود وفق صلاحيات حسابك.
        </Text>
      ) : null}

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
        <>
          <SectionTitle>التاريخ الطبي</SectionTitle>
          <Card>
            <Row label="أمراض مزمنة" value={patient.medicalHistory.chronicDiseases || "لا يوجد مسجل"} />
            <Row label="أدوية حالية" value={patient.medicalHistory.currentMedications || "لا يوجد مسجل"} />
            <Row label="حساسية أدوية" value={patient.medicalHistory.drugAllergies || "لا يوجد مسجل"} last />
          </Card>
        </>
      ) : null}

      {patient.dentalHistory?.chiefComplaint ? (
        <>
          <SectionTitle>الشكوى الرئيسية</SectionTitle>
          <Card>
            <Text style={styles.paragraph}>{patient.dentalHistory.chiefComplaint}</Text>
          </Card>
        </>
      ) : null}

      <PrimaryButton
        title="عرض مواعيد المريض"
        onPress={() =>
          router.push({
            pathname: "/(app)/appointments",
            params: { patientId: patient.id, patientName: fullPatientName(patient) }
          })
        }
      />
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
  name: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  number: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  notice: { color: colors.warning, backgroundColor: colors.warningSoft, padding: spacing.sm, borderRadius: 10, textAlign: "right" },
  row: { minHeight: 48, borderBottomWidth: 1, borderBottomColor: colors.border, flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: spacing.md },
  label: { color: colors.muted, textAlign: "right" },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" },
  paragraph: { color: colors.text, textAlign: "right", lineHeight: 24 }
});
