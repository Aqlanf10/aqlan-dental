import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import {
  canUseInventory,
  inventoryUnit,
  stockState,
  type InventoryAdjustment,
  type InventoryAdjustmentResponse,
  type InventoryItem,
  type InventoryListResponse
} from "@/lib/inventory";
import { colors, spacing } from "@/theme";
import { router, useFocusEffect, useLocalSearchParams } from "expo-router";
import React, { useCallback, useState } from "react";
import { ActivityIndicator, RefreshControl, StyleSheet, Text, View } from "react-native";

export default function InventoryItemScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ id: string }>();
  const id = first(params.id);
  const allowed = canUseInventory(user?.role);
  const [item, setItem] = useState<InventoryItem | null>(null);
  const [adjustments, setAdjustments] = useState<InventoryAdjustment[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!id || !allowed) {
      setLoading(false);
      return;
    }
    setError(null);
    const [listResult, historyResult] = await Promise.allSettled([
      apiRequest<InventoryListResponse>("/api/inventory?page=1&pageSize=100"),
      apiRequest<InventoryAdjustmentResponse>(`/api/inventory/${id}/adjustments?page=1&pageSize=100`)
    ]);

    if (listResult.status === "fulfilled") {
      const found = listResult.value.data.find((entry) => entry.id === id) ?? null;
      setItem(found);
      if (!found) setError("المادة غير موجودة في المخزون الحالي.");
    } else {
      setItem(null);
      setError(listResult.reason instanceof Error ? listResult.reason.message : "تعذر تحميل المادة");
    }

    setAdjustments(historyResult.status === "fulfilled" ? historyResult.value.data ?? [] : []);
    setLoading(false);
  }, [allowed, id]);

  useFocusEffect(useCallback(() => { setLoading(true); void load(); }, [load]));

  async function refresh() {
    setRefreshing(true);
    try { await load(); } finally { setRefreshing(false); }
  }

  if (!allowed) {
    return <Screen><StateMessage title="غير مصرح" message="هذه الشاشة متاحة للإدارة فقط." /></Screen>;
  }
  if (loading && !item) {
    return <Screen><ActivityIndicator size="large" color={colors.primary} /></Screen>;
  }
  if (!item) {
    return <Screen><StateMessage title="تعذر فتح المادة" message={error ?? "المادة غير موجودة"} action={<PrimaryButton title="إعادة المحاولة" onPress={() => void load()} />} /></Screen>;
  }

  const low = stockState(item) === "low";

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      <View>
        <Text style={styles.title}>{item.name}</Text>
        <Text style={[styles.stock, low && { color: colors.danger }]}>{low ? "المخزون منخفض" : "الرصيد ضمن الحد"}</Text>
      </View>

      {error ? <StateMessage title="تنبيه" message={error} /> : null}

      <Card>
        <Row label="التصنيف" value={item.category || "—"} />
        <Row label="الرصيد" value={`${item.quantity} ${inventoryUnit(item)}`} />
        <Row label="الحد الأدنى" value={String(item.minQuantity)} />
        {item.minStockLevel ? <Row label="Min stock level" value={item.minStockLevel} /> : null}
        {item.costPerUnit != null ? <Row label="تكلفة الوحدة" value={`${item.costPerUnit.toLocaleString()} YER`} /> : null}
        <Row label="وحدة الشراء" value={item.purchaseUnit || item.unit || "—"} />
        <Row label="وحدة الصرف" value={item.consumptionUnit || item.unit || "—"} />
        <Row label="رقم الدفعة" value={item.batchNumber || "—"} />
        <Row label="تاريخ الانتهاء" value={item.expiryDate || "—"} />
        <Row label="الموقع" value={item.warehouseLocation || "—"} last />
      </Card>

      <PrimaryButton title="تعديل بيانات المادة" onPress={() => router.push({ pathname: "/(app)/inventory-item-editor", params: { id: item.id } })} />
      <PrimaryButton title="إضافة / خصم كمية" onPress={() => router.push({ pathname: "/(app)/inventory-adjust", params: { id: item.id, name: item.name, quantity: String(item.quantity), unit: inventoryUnit(item) } })} />

      <SectionTitle>سجل الحركة</SectionTitle>
      {adjustments.length === 0 ? <StateMessage title="لا توجد حركات مسجلة لهذه المادة" /> : null}
      {adjustments.map((entry) => (
        <Card key={entry.id}>
          <View style={styles.movementHeader}>
            <Text style={[styles.delta, entry.delta < 0 ? { color: colors.danger } : { color: colors.success }]}>
              {entry.delta > 0 ? "+" : ""}{entry.delta}
            </Text>
            <Text style={styles.movementTitle}>{entry.adjustmentType || "تعديل"}</Text>
          </View>
          <Row label="قبل" value={String(entry.previousQuantity)} />
          <Row label="بعد" value={String(entry.newQuantity)} />
          {entry.reason ? <Text style={styles.notes}>{entry.reason}</Text> : null}
          {entry.labOrderId ? <Text style={styles.linked}>مرتبطة بطلب معمل</Text> : null}
          <Text style={styles.meta}>{entry.createdAt}</Text>
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
  stock: { color: colors.success, marginTop: 4, fontWeight: "800", textAlign: "right" },
  row: { minHeight: 44, flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.border },
  label: { color: colors.muted, textAlign: "right" },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "700" },
  movementHeader: { flexDirection: "row", alignItems: "center", justifyContent: "space-between" },
  movementTitle: { color: colors.text, fontWeight: "800", textAlign: "right" },
  delta: { fontSize: 18, fontWeight: "900" },
  notes: { color: colors.text, marginTop: spacing.sm, textAlign: "right", lineHeight: 22 },
  linked: { color: colors.primary, marginTop: spacing.sm, textAlign: "right", fontWeight: "700" },
  meta: { color: colors.muted, marginTop: spacing.sm, textAlign: "right", fontSize: 12 }
});
