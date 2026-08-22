import { useSession } from "@/auth/SessionProvider";
import { ChoiceRow, FormField, SelectList } from "@/components/forms";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { isoDateLocal } from "@/lib/format";
import { canUseOrthodontics, type CreateOrthoVisitInput, type OrthoCase, type OrthoVisit } from "@/lib/ortho";
import { colors, radius, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useCallback, useEffect, useMemo, useState } from "react";
import { ActivityIndicator, StyleSheet, Text, View } from "react-native";

const YES_NO = [
  { value: "no", label: "لا" },
  { value: "yes", label: "نعم" }
];

export default function NewOrthoVisitScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{
    caseId: string;
    patientName?: string;
    currentStage?: string;
    doctorId?: string;
  }>();
  const caseId = Array.isArray(params.caseId) ? params.caseId[0] : params.caseId;
  const patientNameParam = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const currentStageParam = Array.isArray(params.currentStage) ? params.currentStage[0] : params.currentStage;
  const doctorIdParam = Array.isArray(params.doctorId) ? params.doctorId[0] : params.doctorId;

  const [orthoCase, setOrthoCase] = useState<OrthoCase | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [visitDate, setVisitDate] = useState(() => isoDateLocal(new Date()));
  const [visitType, setVisitType] = useState("متابعة تقويمية");
  const [currentStage, setCurrentStage] = useState(currentStageParam ?? "");
  const [wireUpper, setWireUpper] = useState("");
  const [wireLower, setWireLower] = useState("");
  const [elasticsType, setElasticsType] = useState("");
  const [overjet, setOverjet] = useState("");
  const [overbite, setOverbite] = useState("");
  const [clinicalNotes, setClinicalNotes] = useState("");
  const [patientInstructions, setPatientInstructions] = useState("");
  const [nextAppointmentDate, setNextAppointmentDate] = useState("");
  const [nextAppointmentType, setNextAppointmentType] = useState("متابعة تقويمية");
  const [openBookingAfterSave, setOpenBookingAfterSave] = useState<string | null>("no");

  const allowed = canUseOrthodontics(user?.role);

  const load = useCallback(async () => {
    if (!caseId || !allowed) {
      setLoading(false);
      return;
    }
    setError(null);
    try {
      const result = await apiRequest<OrthoCase>(`/api/ortho-cases/${caseId}`);
      setOrthoCase(result);
      if (!currentStage) setCurrentStage(result.currentStage ?? "");
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحميل حالة التقويم");
    } finally {
      setLoading(false);
    }
  }, [allowed, caseId, currentStage]);

  useEffect(() => {
    void load();
  }, [load]);

  const stageOptions = useMemo(
    () =>
      (orthoCase?.stages ?? []).map((stage) => ({
        value: stage.stageName,
        label: stage.stageName,
        subtitle: stage.status
      })),
    [orthoCase]
  );

  function parseOptionalNumber(value: string, label: string): number | null | undefined {
    if (!value.trim()) return undefined;
    const parsed = Number(value.trim());
    if (!Number.isFinite(parsed)) {
      setError(`${label} يجب أن يكون رقماً صحيحاً.`);
      return null;
    }
    return parsed;
  }

  function validate(): CreateOrthoVisitInput | null {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(visitDate.trim())) {
      setError("تاريخ الزيارة يجب أن يكون بصيغة YYYY-MM-DD.");
      return null;
    }
    if (nextAppointmentDate.trim() && !/^\d{4}-\d{2}-\d{2}$/.test(nextAppointmentDate.trim())) {
      setError("تاريخ الموعد القادم يجب أن يكون بصيغة YYYY-MM-DD.");
      return null;
    }

    const currentOverjet = parseOptionalNumber(overjet, "Overjet");
    if (currentOverjet === null) return null;
    const currentOverbite = parseOptionalNumber(overbite, "Overbite");
    if (currentOverbite === null) return null;

    if (
      !visitType.trim() &&
      !currentStage.trim() &&
      !wireUpper.trim() &&
      !wireLower.trim() &&
      !clinicalNotes.trim()
    ) {
      setError("أدخل بيانات الزيارة التقويمية قبل الحفظ.");
      return null;
    }

    return {
      visitDate: visitDate.trim(),
      visitType: visitType.trim() || undefined,
      currentStage: currentStage.trim() || undefined,
      wireUpper: wireUpper.trim() || undefined,
      wireLower: wireLower.trim() || undefined,
      elasticsType: elasticsType.trim() || undefined,
      currentOverjet,
      currentOverbite,
      clinicalNotes: clinicalNotes.trim() || undefined,
      patientInstructions: patientInstructions.trim() || undefined,
      nextAppointmentDate: nextAppointmentDate.trim() || undefined,
      nextAppointmentType: nextAppointmentType.trim() || undefined,
      doctorId: doctorIdParam || user?.doctorId || orthoCase?.doctorId || undefined
    };
  }

  async function submit() {
    if (!caseId || saving) return;
    const payload = validate();
    if (!payload) return;

    setSaving(true);
    setError(null);
    try {
      await apiRequest<OrthoVisit>(`/api/ortho-cases/${caseId}/visits`, {
        method: "POST",
        body: JSON.stringify(payload)
      });

      if (openBookingAfterSave === "yes" && nextAppointmentDate.trim() && orthoCase) {
        router.replace({
          pathname: "/(app)/appointments-new",
          params: {
            patientId: orthoCase.patientId,
            patientName: orthoCase.patientName,
            date: nextAppointmentDate.trim()
          }
        });
      } else {
        router.back();
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر حفظ الزيارة التقويمية");
    } finally {
      setSaving(false);
    }
  }

  if (!allowed) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="تسجيل زيارة تقويمية متاح للأدمن وأخصائي التقويم فقط." />
      </Screen>
    );
  }

  if (loading) {
    return (
      <Screen>
        <ActivityIndicator size="large" color={colors.primary} />
      </Screen>
    );
  }

  return (
    <Screen>
      <View>
        <Text style={styles.title}>زيارة تقويمية جديدة</Text>
        <Text style={styles.subtitle}>{orthoCase?.patientName || patientNameParam || "المريض"}</Text>
      </View>

      {error ? <StateMessage title="تعذر حفظ الزيارة" message={error} /> : null}

      <SectionTitle>بيانات الزيارة</SectionTitle>
      <Card style={styles.formCard}>
        <FormField
          label="تاريخ الزيارة YYYY-MM-DD"
          value={visitDate}
          onChangeText={setVisitDate}
          keyboardType="numbers-and-punctuation"
        />
        <FormField label="نوع الزيارة" value={visitType} onChangeText={setVisitType} />
        {stageOptions.length > 0 ? (
          <SelectList
            label="المرحلة الحالية"
            value={currentStage || null}
            options={stageOptions}
            onChange={(value) => setCurrentStage(value ?? "")}
            emptyLabel="بدون مرحلة"
          />
        ) : (
          <FormField label="المرحلة الحالية" value={currentStage} onChangeText={setCurrentStage} />
        )}
        <FormField label="السلك العلوي" value={wireUpper} onChangeText={setWireUpper} />
        <FormField label="السلك السفلي" value={wireLower} onChangeText={setWireLower} />
        <FormField label="المطاط / Elastics" value={elasticsType} onChangeText={setElasticsType} />
        <FormField label="Overjet (mm)" value={overjet} onChangeText={setOverjet} keyboardType="decimal-pad" />
        <FormField label="Overbite (mm)" value={overbite} onChangeText={setOverbite} keyboardType="decimal-pad" />
        <FormField label="الملاحظات السريرية" value={clinicalNotes} onChangeText={setClinicalNotes} multiline />
        <FormField label="تعليمات المريض" value={patientInstructions} onChangeText={setPatientInstructions} multiline />
      </Card>

      <SectionTitle>المتابعة القادمة</SectionTitle>
      <Card style={styles.formCard}>
        <FormField
          label="تاريخ المتابعة YYYY-MM-DD"
          value={nextAppointmentDate}
          onChangeText={setNextAppointmentDate}
          keyboardType="numbers-and-punctuation"
        />
        <FormField label="نوع الموعد القادم" value={nextAppointmentType} onChangeText={setNextAppointmentType} />
        <ChoiceRow
          label="فتح شاشة حجز موعد حقيقي بعد الحفظ"
          value={openBookingAfterSave}
          options={YES_NO}
          onChange={setOpenBookingAfterSave}
        />
      </Card>

      <Text style={styles.notice}>
        تاريخ المتابعة داخل زيارة التقويم هو جزء من السجل السريري فقط. اختيار «نعم» يفتح شاشة الحجز بعد الحفظ حتى يتم إنشاء Appointment فعلي ومراجعته قبل التسجيل.
      </Text>

      <PrimaryButton title="حفظ الزيارة التقويمية" onPress={() => void submit()} loading={saving} />
    </Screen>
  );
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  formCard: { gap: spacing.md },
  notice: {
    color: colors.warning,
    backgroundColor: colors.warningSoft,
    borderRadius: radius.sm,
    padding: spacing.sm,
    textAlign: "right",
    lineHeight: 22
  }
});
