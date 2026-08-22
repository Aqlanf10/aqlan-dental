import { FormField, SelectList } from "@/components/forms";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { formatFileSize, pickClinicalDocument, uploadClinicalFile, type PickedClinicalFile } from "@/lib/media";
import { DOCUMENT_TYPE_OPTIONS } from "@/lib/records";
import { colors, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useState } from "react";
import { StyleSheet, Text, View } from "react-native";

export default function DocumentNewScreen() {
  const params = useLocalSearchParams<{ patientId: string; patientName?: string; orthoCaseId?: string }>();
  const patientId = first(params.patientId);
  const patientName = first(params.patientName);
  const orthoCaseId = first(params.orthoCaseId);
  const [title, setTitle] = useState("");
  const [documentType, setDocumentType] = useState<string | null>("other");
  const [notes, setNotes] = useState("");
  const [file, setFile] = useState<PickedClinicalFile | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function chooseFile() {
    try {
      setError(null);
      const picked = await pickClinicalDocument();
      if (picked) {
        setFile(picked);
        if (!title.trim()) setTitle(picked.name.replace(/\.[^.]+$/, ""));
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر اختيار الملف");
    }
  }

  async function save() {
    if (!patientId) return setError("معرّف المريض مفقود.");
    if (!title.trim()) return setError("عنوان المستند مطلوب.");
    setSaving(true);
    setError(null);
    try {
      const upload = file ? await uploadClinicalFile(file, patientId, "document") : null;
      await apiRequest("/api/documents", {
        method: "POST",
        body: JSON.stringify({
          patientId,
          title: title.trim(),
          documentType: documentType || null,
          fileUrl: upload?.url ?? null,
          fileName: upload?.originalName ?? null,
          fileSize: upload?.size ?? null,
          mimeType: upload?.contentType ?? null,
          notes: notes.trim() || null,
          orthoCaseId: orthoCaseId || null
        })
      });
      router.back();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر حفظ المستند");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Screen>
      <View><Text style={styles.title}>إضافة مستند</Text><Text style={styles.subtitle}>{patientName || "ملف المريض"}</Text></View>
      {error ? <StateMessage title="تعذر حفظ المستند" message={error} /> : null}
      <Card>
        <View style={styles.form}>
          <FormField label="عنوان المستند *" value={title} onChangeText={setTitle} />
          <SelectList label="نوع المستند" value={documentType} onChange={setDocumentType} emptyLabel="بدون تصنيف" options={DOCUMENT_TYPE_OPTIONS.map((item) => ({ label: item.label, value: item.value }))} />
          <FormField label="ملاحظات" value={notes} onChangeText={setNotes} multiline />
        </View>
      </Card>
      {file ? (
        <Card>
          <Text style={styles.fileName}>{file.name}</Text>
          <Text style={styles.meta}>{file.mimeType}{file.size ? ` • ${formatFileSize(file.size)}` : ""}</Text>
          <PrimaryButton title="اختيار ملف آخر" onPress={() => void chooseFile()} />
        </Card>
      ) : <PrimaryButton title="اختيار ملف PDF أو صورة" onPress={() => void chooseFile()} />}
      <Text style={styles.note}>يمكن حفظ سجل مستند بدون ملف، أو إرفاق JPG/PNG/WEBP/PDF حتى 10MB.</Text>
      <PrimaryButton title="حفظ المستند" loading={saving} disabled={saving} onPress={() => void save()} />
    </Screen>
  );
}

function first(value?: string | string[]): string { return Array.isArray(value) ? value[0] ?? "" : value ?? ""; }
const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  form: { gap: spacing.md },
  fileName: { color: colors.text, fontWeight: "800", textAlign: "right" },
  meta: { color: colors.muted, marginTop: 4, textAlign: "right" },
  note: { color: colors.muted, fontSize: 12, lineHeight: 20, textAlign: "right" }
});
