import { useSession } from "@/auth/SessionProvider";
import { ChoiceRow, FormField } from "@/components/forms";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { formatMoney, labStatusLabel } from "@/lib/lab";
import { colors } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useState } from "react";
import { StyleSheet, Text, View } from "react-native";

type TransitionKind = "cancelled" | "returned" | "remake";

export default function LabOrderTransitionScreen() {
  const { can } = useSession();
  const params = useLocalSearchParams<{
    id: string;
    transition: string;
    orderNumber?: string;
    currency?: string;
  }>();
  const id = first(params.id);
  const transition = first(params.transition) as TransitionKind;
  const orderNumber = first(params.orderNumber);
  const currency = first(params.currency);
  const allowed = can("lab_orders.edit");

  const [reason, setReason] = useState("");
  const [isFreeRemake, setIsFreeRemake] = useState(true);
  const [remakeCost, setRemakeCost] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const validTransition = transition === "cancelled" || transition === "returned" || transition === "remake";

  async function save() {
    if (!id || !validTransition || saving) return;
    const cleanReason = reason.trim();
    if (!cleanReason) {
      setError("السبب مطلوب حتى يبقى سجل تغيير الحالة واضحًا وقابلًا للمراجعة.");
      return;
    }

    let path = "";
    let body: Record<string, unknown> = { reason: cleanReason };

    if (transition === "cancelled") {
      path = `/api/lab-orders/${id}/cancel`;
    } else if (transition === "returned") {
      path = `/api/lab-orders/${id}/return`;
    } else {
      path = `/api/lab-orders/${id}/remake`;
      const parsed = parseOptionalPositive(remakeCost);
      if (typeof parsed === "string") {
        setError(parsed);
        return;
      }
      if (!isFreeRemake && (parsed == null || parsed <= 0)) {
        setError("إعادة الصناعة غير المجانية تتطلب تكلفة إضافية أكبر من صفر.");
        return;
      }
      body = {
        reason: cleanReason,
        isFreeRemake,
        remakeCost: isFreeRemake ? null : parsed
      };
    }

    setSaving(true);
    setError(null);
    try {
      await apiRequest(path, { method: "POST", body: JSON.stringify(body) });
      router.back();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تغيير حالة طلب المعمل");
    } finally {
      setSaving(false);
    }
  }

  if (!allowed) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="حسابك لا يملك صلاحية lab_orders.edit." />
      </Screen>
    );
  }

  if (!validTransition) {
    return (
      <Screen>
        <StateMessage title="انتقال غير صالح" message="نوع تغيير حالة طلب المعمل غير معروف." />
      </Screen>
    );
  }

  const title = transition === "cancelled" ? "إلغاء طلب المعمل" : transition === "returned" ? "إرجاع الطلب للمعمل" : "إعادة صناعة الطلب";

  return (
    <Screen>
      <View>
        <Text style={styles.title}>{title}</Text>
        <Text style={styles.subtitle}>{orderNumber || "طلب المعمل"}</Text>
      </View>

      {error ? <StateMessage title="تعذر تنفيذ العملية" message={error} /> : null}

      <Card>
        <Text style={styles.note}>
          الانتقال إلى «{labStatusLabel(transition)}» يُسجل في تاريخ الطلب مع المستخدم والسبب. الإلغاء قد يعكس الالتزام المالي وتعديل عمولة الطبيب وفق منطق الخادم الحالي.
        </Text>
      </Card>

      <FormField
        label="السبب"
        value={reason}
        onChangeText={setReason}
        placeholder="اكتب سببًا واضحًا"
        multiline
        maxLength={1000}
      />

      {transition === "remake" ? (
        <>
          <ChoiceRow
            label="نوع إعادة الصناعة"
            value={isFreeRemake ? "free" : "paid"}
            options={[
              { label: "مجانية", value: "free" },
              { label: "بتكلفة إضافية", value: "paid" }
            ]}
            onChange={(value) => {
              if (value === "free") setIsFreeRemake(true);
              if (value === "paid") setIsFreeRemake(false);
            }}
          />
          {!isFreeRemake ? (
            <FormField
              label={`تكلفة إعادة الصناعة${currency ? ` (${currency})` : ""}`}
              value={remakeCost}
              onChangeText={setRemakeCost}
              keyboardType="decimal-pad"
              placeholder="أدخل التكلفة الإضافية"
            />
          ) : null}
          <Card>
            <Text style={styles.warning}>
              {isFreeRemake
                ? "إعادة الصناعة المجانية لا تضيف تكلفة جديدة."
                : `التكلفة الإضافية ستُضاف إلى إجمالي الطلب${currency ? ` بعملة الطلب ${currency}` : ""}. لا يتم تحويلها إلى عملة أخرى داخل هذه الشاشة.`}
            </Text>
            {!isFreeRemake && remakeCost.trim() ? (
              <Text style={styles.previewMoney}>الإضافة: {formatMoney(Number(remakeCost.replace(",", ".")), currency)}</Text>
            ) : null}
          </Card>
        </>
      ) : null}

      <PrimaryButton title="تأكيد العملية" loading={saving} onPress={() => void save()} />
    </Screen>
  );
}

function first(value: string | string[] | undefined): string {
  return Array.isArray(value) ? value[0] ?? "" : value ?? "";
}

function parseOptionalPositive(value: string): number | null | string {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const parsed = Number(trimmed.replace(",", "."));
  if (!Number.isFinite(parsed) || parsed <= 0) return "التكلفة الإضافية يجب أن تكون رقمًا أكبر من صفر.";
  return parsed;
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  note: { color: colors.text, textAlign: "right", lineHeight: 22 },
  warning: { color: colors.warning, textAlign: "right", lineHeight: 22 },
  previewMoney: { color: colors.primary, textAlign: "right", fontWeight: "800", marginTop: 8 }
});
