import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import type { PrescriptionDetail } from "@/lib/records";
import { colors, spacing } from "@/theme";
import { useFocusEffect, useLocalSearchParams } from "expo-router";
import React, { useCallback, useState } from "react";
import { ActivityIndicator, RefreshControl, StyleSheet, Text, View } from "react-native";

export default function PrescriptionDetailScreen() {
  const params = useLocalSearchParams<{ id: string }>();
  const id = first(params.id);
  const [data, setData] = useState<PrescriptionDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!id) {
      setLoading(false);
      return;
    }
    setError(null);
    try {
      setData(await apiRequest<PrescriptionDetail>(`/api/prescriptions/${id}`));
    } catch (err) {
      setData(null);
      setError(err instanceof Error ? err.message : "تعذر تحميل الوصفة");
    } finally {
      setLoading(false);
    }
  }, [id]);

  useFocusEffect(useCallback(() => { setLoading(true); void load(); }, [load]));

  async function refresh() {
    setRefreshing(true);
    try { await load(); } finally { setRefreshing(false); }
  }

  if (loading && !data) return <Screen><ActivityIndicator size="large" color={colors.primary} /></Screen>;
  if (!data) return <Screen><StateMessage title="تعذر فتح الوصفة" message={error ?? "الوصفة غير موجودة"} action={<PrimaryButton title="إعادة المحاولة" onPress={() => void load()} />} /></Screen>;

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      <View><Text style={styles.title}>الوصفة الطبية</Text><Text style={styles.subtitle}>{data.patientName} • {data.createdAt}</Text></View>
      <Card>
        <Row label="الطبيب" value={data.doctorName ? `د. ${data.doctorName}` : "—"} />
        <Row label="التشخيص" value={data.diagnosis || "—"} last />
        {data.notes ? <Text style={styles.notes}>{data.notes}</Text> : null}
      </Card>
      <SectionTitle>الأدوية</SectionTitle>
      {data.drugs.map((drug, index) => (
        <Card key={`${drug.name}-${index}`}>
          <Text style={styles.drugName}>{index + 1}. {drug.name}</Text>
          <Row label="الجرعة" value={drug.dose} />
          <Row label="التكرار" value={drug.frequency} />
          <Row label="المدة" value={drug.duration} last={!drug.notes} />
          {drug.notes ? <Text style={styles.notes}>{drug.notes}</Text> : null}
        </Card>
      ))}
    </Screen>
  );
}

function first(value?: string | string[]): string { return Array.isArray(value) ? value[0] ?? "" : value ?? ""; }
function Row({ label, value, last = false }: { label: string; value: string; last?: boolean }) {
  return <View style={[styles.row, last && { borderBottomWidth: 0 }]}><Text style={styles.value}>{value}</Text><Text style={styles.label}>{label}</Text></View>;
}
const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  drugName: { color: colors.text, fontSize: 17, fontWeight: "800", textAlign: "right", marginBottom: spacing.sm },
  row: { minHeight: 42, flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.border },
  label: { color: colors.muted, textAlign: "right" },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" },
  notes: { color: colors.text, backgroundColor: colors.background, padding: spacing.sm, borderRadius: 10, textAlign: "right", marginTop: spacing.sm, lineHeight: 22 }
});
