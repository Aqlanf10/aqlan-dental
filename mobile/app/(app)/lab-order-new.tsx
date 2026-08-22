import { useSession } from "@/auth/SessionProvider";
import { ChoiceRow, FormField, SelectList } from "@/components/forms";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { isoDateLocal } from "@/lib/format";
import {
  LAB_PRIORITY_OPTIONS,
  type CreateLabOrderInput,
  type LabEntity
} from "@/lib/lab";
import { colors, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useEffect, useMemo, useState } from "react";
import { ActivityIndicator, StyleSheet, Text, View } from "react-native";

type LabsResponse = { data: LabEntity[]; total: number; page: number; pageSize: number };
type SaveMode = "draft" | "send";

export default function LabOrderNewScreen() {
  const { user, can } = useSession();
  const params = useLocalSearchParams<{ patientId: string; patientName?: string; orthoCaseId?: string }>();
  const patientId = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const orthoCaseId = Array.isArray(params.orthoCaseId) ? params.orthoCaseId[0] : params.orthoCaseId;
  const allowed = can("lab_orders.create");
  const canViewLabs = can("labs.view");

  const [applianceType, setApplianceType] = useState("");
  const [labId, setLabId] = useState<string | null>(null);
  const [manualLabName, setManualLabName] = useState("");
  const [expectedDate, setExpectedDate] = useState("");
  const [priority, setPriority] = useState<"urgent" | "normal" | "low">("normal");
  const [instructions, setInstructions] = useState("");
  const [cost, setCost] = useState("");
  const [currency, setCurrency] = useState<"YER" | "SAR" | "USD">("YER");
  const [exchangeRate, setExchangeRate] = useState("");
  const [shade, setShade] = useState("");
  const [restorationType, setRestorationType] = useState("");
  const [labs, setLabs] = useState<LabEntity[]>([]);
  const [loadingLabs, setLoadingLabs] = useState(canViewLabs);
  const [savingMode, setSavingMode] = useState<SaveMode | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!canViewLabs) {
      setLoadingLabs(false);
      return;
    }
    let active = true;
    void apiRequest<LabsResponse>("/api/labs?activeOnly=true&page=1&pageSize=100")
      .then((response) => {
        if (active) setLabs(response.data ?? []);
      })
      .catch((err) => {
        if (active) setError(err instanceof Error ? err.message : "تعذر تحميل قائمة المعامل");
      })
      .finally(() => {
        if (active) setLoadingLabs(false);
      });
    return () => {
      active = false;
    };
  }, [canViewLabs]);

  const selectedLab = useMemo(() => labs.find((lab) => lab.id === labId) ?? null, [labId, labs]);

  async function save(mode: SaveMode) {
    if (!patientId || savingMode) return;
    const cleanAppliance = applianceType.trim();
    if (!cleanAppliance) {
      setError("نوع الجهاز/العمل مطلوب.");
      return;
    }
    if (expectedDate && !isIsoDate(expectedDate)) {
      setError("تاريخ الاستلام المتوقع يجب أن يكون بصيغة YYYY-MM-DD.");
      return;
    }

    const parsedCost = optionalPositiveNumber(cost);
    if (typeof parsedCost === "string") {
      setError(parsedCost);
      return;
    }
    const parsedRate = optionalPositiveNumber(exchangeRate, "سعر الصرف");
    if (typeof parsedRate === "string") {
      setError(parsedRate);
      return;
    }

    if (mode === "send") {
      if (!labId) {
        setError("لا يمكن إرسال الطلب قبل اختيار معمل مسجل.");
        return;
      }
      if (parsedCost == null || parsedCost <= 0) {
        setError("لا يمكن إرسال الطلب قبل إدخال تكلفة صحيحة أكبر من صفر.");
        return;
      }
      if (currency !== "YER" && (parsedRate == null || parsedRate <= 0)) {
        setError("سعر الصرف الفعلي إلى الريال اليمني مطلوب للعملة الأجنبية.");
        return;
      }
    }

    const input: CreateLabOrderInput = {
      patientId,
      orthoCaseId: orthoCaseId || null,
      applianceType: cleanAppliance,
      labId,
      labName: selectedLab?.name || clean(manualLabName),
      sentDate: mode === "send" ? isoDateLocal(new Date()) : null,
      expectedDate: clean(expectedDate),
      priority,
      instructions: clean(instructions),
      cost: parsedCost,
      currency,
      exchangeRateToYer: currency === "YER" ? 1 : parsedRate,
      doctorId: user?.doctorId ?? null,
      shade: clean(shade),
      restorationType: clean(restorationType)
    };

    setSavingMode(mode);
    setError(null);
    try {
      const created = await apiRequest<{ id: string; orderNumber?: string; status?: string }>("/api/lab-orders", {
        method: "POST",
        body: JSON.stringify(input)
      });
      router.replace({
        pathname: "/(app)/lab-order-detail",
        params: {
          id: created.id,
          patientName,
          currency,
          exchangeRateToYer: parsedRate != null ? String(parsedRate) : ""
        }
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر إنشاء طلب المعمل");
    } finally {
      setSavingMode(null);
    }
  }

  if (!allowed) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="حسابك لا يملك صلاحية lab_orders.create." />
      </Screen>
    );
  }

  return (
    <Screen>
      <View>
        <Text style={styles.title}>طلب معمل جديد</Text>
        <Text style={styles.subtitle}>{patientName || "ملف المريض"}</Text>
      </View>

      {error ? <StateMessage title="تعذر الحفظ" message={error} /> : null}

      <FormField
        label="نوع الجهاز / العمل"
        value={applianceType}
        onChangeText={setApplianceType}
        placeholder="Retainer, Crown, Zirconia..."
      />
      <FormField label="نوع الترميم" value={restorationType} onChangeText={setRestorationType} />
      <FormField label="اللون / Shade" value={shade} onChangeText={setShade} placeholder="A1, A2..." />

      {loadingLabs ? (
        <ActivityIndicator color={colors.primary} />
      ) : canViewLabs ? (
        <SelectList
          label="المعمل"
          value={labId}
          options={labs.map((lab) => ({
            label: lab.name,
            value: lab.id,
            subtitle: lab.contactPerson || lab.phone || lab.address || null
          }))}
          onChange={setLabId}
          emptyLabel="بدون معمل — مسودة فقط"
        />
      ) : (
        <FormField
          label="اسم المعمل كنص"
          value={manualLabName}
          onChangeText={setManualLabName}
          placeholder="يمكن حفظ مسودة؛ الإرسال يحتاج LabId وصلاحية عرض المعامل"
        />
      )}

      <FormField
        label="تاريخ الاستلام المتوقع YYYY-MM-DD"
        value={expectedDate}
        onChangeText={setExpectedDate}
        maxLength={10}
      />
      <ChoiceRow
        label="الأولوية"
        value={priority}
        options={LAB_PRIORITY_OPTIONS.map((item) => ({ ...item }))}
        onChange={(value) => {
          if (value === "urgent" || value === "normal" || value === "low") setPriority(value);
        }}
      />
      <FormField label="تعليمات المعمل" value={instructions} onChangeText={setInstructions} multiline />

      <ChoiceRow
        label="العملة"
        value={currency}
        options={[
          { label: "YER", value: "YER" },
          { label: "SAR", value: "SAR" },
          { label: "USD", value: "USD" }
        ]}
        onChange={(value) => {
          if (value === "YER" || value === "SAR" || value === "USD") setCurrency(value);
        }}
      />
      <FormField
        label="التكلفة"
        value={cost}
        onChangeText={setCost}
        keyboardType="decimal-pad"
        placeholder="يمكن تركها فارغة في المسودة"
      />
      {currency !== "YER" ? (
        <FormField
          label={`سعر 1 ${currency} بالريال اليمني`}
          value={exchangeRate}
          onChangeText={setExchangeRate}
          keyboardType="decimal-pad"
        />
      ) : null}

      <Card>
        <Text style={styles.note}>
          «حفظ مسودة» لا يرسل الطلب للمعمل. «حفظ وإرسال» يضع SentDate بتاريخ اليوم ويتطلب معملًا مسجلًا وتكلفة، وسعر صرف فعليًا إذا كانت العملة SAR أو USD.
        </Text>
      </Card>

      <View style={styles.actionGap}>
        <PrimaryButton
          title="حفظ مسودة"
          loading={savingMode === "draft"}
          disabled={savingMode !== null}
          onPress={() => void save("draft")}
        />
        <PrimaryButton
          title="حفظ وإرسال"
          loading={savingMode === "send"}
          disabled={savingMode !== null}
          onPress={() => void save("send")}
        />
      </View>
    </Screen>
  );
}

function isIsoDate(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  return !Number.isNaN(Date.parse(`${value}T00:00:00`));
}

function clean(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length ? trimmed : null;
}

function optionalPositiveNumber(value: string, label = "التكلفة"): number | null | string {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const parsed = Number(trimmed.replace(",", "."));
  if (!Number.isFinite(parsed) || parsed < 0) return `${label} يجب أن يكون رقمًا صحيحًا موجبًا أو صفرًا.`;
  return parsed;
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  note: { color: colors.muted, textAlign: "right", lineHeight: 22 },
  actionGap: { gap: spacing.sm }
});
