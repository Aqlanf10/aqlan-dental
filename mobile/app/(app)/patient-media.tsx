import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiAssetUrl, apiRequest } from "@/lib/api";
import {
  formatFileSize,
  isImageMime,
  PHOTO_CATEGORY_LABELS,
  PHOTO_STAGE_LABELS,
  XRAY_TYPE_LABELS,
  type ClinicalPhotoItem,
  type RadiographItem
} from "@/lib/media";
import { canAccessClinicalRecords, canWriteClinicalRecords } from "@/lib/roles";
import { colors, radius, spacing } from "@/theme";
import { router, useFocusEffect, useLocalSearchParams } from "expo-router";
import React, { useCallback, useState } from "react";
import {
  ActivityIndicator,
  Alert,
  Image,
  Linking,
  Pressable,
  RefreshControl,
  StyleSheet,
  Text,
  View
} from "react-native";

type MediaTab = "photos" | "xrays";

export default function PatientMediaScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ patientId: string; patientName?: string; orthoCaseId?: string }>();
  const patientId = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const orthoCaseId = Array.isArray(params.orthoCaseId) ? params.orthoCaseId[0] : params.orthoCaseId;
  const canRead = canAccessClinicalRecords(user);
  const canWrite = canWriteClinicalRecords(user);

  const [tab, setTab] = useState<MediaTab>("photos");
  const [photos, setPhotos] = useState<ClinicalPhotoItem[]>([]);
  const [xrays, setXrays] = useState<RadiographItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!patientId || !canRead) {
      setLoading(false);
      return;
    }
    setError(null);
    const suffix = orthoCaseId ? `?orthoCaseId=${encodeURIComponent(orthoCaseId)}` : "";
    const results = await Promise.allSettled([
      apiRequest<ClinicalPhotoItem[]>(`/api/clinical-photos/${patientId}${suffix}`),
      apiRequest<RadiographItem[]>(`/api/radiographs/${patientId}${suffix}`)
    ]);
    const [photoResult, xrayResult] = results;
    const errors: string[] = [];
    if (photoResult.status === "fulfilled") setPhotos(photoResult.value ?? []);
    else errors.push("الصور السريرية");
    if (xrayResult.status === "fulfilled") setXrays(xrayResult.value ?? []);
    else errors.push("الأشعة");
    if (errors.length) setError(`تعذر تحميل: ${errors.join("، ")}.`);
    setLoading(false);
  }, [canRead, orthoCaseId, patientId]);

  useFocusEffect(
    useCallback(() => {
      setLoading(true);
      void load();
    }, [load])
  );

  async function refresh() {
    setRefreshing(true);
    try {
      await load();
    } finally {
      setRefreshing(false);
    }
  }

  function confirmDelete(kind: "photo" | "xray", id: string) {
    Alert.alert(
      kind === "photo" ? "حذف الصورة" : "حذف الأشعة",
      "سيتم إخفاء السجل سريريًا (حذف منطقي). هل تريد المتابعة؟",
      [
        { text: "إلغاء", style: "cancel" },
        { text: "حذف", style: "destructive", onPress: () => void deleteItem(kind, id) }
      ]
    );
  }

  async function deleteItem(kind: "photo" | "xray", id: string) {
    if (!canWrite || busyId) return;
    setBusyId(id);
    setError(null);
    try {
      await apiRequest(kind === "photo" ? `/api/clinical-photos/${id}` : `/api/radiographs/${id}`, {
        method: "DELETE"
      });
      if (kind === "photo") setPhotos((current) => current.filter((item) => item.id !== id));
      else setXrays((current) => current.filter((item) => item.id !== id));
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر حذف الملف السريري");
    } finally {
      setBusyId(null);
    }
  }

  async function openAsset(path: string) {
    try {
      await Linking.openURL(apiAssetUrl(path));
    } catch {
      setError("تعذر فتح الملف على هذا الجهاز.");
    }
  }

  if (!canRead) {
    return (
      <Screen>
        <StateMessage title="غير مصرح" message="الصور والأشعة متاحة للأدوار السريرية فقط." />
      </Screen>
    );
  }

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      <View>
        <Text style={styles.title}>الصور والأشعة</Text>
        <Text style={styles.subtitle}>{patientName || "ملف المريض"}</Text>
      </View>

      <View style={styles.tabs}>
        <Tab label={`الصور (${photos.length})`} selected={tab === "photos"} onPress={() => setTab("photos")} />
        <Tab label={`الأشعة (${xrays.length})`} selected={tab === "xrays"} onPress={() => setTab("xrays")} />
      </View>

      {error ? <StateMessage title="تنبيه" message={error} /> : null}
      {loading ? <ActivityIndicator size="large" color={colors.primary} /> : null}

      {tab === "photos" ? (
        <>
          <SectionTitle>الصور السريرية</SectionTitle>
          {canWrite ? (
            <PrimaryButton
              title="إضافة صورة سريرية"
              onPress={() =>
                router.push({
                  pathname: "/(app)/media-photo-new",
                  params: { patientId, patientName, ...(orthoCaseId ? { orthoCaseId } : {}) }
                })
              }
            />
          ) : null}
          {!loading && photos.length === 0 ? <StateMessage title="لا توجد صور سريرية" /> : null}
          <View style={styles.grid}>
            {photos.map((item) => (
              <Card key={item.id} style={styles.mediaCard}>
                <Pressable onPress={() => void openAsset(item.fileUrl)}>
                  <Image
                    source={{ uri: apiAssetUrl(item.thumbnailUrl || item.fileUrl) }}
                    style={styles.preview}
                    resizeMode="cover"
                  />
                </Pressable>
                <Text style={styles.itemTitle}>{PHOTO_CATEGORY_LABELS[item.category] ?? item.category}</Text>
                <Text style={styles.meta}>
                  {item.photoDate}{item.stage ? ` • ${PHOTO_STAGE_LABELS[item.stage] ?? item.stage}` : ""}
                </Text>
                {item.photoType ? <Text style={styles.body}>{item.photoType}</Text> : null}
                {item.notes ? <Text style={styles.body}>{item.notes}</Text> : null}
                {canWrite ? (
                  <DangerAction
                    title={busyId === item.id ? "جارٍ الحذف..." : "حذف الصورة"}
                    disabled={busyId !== null}
                    onPress={() => confirmDelete("photo", item.id)}
                  />
                ) : null}
              </Card>
            ))}
          </View>
        </>
      ) : (
        <>
          <SectionTitle>الأشعة</SectionTitle>
          {canWrite ? (
            <PrimaryButton
              title="إضافة أشعة"
              onPress={() =>
                router.push({
                  pathname: "/(app)/media-xray-new",
                  params: { patientId, patientName, ...(orthoCaseId ? { orthoCaseId } : {}) }
                })
              }
            />
          ) : null}
          {!loading && xrays.length === 0 ? <StateMessage title="لا توجد أشعة" /> : null}
          {xrays.map((item) => (
            <Card key={item.id}>
              {isImageMime(item.mimeType) ? (
                <Pressable onPress={() => void openAsset(item.fileUrl)}>
                  <Image source={{ uri: apiAssetUrl(item.fileUrl) }} style={styles.xrayPreview} resizeMode="contain" />
                </Pressable>
              ) : (
                <Pressable style={styles.pdfBox} onPress={() => void openAsset(item.fileUrl)}>
                  <Text style={styles.pdfTitle}>PDF</Text>
                  <Text style={styles.pdfLink}>فتح ملف الأشعة</Text>
                </Pressable>
              )}
              <Text style={styles.itemTitle}>{XRAY_TYPE_LABELS[item.xrayType] ?? item.xrayType}</Text>
              <Text style={styles.meta}>
                {item.xrayDate}{item.doctorName ? ` • د. ${item.doctorName}` : ""}
              </Text>
              {item.fileName ? <Text style={styles.body}>{item.fileName}</Text> : null}
              {item.fileSize ? <Text style={styles.meta}>{formatFileSize(item.fileSize)}</Text> : null}
              {item.toothRelated ? <Text style={styles.body}>سن/أسنان: {item.toothRelated}</Text> : null}
              {item.notes ? <Text style={styles.body}>{item.notes}</Text> : null}
              {canWrite ? (
                <DangerAction
                  title={busyId === item.id ? "جارٍ الحذف..." : "حذف الأشعة"}
                  disabled={busyId !== null}
                  onPress={() => confirmDelete("xray", item.id)}
                />
              ) : null}
            </Card>
          ))}
        </>
      )}
    </Screen>
  );
}

