import { useSession } from "@/auth/SessionProvider";
import { FormField } from "@/components/forms";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { isoDateLocal } from "@/lib/format";
import {
  canUseSurgery,
  type PostopRecord,
  type SurgeryFollowupItem,
  type SurgeryPrescriptionItem,
  type UpsertPostopInput
} from "@/lib/surgery";
import { colors, radius, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useEffect, useState } from "react";
import { ActivityIndicator, Pressable, StyleSheet, Text, View } from "react-native";

export default function SurgeryPostopScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ id: string; patientName?: string }>();
  const id = Array.isArray(params.id) ? params.id[0] : params.id;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const allowed = canUseSurgery(user?.role);

  const [instructions, setInstructions] = useState("");
  const [prescription, setPrescription] = useState<SurgeryPrescriptionItem[]>([]);
  const [followups, setFollowups] = useState<SurgeryFollowupItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id || !allowed) {
      setLoading(false);
      return;
    }
    let active = true;
    void apiRequest<PostopRecord | null>(`/api/surgery-cases/${id}/postop`)
      .then((existing) => {
        if (!active || !existing) return;
        setInstructions(existing.instructions ?? "");
        setPrescription((existing.prescription ?? []).map(normalizePrescription));
        setFollowups((existing.followupSchedule ?? []).map(normalizeFollowup));
      })
      .catch((err) => {
        if (active) setError(err instanceof Error ? err.message : "تعذر تحميل بيانات ما بعد الجراحة");
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [allowed, id]);

  function addPrescription() {
    setPrescription((current) => [
      ...current,
      { medicine: "", dosage: "", frequency: "", duration: "" }
    ]);
  }

  function updatePrescription(index: number, patch: Partial<SurgeryPrescriptionItem>) {
    setPrescription((current) => current.map((item, i) => (i === index ? { ...item, ...patch } : item)));
  }

  function removePrescription(index: number) {
    setPrescription((current) => current.filter((_, i) => i !== index));
  }

  function addFollowup() {
    setFollowups((current) => [...current, { date: isoDateLocal(new Date()), notes: "" }]);
  }

  function updateFollowup(index: number, patch: Partial<SurgeryFollowupItem>) {
    setFollowups((current) => current.map((item, i) => (i === index ? { ...item, ...patch } : item)));
  }

  function removeFollowup(index: number) {
    setFollowups((current) => current.filter((_, i) => i !== index));
  }

  async function save() {
    if (!id || saving) return;

    for (const [index, item] of followups.entries()) {
      if (!/^\d{4}-\d{2}-\d{2}$/.test(item.date) || Number.isNaN(Date.parse(`${item.date}T00:00:00`))) {
        setError(`تاريخ المتابعة رقم ${index + 1} غير صالح. استخدم YYYY-MM-DD.`);
        return;
      }
    }

    const cleanedPrescription = prescription
      .map((item) => ({
        medicine: item.medicine.trim(),
        dosage: item.dosage.trim(),
        frequency: item.frequency.trim(),
        duration: item.duration.trim()
      }))
      .filter((item) => Object.values(item).some(Boolean));

    if (cleanedPrescription.some((item) => !item.medicine)) {
      setError("كل وصفة دوائية مدخلة يجب أن تحتوي اسم الدواء على الأقل.");
      return;
    }

    const cleanedFollowups = followups.map((item) => ({
      date: item.date.trim(),
      notes: clean(item.notes ?? "")
    }));

    const input: UpsertPostopInput = {
      instructions: clean(instructions),
      prescription: cleanedPrescription,
      followupSchedule: cleanedFollowups
    };

    setSaving(true);
    setError(null);
    try {
      await apiRequest(`/api/surgery-cases/${id}/postop`, {
        method: "PUT",
        body: JSON.stringify(input)
      });
      router.back();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر حفظ بيانات ما بعد الجراحة");
    } finally {
      setSaving(false);
    }
  }

  if (!allowed) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="بيانات ما بعد الجراحة متاحة للأدمن وجراح الفم فقط." />
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
        <Text style={styles.title}>ما بعد الجراحة</Text>
        <Text style={styles.subtitle}>{patientName || "الحالة الجراحية"}</Text>
      </View>

      {error ? <StateMessage title="تعذر الحفظ" message={error} /> : null}

      <FormField
        label="تعليمات ما بعد الجراحة"
        value={instructions}
        onChangeText={setInstructions}
        multiline
      />

      <SectionTitle>الأدوية</SectionTitle>
      {prescription.length === 0 ? <StateMessage title="لا توجد أدوية مضافة" /> : null}
      {prescription.map((item, index) => (
        <Card key={`rx-${index}`}>
          <Text style={styles.itemTitle}>دواء {index + 1}</Text>
          <FormField
            label="اسم الدواء"
            value={item.medicine}
            onChangeText={(value) => updatePrescription(index, { medicine: value })}
            placeholder="Amoxicillin..."
          />
          <FormField
            label="الجرعة"
            value={item.dosage}
            onChangeText={(value) => updatePrescription(index, { dosage: value })}
            placeholder="500 mg"
          />
          <FormField
            label="التكرار"
            value={item.frequency}
            onChangeText={(value) => updatePrescription(index, { frequency: value })}
            placeholder="كل 8 ساعات"
          />
          <FormField
            label="المدة"
            value={item.duration}
            onChangeText={(value) => updatePrescription(index, { duration: value })}
            placeholder="5 أيام"
          />
          <DangerButton title="حذف الدواء" onPress={() => removePrescription(index)} />
        </Card>
      ))}
      <PrimaryButton title="إضافة دواء" onPress={addPrescription} />

      <SectionTitle>جدول المتابعة</SectionTitle>
      {followups.length === 0 ? <StateMessage title="لا توجد متابعة مضافة" /> : null}
      {followups.map((item, index) => (
        <Card key={`fu-${index}`}>
          <Text style={styles.itemTitle}>متابعة {index + 1}</Text>
          <FormField
            label="التاريخ YYYY-MM-DD"
            value={item.date}
            onChangeText={(value) => updateFollowup(index, { date: value })}
            maxLength={10}
          />
          <FormField
            label="ملاحظات المتابعة"
            value={item.notes ?? ""}
            onChangeText={(value) => updateFollowup(index, { notes: value })}
            multiline
          />
          <DangerButton title="حذف المتابعة" onPress={() => removeFollowup(index)} />
        </Card>
      ))}
      <PrimaryButton title="إضافة متابعة" onPress={addFollowup} />

      <Card>
        <Text style={styles.note}>
          هذه الوصفات محفوظة داخل سجل ما بعد الجراحة نفسه وفق عقد الجراحة الحالي، وليست بديلًا عن وحدة الوصفات الطبية العامة التي ستُربط لاحقًا بشكل مستقل.
        </Text>
      </Card>

      <PrimaryButton title="حفظ ما بعد الجراحة" loading={saving} onPress={() => void save()} />
    </Screen>
  );
}

function DangerButton({ title, onPress }: { title: string; onPress: () => void }) {
  return (
    <Pressable onPress={onPress} style={styles.dangerButton}>
      <Text style={styles.dangerText}>{title}</Text>
    </Pressable>
  );
}

function normalizePrescription(item: SurgeryPrescriptionItem): SurgeryPrescriptionItem {
  return {
    medicine: item.medicine ?? "",
    dosage: item.dosage ?? "",
    frequency: item.frequency ?? "",
    duration: item.duration ?? ""
  };
}

function normalizeFollowup(item: SurgeryFollowupItem): SurgeryFollowupItem {
  return { date: item.date ?? "", notes: item.notes ?? "" };
}

function clean(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  itemTitle: { color: colors.text, fontSize: 16, fontWeight: "800", textAlign: "right", marginBottom: spacing.sm },
  dangerButton: {
    minHeight: 42,
    alignItems: "center",
    justifyContent: "center",
    borderWidth: 1,
    borderColor: colors.danger,
    borderRadius: radius.sm,
    backgroundColor: colors.dangerSoft,
    marginTop: spacing.sm
  },
  dangerText: { color: colors.danger, fontWeight: "800" },
  note: { color: colors.muted, textAlign: "right", lineHeight: 22 }
});
