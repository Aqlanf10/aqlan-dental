import { useSession } from "@/auth/SessionProvider";
import { FormField } from "@/components/forms";
import { PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { isoDateLocal } from "@/lib/format";
import { canUseSurgery } from "@/lib/surgery";
import { colors } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useState } from "react";
import { StyleSheet, Text, View } from "react-native";

export default function SurgeryReferralNewScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ id: string; patientName?: string }>();
  const id = Array.isArray(params.id) ? params.id[0] : params.id;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const allowed = canUseSurgery(user?.role);

  const [hospitalName, setHospitalName] = useState("");
  const [reason, setReason] = useState("");
  const [referralDate, setReferralDate] = useState(isoDateLocal(new Date()));
  const [notes, setNotes] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function save() {
    if (!id || saving) return;
    const date = referralDate.trim();
    if (date && (!/^\d{4}-\d{2}-\d{2}$/.test(date) || Number.isNaN(Date.parse(`${date}T00:00:00`)))) {
      setError("تاريخ الإحالة يجب أن يكون بصيغة YYYY-MM-DD.");
      return;
    }
    if (!hospitalName.trim() && !reason.trim()) {
      setError("أدخل اسم المستشفى أو سبب الإحالة على الأقل.");
      return;
    }

    setSaving(true);
    setError(null);
    try {
      await apiRequest(`/api/surgery-cases/${id}/referrals`, {
        method: "POST",
        body: JSON.stringify({
          hospitalName: clean(hospitalName),
          reason: clean(reason),
          referralDate: date || null,
          notes: clean(notes)
        })
      });
      router.back();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر إنشاء الإحالة");
    } finally {
      setSaving(false);
    }
  }

  if (!allowed) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="إحالات الجراحة متاحة للأدمن وجراح الفم فقط." />
      </Screen>
    );
  }

  return (
    <Screen>
      <View>
        <Text style={styles.title}>إحالة مستشفى</Text>
        <Text style={styles.subtitle}>{patientName || "الحالة الجراحية"}</Text>
      </View>

      {error ? <StateMessage title="تعذر إنشاء الإحالة" message={error} /> : null}

      <FormField label="اسم المستشفى" value={hospitalName} onChangeText={setHospitalName} />
      <FormField label="سبب الإحالة" value={reason} onChangeText={setReason} multiline />
      <FormField
        label="تاريخ الإحالة YYYY-MM-DD"
        value={referralDate}
        onChangeText={setReferralDate}
        maxLength={10}
      />
      <FormField label="ملاحظات" value={notes} onChangeText={setNotes} multiline />

      <PrimaryButton title="إنشاء الإحالة" loading={saving} onPress={() => void save()} />
    </Screen>
  );
}

function clean(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" }
});
