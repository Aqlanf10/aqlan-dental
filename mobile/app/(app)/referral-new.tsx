import { useSession } from "@/auth/SessionProvider";
import { FormField, SelectList } from "@/components/forms";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { REFERRAL_PRIORITY_OPTIONS } from "@/lib/records";
import type { DoctorSummary } from "@/lib/types";
import { colors, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useEffect, useMemo, useState } from "react";
import { ActivityIndicator, StyleSheet, Text, View } from "react-native";

export default function ReferralNewScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ patientId: string; patientName?: string }>();
  const patientId = first(params.patientId);
  const patientName = first(params.patientName);
  const [doctors, setDoctors] = useState<DoctorSummary[]>([]);
  const [fromDoctorId, setFromDoctorId] = useState<string | null>(user?.doctorId ?? null);
  const [toDoctorId, setToDoctorId] = useState<string | null>(null);
  const [priority, setPriority] = useState<string | null>("normal");
  const [reason, setReason] = useState("");
  const [notes, setNotes] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    const query = new URLSearchParams({ status: "active" });
    if (user?.role !== "Admin" && user?.branchId) query.set("branchId", user.branchId);
    apiRequest<DoctorSummary[]>(`/api/doctors?${query.toString()}`)
      .then((result) => active && setDoctors(result ?? []))
      .catch((err) => active && setError(err instanceof Error ? err.message : "تعذر تحميل الأطباء"))
      .finally(() => active && setLoading(false));
    return () => { active = false; };
  }, [user?.branchId, user?.role]);

  const doctorOptions = useMemo(() => doctors.map((doctor) => ({
    value: doctor.id,
    label: doctor.name,
    subtitle: doctor.specialty || doctor.branchName || null
  })), [doctors]);

  async function save() {
    if (!patientId) return setError("معرّف المريض مفقود.");
    if (!fromDoctorId) return setError("اختر الطبيب المُحيل.");
    if (!toDoctorId) return setError("اختر الطبيب المستقبِل.");
    if (fromDoctorId === toDoctorId) return setError("لا يمكن إحالة المريض إلى نفس الطبيب.");
    setSaving(true);
    setError(null);
    try {
      await apiRequest("/api/referrals", {
        method: "POST",
        body: JSON.stringify({
          patientId,
          fromDoctorId,
          toDoctorId,
          reason: reason.trim() || null,
          priority: priority || "normal",
          notes: notes.trim() || null
        })
      });
      router.back();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر إنشاء الإحالة");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Screen>
      <View><Text style={styles.title}>إحالة داخلية</Text><Text style={styles.subtitle}>{patientName || "ملف المريض"}</Text></View>
      {error ? <StateMessage title="تعذر إنشاء الإحالة" message={error} /> : null}
      {loading ? <ActivityIndicator size="large" color={colors.primary} /> : (
        <Card>
          <View style={styles.form}>
            <SelectList label="الطبيب المُحيل *" value={fromDoctorId} onChange={setFromDoctorId} options={doctorOptions} emptyLabel="اختر الطبيب المُحيل" />
            <SelectList label="الطبيب المستقبِل *" value={toDoctorId} onChange={setToDoctorId} options={doctorOptions.filter((option) => option.value !== fromDoctorId)} emptyLabel="اختر الطبيب المستقبِل" />
            <SelectList label="الأولوية" value={priority} onChange={setPriority} options={REFERRAL_PRIORITY_OPTIONS.map((item) => ({ label: item.label, value: item.value }))} emptyLabel="عادي" />
            <FormField label="سبب الإحالة" value={reason} onChangeText={setReason} multiline />
            <FormField label="ملاحظات" value={notes} onChangeText={setNotes} multiline />
          </View>
        </Card>
      )}
      <PrimaryButton title="إنشاء الإحالة" loading={saving} disabled={saving || loading} onPress={() => void save()} />
    </Screen>
  );
}

function first(value?: string | string[]): string { return Array.isArray(value) ? value[0] ?? "" : value ?? ""; }
const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  form: { gap: spacing.md }
});
