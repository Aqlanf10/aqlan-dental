import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import {
  INVOICE_STATUS_LABELS,
  PAYMENT_METHOD_LABELS,
  formatRecordedMoney,
  type AccountStatement,
  type FinanceInvoice,
  type FinancePayment
} from "@/lib/finance";
import { canFinanceJourney } from "@/lib/journey";
import { colors, radius, spacing } from "@/theme";
import { router, useFocusEffect, useLocalSearchParams } from "expo-router";
import React, { useCallback, useMemo, useState } from "react";
import { ActivityIndicator, RefreshControl, StyleSheet, Text, View } from "react-native";

type LoadState<T> = {
  data: T | null;
  error: string | null;
};

export default function PatientFinanceScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ patientId: string; patientName?: string }>();
  const patientId = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;

  const [statement, setStatement] = useState<LoadState<AccountStatement>>({ data: null, error: null });
  const [payments, setPayments] = useState<LoadState<FinancePayment[]>>({ data: null, error: null });
  const [invoices, setInvoices] = useState<LoadState<FinanceInvoice[]>>({ data: null, error: null });
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

  const canAccess = canFinanceJourney(user);

  const load = useCallback(async () => {
    if (!patientId || !canAccess) {
      setLoading(false);
      return;
    }

    const [statementResult, paymentsResult, invoicesResult] = await Promise.allSettled([
      apiRequest<AccountStatement>(`/api/patients/${patientId}/account-statement`),
      apiRequest<FinancePayment[]>(`/api/patients/${patientId}/payments`),
      apiRequest<FinanceInvoice[]>(`/api/patients/${patientId}/invoices`)
    ]);

    setStatement(
      statementResult.status === "fulfilled"
        ? { data: statementResult.value, error: null }
        : {
            data: null,
            error:
              statementResult.reason instanceof Error
                ? statementResult.reason.message
                : "تعذر تحميل كشف الحساب"
          }
    );
    setPayments(
      paymentsResult.status === "fulfilled"
        ? { data: paymentsResult.value, error: null }
        : {
            data: null,
            error:
              paymentsResult.reason instanceof Error
                ? paymentsResult.reason.message
                : "تعذر تحميل المدفوعات"
          }
    );
    setInvoices(
      invoicesResult.status === "fulfilled"
        ? { data: invoicesResult.value, error: null }
        : {
            data: null,
            error:
              invoicesResult.reason instanceof Error
                ? invoicesResult.reason.message
                : "تعذر تحميل الفواتير"
          }
    );
    setLoading(false);
  }, [canAccess, patientId]);

  useFocusEffect(
    useCallback(() => {
      setLoading(true);
      void load();
    }, [load])
  );

  const paymentList = payments.data ?? [];
  const totalsByCurrency = useMemo(() => {
    const totals = new Map<string, number>();
    for (const payment of paymentList) {
      if (payment.isActive === false) continue;
      const currency = payment.currency || "YER";
      totals.set(currency, (totals.get(currency) ?? 0) + payment.amount);
    }
    return [...totals.entries()].sort(([a], [b]) => a.localeCompare(b));
  }, [paymentList]);

  async function refresh() {
    setRefreshing(true);
    try {
      await load();
    } finally {
      setRefreshing(false);
    }
  }

  if (!canAccess) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="وحدة المالية متاحة للحسابات المالية والاستقبال والإدارة فقط." />
      </Screen>
    );
  }

  if (loading && !statement.data && !payments.data && !invoices.data) {
    return (
      <Screen>
        <ActivityIndicator size="large" color={colors.primary} />
      </Screen>
    );
  }

  return (
    <Screen
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}
    >
      <View>
        <Text style={styles.title}>مالية المريض</Text>
        <Text style={styles.subtitle}>{patientName || statement.data?.patientName || "المريض"}</Text>
      </View>

      <PrimaryButton
        title="إضافة دفعة"
        onPress={() =>
          router.push({
            pathname: "/(app)/payment-new",
            params: { patientId, patientName: patientName || statement.data?.patientName || "" }
          })
        }
      />

      <SectionTitle>كشف الحساب</SectionTitle>
      {statement.error ? (
        <StateMessage
          title="كشف الحساب غير متاح"
          message={`${statement.error}\nلم يتم افتراض أن الرصيد صفر.`}
        />
      ) : statement.data ? (
        <>
          <Text style={styles.currencyCaveat}>
            هذا endpoint لا يعيد عملة إجماليات كشف الحساب؛ لذلك تُعرض القيم التالية بدون رمز عملة ولا يتم تحويلها أو جمعها مع عملات أخرى داخل التطبيق.
          </Text>
          <View style={styles.metrics}>
            <Metric label="إجمالي العقود" value={formatRecordedMoney(statement.data.totalContracted)} />
            <Metric label="إجمالي المدفوع" value={formatRecordedMoney(statement.data.totalPaid)} />
            <Metric label="المتبقي" value={formatRecordedMoney(statement.data.totalRemaining)} warning={statement.data.totalRemaining > 0} />
            <Metric label="الخصومات" value={formatRecordedMoney(statement.data.totalDiscounts)} />
          </View>
        </>
      ) : null}

      {statement.data ? (
        <>
          <SectionTitle>العقود</SectionTitle>
          {statement.data.contracts.length === 0 ? (
            <StateMessage title="لا توجد عقود مسجلة" />
          ) : (
            statement.data.contracts.map((contract) => (
              <Card key={contract.id}>
                <Row label="التخصص" value={contract.specialty || "عقد علاج"} />
                <Row label="الحالة" value={contract.status} />
                <Row label="الإجمالي" value={formatRecordedMoney(contract.totalAmount)} />
                <Row label="المدفوع" value={formatRecordedMoney(contract.paidAmount)} />
                <Row label="المتبقي" value={formatRecordedMoney(contract.remainingAmount)} last />
              </Card>
            ))
          )}
        </>
      ) : null}

      <SectionTitle>المدفوعات</SectionTitle>
      {payments.error ? (
        <StateMessage
          title="تعذر تحميل المدفوعات"
          message={`${payments.error}\nلن أعرض إجماليًا غير مؤكد.`}
        />
      ) : payments.data ? (
        <>
          {totalsByCurrency.length > 0 ? (
            <View style={styles.currencyTotals}>
              {totalsByCurrency.map(([currency, total]) => (
                <Card key={currency} style={styles.totalCard}>
                  <Text style={styles.totalLabel}>إجمالي المدفوعات — {currency}</Text>
                  <Text style={styles.totalValue}>{formatRecordedMoney(total, currency)}</Text>
                </Card>
              ))}
            </View>
          ) : null}
          {paymentList.length === 0 ? (
            <StateMessage title="لا توجد مدفوعات مسجلة" />
          ) : (
            paymentList.map((payment) => (
              <Card key={payment.id}>
                <View style={styles.paymentHeader}>
                  <Text style={styles.paymentAmount}>
                    {formatRecordedMoney(payment.amount, payment.currency || "YER")}
                  </Text>
                  <Text style={styles.paymentDate}>{payment.paymentDate}</Text>
                </View>
                {payment.accountCurrency ? (
                  <Row
                    label="المطبق على الحساب"
                    value={formatRecordedMoney(payment.appliedAmount ?? payment.amount, payment.accountCurrency)}
                  />
                ) : null}
                <Row
                  label="طريقة الدفع"
                  value={PAYMENT_METHOD_LABELS[payment.paymentMethod ?? ""] ?? payment.paymentMethod ?? "—"}
                />
                {payment.receiptNumber ? <Row label="رقم السند" value={payment.receiptNumber} /> : null}
                {payment.serviceDescription ? <Row label="الوصف" value={payment.serviceDescription} /> : null}
                {payment.doctorName ? <Row label="الطبيب" value={payment.doctorName} last /> : null}
              </Card>
            ))
          )}
        </>
      ) : null}

      <SectionTitle>الفواتير</SectionTitle>
      {invoices.error ? (
        <StateMessage title="تعذر تحميل الفواتير" message={invoices.error} />
      ) : invoices.data ? (
        invoices.data.length === 0 ? (
          <StateMessage title="لا توجد فواتير مسجلة" />
        ) : (
          <>
            <Text style={styles.currencyCaveat}>
              قائمة الفواتير الحالية لا تعيد حقل العملة في هذا العقد، لذلك تُعرض مبالغها بدون اختلاق رمز عملة.
            </Text>
            {invoices.data.map((invoice) => (
              <Card key={invoice.id}>
                <View style={styles.paymentHeader}>
                  <Text style={styles.invoiceNumber}>{invoice.invoiceNumber}</Text>
                  <Text style={styles.status}>
                    {INVOICE_STATUS_LABELS[invoice.status] ?? invoice.statusArabic ?? invoice.status}
                  </Text>
                </View>
                <Row label="الإجمالي" value={formatRecordedMoney(invoice.totalAmount)} />
                {invoice.paidAmount != null ? (
                  <Row label="المدفوع" value={formatRecordedMoney(invoice.paidAmount)} />
                ) : null}
                {invoice.balance != null ? (
                  <Row label="المتبقي" value={formatRecordedMoney(invoice.balance)} last />
                ) : null}
              </Card>
            ))}
          </>
        )
      ) : null}
    </Screen>
  );
}

