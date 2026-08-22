import { useSession } from "@/auth/SessionProvider";
import { ChoiceRow, FormField, SelectList } from "@/components/forms";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { isoDateLocal } from "@/lib/format";
import {
  captureClinicalPhoto,
  formatFileSize,
  isImageMime,
  pickRadiographFile,
  uploadClinicalFile,
  XRAY_TYPES,
  type PickedClinicalFile
} from "@/lib/media";
import { canWriteClinicalRecords } from "@/lib/roles";
import type { DoctorSummary } from "@/lib/types";
import { colors, radius, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useEffect, useState } from "react";
import { Image, StyleSheet, Text, View } from "react-native";

export default function MediaXrayNewScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ patientId: string; patientName?: string; orthoCaseId?: string }>();
  const patientId = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const orthoCaseId = Array.isArray(params.orthoCaseId) ? params.orthoCaseId[0] : params.orthoCaseId;
  const canWrite = canWriteClinicalRecords(user);

  const [file, setFile] = useState<PickedClinicalFile | null>(null);
  const [xrayType, setXrayType] = useState("OPG");
  const [toothRelated, setToothRelated] = useState("");
  const [notes, setNotes] = useState("");
  const [xrayDate, setXrayDate] = useState(isoDateLocal(new Date()));
  const [doctorId, setDoctorId] = useState<string | null>(user?.doctorId ?? null);
  const [doctors, setDoctors] = useState<DoctorSummary[]>([]);
  const [selecting, setSelecting] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (user?.role !== "Admin") return;
    let active = true;
    void apiRequest<DoctorSummary[]>("/api/doctors?status=active")
      .then((items) => {
        if (active) setDoctors(items ?? []);
      })
      .catch((err) => {
        if (active) setError(err instanceof Error ? err.message : "تعذر تحميل الأطباء");
      });
    return () => {
      active = false;
    };
  }, [user?.role]);

  async function chooseFile() {
    if (selecting) return;
    setSelecting(true);
    setError(null);
    try {
      const selected = await pickRadiographFile();
      if (selected) setFile(selected);
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر اختيار ملف الأشعة");
    } finally {
      setSelecting(false);
    }
  }

  async function captureImage() {
    if (selecting) return;
    setSelecting(true);
    setError(null);
    try {
      const selected = await captureClinicalPhoto();
      if (selected) setFile(selected);
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر التقاط صورة الأشعة");
    } finally {
      setSelecting(false);
    }
  }

  async function save() {
    if (!patientId || saving) return;
    if (!file) {
      setError("اختر ملف الأشعة أولًا.");
      return;
    }
    if (!isIsoDate(xrayDate)) {
      setError("تاريخ الأشعة يجب أن يكون بصيغة YYYY-MM-DD.");
      return;
    }

    setSaving(true);
    setError(null);
    try {
      const uploaded = await uploadClinicalFile(file, patientId, "radiograph");
      await apiRequest("/api/radiographs", {
        method: "POST",
        body: JSON.stringify({
          patientId,
          fileUrl: uploaded.url,
          fileName: uploaded.originalName,
          fileSize: uploaded.size,
          mimeType: uploaded.contentType,
          xrayType,
          toothRelated: clean(toothRelated),
          notes: clean(notes),
          doctorId,
          xrayDate,
          orthoCaseId: orthoCaseId || null
        })
      });
      router.back();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر رفع الأشعة");
    } finally {
      setSaving(false);
    }
  }

  if (!canWrite) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="رفع الأشعة متاح للأدوار التي تملك ClinicalWrite فقط." />
      </Screen>
    );
  }

  return (
    <Screen>
      <View>
        <Text style={styles.title}>إضافة أشعة</Text>
        <Text style={styles.subtitle}>{patientName || "ملف المريض"}</Text>
      </View>

      {error ? <StateMessage title="تعذر رفع الأشعة" message={error} /> : null}

      <View style={styles.sourceRow}>
        <View style={styles.sourceButton}>
          <PrimaryButton title="اختيار صورة أو PDF" loading={selecting} onPress={() => void chooseFile()} />
        </View>
        <View style={styles.sourceButton}>
          <PrimaryButton title="التقاط بالكاميرا" disabled={selecting} onPress={() => void captureImage()} />
        </View>
      </View>

      {file ? (
        <Card>
          {isImageMime(file.mimeType) ? (
            <Image source={{ uri: file.uri }} style={styles.preview} resizeMode="contain" />
          ) : (
            <View style={styles.pdfBox}>
              <Text style={styles.pdfTitle}>PDF</Text>
            </View>
          )}
          <Text style={styles.fileName}>{file.name}</Text>
          {file.size ? <Text style={styles.fileMeta}>{formatFileSize(file.size)}</Text> : null}
        </Card>
      ) : null}

      <ChoiceRow
        label="نوع الأشعة"
        value={xrayType}
        options={XRAY_TYPES.map((item) => ({ ...item }))}
        onChange={(value) => {
          if (value) setXrayType(value);
        }}
      />
      <FormField
        label="السن/الأسنان المرتبطة"
        value={toothRelated}
        onChangeText={setToothRelated}
        placeholder="مثال: 36"
      />
      <FormField
        label="تاريخ الأشعة YYYY-MM-DD"
        value={xrayDate}
        onChangeText={setXrayDate}
        maxLength={10}
      />
      <FormField label="ملاحظات" value={notes} onChangeText={setNotes} multiline />

      {user?.role === "Admin" ? (
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
      ) : null}

      <Card>
        <Text style={styles.note}>
          المسموح JPG وPNG وWEBP وPDF حتى 10 MB. يرتبط الملف بالمريض أولًا في سجل الرفع ثم يُنشأ سجل الأشعة، ويُتحقق من ملكية حالة التقويم إذا تم تمريرها.
        </Text>
      </Card>

      <PrimaryButton title="رفع وحفظ الأشعة" loading={saving} onPress={() => void save()} />
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
  pdfBox: { height: 180, alignItems: "center", justifyContent: "center", backgroundColor: colors.background, borderRadius: radius.sm },
  pdfTitle: { color: colors.danger, fontSize: 36, fontWeight: "900" },
  fileName: { color: colors.text, textAlign: "right", marginTop: spacing.sm, fontWeight: "700" },
  fileMeta: { color: colors.muted, textAlign: "right", marginTop: 4 },
  note: { color: colors.muted, textAlign: "right", lineHeight: 22 }
});