function Tab({ label, selected, onPress }: { label: string; selected: boolean; onPress: () => void }) {
  return (
    <Pressable onPress={onPress} style={[styles.tab, selected && styles.tabSelected]}>
      <Text style={[styles.tabText, selected && styles.tabTextSelected]}>{label}</Text>
    </Pressable>
  );
}

function DangerAction({
  title,
  onPress,
  disabled
}: {
  title: string;
  onPress: () => void;
  disabled: boolean;
}) {
  return (
    <Pressable disabled={disabled} onPress={onPress} style={[styles.danger, disabled && styles.disabled]}>
      <Text style={styles.dangerText}>{title}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  tabs: { flexDirection: "row-reverse", gap: spacing.sm },
  tab: {
    flex: 1,
    minHeight: 44,
    alignItems: "center",
    justifyContent: "center",
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.sm,
    backgroundColor: colors.surface
  },
  tabSelected: { borderColor: colors.primary, backgroundColor: colors.primarySoft },
  tabText: { color: colors.muted, fontWeight: "700" },
  tabTextSelected: { color: colors.primary },
  grid: { gap: spacing.md },
  mediaCard: { overflow: "hidden" },
  preview: { width: "100%", height: 210, borderRadius: radius.sm, backgroundColor: colors.background },
  xrayPreview: { width: "100%", height: 240, borderRadius: radius.sm, backgroundColor: colors.background },
  itemTitle: { color: colors.text, fontSize: 16, fontWeight: "800", textAlign: "right", marginTop: spacing.sm },
  meta: { color: colors.muted, fontSize: 12, textAlign: "right", marginTop: 4 },
  body: { color: colors.text, textAlign: "right", marginTop: 5, lineHeight: 21 },
  pdfBox: {
    height: 120,
    alignItems: "center",
    justifyContent: "center",
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.sm,
    backgroundColor: colors.background
  },
  pdfTitle: { color: colors.danger, fontSize: 28, fontWeight: "900" },
  pdfLink: { color: colors.primary, marginTop: 5, fontWeight: "700" },
  danger: {
    minHeight: 40,
    alignItems: "center",
    justifyContent: "center",
    borderWidth: 1,
    borderColor: colors.danger,
    borderRadius: radius.sm,
    backgroundColor: colors.dangerSoft,
    marginTop: spacing.md
  },
  dangerText: { color: colors.danger, fontWeight: "800" },
  disabled: { opacity: 0.5 }
});
