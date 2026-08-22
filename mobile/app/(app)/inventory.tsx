import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import {
  canUseInventory,
  inventoryUnit,
  stockState,
  type ExpiringInventoryItem,
  type InventoryItem,
  type InventoryListResponse,
  type InventoryValuation
} from "@/lib/inventory";
import { colors, radius, spacing } from "@/theme";
import { router, useFocusEffect } from "expo-router";
import React, { useCallback, useMemo, useState } from "react";
import { ActivityIndicator, Pressable, RefreshControl, StyleSheet, Text, View } from "react-native";

type Filter = "all" | "low" | "expiring";

export default function InventoryScreen() {
  const { user } = useSession();
  const allowed = canUseInventory(user?.role);
  const [filter, setFilter] = useState<Filter>("all");
  const [items, setItems] = useState<InventoryItem[]>([]);
  const [expiring, setExpiring] = useState<ExpiringInventoryItem[]>([]);
  const [valuation, setValuation] = useState<InventoryValuation | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fallbackWarning, setFallbackWarning] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!allowed) {
      setLoading(false);
      return;
    }
    setError(null);
    setFallbackWarning(null);
    const [listResult, valuationResult, expiringResult] = await Promise.allSettled([
      apiRequest<InventoryListResponse>("/api/inventory?page=1&pageSize=100"),
      apiRequest<InventoryValuation>("/api/inventory/valuation"),
      apiRequest<ExpiringInventoryItem[]>("/api/inventory/expiring-soon?days=60")
    ]);

    if (listResult.status === "fulfilled") {
      setItems(listResult.value.data ?? []);
      if (listResult.value.readFallback) {
        setFallbackWarning(
          `الخادم استخدم قراءة توافقية للمخزون${listResult.value.fallbackReason ? ` (${listResult.value.fallbackReason})` : ""}. بعض الحقول الحديثة قد لا تظهر.`
        );
      }
    } else {
      setItems([]);
      setError(listResult.reason instanceof Error ? listResult.reason.message : "تعذر تحميل المخزون");
    }

    setValuation(valuationResult.status === "fulfilled" ? valuationResult.value : null);
    setExpiring(expiringResult.status === "fulfilled" ? expiringResult.value ?? [] : []);
    setLoading(false);
  }, [allowed]);

  useFocusEffect(
    useCallback(() => {
      setLoading(true);
      void load();
    }, [load])
  );

  async function refresh() {
    setRefreshing(true);
    try {
      await load();
    } finally {
      setRefreshing(false);
    }
  }

  const filtered = useMemo(() => {
    if (filter === "low") return items.filter((item) => stockState(item) === "low");
    return items;
  }, [filter, items]);

  if (!allowed) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="المخزون متاح حاليًا للإدارة فقط لأن InventoryController محمي بـ AdminOnly." />
      </Screen>
    );
  }

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      <View>
        <Text style={styles.title}>المخزون</Text>
        <Text style={styles.subtitle}>الرصيد، النواقص، الصلاحية وحركة المواد</Text>
      </View>

      {error ? <StateMessage title="تعذر تحميل بعض بيانات المخزون" message={error} /> : null}
      {fallbackWarning ? <StateMessage title="تنبيه توافق قاعدة البيانات" message={fallbackWarning} /> : null}

      {valuation ? (
        <View style={styles.grid}>
          <Metric label="المواد" value={valuation.totalItems.toLocaleString()} />
          <Metric label="إجمالي الكمية" value={valuation.totalQuantity.toLocaleString()} />
          <Metric label="مخزون منخفض" value={valuation.lowStockCount.toLocaleString()} danger={valuation.lowStockCount > 0} />
          <Metric label="قيمة المخزون" value={`${valuation.totalValue.toLocaleString()} YER`} wide />
        </View>
      ) : null}

      <PrimaryButton title="إضافة مادة جديدة" onPress={() => router.push("/(app)/inventory-item-editor")} />

      <View style={styles.filters}>
        {([
          ["all", "كل المواد"],
          ["low", "المخزون المنخفض"],
          ["expiring", "تنتهي خلال 60 يومًا"]
        ] as Array<[Filter, string]>).map(([key, label]) => (
          <Pressable key={key} onPress={() => setFilter(key)} style={[styles.filter, filter === key && styles.filterActive]}>
            <Text style={[styles.filterText, filter === key && styles.filterTextActive]}>{label}</Text>
          </Pressable>
        ))}
      </View>

      {loading ? <ActivityIndicator size="large" color={colors.primary} /> : null}

      {filter === "expiring" ? (
        <>
          <SectionTitle>قرب انتهاء الصلاحية</SectionTitle>
          {expiring.length === 0 && !loading ? <StateMessage title="لا توجد مواد منتهية أو قريبة الانتهاء خلال 60 يومًا" /> : null}
          {expiring.map((item) => (
            <Card key={item.id}>
              <Text style={styles.itemTitle}>{item.name}</Text>
              <Row label="الكمية" value={String(item.quantity)} />
              <Row label="الدفعة" value={item.batchNumber || "—"} />
              <Row label="الصلاحية" value={item.expiryDate} />
              <Row
                label="الحالة"
                value={item.isExpired ? `منتهية منذ ${Math.abs(item.daysUntilExpiry)} يوم` : `متبقي ${item.daysUntilExpiry} يوم`}
                danger={item.isExpired || item.daysUntilExpiry <= 14}
                last
              />
            </Card>
          ))}
        </>
      ) : (
        <>
          <SectionTitle>{filter === "low" ? "المخزون المنخفض" : "كل المواد"}</SectionTitle>
          {!loading && filtered.length === 0 ? <StateMessage title="لا توجد مواد في هذا العرض" /> : null}
          {filtered.map((item) => (
            <Pressable
              key={item.id}
              onPress={() => router.push({ pathname: "/(app)/inventory-item", params: { id: item.id } })}
            >
              <Card>
                <View style={styles.itemHeader}>
                  <Text style={[styles.stockBadge, stockState(item) === "low" && styles.stockLow]}>
                    {stockState(item) === "low" ? "منخفض" : "متوفر"}
                  </Text>
                  <View style={{ flex: 1 }}>
                    <Text style={styles.itemTitle}>{item.name}</Text>
                    <Text style={styles.meta}>{item.category || "بدون تصنيف"}</Text>
                  </View>
                </View>
                <Row label="الرصيد" value={`${item.quantity} ${inventoryUnit(item)}`} />
                <Row label="الحد الأدنى" value={String(item.minQuantity)} />
                {item.costPerUnit != null ? <Row label="تكلفة الوحدة" value={`${item.costPerUnit.toLocaleString()} YER`} /> : null}
                {item.warehouseLocation ? <Row label="الموقع" value={item.warehouseLocation} /> : null}
                {item.expiryDate ? <Row label="الصلاحية" value={item.expiryDate} last /> : null}
              </Card>
            </Pressable>
          ))}
        </>
      )}
    </Screen>
  );
}

