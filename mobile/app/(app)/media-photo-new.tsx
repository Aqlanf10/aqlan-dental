import { useSession } from "@/auth/SessionProvider";
import { ChoiceRow, FormField } from "@/components/forms";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { isoDateLocal } from "@/lib/format";
import {
  captureClinicalPhoto,
  PHOTO_CATEGORIES,
  PHOTO_STAGE_OPTIONS,
  pickClinicalPhoto,
  uploadClinicalFile,
  type PickedClinicalFile
} from "@/lib/media";
import { canWriteClinicalRecords } from "@/lib/roles";
import { colors, radius, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useState } from "react";
import { Image, StyleSheet, Text, View } from "react-native";

export default function MediaPhotoNewScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ patientId: string; patientName?: string; orthoCaseId?: string }>();
  const patientId = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const orthoCaseId = Array.isArray(params.orthoCaseId) ? params.orthoCaseId[0] : params.orthoCaseId;
  const canWrite = canWriteClinicalRecords(user);

  const [file, setFile] = useState<PickedClinicalFile | null>(null);
  const [category, setCategory] = useState("intraoral");
  const [photoType, setPhotoType] = useState("");
  const [stage, setStage] = useState("initial");
  const [notes, setNotes] = useState("");
  const [photoDate, setPhotoDate] = useState(isoDateLocal(new Date()));
  const [selecting, setSelecting] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function choose(source: "camera" | "library") {
    if (selecting) return;
    setSelecting(true);
    setError(null);
    try {
      const selected = source === "camera" ? await captureClinicalPhoto() : await pickClinicalPhoto();
      if (selected) setFile(selected);
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر اختيار الصورة");
    } finally {
      setSelecting(false);
    }
  }

  async function save() {
    if (!patientId || saving) return;
    if (!file) {
      setError("اختر صورة أو التقط صورة بالكاميرا أولًا.");
      return;
    }
    if (!isIsoDate(photoDate)) {
      setError("تاريخ الصورة يجب أن يكون بصيغة YYYY-MM-DD.");
      return;
    }

    setSaving(true);
    setError(null);
    try {
      const uploaded = await uploadClinicalFile(file, patientId, "clinical-photo");
      await apiRequest("/api/clinical-photos", {
        method: "POST",
        body: JSON.stringify({
          patientId,
          orthoCaseId: orthoCaseId || null,
          fileUrl: uploaded.url,
          category,
          photoType: clean(photoType),
          stage: stage || null,
          notes: clean(notes),
          photoDate
        })
      });
      router.back();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر رفع الصورة السريرية");
    } finally {
      setSaving(false);
    }
  }

  if (!canWrite) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="رفع الصور متاح للأدوار التي تملك ClinicalWrite فقط." />
      </Screen>
    );
  }

  return (
    <Screen>
      <View>
        <Text style={styles.title}>صورة سريرية جديدة</Text>
        <Text style={styles.subtitle}>{patientName || "ملف المريض"}</Text>
      </View>

      {error ? <StateMessage title="تعذر رفع الصورة" message={error} /> : null}

      <View style={styles.sourceRow}>
        <View style={styles.sourceButton}>
          <PrimaryButton title="التقاط بالكاميرا" loading={selecting} onPress={() => void choose("camera")} />
        </View>
        <View style={styles.sourceButton}>
          <PrimaryButton title="اختيار من الصور" disabled={selecting} onPress={() => void choose("library")} />
        </View>
      </View>

      {file ? (
        <Card>
          <Image source={{ uri: file.uri }} style={styles.preview} resizeMode="contain" />
          <Text style={styles.fileName}>{file.name}</Text>
        </Card>
      ) : null}

      <ChoiceRow
        label="الفئة"
        value={category}
        options={PHOTO_CATEGORIES.map((item) => ({ ...item }))}
        onChange={(value) => {
          if (value) setCategory(value);
        }}
      />
      <FormField
        label="نوع/وصف الصورة"
        value={photoType}
        onChangeText={setPhotoType}
        placeholder="مثال: Right buccal"
      />
      <ChoiceRow
        label="المرحلة"
        value={stage || null}
        options={PHOTO_STAGE_OPTIONS.map((item) => ({ ...item }))}
        onChange={(value) => setStage(value ?? "")}
      />
      <FormField
        label="تاريخ الصورة YYYY-MM-DD"
        value={photoDate}
        onChangeText={setPhotoDate}
        maxLength={10}
      />
      <FormField label="ملاحظات" value={notes} onChangeText={setNotes} multiline />

      <Card>
        <Text style={styles.note}>
          الحد الأقصى 10 MB. الخادم يتحقق من محتوى الملف نفسه، وليس الامتداد فقط. الصور المدعومة: JPG وPNG وWEBP.
        </Text>
      </Card>

      <PrimaryButton title="رفع وحفظ الصورة" loading={saving} onPress={() => void save()} />
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

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  sourceRow: { flexDirection: "row-reverse", gap: spacing.sm },
  sourceButton: { flex: 1 },
  preview: { width: "100%", height: 300, borderRadius: radius.sm, backgroundColor: colors.background },
  fileName: { color: colors.muted, textAlign: "right", marginTop: spacing.sm },
  note: { color: colors.muted, textAlign: "right", lineHeight: 22 }
});
