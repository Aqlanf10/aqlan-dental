import { FormField } from "@/components/forms";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import type { DrugItem } from "@/lib/records";
import { colors, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useState } from "react";
import { Pressable, StyleSheet, Text, View } from "react-native";

const emptyDrug = (): DrugItem => ({ name: "", dose: "", frequency: "", duration: "", notes: null });

export default function PrescriptionNewScreen() {
  const params = useLocalSearchParams<{ patientId: string; patientName?: string; visitId?: string }>();
  const patientId = first(params.patientId);
  const patientName = first(params.patientName);
  const visitId = first(params.visitId);
  const [diagnosis, setDiagnosis] = useState("");
  const [notes, setNotes] = useState("");
  const [drugs, setDrugs] = useState<DrugItem[]>([emptyDrug()]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function patchDrug(index: number, patch: Partial<DrugItem>) {
    setDrugs((current) => current.map((drug, i) => i === index ? { ...drug, ...patch } : drug));
  }

  function removeDrug(index: number) {
    setDrugs((current) => current.length === 1 ? current : current.filter((_, i) => i !== index));
  }

  async function save() {
    if (!patientId) return setError("معرّف المريض مفقود.");
    const normalized = drugs.map((drug) => ({
      name: drug.name.trim(),
      dose: drug.dose.trim(),
      frequency: drug.frequency.trim(),
      duration: drug.duration.trim(),
      notes: drug.notes?.trim() || null
    }));
    const invalidIndex = normalized.findIndex((drug) => !drug.name || !drug.dose || !drug.frequency || !drug.duration);
    if (invalidIndex >= 0) return setError(`أكمل اسم الدواء والجرعة والتكرار والمدة للدواء رقم ${invalidIndex + 1}.`);

    setSaving(true);
    setError(null);
    try {
      const created = await apiRequest<{ id: string }>("/api/prescriptions", {
        method: "POST",
        body: JSON.stringify({
          patientId,
          visitId: visitId || null,
          diagnosis: diagnosis.trim() || null,
          drugs: normalized,
          notes: notes.trim() || null
        })
      });
      router.replace({ pathname: "/(app)/prescription-detail", params: { id: created.id } });
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر حفظ الوصفة الطبية");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Screen>
      <View><Text style={styles.title}>وصفة طبية جديدة</Text><Text style={styles.subtitle}>{patientName || "ملف المريض"}</Text></View>
      {error ? <StateMessage title="تعذر حفظ الوصفة" message={error} /> : null}
      <Card><View style={styles.form}><FormField label="التشخيص" value={diagnosis} onChangeText={setDiagnosis} multiline /><FormField label="ملاحظات عامة" value={notes} onChangeText={setNotes} multiline /></View></Card>

      <SectionTitle>الأدوية</SectionTitle>
      {drugs.map((drug, index) => (
        <Card key={index}>
          <View style={styles.drugHeader}>
            {drugs.length > 1 ? <Pressable onPress={() => removeDrug(index)}><Text style={styles.remove}>حذف</Text></Pressable> : <View />}
            <Text style={styles.drugTitle}>دواء {index + 1}</Text>
          </View>
          <View style={styles.form}>
            <FormField label="اسم الدواء *" value={drug.name} onChangeText={(value) => patchDrug(index, { name: value })} />
            <FormField label="الجرعة *" value={drug.dose} onChangeText={(value) => patchDrug(index, { dose: value })} placeholder="مثال: 500 mg" />
            <FormField label="التكرار *" value={drug.frequency} onChangeText={(value) => patchDrug(index, { frequency: value })} placeholder="مثال: كل 8 ساعات" />
            <FormField label="المدة *" value={drug.duration} onChangeText={(value) => patchDrug(index, { duration: value })} placeholder="مثال: 5 أيام" />
            <FormField label="ملاحظات الدواء" value={drug.notes || ""} onChangeText={(value) => patchDrug(index, { notes: value })} />
          </View>
        </Card>
      ))}
      <PrimaryButton title="إضافة دواء آخر" onPress={() => setDrugs((current) => [...current, emptyDrug()])} />
      <PrimaryButton title="حفظ الوصفة" loading={saving} disabled={saving} onPress={() => void save()} />
    </Screen>
  );
}

function first(value?: string | string[]): string { return Array.isArray(value) ? value[0] ?? "" : value ?? ""; }
const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  form: { gap: spacing.md },
  drugHeader: { flexDirection: "row", justifyContent: "space-between", alignItems: "center", marginBottom: spacing.sm },
  drugTitle: { color: colors.text, fontWeight: "800", fontSize: 16 },
  remove: { color: colors.danger, fontWeight: "800" }
});
