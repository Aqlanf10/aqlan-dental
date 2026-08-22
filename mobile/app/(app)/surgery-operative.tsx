import { useSession } from "@/auth/SessionProvider";
import { ChoiceRow, FormField, SelectList } from "@/components/forms";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import {
  canUseSurgery,
  type OperativeReport,
  type UpsertOperativeInput
} from "@/lib/surgery";
import type { DoctorSummary } from "@/lib/types";
import { colors } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useEffect, useState } from "react";
import { ActivityIndicator, StyleSheet, Text, View } from "react-native";

const OUTCOME_OPTIONS = [
  "ناجحة بدون مضاعفات",
  "ناجحة مع مضاعفات بسيطة",
  "مضاعفات",
  "تحتاج متابعة"
].map((value) => ({ label: value, value }));

export default function SurgeryOperativeScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ id: string; patientName?: string }>();
  const id = Array.isArray(params.id) ? params.id[0] : params.id;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const allowed = canUseSurgery(user?.role);

  const [surgeryDateTime, setSurgeryDateTime] = useState(localDateTimeInput(new Date()));
  const [durationMinutes, setDurationMinutes] = useState("");
  const [anesthesiaUsed, setAnesthesiaUsed] = useState("");
  const [technique, setTechnique] = useState("");
  const [detailedDescription, setDetailedDescription] = useState("");
  const [outcome, setOutcome] = useState("");
  const [complications, setComplications] = useState("");
  const [suturesCount, setSuturesCount] = useState("");
  const [specimenSent, setSpecimenSent] = useState(false);
  const [doctorId, setDoctorId] = useState<string | null>(user?.doctorId ?? null);
  const [approvedAt, setApprovedAt] = useState<string | null>(null);
  const [doctors, setDoctors] = useState<DoctorSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id || !allowed) {
      setLoading(false);
      return;
    }

    let active = true;
    const requests: Promise<unknown>[] = [
      apiRequest<OperativeReport | null>(`/api/surgery-cases/${id}/operative`).then((existing) => {
        if (!active || !existing) return;
        setSurgeryDateTime(existing.surgeryDateTime ?? "");
        setDurationMinutes(existing.durationMinutes != null ? String(existing.durationMinutes) : "");
        setAnesthesiaUsed(existing.anesthesiaUsed ?? "");
        setTechnique(existing.technique ?? "");
        setDetailedDescription(existing.detailedDescription ?? "");
        setOutcome(existing.outcome ?? "");
        setComplications(existing.complications ?? "");
        setSuturesCount(existing.suturesCount != null ? String(existing.suturesCount) : "");
        setSpecimenSent(existing.specimenSent);
        setDoctorId(existing.doctorId ?? user?.doctorId ?? null);
        setApprovedAt(existing.approvedAt ?? null);
      })
    ];

    if (user?.role === "Admin") {
      requests.push(
        apiRequest<DoctorSummary[]>("/api/doctors?status=active").then((items) => {
          if (active) setDoctors(items ?? []);
        })
      );
    }

    void Promise.all(requests)
      .catch((err) => {
        if (active) setError(err instanceof Error ? err.message : "تعذر تحميل التقرير الجراحي");
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [allowed, id, user?.doctorId, user?.role]);

  async function save() {
    if (!id || saving) return;
    const dateTime = surgeryDateTime.trim();
    if (dateTime && !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(dateTime)) {
      setError("تاريخ ووقت الجراحة يجب أن يكون بصيغة YYYY-MM-DDTHH:mm.");
      return;
    }

    const duration = optionalNonNegativeNumber(durationMinutes, "مدة الجراحة");
    if (typeof duration === "string") {
      setError(duration);
      return;
    }
    const sutures = optionalNonNegativeInteger(suturesCount, "عدد الغرز");
    if (typeof sutures === "string") {
      setError(sutures);
      return;
    }

    const input: UpsertOperativeInput = {
      surgeryDateTime: dateTime || null,
      durationMinutes: duration,
      anesthesiaUsed: clean(anesthesiaUsed),
      technique: clean(technique),
      detailedDescription: clean(detailedDescription),
      outcome: clean(outcome),
      complications: clean(complications),
      suturesCount: sutures,
      specimenSent,
      doctorId
    };

    setSaving(true);
    setError(null);
    try {
      await apiRequest(`/api/surgery-cases/${id}/operative`, {
        method: "PUT",
        body: JSON.stringify(input)
      });
      router.back();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر حفظ التقرير الجراحي");
    } finally {
      setSaving(false);
    }
  }

  if (!allowed) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="التقرير الجراحي متاح للأدمن وجراح الفم فقط." />
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
        <Text style={styles.title}>تقرير الجراحة</Text>
        <Text style={styles.subtitle}>{patientName || "الحالة الجراحية"}</Text>
      </View>

      {error ? <StateMessage title="تعذر الحفظ" message={error} /> : null}
      {approvedAt ? (
        <Card>
          <Text style={styles.warningTitle}>التقرير معتمد</Text>
          <Text style={styles.warningBody}>
            تم اعتماد التقرير في {approvedAt}. الـBackend الحالي لا يقفل التعديل بعد الاعتماد، لذلك أي تعديل هنا يجب أن يكون تصحيحًا مقصودًا وليس تغييرًا عاديًا للسجل.
          </Text>
        </Card>
      ) : null}

      <FormField
        label="تاريخ ووقت الجراحة YYYY-MM-DDTHH:mm"
        value={surgeryDateTime}
        onChangeText={setSurgeryDateTime}
        placeholder="2026-08-23T10:30"
      />
      <FormField
        label="المدة بالدقائق"
        value={durationMinutes}
        onChangeText={setDurationMinutes}
        keyboardType="number-pad"
        placeholder="مثال: 45"
      />
      <ChoiceRow
        label="نوع التخدير"
        value={anesthesiaUsed || null}
        options={[
          { label: "موضعي", value: "Local" },
          { label: "تهدئة", value: "Sedation" },
          { label: "عام", value: "General" }
        ]}
        onChange={(value) => setAnesthesiaUsed(value ?? "")}
      />
      <FormField label="التخدير المستخدم / تفاصيل" value={anesthesiaUsed} onChangeText={setAnesthesiaUsed} />
      <FormField label="التقنية الجراحية" value={technique} onChangeText={setTechnique} multiline />
      <FormField
        label="الوصف التفصيلي"
        value={detailedDescription}
        onChangeText={setDetailedDescription}
        multiline
      />
      <ChoiceRow
        label="النتيجة"
        value={outcome || null}
        options={OUTCOME_OPTIONS}
        onChange={(value) => setOutcome(value ?? "")}
      />
      <FormField label="النتيجة / وصف مخصص" value={outcome} onChangeText={setOutcome} />
      <FormField label="المضاعفات" value={complications} onChangeText={setComplications} multiline />
      <FormField
        label="عدد الغرز"
        value={suturesCount}
        onChangeText={setSuturesCount}
        keyboardType="number-pad"
      />
      <ChoiceRow
        label="هل أُرسلت عينة للفحص؟"
        value={specimenSent ? "yes" : "no"}
        options={[
          { label: "نعم", value: "yes" },
          { label: "لا", value: "no" }
        ]}
        onChange={(value) => {
          if (value === "yes") setSpecimenSent(true);
          if (value === "no") setSpecimenSent(false);
        }}
      />

      {user?.role === "Admin" ? (
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
      ) : null}

      <PrimaryButton title="حفظ التقرير الجراحي" loading={saving} onPress={() => void save()} />
    </Screen>
  );
}

function clean(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

function optionalNonNegativeNumber(value: string, label: string): number | null | string {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const parsed = Number(trimmed.replace(",", "."));
  if (!Number.isFinite(parsed) || parsed < 0) return `${label} يجب أن تكون رقمًا موجبًا أو صفرًا.`;
  return parsed;
}

function optionalNonNegativeInteger(value: string, label: string): number | null | string {
  const parsed = optionalNonNegativeNumber(value, label);
  if (typeof parsed === "string" || parsed === null) return parsed;
  if (!Number.isInteger(parsed)) return `${label} يجب أن يكون عددًا صحيحًا.`;
  return parsed;
}

function localDateTimeInput(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  const hour = String(date.getHours()).padStart(2, "0");
  const minute = String(date.getMinutes()).padStart(2, "0");
  return `${year}-${month}-${day}T${hour}:${minute}`;
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  warningTitle: { color: colors.warning, fontWeight: "800", textAlign: "right" },
  warningBody: { color: colors.warning, marginTop: 5, textAlign: "right", lineHeight: 22 }
});
