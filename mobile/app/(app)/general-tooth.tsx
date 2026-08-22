import { useSession } from "@/auth/SessionProvider";
import { ChoiceRow, FormField } from "@/components/forms";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import {
  canUseGeneralDentistry,
  TOOTH_CONDITION_OPTIONS,
  type DentalChart,
  type ToothCondition,
  type UpdateToothInput
} from "@/lib/general";
import { colors, radius, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useCallback, useEffect, useMemo, useState } from "react";
import { ActivityIndicator, Pressable, StyleSheet, Text, View } from "react-native";

const SURFACES = ["M", "D", "O", "B", "L", "P"] as const;

export default function GeneralToothScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ patientId: string; patientName?: string; toothNumber: string }>();
  const patientId = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const toothNumber = Array.isArray(params.toothNumber) ? params.toothNumber[0] : params.toothNumber;
  const allowed = canUseGeneralDentistry(user?.role);

  const [condition, setCondition] = useState("");
  const [surfacesAffected, setSurfacesAffected] = useState("");
  const [treatmentDone, setTreatmentDone] = useState("");
  const [notes, setNotes] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const selectedSurfaces = useMemo(
    () =>
      new Set(
        surfacesAffected
          .split(",")
          .map((item) => item.trim().toUpperCase())
          .filter(Boolean)
      ),
    [surfacesAffected]
  );

  const load = useCallback(async () => {
    if (!patientId || !toothNumber || !allowed) {
      setLoading(false);
      return;
    }
    setError(null);
    try {
      const chart = await apiRequest<DentalChart>(`/api/dental-chart/${patientId}`);
      const existing = chart.teeth.find((item) => item.toothNumber === toothNumber);
      if (existing) applyToForm(existing);
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحميل حالة السن");
    } finally {
      setLoading(false);
    }
  }, [allowed, patientId, toothNumber]);

  useEffect(() => {
    void load();
  }, [load]);

  function applyToForm(existing: ToothCondition) {
    setCondition(existing.condition ?? "");
    setSurfacesAffected(existing.surfacesAffected ?? "");
    setTreatmentDone(existing.treatmentDone ?? "");
    setNotes(existing.notes ?? "");
  }

  function toggleSurface(surface: string) {
    const next = new Set(selectedSurfaces);
    if (next.has(surface)) next.delete(surface);
    else next.add(surface);
    setSurfacesAffected(SURFACES.filter((item) => next.has(item)).join(","));
  }

  async function save() {
    if (!patientId || !toothNumber || saving) return;
    setSaving(true);
    setError(null);
    const input: UpdateToothInput = {
      toothNumber,
      condition: clean(condition),
      surfacesAffected: clean(surfacesAffected),
      treatmentDone: clean(treatmentDone),
      notes: clean(notes)
    };

    try {
      await apiRequest<ToothCondition>(`/api/dental-chart/${patientId}/teeth`, {
        method: "PUT",
        body: JSON.stringify(input)
      });
      router.back();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر حفظ حالة السن");
    } finally {
      setSaving(false);
    }
  }

  if (!allowed) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="تعديل مخطط الأسنان متاح للأدمن وطبيب الأسنان العام فقط." />
      </Screen>
    );
  }

  return (
    <Screen>
      <View>
        <Text style={styles.title}>سن {toothNumber || "—"}</Text>
        <Text style={styles.subtitle}>{patientName || "مخطط FDI"}</Text>
      </View>

      {loading ? <ActivityIndicator size="large" color={colors.primary} /> : null}
      {error ? <StateMessage title="تعذر تنفيذ العملية" message={error} /> : null}

      {!loading ? (
        <>
          <Card style={styles.toothCard}>
            <Text style={styles.bigTooth}>{toothNumber}</Text>
            <Text style={styles.hint}>FDI tooth number</Text>
          </Card>

          <ChoiceRow
            label="حالة شائعة"
            value={condition || null}
            options={TOOTH_CONDITION_OPTIONS.map((item) => ({ ...item }))}
            onChange={(value) => setCondition(value ?? "")}
          />
          <FormField
            label="الحالة / التشخيص المختصر"
            value={condition}
            onChangeText={setCondition}
            placeholder="مثال: caries أو وصف مخصص"
            maxLength={100}
          />

          <View style={styles.fieldGroup}>
            <Text style={styles.label}>الأسطح المتأثرة</Text>
            <View style={styles.surfaceRow}>
              {SURFACES.map((surface) => {
                const selected = selectedSurfaces.has(surface);
                return (
                  <Pressable
                    key={surface}
                    accessibilityRole="button"
                    accessibilityState={{ selected }}
                    onPress={() => toggleSurface(surface)}
                    style={[styles.surface, selected && styles.surfaceSelected]}
                  >
                    <Text style={[styles.surfaceText, selected && styles.surfaceTextSelected]}>{surface}</Text>
                  </Pressable>
                );
              })}
            </View>
            <FormField
              label="الأسطح كنص"
              value={surfacesAffected}
              onChangeText={setSurfacesAffected}
              placeholder="M,D,O,B,L أو وصف مخصص"
              maxLength={50}
            />
          </View>

          <FormField
            label="العلاج المنفذ"
            value={treatmentDone}
            onChangeText={setTreatmentDone}
            placeholder="الحشوة، التاج، RCT..."
            multiline
            maxLength={200}
          />
          <FormField
            label="ملاحظات"
            value={notes}
            onChangeText={setNotes}
            placeholder="ملاحظات سريرية إضافية"
            multiline
            maxLength={500}
          />

          <PrimaryButton title="حفظ حالة السن" loading={saving} onPress={() => void save()} />
        </>
      ) : null}
    </Screen>
  );
}

function clean(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  toothCard: { alignItems: "center", justifyContent: "center" },
  bigTooth: { color: colors.primary, fontSize: 38, fontWeight: "900" },
  hint: { color: colors.muted, marginTop: 4, fontSize: 12 },
  fieldGroup: { gap: spacing.sm },
  label: { color: colors.text, fontSize: 14, fontWeight: "700", textAlign: "right" },
  surfaceRow: { flexDirection: "row-reverse", flexWrap: "wrap", gap: spacing.sm },
  surface: {
    minWidth: 46,
    minHeight: 42,
    alignItems: "center",
    justifyContent: "center",
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.sm,
    backgroundColor: colors.surface
  },
  surfaceSelected: { borderColor: colors.primary, backgroundColor: colors.primarySoft },
  surfaceText: { color: colors.text, fontWeight: "800" },
  surfaceTextSelected: { color: colors.primary }
});
