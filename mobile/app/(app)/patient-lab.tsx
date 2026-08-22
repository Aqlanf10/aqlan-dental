import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import {
  formatMoney,
  LAB_PRIORITY_LABELS,
  LAB_STATUS_LABELS,
  labStatusLabel,
  type LabOrderListItem,
  type LabOrderListResponse
} from "@/lib/lab";
import { colors, radius, spacing } from "@/theme";
import { router, useFocusEffect, useLocalSearchParams } from "expo-router";
import React, { useCallback, useMemo, useState } from "react";
import { ActivityIndicator, Pressable, RefreshControl, StyleSheet, Text, View } from "react-native";

export default function PatientLabScreen() {
  const { can } = useSession();
  const params = useLocalSearchParams<{ patientId: string; patientName?: string; orthoCaseId?: string }>();
  const patientId = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const orthoCaseId = Array.isArray(params.orthoCaseId) ? params.orthoCaseId[0] : params.orthoCaseId;
  const canView = can("lab_orders.view");
  const canCreate = can("lab_orders.create");

  const [orders, setOrders] = useState<LabOrderListItem[]>([]);
  const [status, setStatus] = useState<string>("");
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!patientId || !canView) {
      setLoading(false);
      return;
    }
    setError(null);
    const query = new URLSearchParams({ patientId, page: "1", pageSize: "100" });
    if (status) query.set("status", status);
    try {
      const response = await apiRequest<LabOrderListResponse>(`/api/lab-orders?${query.toString()}`);
      setOrders(response.data ?? []);
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحميل طلبات المعمل");
    } finally {
      setLoading(false);
    }
  }, [canView, patientId, status]);

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

  const statusOptions = useMemo(
    () => ["", "draft", "sent", "manufacturing", "tryIn", "ready", "received", "delivered", "returned", "remake", "cancelled"],
    []
  );

  if (!canView) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="حسابك لا يملك صلاحية lab_orders.view." />
      </Screen>
    );
  }

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      <View>
        <Text style={styles.title}>طلبات المعمل</Text>
        <Text style={styles.subtitle}>{patientName || "ملف المريض"}</Text>
      </View>

      {canCreate ? (
        <PrimaryButton
          title="طلب معمل جديد"
          onPress={() =>
            router.push({
              pathname: "/(app)/lab-order-new",
              params: { patientId, patientName, ...(orthoCaseId ? { orthoCaseId } : {}) }
            })
          }
        />
      ) : null}

      <View style={styles.filters}>
        {statusOptions.map((value) => (
          <Pressable
            key={value || "all"}
            onPress={() => setStatus(value)}
            style={[styles.filter, status === value && styles.filterSelected]}
          >
            <Text style={[styles.filterText, status === value && styles.filterTextSelected]}>
              {value ? LAB_STATUS_LABELS[value] ?? value : "الكل"}
            </Text>
          </Pressable>
        ))}
      </View>

      {loading ? <ActivityIndicator size="large" color={colors.primary} /> : null}
      {error ? (
        <StateMessage
          title="تعذر تحميل طلبات المعمل"
          message={error}
          action={<PrimaryButton title="إعادة المحاولة" onPress={() => void load()} />}
        />
      ) : null}
      {!loading && !error && orders.length === 0 ? <StateMessage title="لا توجد طلبات معمل مطابقة" /> : null}

      {orders.map((order) => (
        <Pressable
          key={order.id}
          onPress={() =>
            router.push({
              pathname: "/(app)/lab-order-detail",
              params: {
                id: order.id,
                patientName: order.patientName,
                currency: order.currency ?? "",
                exchangeRateToYer: order.exchangeRateToYer != null ? String(order.exchangeRateToYer) : ""
              }
            })
          }
        >
          <Card>
            <View style={styles.header}>
              <Text style={styles.status}>{labStatusLabel(order.status)}</Text>
              <View style={styles.headerText}>
                <Text style={styles.orderNumber}>{order.orderNumber || "طلب معمل"}</Text>
                <Text style={styles.appliance}>{order.applianceType || order.restorationType || "عمل معمل"}</Text>
              </View>
            </View>
            <Row label="المعمل" value={order.labEntityName || order.labName || "غير محدد"} />
            {order.shade ? <Row label="اللون" value={order.shade} /> : null}
            <Row label="الأولوية" value={LAB_PRIORITY_LABELS[order.priority] ?? order.priority} />
            <Row label="تاريخ الإرسال" value={order.sentDate || "مسودة"} />
            <Row label="الاستلام المتوقع" value={order.expectedDate || "—"} />
            {order.totalCost != null || order.cost != null ? (
              <Row label="التكلفة" value={formatMoney(order.totalCost ?? order.cost, order.currency)} />
            ) : null}
            <Row label="الطبيب" value={order.doctorName || "—"} last />
          </Card>
        </Pressable>
      ))}
    </Screen>
  );
}

function Row({ label, value, last = false }: { label: string; value: string; last?: boolean }) {
  return (
    <View style={[styles.row, last && { borderBottomWidth: 0 }]}>
      <Text style={styles.value}>{value}</Text>
      <Text style={styles.label}>{label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  filters: { flexDirection: "row-reverse", flexWrap: "wrap", gap: spacing.xs },
  filter: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 999,
    paddingHorizontal: spacing.sm,
    paddingVertical: 7,
    backgroundColor: colors.surface
  },
  filterSelected: { borderColor: colors.primary, backgroundColor: colors.primarySoft },
  filterText: { color: colors.muted, fontWeight: "700", fontSize: 12 },
  filterTextSelected: { color: colors.primary },
  header: { flexDirection: "row", alignItems: "flex-start", justifyContent: "space-between", gap: spacing.sm },
  headerText: { flex: 1 },
  orderNumber: { color: colors.text, fontWeight: "800", fontSize: 17, textAlign: "right" },
  appliance: { color: colors.muted, textAlign: "right", marginTop: 4 },
  status: {
    color: colors.primary,
    backgroundColor: colors.primarySoft,
    borderRadius: radius.sm,
    paddingHorizontal: spacing.sm,
    paddingVertical: 5,
    fontWeight: "800",
    fontSize: 12
  },
  row: {
    minHeight: 42,
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    gap: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border
  },
  label: { color: colors.muted, textAlign: "right" },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" }
});
