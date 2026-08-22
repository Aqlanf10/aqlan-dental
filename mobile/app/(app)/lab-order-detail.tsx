import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { isoDateLocal } from "@/lib/format";
import {
  formatMoney,
  LAB_PRIORITY_LABELS,
  labStatusLabel,
  labStatusTransitions,
  type LabOrderDetail,
  type LabOrderHistory,
  type LabOrderListResponse
} from "@/lib/lab";
import { colors, radius, spacing } from "@/theme";
import { router, useFocusEffect, useLocalSearchParams } from "expo-router";
import React, { useCallback, useMemo, useState } from "react";
import {
  ActivityIndicator,
  Linking,
  Pressable,
  RefreshControl,
  StyleSheet,
  Text,
  View
} from "react-native";

type HistoryResponse = { data: LabOrderHistory[] };
type CurrencyInfo = { currency?: string | null; exchangeRateToYer?: number | null };
type WhatsAppPayload = { phone: string; labName?: string | null; message: string };

export default function LabOrderDetailScreen() {
  const { can } = useSession();
  const params = useLocalSearchParams<{
    id: string;
    patientName?: string;
    currency?: string;
    exchangeRateToYer?: string;
  }>();
  const id = first(params.id);
  const fallbackPatientName = first(params.patientName);
  const routedCurrency = first(params.currency) || null;
  const routedRateText = first(params.exchangeRateToYer);
  const routedRate = routedRateText ? Number(routedRateText) : null;
  const canView = can("lab_orders.view");
  const canEdit = can("lab_orders.edit");

  const [order, setOrder] = useState<LabOrderDetail | null>(null);
  const [history, setHistory] = useState<LabOrderHistory[]>([]);
  const [currencyInfo, setCurrencyInfo] = useState<CurrencyInfo>({
    currency: routedCurrency,
    exchangeRateToYer: Number.isFinite(routedRate) ? routedRate : null
  });
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busyAction, setBusyAction] = useState<string | null>(null);

  const recoverCurrency = useCallback(async (detail: LabOrderDetail) => {
    try {
      const response = await apiRequest<LabOrderListResponse>(
        `/api/lab-orders?patientId=${detail.patientId}&page=1&pageSize=100`
      );
      const row = response.data.find((item) => item.id === detail.id);
      if (row) {
        setCurrencyInfo({
          currency: row.currency ?? routedCurrency,
          exchangeRateToYer: row.exchangeRateToYer ?? (Number.isFinite(routedRate) ? routedRate : null)
        });
      }
    } catch {
      // Detail DTO currently omits Currency/ExchangeRateToYer. Failure to recover those
      // fields must never block the clinical order view or make us invent a currency.
    }
  }, [routedCurrency, routedRate]);

  const load = useCallback(async () => {
    if (!id || !canView) {
      setLoading(false);
      return;
    }
    setError(null);
    const [detailResult, historyResult] = await Promise.allSettled([
      apiRequest<LabOrderDetail>(`/api/lab-orders/${id}`),
      apiRequest<HistoryResponse>(`/api/lab-orders/${id}/history`)
    ]);

    if (detailResult.status === "fulfilled") {
      setOrder(detailResult.value);
      void recoverCurrency(detailResult.value);
    } else {
      setOrder(null);
      setError(detailResult.reason instanceof Error ? detailResult.reason.message : "تعذر تحميل طلب المعمل");
    }

    if (historyResult.status === "fulfilled") {
      setHistory(historyResult.value.data ?? []);
    } else {
      setHistory([]);
    }
    setLoading(false);
  }, [canView, id, recoverCurrency]);

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

  async function directTransition(nextStatus: string) {
    if (!order || busyAction || !canEdit) return;
    setBusyAction(nextStatus);
    setError(null);
    try {
      if (nextStatus === "received") {
        await apiRequest(`/api/lab-orders/${order.id}/mark-received`, {
          method: "POST",
          body: JSON.stringify({ receivedDate: isoDateLocal(new Date()) })
        });
      } else if (nextStatus === "delivered") {
        await apiRequest(`/api/lab-orders/${order.id}/mark-delivered`, { method: "POST" });
      } else {
        await apiRequest(`/api/lab-orders/${order.id}/status`, {
          method: "PUT",
          body: JSON.stringify({ status: nextStatus })
        });
      }
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحديث حالة طلب المعمل");
    } finally {
      setBusyAction(null);
    }
  }

  function reasonedTransition(transition: "cancelled" | "returned" | "remake") {
    if (!order) return;
    router.push({
      pathname: "/(app)/lab-order-transition",
      params: {
        id: order.id,
        transition,
        orderNumber: order.orderNumber ?? "",
        currency: currencyInfo.currency ?? ""
      }
    });
  }

  async function openWhatsApp() {
    if (!order || busyAction) return;
    setBusyAction("whatsapp");
    setError(null);
    try {
      const payload = await apiRequest<WhatsAppPayload>(`/api/lab-orders/${order.id}/whatsapp-message`);
      const phone = formatPhoneForWhatsApp(payload.phone);
      if (!phone) {
        throw new Error("رقم المعمل غير صالح — راجعه من إعدادات المعامل.");
      }
      const url = `https://wa.me/${phone}?text=${encodeURIComponent(payload.message)}`;
      if (!(await Linking.canOpenURL(url))) {
        throw new Error("تعذر فتح واتساب على هذا الجهاز.");
      }
      await Linking.openURL(url);
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تجهيز رسالة واتساب للمعمل");
    } finally {
      setBusyAction(null);
    }
  }

  const transitions = useMemo(() => labStatusTransitions(order?.status), [order?.status]);

  if (!canView) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="حسابك لا يملك صلاحية lab_orders.view." />
      </Screen>
    );
  }

  if (loading && !order) {
    return (
      <Screen>
        <ActivityIndicator size="large" color={colors.primary} />
      </Screen>
    );
  }

  if (!order) {
    return (
      <Screen>
        <StateMessage
          title="تعذر فتح طلب المعمل"
          message={error ?? "الطلب غير موجود"}
          action={<PrimaryButton title="إعادة المحاولة" onPress={() => void load()} />}
        />
      </Screen>
    );
  }

  const amount = order.totalCost ?? order.cost;
  const currency = currencyInfo.currency;

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      <View>
        <Text style={styles.title}>{order.orderNumber || "طلب معمل"}</Text>
        <Text style={styles.subtitle}>{order.patientName || fallbackPatientName || "ملف المريض"}</Text>
      </View>

      {error ? <StateMessage title="تنبيه" message={error} /> : null}

      <Card>
        <View style={styles.header}>
          <Text style={styles.status}>{labStatusLabel(order.status)}</Text>
          <View style={styles.headerText}>
            <Text style={styles.itemTitle}>{order.applianceType || order.restorationType || "عمل معمل"}</Text>
            <Text style={styles.meta}>{order.orthoCaseNumber ? `حالة تقويم ${order.orthoCaseNumber}` : ""}</Text>
          </View>
        </View>
        <Row label="المعمل" value={order.labEntityName || order.labName || "غير محدد"} />
        <Row label="الطبيب" value={order.doctorName || "—"} />
        <Row label="الأولوية" value={LAB_PRIORITY_LABELS[order.priority] ?? order.priority} />
        {order.shade ? <Row label="Shade" value={order.shade} /> : null}
        {order.restorationType ? <Row label="الترميم" value={order.restorationType} /> : null}
        <Row label="أُرسل" value={order.sentDate || "لم يُرسل"} />
        <Row label="متوقع" value={order.expectedDate || "—"} />
        <Row label="استُلم" value={order.receivedDate || "—"} />
        <Row label="سُلّم للمريض" value={order.deliveredDate || "—"} />
        <Row label="زيارة مرتبطة" value={order.visitId ? "نعم" : "لا"} />
        {amount != null ? (
          <Row
            label="التكلفة"
            value={currency ? formatMoney(amount, currency) : `${amount.toLocaleString()} (العملة غير متاحة في Detail API)`}
            last={!order.instructions}
          />
        ) : null}
        {order.instructions ? <Text style={styles.notes}>{order.instructions}</Text> : null}
        {order.cancellationReason ? <Text style={styles.warning}>سبب الإلغاء: {order.cancellationReason}</Text> : null}
      </Card>

      {currency && currency !== "YER" && currencyInfo.exchangeRateToYer ? (
        <Card>
          <Text style={styles.moneyNote}>
            سعر الصرف المسجل: 1 {currency} = {currencyInfo.exchangeRateToYer.toLocaleString()} YER
          </Text>
        </Card>
      ) : null}

      <PrimaryButton
        title="إرسال تفاصيل الطلب للمعمل عبر واتساب"
        loading={busyAction === "whatsapp"}
        disabled={busyAction !== null}
        onPress={() => void openWhatsApp()}
      />

      {canEdit && transitions.length > 0 ? (
        <>
          <SectionTitle>تحديث حالة الطلب</SectionTitle>
          <View style={styles.actions}>
            {transitions.map((next) => {
              const reasoned = next === "cancelled" || next === "returned" || next === "remake";
              const blockedDelivery = next === "delivered" && !order.visitId;
              return (
                <Pressable
                  key={next}
                  disabled={busyAction !== null || blockedDelivery}
                  onPress={() => {
                    if (reasoned) reasonedTransition(next as "cancelled" | "returned" | "remake");
                    else void directTransition(next);
                  }}
                  style={[
                    styles.action,
                    next === "cancelled" && styles.dangerAction,
                    (busyAction !== null || blockedDelivery) && styles.disabled
                  ]}
                >
                  {busyAction === next ? (
                    <ActivityIndicator color={next === "cancelled" ? colors.danger : colors.primary} />
                  ) : (
                    <Text style={[styles.actionText, next === "cancelled" && styles.dangerText]}>
                      {actionLabel(next)}
                    </Text>
                  )}
                </Pressable>
              );
            })}
          </View>
          {transitions.includes("delivered") && !order.visitId ? (
            <StateMessage
              title="التسليم للمريض متوقف"
              message="الخادم يشترط زيارة مرتبطة قبل تسليم طلب المعمل. هذا الطلب لا يحمل VisitId، ولا يوجد endpoint حالي لربط زيارة بعد الإنشاء؛ لذلك تم تعطيل زر التسليم بدل تجاوز السجل السريري."
            />
          ) : null}
        </>
      ) : null}

      <SectionTitle>بنود الطلب</SectionTitle>
      {order.items?.length ? (
        order.items.map((item) => (
          <Card key={item.id}>
            <Text style={styles.itemTitle}>{item.workTypeName || "بند معمل"}</Text>
            {item.toothNumber ? <Row label="السن" value={item.toothNumber} /> : null}
            {item.arch ? <Row label="القوس" value={item.arch} /> : null}
            {item.shade ? <Row label="Shade" value={item.shade} /> : null}
            {item.restorationType ? <Row label="الترميم" value={item.restorationType} /> : null}
            <Row label="الوحدات" value={String(item.unitsCount)} />
            {item.totalPrice != null ? (
              <Row
                label="إجمالي البند"
                value={currency ? formatMoney(item.totalPrice, currency) : item.totalPrice.toLocaleString()}
                last={!item.instructions}
              />
            ) : null}
            {item.instructions ? <Text style={styles.notes}>{item.instructions}</Text> : null}
          </Card>
        ))
      ) : (
        <StateMessage title="لا توجد بنود تفصيلية — نوع العمل الرئيسي هو وصف الطلب الحالي" />
      )}

      <SectionTitle>سجل الحالات</SectionTitle>
      {history.length ? (
        history.map((entry) => (
          <Card key={entry.id}>
            <Text style={styles.historyTitle}>
              {labStatusLabel(entry.fromStatus)} ← {labStatusLabel(entry.toStatus)}
            </Text>
            <Text style={styles.meta}>{entry.createdAt}{entry.changedByName ? ` • ${entry.changedByName}` : ""}</Text>
            {entry.reason ? <Text style={styles.notes}>{entry.reason}</Text> : null}
          </Card>
        ))
      ) : (
        <StateMessage title="لا يوجد سجل انتقالات بعد" />
      )}
    </Screen>
  );
}

