import { ChoiceRow, FormField, SelectList } from "@/components/forms";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import {
  PAYMENT_METHOD_OPTIONS,
  type ActiveCashierSession,
  type CreatePaymentInput,
  type FinanceContract,
  type FinanceInvoice,
  type FinancePayment
} from "@/lib/finance";
import { colors, radius, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useCallback, useEffect, useMemo, useState } from "react";
import { ActivityIndicator, StyleSheet, Text, View } from "react-native";

const CURRENCIES = [
  { value: "YER", label: "ريال يمني YER" },
  { value: "SAR", label: "ريال سعودي SAR" },
  { value: "USD", label: "دولار أمريكي USD" }
];

export default function NewPaymentScreen() {
  const params = useLocalSearchParams<{ patientId: string; patientName?: string; invoiceId?: string }>();
  const patientId = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const initialInvoiceId = Array.isArray(params.invoiceId) ? params.invoiceId[0] : params.invoiceId;

  const [cashier, setCashier] = useState<ActiveCashierSession | null>(null);
  const [cashierError, setCashierError] = useState<string | null>(null);
  const [contracts, setContracts] = useState<FinanceContract[]>([]);
  const [invoices, setInvoices] = useState<FinanceInvoice[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [amount, setAmount] = useState("");
  const [currency, setCurrency] = useState("YER");
  const [accountCurrency, setAccountCurrency] = useState("YER");
  const [exchangeRateToAccountCurrency, setExchangeRateToAccountCurrency] = useState("");
  const [exchangeRateToYer, setExchangeRateToYer] = useState("");
  const [paymentMethod, setPaymentMethod] = useState<string | null>("cash");
  const [contractId, setContractId] = useState<string | null>(null);
  const [invoiceId, setInvoiceId] = useState<string | null>(initialInvoiceId ?? null);
  const [serviceDescription, setServiceDescription] = useState("");
  const [notes, setNotes] = useState("");

  const load = useCallback(async () => {
    if (!patientId) return;
    setLoading(true);
    setError(null);
    setCashierError(null);

    const [cashierResult, contractsResult, invoicesResult] = await Promise.allSettled([
      apiRequest<ActiveCashierSession & { hasActiveSession?: boolean }>("/api/cashier-sessions/active"),
      apiRequest<FinanceContract[]>(`/api/contracts?patientId=${patientId}&status=active`),
      apiRequest<FinanceInvoice[]>(`/api/patients/${patientId}/invoices`)
    ]);

    if (cashierResult.status === "fulfilled" && cashierResult.value?.hasActiveSession !== false && cashierResult.value.id) {
      setCashier(cashierResult.value);
    } else {
      setCashier(null);
      setCashierError(
        cashierResult.status === "rejected" && cashierResult.reason instanceof Error
          ? cashierResult.reason.message
          : "لا توجد وردية كاشير مفتوحة لهذا الحساب."
      );
    }

    setContracts(contractsResult.status === "fulfilled" ? contractsResult.value : []);
    setInvoices(
      invoicesResult.status === "fulfilled"
        ? invoicesResult.value.filter((invoice) => invoice.status !== "Cancelled" && invoice.status !== "Paid")
        : []
    );
    setLoading(false);
  }, [patientId]);

  useEffect(() => {
    void load();
  }, [load]);

  const contractOptions = useMemo(
    () =>
      contracts.map((contract) => ({
        value: contract.id,
        label: contract.specialty || "عقد علاج",
        subtitle: `المتبقي المسجل: ${contract.remainingAmount.toLocaleString()}`
      })),
    [contracts]
  );

  const invoiceOptions = useMemo(
    () =>
      invoices.map((invoice) => ({
        value: invoice.id,
        label: invoice.invoiceNumber,
        subtitle: `المتبقي المسجل: ${(invoice.balance ?? invoice.totalAmount).toLocaleString()}`
      })),
    [invoices]
  );

  function validate(): CreatePaymentInput | null {
    if (!cashier) {
      setError("يجب فتح وردية الكاشير قبل تسجيل أي دفعة.");
      return null;
    }

    const parsedAmount = Number(amount.trim());
    if (!Number.isFinite(parsedAmount) || parsedAmount <= 0) {
      setError("أدخل مبلغًا أكبر من صفر.");
      return null;
    }

    let accountRate: number | undefined;
    if (currency !== accountCurrency) {
      accountRate = Number(exchangeRateToAccountCurrency.trim());
      if (!Number.isFinite(accountRate) || accountRate <= 0) {
        setError(`أدخل سعر التحويل: 1 ${currency} = كم ${accountCurrency}.`);
        return null;
      }
    }

    let rateToYer: number | undefined;
    if (currency !== "YER") {
      rateToYer = Number(exchangeRateToYer.trim());
      if (!Number.isFinite(rateToYer) || rateToYer <= 0) {
        setError(`أدخل سعر الصرف الفعلي: 1 ${currency} = كم YER.`);
        return null;
      }
    }

    return {
      patientId,
      contractId: contractId || undefined,
      invoiceId: invoiceId || undefined,
      amount: parsedAmount,
      currency,
      accountCurrency,
      exchangeRateToAccountCurrency: accountRate,
      exchangeRateToYer: currency === "YER" ? 1 : rateToYer,
      exchangeRateSource: currency !== accountCurrency || currency !== "YER" ? "manual-mobile" : undefined,
      paymentMethod: paymentMethod || "cash",
      serviceDescription: serviceDescription.trim() || undefined,
      notes: notes.trim() || undefined
    };
  }

  async function submit() {
    if (saving) return;
    const payload = validate();
    if (!payload) return;

    setSaving(true);
    setError(null);
    try {
      await apiRequest<FinancePayment>("/api/payments", {
        method: "POST",
        body: JSON.stringify(payload)
      });
      router.back();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تسجيل الدفعة");
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <Screen>
        <ActivityIndicator size="large" color={colors.primary} />
      </Screen>
    );
  }

  return (
    <Screen>
      <View>
        <Text style={styles.title}>إضافة دفعة</Text>
        <Text style={styles.subtitle}>{patientName || "المريض"}</Text>
      </View>

      {cashier ? (
        <View style={styles.cashierOk}>
          <Text style={styles.cashierTitle}>وردية الكاشير مفتوحة</Text>
          <Text style={styles.cashierText}>{cashier.sessionNumber} • {cashier.cashierName}</Text>
        </View>
      ) : (
        <StateMessage
          title="لا يمكن تسجيل دفعة الآن"
          message={`${cashierError ?? "لا توجد وردية كاشير مفتوحة."}\nافتح الوردية من النظام المالي ثم أعد المحاولة.`}
          action={<PrimaryButton title="إعادة الفحص" onPress={() => void load()} />}
        />
      )}

      {error ? <StateMessage title="تعذر حفظ الدفعة" message={error} /> : null}

      <SectionTitle>بيانات الدفعة</SectionTitle>
      <Card style={styles.formCard}>
        <FormField label={`المبلغ (${currency})`} value={amount} onChangeText={setAmount} keyboardType="decimal-pad" />
        <ChoiceRow label="عملة الدفع" value={currency} options={CURRENCIES} onChange={(value) => value && setCurrency(value)} />
        <ChoiceRow
          label="عملة الحساب الذي ستُطبق عليه الدفعة"
          value={accountCurrency}
          options={CURRENCIES}
          onChange={(value) => value && setAccountCurrency(value)}
        />

        {currency !== accountCurrency ? (
          <FormField
            label={`سعر التحويل: 1 ${currency} = كم ${accountCurrency}`}
            value={exchangeRateToAccountCurrency}
            onChangeText={setExchangeRateToAccountCurrency}
            keyboardType="decimal-pad"
          />
        ) : null}

        {currency !== "YER" ? (
          <FormField
            label={`سعر الصرف الفعلي: 1 ${currency} = كم YER`}
            value={exchangeRateToYer}
            onChangeText={setExchangeRateToYer}
            keyboardType="decimal-pad"
          />
        ) : null}

        <ChoiceRow
          label="طريقة الدفع"
          value={paymentMethod}
          options={[...PAYMENT_METHOD_OPTIONS]}
          onChange={setPaymentMethod}
        />

        <SelectList
          label="العقد المرتبط — اختياري"
          value={contractId}
          options={contractOptions}
          onChange={setContractId}
          emptyLabel="بدون عقد"
        />
        <SelectList
          label="الفاتورة المرتبطة — اختياري"
          value={invoiceId}
          options={invoiceOptions}
          onChange={setInvoiceId}
          emptyLabel="بدون فاتورة"
        />
        <FormField
          label="وصف الخدمة — اختياري"
          value={serviceDescription}
          onChangeText={setServiceDescription}
        />
        <FormField label="ملاحظات — اختياري" value={notes} onChangeText={setNotes} multiline />
      </Card>

      <Text style={styles.notice}>
        لا يرسل التطبيق رقم الفرع من الهاتف؛ الخادم يربط الدفعة بفرع الحساب المسجل دخولًا. عملة العقد والفاتورة لا يعيدها هذا الملخص، لذلك تحقق من عملة الحساب قبل الربط. أسعار الصرف تُحفظ مع الدفعة لأغراض التدقيق.
      </Text>

      <PrimaryButton
        title="تسجيل الدفعة"
        onPress={() => void submit()}
        loading={saving}
        disabled={!cashier}
      />
    </Screen>
  );
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  cashierOk: {
    padding: spacing.md,
    borderWidth: 1,
    borderColor: "#bbf7d0",
    borderRadius: radius.sm,
    backgroundColor: colors.successSoft
  },
  cashierTitle: { color: colors.success, fontWeight: "800", textAlign: "right" },
  cashierText: { color: colors.success, marginTop: 4, textAlign: "right" },
  formCard: { gap: spacing.md },
  notice: {
    color: colors.warning,
    backgroundColor: colors.warningSoft,
    padding: spacing.sm,
    borderRadius: radius.sm,
    textAlign: "right",
    lineHeight: 22
  }
});
