import { useSession } from "@/auth/SessionProvider";
import { FormField } from "@/components/forms";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { canUseInventory } from "@/lib/inventory";
import { colors, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useState } from "react";
import { StyleSheet, Text, View } from "react-native";

export default function InventoryAdjustScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ id: string; name?: string; quantity?: string; unit?: string }>();
  const id = first(params.id);
  const name = first(params.name) || "المادة";
  const currentQuantity = Number(first(params.quantity) || "0");
  const unit = first(params.unit) || "وحدة";
  const allowed = canUseInventory(user?.role);
  const [delta, setDelta] = useState("");
  const [reason, setReason] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function save() {
    const parsed = Number(delta);
    if (!Number.isInteger(parsed) || parsed === 0) return setError("أدخل رقمًا صحيحًا غير صفر. استخدم قيمة سالبة للخصم.");
    if (Number.isFinite(currentQuantity) && currentQuantity + parsed < 0) return setError("هذا التعديل سيجعل الرصيد سالبًا.");
    setSaving(true);
    setError(null);
    try {
      await apiRequest(`/api/inventory/${id}/adjust`, {
        method: "PUT",
        body: JSON.stringify({ delta: parsed, reason: reason.trim() || "تعديل يدوي من تطبيق الهاتف" })
      });
      router.back();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تعديل الكمية");
    } finally {
      setSaving(false);
    }
  }

  if (!allowed) return <Screen><StateMessage title="غير مصرح" message="تعديل المخزون متاح للإدارة فقط." /></Screen>;

  const preview = Number.isInteger(Number(delta)) ? currentQuantity + Number(delta) : currentQuantity;

  return (
    <Screen>
      <View><Text style={styles.title}>تعديل الكمية</Text><Text style={styles.subtitle}>{name}</Text></View>
      {error ? <StateMessage title="تعذر تنفيذ التعديل" message={error} /> : null}
      <Card>
        <Row label="الرصيد الحالي" value={`${currentQuantity} ${unit}`} />
        <Row label="الرصيد بعد التعديل" value={`${preview} ${unit}`} last />
      </Card>
      <Card>
        <View style={styles.form}>
          <FormField label="التعديل *" value={delta} onChangeText={setDelta} keyboardType="numbers-and-punctuation" placeholder="مثال: 10 أو -3" />
          <FormField label="السبب" value={reason} onChangeText={setReason} multiline placeholder="سبب الإضافة أو الخصم" />
        </View>
      </Card>
      <Text style={styles.note}>كل تعديل يُسجل في InventoryAdjustments مع الرصيد السابق والجديد والسبب.</Text>
      <PrimaryButton title="حفظ حركة المخزون" loading={saving} disabled={saving} onPress={() => void save()} />
    </Screen>
  );
}

function first(value?: string | string[]): string { return Array.isArray(value) ? value[0] ?? "" : value ?? ""; }
function Row({ label, value, last = false }: { label: string; value: string; last?: boolean }) {
  return <View style={[styles.row, last && { borderBottomWidth: 0 }]}><Text style={styles.value}>{value}</Text><Text style={styles.label}>{label}</Text></View>;
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, textAlign: "right", fontWeight: "700" },
  form: { gap: spacing.md },
  row: { minHeight: 44, flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.border },
  label: { color: colors.muted, textAlign: "right" },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "700" },
  note: { color: colors.muted, textAlign: "right", lineHeight: 22 }
});