function actionLabel(status: string): string {
  switch (status) {
    case "sent": return "إرسال للمعمل";
    case "manufacturing": return "بدء التصنيع";
    case "tryIn": return "تحويل للتجربة";
    case "ready": return "جاهز للاستلام";
    case "received": return "تأكيد الاستلام من المعمل";
    case "delivered": return "تسليم للمريض";
    case "returned": return "إرجاع للمعمل";
    case "remake": return "إعادة صناعة";
    case "cancelled": return "إلغاء الطلب";
    default: return labStatusLabel(status);
  }
}

function formatPhoneForWhatsApp(phone: string | null | undefined): string {
  if (!phone) return "";
  let value = phone
    .replace(/[٠-٩۰-۹]/g, (char) => ARABIC_DIGITS[char] ?? char)
    .replace(/[\s\-.()]/g, "");
  if (value.startsWith("+")) value = value.slice(1);
  if (value.startsWith("00")) value = value.slice(2);
  if (value.startsWith("0") && value.length >= 9) value = `967${value.slice(1)}`;
  else if (value.startsWith("7") && value.length === 9) value = `967${value}`;
  return /^\d+$/.test(value) ? value : "";
}

const ARABIC_DIGITS: Record<string, string> = {
  "٠": "0", "۰": "0", "١": "1", "۱": "1", "٢": "2", "۲": "2",
  "٣": "3", "۳": "3", "٤": "4", "۴": "4", "٥": "5", "۵": "5",
  "٦": "6", "۶": "6", "٧": "7", "۷": "7", "٨": "8", "۸": "8",
  "٩": "9", "۹": "9"
};

