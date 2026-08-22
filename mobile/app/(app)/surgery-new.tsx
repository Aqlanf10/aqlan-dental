import { useSession } from "@/auth/SessionProvider";
import { ChoiceRow, FormField, SelectList } from "@/components/forms";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import {
  canUseSurgery,
  SURGERY_TYPES,
  type CreateSurgeryCaseInput
} from "@/lib/surgery";
import type { DoctorSummary } from "@/lib/types";
import { colors } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useEffect, useState } from "react";
import { ActivityIndicator, StyleSheet, Text, View } from "react-native";

type CreateResult = { id: string; caseNumber: string };

export default function SurgeryNewScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ patientId: string; patientName?: string }>();
  const patientId = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const allowed = canUseSurgery(user?.role);

  const [surgeryType, setSurgeryType] = useState("");
  const [teethInvolved, setTeethInvolved] = useState("");
  const [doctorId, setDoctorId] = useState<string | null>(user?.doctorId ?? null);
  const [doctors, setDoctors] = useState<DoctorSummary[]>([]);
  const [loadingDoctors, setLoadingDoctors] = useState(user?.role === "Admin");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (user?.role !== "Admin") {
      setLoadingDoctors(false);
      return;
    }
    let active = true;
    void apiRequest<DoctorSummary[]>("/api/doctors?status=active")
      .then((items) => {
        if (active) setDoctors(items ?? []);
      })
      .catch((err) => {
        if (active) setError(err instanceof Error ? err.message : "تعذر تحميل قائمة الأطباء");
      })
      .finally(() => {
        if (active) setLoadingDoctors(false);
      });
    return () => {
      active = false;
    };
  }, [user?.role]);

  async function save() {
    if (!patientId || saving) return;
    const cleanType = surgeryType.trim();
    if (!cleanType) {
      setError("نوع الجراحة مطلوب.");
      return;
    }

    const input: CreateSurgeryCaseInput = {
      patientId,
      doctorId,
      surgeryType: cleanType,
      teethInvolved: clean(teethInvolved)
    };

    setSaving(true);
    setError(null);
    try {
      const created = await apiRequest<CreateResult>("/api/surgery-cases", {
        method: "POST",
        body: JSON.stringify(input)
      });
      router.replace({
        pathname: "/(app)/surgery-case",
        params: { id: created.id, patientName }
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر إنشاء الحالة الجراحية");
    } finally {
      setSaving(false);
    }
  }

  if (!allowed) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="إنشاء الحالات الجراحية متاح للأدمن وجراح الفم فقط." />
      </Screen>
    );
  }

  return (
    <Screen>
      <View>
        <Text style={styles.title}>حالة جراحية جديدة</Text>
        <Text style={styles.subtitle}>{patientName || "ملف المريض"}</Text>
      </View>

      {error ? <StateMessage title="تعذر الإنشاء" message={error} /> : null}

      <ChoiceRow
        label="نوع الجراحة"
        value={surgeryType || null}
        options={SURGERY_TYPES.map((value) => ({ label: value, value }))}
        onChange={(value) => setSurgeryType(value ?? "")}
      />
      <FormField
        label="نوع الجراحة / وصف مخصص"
        value={surgeryType}
        onChangeText={setSurgeryType}
        maxLength={200}
      />
      <FormField
        label="الأسنان المعنية"
        value={teethInvolved}
        onChangeText={setTeethInvolved}
        placeholder="مثال: 38 أو 18, 28"
        maxLength={100}
      />

      {user?.role === "Admin" ? (
        loadingDoctors ? (
          <ActivityIndicator color={colors.primary} />
        ) : (
          <SelectList
            label="الجراح"
            value={doctorId}
            options={doctors.map((doctor) => ({
              label: doctor.name,
              value: doctor.id,
              subtitle: doctor.specialty || doctor.branchName || null
            }))}
            onChange={setDoctorId}
            emptyLabel="بدون جراح محدد"
          />
        )
      ) : null}

      <Card>
        <Text style={styles.note}>
          تاريخ الجراحة لا يُدخل في شاشة الإنشاء لأن الـBackend الحالي يتحقق من ScheduledDate لكنه لا يحفظه. بعد إنشاء الحالة افتح «ما قبل الجراحة» وسجّل التاريخ هناك ليُحفظ فعليًا.
        </Text>
      </Card>

      <PrimaryButton title="إنشاء الحالة" loading={saving} onPress={() => void save()} />
    </Screen>
  );
}

function clean(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  note: { color: colors.warning, textAlign: "right", lineHeight: 22 }
});