function Metric({ label, value, warning = false }: { label: string; value: string; warning?: boolean }) {
  return (
    <View style={styles.metric}>
      <Text style={[styles.metricValue, warning && styles.warning]}>{value}</Text>
      <Text style={styles.metricLabel}>{label}</Text>
    </View>
  );
}

function Row({ label, value, last = false }: { label: string; value: string; last?: boolean }) {
  return (
    <View style={[styles.row, last && styles.last]}>
      <Text style={styles.value}>{value}</Text>
      <Text style={styles.label}>{label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  currencyCaveat: {
    color: colors.warning,
    backgroundColor: colors.warningSoft,
    borderRadius: radius.sm,
    padding: spacing.sm,
    textAlign: "right",
    lineHeight: 21
  },
  metrics: { flexDirection: "row-reverse", flexWrap: "wrap", gap: spacing.sm },
  metric: {
    width: "48%",
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    padding: spacing.md
  },
  metricValue: { color: colors.primary, fontSize: 18, fontWeight: "800", textAlign: "right" },
  metricLabel: { color: colors.muted, marginTop: 4, textAlign: "right" },
  warning: { color: colors.danger },
  row: {
    minHeight: 44,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    gap: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border
  },
  last: { borderBottomWidth: 0 },
  label: { color: colors.muted, textAlign: "right" },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" },
  currencyTotals: { gap: spacing.sm },
  totalCard: { backgroundColor: colors.successSoft },
  totalLabel: { color: colors.success, textAlign: "right" },
  totalValue: { color: colors.success, fontSize: 22, fontWeight: "800", textAlign: "right", marginTop: 4 },
  paymentHeader: { flexDirection: "row", justifyContent: "space-between", gap: spacing.sm, marginBottom: spacing.sm },
  paymentAmount: { color: colors.success, fontSize: 18, fontWeight: "800" },
  paymentDate: { color: colors.muted },
  invoiceNumber: { color: colors.text, fontWeight: "800" },
  status: { color: colors.primary, fontWeight: "700" }
});