function first(value: string | string[] | undefined): string {
  return Array.isArray(value) ? value[0] ?? "" : value ?? "";
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
  header: { flexDirection: "row", alignItems: "flex-start", justifyContent: "space-between", gap: spacing.sm },
  headerText: { flex: 1 },
  status: {
    color: colors.primary,
    backgroundColor: colors.primarySoft,
    paddingHorizontal: spacing.sm,
    paddingVertical: 5,
    borderRadius: 999,
    fontWeight: "800",
    fontSize: 12
  },
  itemTitle: { color: colors.text, fontSize: 17, fontWeight: "800", textAlign: "right" },
  meta: { color: colors.muted, marginTop: 4, fontSize: 12, textAlign: "right" },
  row: {
    minHeight: 42,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    gap: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border
  },
  label: { color: colors.muted, textAlign: "right" },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" },
  notes: {
    color: colors.text,
    textAlign: "right",
    lineHeight: 22,
    backgroundColor: colors.background,
    borderRadius: radius.sm,
    padding: spacing.sm,
    marginTop: spacing.sm
  },
  warning: { color: colors.warning, textAlign: "right", marginTop: spacing.sm, lineHeight: 22 },
  moneyNote: { color: colors.primary, textAlign: "right", fontWeight: "700" },
  actions: { flexDirection: "row-reverse", flexWrap: "wrap", gap: spacing.sm },
  action: {
    minWidth: 145,
    minHeight: 44,
    alignItems: "center",
    justifyContent: "center",
    borderWidth: 1,
    borderColor: colors.primary,
    borderRadius: radius.sm,
    backgroundColor: colors.primarySoft,
    paddingHorizontal: spacing.md
  },
  dangerAction: { borderColor: colors.danger, backgroundColor: colors.dangerSoft },
  actionText: { color: colors.primary, fontWeight: "800", textAlign: "center" },
  dangerText: { color: colors.danger },
  disabled: { opacity: 0.45 },
  historyTitle: { color: colors.text, fontWeight: "800", textAlign: "right" }
});
