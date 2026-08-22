import { useSession } from "@/auth/SessionProvider";
import { ChoiceRow, FormField, SelectList } from "@/components/forms";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import {
  canUseGeneralDentistry,
  type CreateGeneralTreatmentInput,
  type GeneralTreatment
} from "@/lib/general";
import type { DoctorSummary } from "@/lib/types";
import { colors } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useEffect, useState } from "react";
import { ActivityIndicator, StyleSheet, Text, View } from "react-native";

const TREATMENT_TYPES = [
  { label: "حشوة / Restoration", value: "Restoration" },
  { label: "علاج عصب / RCT", value: "Root Canal Treatment" },
  { label: "خلع / Extraction", value: "Extraction" },
  { label: "تنظيف / Scaling", value: "Scaling" },
  { label: "تاج / Crown", value: "Crown" },
  { label: "جسر / Bridge", value: "Bridge" },
  { label: "تبييض / Whitening", value: "Whitening" }
];

const ANESTHESIA_OPTIONS = [
  { label: "بدون", value: "None" },
  { label: "موضعي", value: "Local" },
  { label: "سطحي", value: "Topical" }
];

export default function GeneralTreatmentNewScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ patientId: string; patientName?: string; toothNumber?: string }>();
  const patientId = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const initialTooth = Array.isArray(params.toothNumber) ? params.toothNumber[0] : params.toothNumber;
  const allowed = canUseGeneralDentistry(user?.role);

  const [treatmentType, setTreatmentType] = useState("");
  const [toothNumber, setToothNumber] = useState(initialTooth ?? "");
  const [materialUsed, setMaterialUsed] = useState("");
  const [anesthesiaType, setAnesthesiaType] = useState("");
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
    const cleanType = treatmentType.trim();
    if (!cleanType) {
      setError("نوع العلاج مطلوب.");
      return;
    }

    const input: CreateGeneralTreatmentInput = {
      patientId,
      treatmentType: cleanType,
      toothNumber: clean(toothNumber),
      materialUsed: clean(materialUsed),
      anesthesiaType: clean(anesthesiaType),
      doctorId,
      notes: clean(notes)
    };

    setSaving(true);
    setError(null);
    try {
      await apiRequest<GeneralTreatment>("/api/general-treatments", {
        method: "POST",
        body: JSON.stringify(input)
      });
      router.back();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تسجيل العلاج");
    } finally {
      setSaving(false);
    }
  }

  if (!allowed) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="تسجيل العلاج العام متاح للأدمن وطبيب الأسنان العام فقط." />
      </Screen>
    );
  }

  return (
    <Screen>
      <View>
        <Text style={styles.title}>تسجيل علاج عام</Text>
        <Text style={styles.subtitle}>{patientName || "ملف المريض"}</Text>
      </View>

      {error ? <StateMessage title="تعذر الحفظ" message={error} /> : null}

      <ChoiceRow
        label="نوع العلاج الشائع"
        value={treatmentType || null}
        options={TREATMENT_TYPES}
        onChange={(value) => setTreatmentType(value ?? "")}
      />
      <FormField
        label="نوع العلاج"
        value={treatmentType}
        onChangeText={setTreatmentType}
        placeholder="يمكن كتابة نوع مخصص"
        maxLength={100}
      />
      <FormField
        label="رقم السن / الأسنان"
        value={toothNumber}
        onChangeText={setToothNumber}
        placeholder="مثال: 16"
        maxLength={10}
      />
      <FormField
        label="المادة المستخدمة"
        value={materialUsed}
        onChangeText={setMaterialUsed}
        placeholder="Composite, GIC, Zirconia..."
        maxLength={100}
      />
      <ChoiceRow
        label="التخدير"
        value={anesthesiaType || null}
        options={ANESTHESIA_OPTIONS}
        onChange={(value) => setAnesthesiaType(value ?? "")}
      />
      <FormField
        label="نوع التخدير / تفاصيل"
        value={anesthesiaType}
        onChangeText={setAnesthesiaType}
        placeholder="Local أو وصف مخصص"
        maxLength={100}
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
            label="الطبيب المنفذ"
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
        <Text style={styles.warningTitle}>ملاحظة مالية</Text>
        <Text style={styles.warningBody}>
          لم نضف حقل تكلفة هنا لأن عقد العلاج العام الحالي لا يحدد العملة. تُسجَّل الدفعات والمبالغ متعددة العملات من وحدة المالية المخصصة.
        </Text>
      </Card>

      <PrimaryButton title="حفظ العلاج" loading={saving} onPress={() => void save()} />
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
  warningTitle: { color: colors.warning, fontWeight: "800", textAlign: "right" },
  warningBody: { color: colors.warning, marginTop: 5, textAlign: "right", lineHeight: 21 }
});
