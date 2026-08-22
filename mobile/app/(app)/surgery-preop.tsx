import { useSession } from "@/auth/SessionProvider";
import { ChoiceRow, FormField, SelectList } from "@/components/forms";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { isoDateLocal } from "@/lib/format";
import {
  canUseSurgery,
  PREOP_CHECKLIST_ITEMS,
  type PreopReport,
  type UpsertPreopInput
} from "@/lib/surgery";
import type { DoctorSummary } from "@/lib/types";
import { colors, radius, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useEffect, useState } from "react";
import { ActivityIndicator, Pressable, StyleSheet, Text, View } from "react-native";

export default function SurgeryPreopScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ id: string; patientName?: string }>();
  const id = Array.isArray(params.id) ? params.id[0] : params.id;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const allowed = canUseSurgery(user?.role);

  const [surgeryDate, setSurgeryDate] = useState(isoDateLocal(new Date()));
  const [surgeryLocation, setSurgeryLocation] = useState("");
  const [anesthesiaType, setAnesthesiaType] = useState("");
  const [consentSigned, setConsentSigned] = useState(false);
  const [checklist, setChecklist] = useState<Record<string, boolean>>({});
  const [requiredTestsText, setRequiredTestsText] = useState("");
  const [doctorId, setDoctorId] = useState<string | null>(user?.doctorId ?? null);
  const [doctors, setDoctors] = useState<DoctorSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id || !allowed) {
      setLoading(false);
      return;
    }

    let active = true;
    const requests: Promise<unknown>[] = [
      apiRequest<PreopReport | null>(`/api/surgery-cases/${id}/preop`).then((existing) => {
        if (!active || !existing) return;
        setSurgeryDate(existing.surgeryDate ?? "");
        setSurgeryLocation(existing.surgeryLocation ?? "");
        setAnesthesiaType(existing.anesthesiaType ?? "");
        setConsentSigned(existing.consentSigned);
        setChecklist(existing.checklist ?? {});
        setRequiredTestsText((existing.requiredTests ?? []).join("\n"));
        setDoctorId(existing.doctorId ?? user?.doctorId ?? null);
      })
    ];

    if (user?.role === "Admin") {
      requests.push(
        apiRequest<DoctorSummary[]>("/api/doctors?status=active").then((items) => {
          if (active) setDoctors(items ?? []);
        })
      );
    }

    void Promise.all(requests)
      .catch((err) => {
        if (active) setError(err instanceof Error ? err.message : "تعذر تحميل بيانات ما قبل الجراحة");
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [allowed, id, user?.doctorId, user?.role]);

  function toggleChecklist(key: string) {
    setChecklist((current) => ({ ...current, [key]: !current[key] }));
  }

  async function save() {
    if (!id || saving) return;
    const date = surgeryDate.trim();
    if (date && !isIsoDate(date)) {
      setError("تاريخ الجراحة يجب أن يكون بصيغة YYYY-MM-DD.");
      return;
    }

    const requiredTests = requiredTestsText
      .split(/\n|،|,/)
      .map((item) => item.trim())
      .filter(Boolean);

    const input: UpsertPreopInput = {
      surgeryDate: date || null,
      surgeryLocation: clean(surgeryLocation),
      anesthesiaType: clean(anesthesiaType),
      consentSigned,
      doctorId,
      checklist,
      requiredTests
    };

    setSaving(true);
    setError(null);
    try {
      await apiRequest(`/api/surgery-cases/${id}/preop`, {
        method: "PUT",
        body: JSON.stringify(input)
      });
      router.back();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر حفظ بيانات ما قبل الجراحة");
    } finally {
      setSaving(false);
    }
  }

  if (!allowed) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="بيانات ما قبل الجراحة متاحة للأدمن وجراح الفم فقط." />
      </Screen>
    );
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
        <Text style={styles.title}>ما قبل الجراحة</Text>
        <Text style={styles.subtitle}>{patientName || "الحالة الجراحية"}</Text>
      </View>

      {error ? <StateMessage title="تعذر الحفظ" message={error} /> : null}

      <FormField
        label="تاريخ الجراحة YYYY-MM-DD"
        value={surgeryDate}
        onChangeText={setSurgeryDate}
        placeholder="2026-08-23"
        maxLength={10}
      />
      <FormField
        label="مكان الجراحة"
        value={surgeryLocation}
        onChangeText={setSurgeryLocation}
        placeholder="العيادة / المستشفى"
      />
      <ChoiceRow
        label="نوع التخدير"
        value={anesthesiaType || null}
        options={[
          { label: "موضعي", value: "Local" },
          { label: "تهدئة", value: "Sedation" },
          { label: "عام", value: "General" }
        ]}
        onChange={(value) => setAnesthesiaType(value ?? "")}
      />
      <FormField
        label="التخدير / تفاصيل"
        value={anesthesiaType}
        onChangeText={setAnesthesiaType}
      />
      <ChoiceRow
        label="الموافقة الجراحية"
        value={consentSigned ? "yes" : "no"}
        options={[
          { label: "موقعة", value: "yes" },
          { label: "غير موقعة", value: "no" }
        ]}
        onChange={(value) => {
          if (value === "yes") setConsentSigned(true);
          if (value === "no") setConsentSigned(false);
        }}
      />

      <Card>
        <Text style={styles.sectionLabel}>Pre-op checklist</Text>
        <View style={styles.checklist}>
          {PREOP_CHECKLIST_ITEMS.map((entry) => {
            const checked = Boolean(checklist[entry.key]);
            return (
              <Pressable
                key={entry.key}
                accessibilityRole="checkbox"
                accessibilityState={{ checked }}
                onPress={() => toggleChecklist(entry.key)}
                style={[styles.checkItem, checked && styles.checkItemSelected]}
              >
                <Text style={[styles.checkText, checked && styles.checkTextSelected]}>
                  {checked ? "✓ " : "○ "}{entry.label}
                </Text>
              </Pressable>
            );
          })}
        </View>
      </Card>

      <FormField
        label="الفحوص المطلوبة — كل فحص في سطر"
        value={requiredTestsText}
        onChangeText={setRequiredTestsText}
        placeholder={"CBC\nINR\nPanoramic X-ray"}
        multiline
      />

      {user?.role === "Admin" ? (
        <SelectList
          label="الجراح"
          value={doctorId}
          options={doctors.map((doctor) => ({
            label: doctor.name,
            value: doctor.id,
            subtitle: doctor.specialty || doctor.branchName || null
          }))}
          onChange={setDoctorId}
          emptyLabel="بدون جراح محدد"
        />
      ) : null}

      <PrimaryButton title="حفظ ما قبل الجراحة" loading={saving} onPress={() => void save()} />
    </Screen>
  );
}

function isIsoDate(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  return !Number.isNaN(Date.parse(`${value}T00:00:00`));
}

function clean(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  sectionLabel: { color: colors.text, fontWeight: "800", textAlign: "right" },
  checklist: { marginTop: spacing.sm, gap: spacing.sm },
  checkItem: {
    minHeight: 44,
    justifyContent: "center",
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.sm,
    backgroundColor: colors.surface,
    paddingHorizontal: spacing.md
  },
  checkItemSelected: { borderColor: colors.primary, backgroundColor: colors.primarySoft },
  checkText: { color: colors.text, textAlign: "right", fontWeight: "600" },
  checkTextSelected: { color: colors.primary }
});
