import { useSession } from "@/auth/SessionProvider";
import { FormField } from "@/components/forms";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import {
  canUseInventory,
  inventoryUnit,
  type InventoryItem,
  type InventoryListResponse,
  type LabInventoryConsumptionResult
} from "@/lib/inventory";
import { colors, radius, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useEffect, useMemo, useState } from "react";
import { ActivityIndicator, Pressable, StyleSheet, Text, View } from "react-native";

type ExistingConsumables = {
  labOrderId: string;
  orderNumber?: string | null;
  lines: Array<{
    id: string;
    inventoryItemId: string;
    itemName: string;
    unit?: string | null;
    consumedQuantity: number;
    costPerUnit?: number | null;
    reason?: string | null;
    createdAt: string;
  }>;
  materialCost: number;
  currency: string;
  unpricedLineCount: number;
};

export default function LabOrderConsumeInventoryScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ id: string; orderNumber?: string }>();
  const id = first(params.id);
  const orderNumber = first(params.orderNumber) || "طلب المعمل";
  const allowed = canUseInventory(user?.role);
  const [items, setItems] = useState<InventoryItem[]>([]);
  const [existing, setExisting] = useState<ExistingConsumables | null>(null);
  const [quantities, setQuantities] = useState<Record<string, string>>({});
  const [notes, setNotes] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<LabInventoryConsumptionResult | null>(null);

  useEffect(() => {
    if (!allowed || !id) {
      setLoading(false);
      return;
    }
    let active = true;
    Promise.allSettled([
      apiRequest<InventoryListResponse>("/api/inventory?page=1&pageSize=100"),
      apiRequest<ExistingConsumables>(`/api/lab-orders/${id}/consumables`)
    ]).then(([inventoryResult, existingResult]) => {
      if (!active) return;
      if (inventoryResult.status === "fulfilled") setItems(inventoryResult.value.data ?? []);
      else setError(inventoryResult.reason instanceof Error ? inventoryResult.reason.message : "تعذر تحميل المخزون");
      if (existingResult.status === "fulfilled") setExisting(existingResult.value);
    }).finally(() => active && setLoading(false));
    return () => { active = false; };
  }, [allowed, id]);

  const selected = useMemo(() => Object.entries(quantities)
    .map(([inventoryItemId, text]) => ({ inventoryItemId, quantity: Number(text) }))
    .filter((line) => Number.isInteger(line.quantity) && line.quantity > 0), [quantities]);

  async function consume() {
    if (saving) return;
    if (selected.length === 0) return setError("حدد مادة واحدة على الأقل وأدخل كمية صحيحة أكبر من صفر.");
    for (const line of selected) {
      const item = items.find((entry) => entry.id === line.inventoryItemId);
      if (item && line.quantity > item.quantity) return setError(`الكمية المطلوبة من «${item.name}» أكبر من الرصيد المتاح.`);
    }
    setSaving(true);
    setError(null);
    setResult(null);
    try {
      const response = await apiRequest<LabInventoryConsumptionResult>("/api/inventory/consume-lab-order", {
        method: "POST",
        body: JSON.stringify({ labOrderId: id, items: selected, notes: notes.trim() || null })
      });
      setResult(response);
      setQuantities({});
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر صرف المواد");
    } finally {
      setSaving(false);
    }
  }

  if (!allowed) return <Screen><StateMessage title="غير مصرح" message="صرف المخزون مرتبط حاليًا بـ InventoryController المحمي للإدارة فقط." /></Screen>;
  if (loading) return <Screen><ActivityIndicator size="large" color={colors.primary} /></Screen>;

  return (
    <Screen>
      <View><Text style={styles.title}>صرف مواد للمعمل</Text><Text style={styles.subtitle}>{orderNumber}</Text></View>
      {error ? <StateMessage title="تعذر تنفيذ الصرف" message={error} /> : null}
      {result ? (
        <StateMessage title="تم صرف المواد" message={`${result.message} — تكلفة المواد الحالية ${result.materialCost.toLocaleString()} ${result.currency}`} />
      ) : null}

      {existing ? (
        <Card>
          <Text style={styles.cardTitle}>الاستهلاك المسجل سابقًا</Text>
          <Row label="عدد الحركات" value={String(existing.lines.length)} />
          <Row label="التكلفة الحالية" value={`${existing.materialCost.toLocaleString()} ${existing.currency}`} />
          <Row label="بنود بلا سعر" value={String(existing.unpricedLineCount)} last />
        </Card>
      ) : null}

      <SectionTitle>المواد المتاحة</SectionTitle>
      {items.length === 0 ? <StateMessage title="لا توجد مواد متاحة في المخزون" /> : null}
      {items.map((item) => {
        const active = quantities[item.id] !== undefined;
        return (
          <Card key={item.id}>
            <View style={styles.itemHeader}>
              <Pressable
                onPress={() => setQuantities((current) => {
                  const next = { ...current };
                  if (active) delete next[item.id]; else next[item.id] = "1";
                  return next;
                })}
                style={[styles.selectButton, active && styles.selectButtonActive]}
              >
                <Text style={[styles.selectText, active && styles.selectTextActive]}>{active ? "محدد" : "اختيار"}</Text>
              </Pressable>
              <View style={{ flex: 1 }}>
                <Text style={styles.itemName}>{item.name}</Text>
                <Text style={styles.meta}>متاح: {item.quantity} {inventoryUnit(item)}{item.costPerUnit != null ? ` • ${item.costPerUnit.toLocaleString()} YER/${inventoryUnit(item)}` : ""}</Text>
              </View>
            </View>
            {active ? (
              <View style={{ marginTop: spacing.sm }}>
                <FormField label="الكمية المصروفة" value={quantities[item.id] ?? "1"} onChangeText={(value) => setQuantities((current) => ({ ...current, [item.id]: value }))} keyboardType="number-pad" />
              </View>
            ) : null}
          </Card>
        );
      })}

      <FormField label="ملاحظة الصرف" value={notes} onChangeText={setNotes} multiline maxLength={300} />
      <PrimaryButton title="تأكيد صرف المواد" loading={saving} disabled={saving || selected.length === 0} onPress={() => void consume()} />
      <PrimaryButton title="العودة إلى طلب المعمل" onPress={() => router.back()} />
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
  cardTitle: { color: colors.text, fontWeight: "800", textAlign: "right", marginBottom: spacing.sm },
  itemHeader: { flexDirection: "row", alignItems: "center", gap: spacing.sm },
  itemName: { color: colors.text, fontWeight: "800", textAlign: "right" },
  meta: { color: colors.muted, marginTop: 4, textAlign: "right", fontSize: 12 },
  selectButton: { borderWidth: 1, borderColor: colors.border, borderRadius: radius.sm, paddingHorizontal: spacing.md, paddingVertical: spacing.sm },
  selectButtonActive: { borderColor: colors.primary, backgroundColor: colors.primarySoft },
  selectText: { color: colors.text, fontWeight: "700" },
  selectTextActive: { color: colors.primary },
  row: { minHeight: 44, flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.border },
  label: { color: colors.muted, textAlign: "right" },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "700" }
});
