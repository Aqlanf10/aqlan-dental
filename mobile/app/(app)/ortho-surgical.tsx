import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import {
  canViewOrthognathic,
  orthoSurgicalStatusLabel,
  type OrthoSurgicalCaseListItem
} from "@/lib/orthognathic";
import { colors, spacing } from "@/theme";
import { router, useFocusEffect, useLocalSearchParams } from "expo-router";
import React, { useCallback, useState } from "react";
import { ActivityIndicator, Pressable, RefreshControl, StyleSheet, Text, View } from "react-native";

type ListResponse = { data: OrthoSurgicalCaseListItem[]; total: number; page: number; pageSize: number };

export default function OrthoSurgicalScreen() {
  const { can } = useSession();
  const params = useLocalSearchParams<{ orthoCaseId: string; patientId?: string; patientName?: string }>();
  const orthoCaseId = first(params.orthoCaseId);
  const patientId = first(params.patientId);
  const patientName = first(params.patientName);
  const canView = canViewOrthognathic(can);
  const canCreate = can("ortho_surgical.create");
  const [cases, setCases] = useState<OrthoSurgicalCaseListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!canView || !orthoCaseId) {
      setLoading(false);
      return;
    }
    setError(null);
    try {
      const response = await apiRequest<ListResponse>(
        `/api/ortho-surgical-cases?orthoCaseId=${encodeURIComponent(orthoCaseId)}&page=1&pageSize=20`
      );
      setCases(response.data ?? []);
    } catch (err) {
      setCases([]);
      setError(err instanceof Error ? err.message : "تعذر تحميل التخطيط التقويمي الجراحي");
    } finally {
      setLoading(false);
    }
  }, [canView, orthoCaseId]);

  useFocusEffect(useCallback(() => { setLoading(true); void load(); }, [load]));

  async function refresh() {
    setRefreshing(true);
    try { await load(); } finally { setRefreshing(false); }
  }

  if (!canView) {
    return <Screen><StateMessage title="غير مصرح" message="حسابك لا يملك صلاحية ortho_surgical.view." /></Screen>;
  }

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      <View>
        <Text style={styles.title}>التخطيط التقويمي الجراحي</Text>
        <Text style={styles.subtitle}>{patientName || "Orthognathic workspace"}</Text>
      </View>

      {error ? <StateMessage title="تعذر تحميل مساحة التخطيط" message={error} action={<PrimaryButton title="إعادة المحاولة" onPress={() => void load()} />} /> : null}
      {loading ? <ActivityIndicator size="large" color={colors.primary} /> : null}

      {!loading && cases.length === 0 ? (
        <>
          <StateMessage
            title="لا توجد خطة Orthognathic مرتبطة بهذه الحالة"
            message="مساحة التخطيط المشتركة تربط حالة التقويم والسيفالو والجراح بحالة الجراحة الحقيقية دون تكرار السجلات."
          />
          {canCreate ? (
            <PrimaryButton
              title="إنشاء خطة تقويمية جراحية"
              onPress={() => router.push({
                pathname: "/(app)/ortho-surgical-new",
                params: { orthoCaseId, patientId, patientName }
              })}
            />
          ) : (
            <StateMessage title="الإنشاء غير مفعّل" message="مفتاح ortho_surgical.create غير مفعّل لهذا الحساب." />
          )}
        </>
      ) : null}

      {cases.map((item) => (
        <Pressable
          key={item.id}
          onPress={() => router.push({ pathname: "/(app)/ortho-surgical-case", params: { id: item.id } })}
        >
          <Card>
            <View style={styles.header}>
              <Text style={styles.status}>{item.statusLabel || orthoSurgicalStatusLabel(item.status)}</Text>
              <View style={{ flex: 1 }}>
                <Text style={styles.caseNumber}>{item.caseNumber}</Text>
                <Text style={styles.meta}>{item.responsibleParty || "—"}</Text>
              </View>
            </View>
            <Row label="أخصائي التقويم" value={item.orthodontistName || "—"} />
            <Row label="الجراح" value={item.surgeonName || "غير محدد"} />
            <Row label="اعتماد التقويم" value={item.orthodontistApprovedAt ? "تم" : "بانتظار الاعتماد"} />
            <Row label="اعتماد الجراح" value={item.surgeonApprovedAt ? "تم" : "بانتظار الاعتماد"} last />
          </Card>
        </Pressable>
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
  header: { flexDirection: "row", gap: spacing.sm, alignItems: "flex-start" },
  status: { color: colors.primary, backgroundColor: colors.primarySoft, paddingHorizontal: spacing.sm, paddingVertical: 5, borderRadius: 999, fontSize: 11, fontWeight: "800" },
  caseNumber: { color: colors.text, fontSize: 18, fontWeight: "800", textAlign: "right" },
  meta: { color: colors.muted, marginTop: 4, textAlign: "right" },
  row: { minHeight: 43, flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.border },
  label: { color: colors.muted, textAlign: "right" },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" }
});
