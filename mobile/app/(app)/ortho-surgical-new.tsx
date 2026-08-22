import { useSession } from "@/auth/SessionProvider";
import { FormField, SelectList } from "@/components/forms";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import type { DoctorSummary } from "@/lib/types";
import { colors, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useEffect, useMemo, useState } from "react";
import { ActivityIndicator, StyleSheet, Text, View } from "react-native";

type CephAnalysisListItem = {
  id: string;
  orthoCaseId: string;
  patientName: string;
  analysisType: string;
  analysisDate: string;
  isApproved: boolean;
  landmarkCount: number;
  hasMeasurements: boolean;
};

export default function OrthoSurgicalNewScreen() {
  const { user, can } = useSession();
  const params = useLocalSearchParams<{ orthoCaseId: string; patientId?: string; patientName?: string }>();
  const orthoCaseId = first(params.orthoCaseId);
  const patientName = first(params.patientName);
  const allowed = can("ortho_surgical.create");
  const [surgeons, setSurgeons] = useState<DoctorSummary[]>([]);
  const [ceph, setCeph] = useState<CephAnalysisListItem[]>([]);
  const [surgeonId, setSurgeonId] = useState<string | null>(null);
  const [cephAnalysisId, setCephAnalysisId] = useState<string | null>(null);
  const [diagnosisSummary, setDiagnosisSummary] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!allowed || !orthoCaseId) {
      setLoading(false);
      return;
    }
    let active = true;
    const doctorQuery = new URLSearchParams({ status: "active" });
    if (user?.role !== "Admin" && user?.branchId) doctorQuery.set("branchId", user.branchId);
    Promise.allSettled([
      apiRequest<DoctorSummary[]>(`/api/doctors?${doctorQuery.toString()}`),
      apiRequest<CephAnalysisListItem[]>(`/api/ceph?orthoCaseId=${encodeURIComponent(orthoCaseId)}`)
    ]).then(([doctorResult, cephResult]) => {
      if (!active) return;
      if (doctorResult.status === "fulfilled") {
        setSurgeons((doctorResult.value ?? []).filter((doctor) => {
          const value = (doctor.specialty || "").toLowerCase();
          return value.includes("surg") || value.includes("جراح") || value.includes("oral");
        }));
      }
      if (cephResult.status === "fulfilled") {
        const analyses = cephResult.value ?? [];
        setCeph(analyses);
        const approved = analyses.find((item) => item.isApproved);
        if (approved) setCephAnalysisId(approved.id);
      }
      const rejected = [doctorResult, cephResult].find((result) => result.status === "rejected") as PromiseRejectedResult | undefined;
      if (rejected) setError(rejected.reason instanceof Error ? rejected.reason.message : "تعذر تحميل بعض بيانات التخطيط");
    }).finally(() => active && setLoading(false));
    return () => { active = false; };
  }, [allowed, orthoCaseId, user?.branchId, user?.role]);

  const surgeonOptions = useMemo(() => surgeons.map((doctor) => ({
    value: doctor.id,
    label: doctor.name,
    subtitle: doctor.specialty || doctor.branchName || null
  })), [surgeons]);

  const cephOptions = useMemo(() => ceph.map((item) => ({
    value: item.id,
    label: `${item.analysisType.toUpperCase()} — ${item.analysisDate}`,
    subtitle: item.isApproved ? "معتمد — مناسب لـ Surgical VTO" : "غير معتمد"
  })), [ceph]);

  async function save() {
    if (!allowed) return setError("صلاحية ortho_surgical.create غير مفعلة.");
    if (!orthoCaseId) return setError("حالة التقويم غير محددة.");
    setSaving(true);
    setError(null);
    try {
      const created = await apiRequest<{ id: string; caseNumber: string }>("/api/ortho-surgical-cases", {
        method: "POST",
        body: JSON.stringify({
          orthoCaseId,
          surgeonId: surgeonId || null,
          cephAnalysisId: cephAnalysisId || null,
          diagnosisSummary: diagnosisSummary.trim() || null
        })
      });
      router.replace({ pathname: "/(app)/ortho-surgical-case", params: { id: created.id } });
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر إنشاء خطة Orthognathic");
    } finally {
      setSaving(false);
    }
  }

  if (!allowed) return <Screen><StateMessage title="الإنشاء غير مسموح" message="مفتاح ortho_surgical.create غير مفعّل لهذا الحساب." /></Screen>;
  if (loading) return <Screen><ActivityIndicator size="large" color={colors.primary} /></Screen>;

  return (
    <Screen>
      <View><Text style={styles.title}>خطة Orthognathic جديدة</Text><Text style={styles.subtitle}>{patientName || "حالة التقويم"}</Text></View>
      {error ? <StateMessage title="تعذر إنشاء الخطة" message={error} /> : null}
      <Card>
        <View style={styles.form}>
          <SelectList label="الجراح" value={surgeonId} onChange={setSurgeonId} options={surgeonOptions} emptyLabel="يمكن تحديد الجراح لاحقًا" />
          <SelectList label="تحليل السيفالو المرتبط" value={cephAnalysisId} onChange={setCephAnalysisId} options={cephOptions} emptyLabel="بدون ربط الآن" />
          <FormField label="ملخص التشخيص" value={diagnosisSummary} onChangeText={setDiagnosisSummary} multiline placeholder="ملخص المشكلة الهيكلية والهدف من الإحالة الجراحية" />
        </View>
      </Card>
      {!ceph.some((item) => item.isApproved) ? (
        <StateMessage title="لا يوجد سيفالو معتمد" message="يمكن إنشاء مساحة التخطيط، لكن Surgical VTO لن يعمل حتى ربط تحليل سيفالو معتمد." />
      ) : null}
      <Text style={styles.note}>إنشاء المساحة لا ينشئ SurgeryCase ولا يكرر التشخيص أو السيفالو؛ التنفيذ الجراحي الحقيقي يُفتح لاحقًا من الخطة المشتركة عندما تصبح ReadyForSurgery.</Text>
      <PrimaryButton title="إنشاء مساحة التخطيط" loading={saving} disabled={saving} onPress={() => void save()} />
    </Screen>
  );
}

function first(value?: string | string[]): string { return Array.isArray(value) ? value[0] ?? "" : value ?? ""; }
const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  form: { gap: spacing.md },
  note: { color: colors.muted, fontSize: 12, lineHeight: 20, textAlign: "right" }
});
