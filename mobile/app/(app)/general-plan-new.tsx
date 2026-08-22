import { useSession } from "@/auth/SessionProvider";
import { ChoiceRow, FormField, SelectList } from "@/components/forms";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import {
  canUseGeneralDentistry,
  TREATMENT_PLAN_PRIORITY_OPTIONS,
  type CreateGeneralTreatmentPlanInput,
  type GeneralTreatmentPlanItem
} from "@/lib/general";
import type { DoctorSummary } from "@/lib/types";
import { colors } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useEffect, useState } from "react";
import { ActivityIndicator, StyleSheet, Text, View } from "react-native";

export default function GeneralPlanNewScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ patientId: string; patientName?: string; toothNumber?: string }>();
  const patientId = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const initialTooth = Array.isArray(params.toothNumber) ? params.toothNumber[0] : params.toothNumber;
  const allowed = canUseGeneralDentistry(user?.role);

  const [treatment, setTreatment] = useState("");
  const [toothNumber, setToothNumber] = useState(initialTooth ?? "");
  const [priority, setPriority] = useState<"low" | "medium" | "high" | "urgent">("medium");
  const [notes, setNotes] = useState("");
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
    const cleanTreatment = treatment.trim();
    if (!cleanTreatment) {
      setError("وصف العلاج مطلوب.");
      return;
    }

    const input: CreateGeneralTreatmentPlanInput = {
      patientId,
      toothNumber: clean(toothNumber),
      treatment: cleanTreatment,
      priority,
      notes: clean(notes),
      doctorId
    };

    setSaving(true);
    setError(null);
    try {
      await apiRequest<GeneralTreatmentPlanItem>("/api/general/treatment-plans", {
        method: "POST",
        body: JSON.stringify(input)
      });
      router.back();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر إضافة عنصر خطة العلاج");
    } finally {
      setSaving(false);
    }
  }

  if (!allowed) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="خطة العلاج العام متاحة للأدمن وطبيب الأسنان العام فقط." />
      </Screen>
    );
  }

  return (
    <Screen>
      <View>
        <Text style={styles.title}>عنصر خطة علاج</Text>
        <Text style={styles.subtitle}>{patientName || "ملف المريض"}</Text>
      </View>

      {error ? <StateMessage title="تعذر الحفظ" message={error} /> : null}

      <FormField
        label="العلاج المخطط"
        value={treatment}
        onChangeText={setTreatment}
        placeholder="مثال: Composite restoration"
        multiline
        maxLength={200}
      />
      <FormField
        label="رقم السن / الأسنان"
        value={toothNumber}
        onChangeText={setToothNumber}
        placeholder="مثال: 16"
        maxLength={10}
      />
      <ChoiceRow
        label="الأولوية"
        value={priority}
        options={TREATMENT_PLAN_PRIORITY_OPTIONS.map((item) => ({ ...item }))}
        onChange={(value) => {
          if (value === "low" || value === "medium" || value === "high" || value === "urgent") {
            setPriority(value);
          }
        }}
      />
      <FormField
        label="ملاحظات"
        value={notes}
        onChangeText={setNotes}
        multiline
        maxLength={500}
      />

      {user?.role === "Admin" ? (
        loadingDoctors ? (
          <ActivityIndicator color={colors.primary} />
        ) : (
          <SelectList
            label="الطبيب المسؤول"
            value={doctorId}
            options={doctors.map((doctor) => ({
              label: doctor.name,
              value: doctor.id,
              subtitle: doctor.specialty || doctor.branchName || null
            }))}
            onChange={setDoctorId}
            emptyLabel="بدون طبيب محدد"
          />
        )
      ) : null}

      <Card>
        <Text style={styles.note}>
          التكلفة التقديرية لا تُنشأ من الهاتف حاليًا لأن الـAPI لا يربطها بعملة. هذا يمنع تسجيل رقم مالي ملتبس بين YER/SAR/USD.
        </Text>
      </Card>

      <PrimaryButton title="إضافة للخطة" loading={saving} onPress={() => void save()} />
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
  note: { color: colors.warning, textAlign: "right", lineHeight: 21 }
});
