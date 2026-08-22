import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { canClinicalJourney } from "@/lib/journey";
import type { ClinicalVisit } from "@/lib/types";
import { colors, radius, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useCallback, useEffect, useState } from "react";
import { ActivityIndicator, StyleSheet, Text, TextInput, View } from "react-native";

export default function JourneyHandoffScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{
    visitId: string;
    patientId?: string;
    patientName?: string;
  }>();
  const visitId = Array.isArray(params.visitId) ? params.visitId[0] : params.visitId;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [chiefComplaint, setChiefComplaint] = useState("");
  const [diagnosis, setDiagnosis] = useState("");
  const [treatmentDone, setTreatmentDone] = useState("");
  const [proposedProcedure, setProposedProcedure] = useState("");
  const [instructions, setInstructions] = useState("");
  const [nextVisitPlan, setNextVisitPlan] = useState("");
  const [followUpDate, setFollowUpDate] = useState("");
  const [amountDue, setAmountDue] = useState("");
  const [notes, setNotes] = useState("");

  const load = useCallback(async () => {
    if (!visitId || !canClinicalJourney(user)) {
      setLoading(false);
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const visit = await apiRequest<ClinicalVisit>(`/api/visits/${visitId}`);
      setChiefComplaint(visit.chiefComplaint ?? "");
      setDiagnosis(visit.diagnosis ?? "");
      setTreatmentDone(visit.treatmentDone ?? "");
      setProposedProcedure(visit.proposedProcedure ?? "");
      setInstructions(visit.instructions ?? "");
      setNextVisitPlan(visit.nextVisitPlan ?? "");
      setFollowUpDate(visit.nextVisitDate ?? "");
      setAmountDue(visit.amountDueReference != null ? String(visit.amountDueReference) : "");
      setNotes(visit.clinicalNotes ?? "");
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحميل بيانات الزيارة");
    } finally {
      setLoading(false);
    }
  }, [user, visitId]);

  useEffect(() => {
    void load();
  }, [load]);

  async function submit() {
    if (!visitId || saving) return;

    const amount = amountDue.trim() ? Number(amountDue.trim()) : undefined;
    if (amount !== undefined && (!Number.isFinite(amount) || amount < 0)) {
      setError("المبلغ المستحق يجب أن يكون رقمًا صحيحًا أو صفرًا.");
      return;
    }
    if (followUpDate.trim() && !/^\d{4}-\d{2}-\d{2}$/.test(followUpDate.trim())) {
      setError("تاريخ المتابعة يجب أن يكون بصيغة YYYY-MM-DD.");
      return;
    }
    if (!treatmentDone.trim() && !diagnosis.trim() && !proposedProcedure.trim()) {
      setError("سجل العلاج أو التشخيص أو الإجراء المقترح قبل التسليم للاستقبال.");
      return;
    }

    setSaving(true);
    setError(null);
    try {
      await apiRequest<unknown>(`/api/patient-journey/${visitId}/handoff-to-reception`, {
        method: "POST",
        body: JSON.stringify({
          chiefComplaint: chiefComplaint.trim() || null,
          diagnosis: diagnosis.trim() || null,
          treatmentDone: treatmentDone.trim() || null,
          proposedProcedure: proposedProcedure.trim() || null,
          instructions: instructions.trim() || null,
          nextVisitPlan: nextVisitPlan.trim() || null,
          followUpDate: followUpDate.trim() || null,
          amountDue: amount ?? null,
          notes: notes.trim() || null
        })
      });
      router.back();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تسليم الزيارة للاستقبال");
    } finally {
      setSaving(false);
    }
  }

  if (!canClinicalJourney(user)) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="هذه الشاشة مخصصة للحسابات السريرية فقط." />
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
        <Text style={styles.title}>تسليم الزيارة للاستقبال</Text>
        <Text style={styles.subtitle}>{patientName || "المريض"}</Text>
      </View>

      {error ? <StateMessage title="تعذر إكمال التسليم" message={error} /> : null}

      <SectionTitle>السجل السريري</SectionTitle>
      <Card style={styles.formCard}>
        <Field label="الشكوى الرئيسية" value={chiefComplaint} onChangeText={setChiefComplaint} />
        <Field label="التشخيص" value={diagnosis} onChangeText={setDiagnosis} multiline />
        <Field label="العلاج المنفذ" value={treatmentDone} onChangeText={setTreatmentDone} multiline />
        <Field
          label="الإجراء المقترح"
          value={proposedProcedure}
          onChangeText={setProposedProcedure}
          multiline
        />
        <Field label="تعليمات المريض" value={instructions} onChangeText={setInstructions} multiline />
        <Field label="خطة الزيارة القادمة" value={nextVisitPlan} onChangeText={setNextVisitPlan} multiline />
        <Field
          label="تاريخ المتابعة YYYY-MM-DD"
          value={followUpDate}
          onChangeText={setFollowUpDate}
          keyboardType="numbers-and-punctuation"
        />
        <Field
          label="المبلغ المستحق المرجعي"
          value={amountDue}
          onChangeText={setAmountDue}
          keyboardType="decimal-pad"
        />
        <Field label="ملاحظات إضافية" value={notes} onChangeText={setNotes} multiline />
      </Card>

      <Text style={styles.notice}>
        التسليم يجهز الزيارة للاستقبال فقط. تسجيل الدفعة المالية الفعلية يبقى من وحدة المالية.
      </Text>

      <PrimaryButton title="تسليم للاستقبال" onPress={() => void submit()} loading={saving} />
    </Screen>
  );
}

function Field({
  label,
  value,
  onChangeText,
  multiline = false,
  keyboardType
}: {
  label: string;
  value: string;
  onChangeText: (value: string) => void;
  multiline?: boolean;
  keyboardType?: "default" | "decimal-pad" | "numbers-and-punctuation";
}) {
  return (
    <View style={styles.field}>
      <Text style={styles.label}>{label}</Text>
      <TextInput
        value={value}
        onChangeText={onChangeText}
        multiline={multiline}
        keyboardType={keyboardType}
        textAlign="right"
        placeholderTextColor={colors.muted}
        style={[styles.input, multiline && styles.multiline]}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 24, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  formCard: { gap: spacing.md },
  field: { gap: 6 },
  label: { color: colors.text, fontWeight: "700", textAlign: "right" },
  input: {
    minHeight: 46,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.sm,
    paddingHorizontal: spacing.sm,
    paddingVertical: 10,
    color: colors.text,
    backgroundColor: colors.surface
  },
  multiline: { minHeight: 92, textAlignVertical: "top" },
  notice: {
    color: colors.warning,
    backgroundColor: colors.warningSoft,
    padding: spacing.sm,
    borderRadius: radius.sm,
    textAlign: "right",
    lineHeight: 22
  }
});