function Metric({ label, value, danger = false, wide = false }: { label: string; value: string; danger?: boolean; wide?: boolean }) {
  return (
    <View style={[styles.metric, wide && styles.metricWide]}>
      <Text style={[styles.metricValue, danger && { color: colors.danger }]}>{value}</Text>
      <Text style={styles.meta}>{label}</Text>
    </View>
  );
}

function Row({ label, value, danger = false, last = false }: { label: string; value: string; danger?: boolean; last?: boolean }) {
  return (
    <View style={[styles.row, last && { borderBottomWidth: 0 }]}>
      <Text style={[styles.rowValue, danger && { color: colors.danger }]}>{value}</Text>
      <Text style={styles.rowLabel}>{label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 26, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.muted, marginTop: 4, textAlign: "right" },
  grid: { flexDirection: "row-reverse", flexWrap: "wrap", gap: spacing.sm },
  metric: { width: "48%", padding: spacing.md, backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border, borderRadius: radius.md },
  metricWide: { width: "100%" },
  metricValue: { color: colors.primary, fontSize: 20, fontWeight: "800", textAlign: "right" },
  filters: { flexDirection: "row-reverse", flexWrap: "wrap", gap: spacing.sm },
  filter: { borderWidth: 1, borderColor: colors.border, borderRadius: 999, paddingHorizontal: spacing.md, paddingVertical: spacing.sm, backgroundColor: colors.surface },
  filterActive: { borderColor: colors.primary, backgroundColor: colors.primarySoft },
  filterText: { color: colors.text, fontWeight: "700" },
  filterTextActive: { color: colors.primary },
  itemHeader: { flexDirection: "row", alignItems: "flex-start", gap: spacing.sm },
  itemTitle: { color: colors.text, fontSize: 17, fontWeight: "800", textAlign: "right" },
  meta: { color: colors.muted, marginTop: 3, textAlign: "right" },
  stockBadge: { color: colors.success, backgroundColor: colors.successSoft, borderRadius: 999, paddingHorizontal: spacing.sm, paddingVertical: 5, fontSize: 11, fontWeight: "800" },
  stockLow: { color: colors.danger, backgroundColor: colors.dangerSoft },
  row: { minHeight: 44, flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.border },
  rowLabel: { color: colors.muted, textAlign: "right" },
  rowValue: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "700" }
});
