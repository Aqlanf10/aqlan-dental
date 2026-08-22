import { useSession } from "@/auth/SessionProvider";
import { FormField } from "@/components/forms";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { canUseInventory, type InventoryItem, type InventoryItemInput, type InventoryListResponse } from "@/lib/inventory";
import { colors, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useEffect, useState } from "react";
import { ActivityIndicator, StyleSheet, Text, View } from "react-native";

export default function InventoryItemEditorScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ id?: string }>();
  const id = first(params.id);
  const allowed = canUseInventory(user?.role);
  const editing = Boolean(id);

  const [loading, setLoading] = useState(editing);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [category, setCategory] = useState("");
  const [quantity, setQuantity] = useState("0");
  const [minQuantity, setMinQuantity] = useState("0");
  const [unit, setUnit] = useState("");
  const [costPerUnit, setCostPerUnit] = useState("");
  const [batchNumber, setBatchNumber] = useState("");
  const [expiryDate, setExpiryDate] = useState("");
  const [minStockLevel, setMinStockLevel] = useState("");
  const [purchaseUnit, setPurchaseUnit] = useState("");
  const [consumptionUnit, setConsumptionUnit] = useState("");
  const [warehouseLocation, setWarehouseLocation] = useState("");
  const [imageUrl, setImageUrl] = useState("");

  useEffect(() => {
    if (!editing || !allowed) return;
    let active = true;
    apiRequest<InventoryListResponse>("/api/inventory?page=1&pageSize=100")
      .then((response) => {
        if (!active) return;
        const item = response.data.find((entry) => entry.id === id);
        if (!item) throw new Error("المادة غير موجودة");
        fill(item);
      })
      .catch((err) => active && setError(err instanceof Error ? err.message : "تعذر تحميل المادة"))
      .finally(() => active && setLoading(false));
    return () => { active = false; };
  }, [allowed, editing, id]);

  function fill(item: InventoryItem) {
    setName(item.name || "");
    setCategory(item.category || "");
    setQuantity(String(item.quantity ?? 0));
    setMinQuantity(String(item.minQuantity ?? 0));
    setUnit(item.unit || "");
    setCostPerUnit(item.costPerUnit == null ? "" : String(item.costPerUnit));
    setBatchNumber(item.batchNumber || "");
    setExpiryDate(item.expiryDate || "");
    setMinStockLevel(item.minStockLevel || "");
    setPurchaseUnit(item.purchaseUnit || "");
    setConsumptionUnit(item.consumptionUnit || "");
    setWarehouseLocation(item.warehouseLocation || "");
    setImageUrl(item.imageUrl || "");
  }

  async function save() {
    if (saving) return;
    const qty = Number(quantity);
    const minQty = Number(minQuantity);
    const cost = costPerUnit.trim() ? Number(costPerUnit) : null;
    const minStock = minStockLevel.trim() ? Number(minStockLevel) : null;
    if (!name.trim()) return setError("اسم المادة مطلوب.");
    if (!Number.isInteger(qty) || qty < 0) return setError("الكمية يجب أن تكون رقمًا صحيحًا صفر أو أكثر.");
    if (!Number.isInteger(minQty) || minQty < 0) return setError("الحد الأدنى يجب أن يكون رقمًا صحيحًا صفر أو أكثر.");
    if (cost != null && (!Number.isFinite(cost) || cost < 0)) return setError("تكلفة الوحدة غير صالحة.");
    if (minStock != null && (!Number.isFinite(minStock) || minStock < 0)) return setError("الحد الأدنى للمخزون غير صالح.");
    if (expiryDate.trim() && !/^\d{4}-\d{2}-\d{2}$/.test(expiryDate.trim())) return setError("تاريخ الصلاحية يجب أن يكون YYYY-MM-DD.");

    const payload: InventoryItemInput = {
      name: name.trim(), category: nullable(category), quantity: qty, minQuantity: minQty,
      unit: nullable(unit), costPerUnit: cost, batchNumber: nullable(batchNumber), expiryDate: nullable(expiryDate),
      minStockLevel: minStock, purchaseUnit: nullable(purchaseUnit), consumptionUnit: nullable(consumptionUnit),
      warehouseLocation: nullable(warehouseLocation), imageUrl: nullable(imageUrl)
    };

    setSaving(true);
    setError(null);
    try {
      await apiRequest(editing ? `/api/inventory/${id}` : "/api/inventory", {
        method: editing ? "PUT" : "POST",
        body: JSON.stringify(payload)
      });
      router.back();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر حفظ المادة");
    } finally {
      setSaving(false);
    }
  }

  if (!allowed) return <Screen><StateMessage title="غير مصرح" message="إدارة المخزون متاحة للإدارة فقط." /></Screen>;
  if (loading) return <Screen><ActivityIndicator size="large" color={colors.primary} /></Screen>;

  return (
    <Screen>
      <View><Text style={styles.title}>{editing ? "تعديل مادة" : "إضافة مادة"}</Text><Text style={styles.subtitle}>البيانات التي يعتمد عليها الرصيد والتنبيهات والتقييم</Text></View>
      {error ? <StateMessage title="تعذر الحفظ" message={error} /> : null}
      <Card>
        <View style={styles.form}>
          <FormField label="اسم المادة *" value={name} onChangeText={setName} />
          <FormField label="التصنيف" value={category} onChangeText={setCategory} />
          <FormField label="الكمية *" value={quantity} onChangeText={setQuantity} keyboardType="number-pad" />
          <FormField label="الحد الأدنى *" value={minQuantity} onChangeText={setMinQuantity} keyboardType="number-pad" />
          <FormField label="الوحدة" value={unit} onChangeText={setUnit} />
          <FormField label="تكلفة الوحدة YER" value={costPerUnit} onChangeText={setCostPerUnit} keyboardType="decimal-pad" />
          <FormField label="رقم الدفعة" value={batchNumber} onChangeText={setBatchNumber} />
          <FormField label="تاريخ الصلاحية YYYY-MM-DD" value={expiryDate} onChangeText={setExpiryDate} />
          <FormField label="Min stock level" value={minStockLevel} onChangeText={setMinStockLevel} keyboardType="decimal-pad" />
          <FormField label="وحدة الشراء" value={purchaseUnit} onChangeText={setPurchaseUnit} />
          <FormField label="وحدة الصرف" value={consumptionUnit} onChangeText={setConsumptionUnit} />
          <FormField label="موقع التخزين" value={warehouseLocation} onChangeText={setWarehouseLocation} />
          <FormField label="رابط صورة المادة" value={imageUrl} onChangeText={setImageUrl} autoCapitalize="none" />
        </View>
      </Card>
      <PrimaryButton title={editing ? "حفظ التعديلات" : "إضافة المادة"} loading={saving} disabled={saving} onPress={() => void save()} />
    </Screen>
  );
}

function first(value?: string | string[]): string { return Array.isArray(value) ? value[0] ?? "" : value ?? ""; }
function nullable(value: string): string | null { const result = value.trim(); return result || null; }

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.muted, marginTop: 4, textAlign: "right" },
  form: { gap: spacing.md }
});
