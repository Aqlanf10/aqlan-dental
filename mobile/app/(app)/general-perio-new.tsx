import { useSession } from "@/auth/SessionProvider";
import { ChoiceRow, FormField, SelectList } from "@/components/forms";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import {
  canUseGeneralDentistry,
  isPermanentFdiTooth,
  type CreatePerioRecordInput,
  type PerioRecord
} from "@/lib/general";
import type { DoctorSummary } from "@/lib/types";
import { colors } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useEffect, useState } from "react";
import { ActivityIndicator, StyleSheet, Text, View } from "react-native";

const INDEX_OPTIONS = [0, 1, 2, 3].map((value) => ({ label: String(value), value: String(value) }));

export default function GeneralPerioNewScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ patientId: string; patientName?: string; toothNumber?: string }>();
  const patientId = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const initialTooth = Array.isArray(params.toothNumber) ? params.toothNumber[0] : params.toothNumber;
  const allowed = canUseGeneralDentistry(user?.role);

  const [toothNumber, setToothNumber] = useState(initialTooth ?? "");
  const [probingDepth, setProbingDepth] = useState("");
  const [clinicalAttachment, setClinicalAttachment] = useState("");
  const [bleedingOnProbing, setBleedingOnProbing] = useState(false);
  const [plaqueIndex, setPlaqueIndex] = useState(0);
  const [gingivalIndex, setGingivalIndex] = useState(0);
  const [furcation, setFurcation] = useState(0);
  const [mobility, setMobility] = useState(0);
  const [notes, setNotes] = useState("");
  const [doctorId, setDoctorId] = useState<string | null>(user?.doctorId ?? null);
  const [doctors, setDoctors] = useState<DoctorSummary[]>([]);
  const [loadingDoctors, setLoadingDoctors] = useState(user?.role === "Admin");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (user?.role !== "Admin") {
      setLoadingDoctors(false);
      return;
    }

    let active = true;
    void apiRequest<DoctorSummary[]>("/api/doctors?status=active")
      .then((items) => {
        if (active) setDoctors(items ?? []);
      })
      .catch((err) => {
        if (active) setError(err instanceof Error ? err.message : "تعذر تحميل قائمة الأطباء");
      })
      .finally(() => {
        if (active) setLoadingDoctors(false);
      });
    return () => {
      active = false;
    };
  }, [user?.role]);

  async function save() {
    if (!patientId || saving) return;
    const tooth = toothNumber.trim();
    if (!isPermanentFdiTooth(tooth)) {
      setError("أدخل رقم سن دائم صحيح بنظام FDI، مثل 11 أو 36 أو 48.");
      return;
    }

    const pd = parseMeasurement(probingDepth);
    const cal = parseMeasurement(clinicalAttachment);
    if (pd == null || pd < 0 || pd > 15) {
      setError("Probing depth يجب أن يكون بين 0 و15 مم.");
      return;
    }
    if (cal == null || cal < 0 || cal > 15) {
      setError("Clinical attachment يجب أن يكون بين 0 و15 مم.");
      return;
    }

    const input: CreatePerioRecordInput = {
      patientId,
      toothNumber: Number(tooth),
      probingDepth: pd,
      clinicalAttachment: cal,
      bleedingOnProbing,
      plaqueIndex,
      gingivalIndex,
      furcation,
      mobility,
      notes: clean(notes),
      doctorId
    };

    setSaving(true);
    setError(null);
    try {
      await apiRequest<PerioRecord>("/api/general/perio", {
        method: "POST",
        body: JSON.stringify(input)
      });
      router.back();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر حفظ القياس اللثوي");
    } finally {
      setSaving(false);
    }
  }

  if (!allowed) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="سجل اللثة العام متاح للأدمن وطبيب الأسنان العام فقط." />
      </Screen>
    );
  }

  return (
    <Screen>
      <View>
        <Text style={styles.title}>قياس لثوي</Text>
        <Text style={styles.subtitle}>{patientName || "ملف المريض"}</Text>
      </View>

      {error ? <StateMessage title="تعذر الحفظ" message={error} /> : null}

      <Card>
        <Text style={styles.info}>
          عقد الـBackend الحالي يسجل قياسًا واحدًا لكل سجل سن، وليس six-site periodontal chart. لذلك لا تختصر عدة مواقع بقياس غير حقيقي؛ أضف السجل بحسب ما تم قياسه فعليًا.
        </Text>
      </Card>

      <FormField
        label="رقم السن FDI"
        value={toothNumber}
        onChangeText={setToothNumber}
        placeholder="مثال: 36"
        keyboardType="number-pad"
        maxLength={2}
      />
      <FormField
        label="Probing depth (mm)"
        value={probingDepth}
        onChangeText={setProbingDepth}
        placeholder="0 - 15"
        keyboardType="decimal-pad"
      />
      <FormField
        label="Clinical attachment (mm)"
        value={clinicalAttachment}
        onChangeText={setClinicalAttachment}
        placeholder="0 - 15"
        keyboardType="decimal-pad"
      />
      <ChoiceRow
        label="Bleeding on probing"
        value={bleedingOnProbing ? "yes" : "no"}
        options={[
          { label: "نعم", value: "yes" },
          { label: "لا", value: "no" }
        ]}
        onChange={(value) => {
          if (value === "yes") setBleedingOnProbing(true);
          if (value === "no") setBleedingOnProbing(false);
        }}
      />
      <IndexChoice label="Plaque index" value={plaqueIndex} onChange={setPlaqueIndex} />
      <IndexChoice label="Gingival index" value={gingivalIndex} onChange={setGingivalIndex} />
      <IndexChoice label="Furcation" value={furcation} onChange={setFurcation} />
      <IndexChoice label="Mobility" value={mobility} onChange={setMobility} />
      <FormField label="ملاحظات" value={notes} onChangeText={setNotes} multiline maxLength={500} />

      {user?.role === "Admin" ? (
        loadingDoctors ? (
          <ActivityIndicator color={colors.primary} />
        ) : (
          <SelectList
            label="الطبيب"
            value={doctorId}
            options={doctors.map((doctor) => ({
              label: doctor.name,
              value: doctor.id,
              subtitle: doctor.specialty || doctor.branchName || null
            }))}
            onChange={setDoctorId}
            emptyLabel="بدون طبيب محدد"
          />
        )
      ) : null}

      <PrimaryButton title="حفظ القياس" loading={saving} onPress={() => void save()} />
    </Screen>
  );
}

function IndexChoice({
  label,
  value,
  onChange
}: {
  label: string;
  value: number;
  onChange: (value: number) => void;
}) {
  return (
    <ChoiceRow
      label={label}
      value={String(value)}
      options={INDEX_OPTIONS}
      onChange={(next) => {
        if (next !== null) onChange(Number(next));
      }}
    />
  );
}

function parseMeasurement(value: string): number | null {
  const normalized = value.trim().replace(",", ".");
  if (!normalized) return null;
  const parsed = Number(normalized);
  return Number.isFinite(parsed) ? parsed : null;
}

function clean(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  info: { color: colors.muted, textAlign: "right", lineHeight: 22 }
});
