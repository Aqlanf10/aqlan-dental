import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiAssetUrl, apiRequest } from "@/lib/api";
import { canAccessClinicalRecords } from "@/lib/roles";
import {
  DOCUMENT_TYPE_LABELS,
  REFERRAL_PRIORITY_LABELS,
  REFERRAL_STATUS_LABELS,
  type InternalReferral,
  type PatientDocument,
  type PatientDocumentList,
  type PrescriptionList,
  type PrescriptionListItem,
  type ReferralList
} from "@/lib/records";
import { colors, radius, spacing } from "@/theme";
import { router, useFocusEffect, useLocalSearchParams } from "expo-router";
import React, { useCallback, useState } from "react";
import { ActivityIndicator, Alert, Linking, Pressable, RefreshControl, StyleSheet, Text, View } from "react-native";

type Tab = "documents" | "prescriptions" | "referrals";

export default function PatientRecordsScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ patientId: string; patientName?: string; orthoCaseId?: string }>();
  const patientId = first(params.patientId);
  const patientName = first(params.patientName);
  const orthoCaseId = first(params.orthoCaseId);
  const allowed = canAccessClinicalRecords(user);
  const [tab, setTab] = useState<Tab>("documents");
  const [documents, setDocuments] = useState<PatientDocument[]>([]);
  const [prescriptions, setPrescriptions] = useState<PrescriptionListItem[]>([]);
  const [referrals, setReferrals] = useState<InternalReferral[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [actingId, setActingId] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!allowed || !patientId) {
      setLoading(false);
      return;
    }
    setError(null);
    const docQuery = new URLSearchParams({ patientId, page: "1", pageSize: "100" });
    if (orthoCaseId) docQuery.set("orthoCaseId", orthoCaseId);
    const [docResult, rxResult, referralResult] = await Promise.allSettled([
      apiRequest<PatientDocumentList>(`/api/documents?${docQuery.toString()}`),
      apiRequest<PrescriptionList>(`/api/prescriptions?patientId=${encodeURIComponent(patientId)}&page=1&pageSize=100`),
      apiRequest<ReferralList>(`/api/referrals?patientId=${encodeURIComponent(patientId)}&page=1&pageSize=100`)
    ]);

    if (docResult.status === "fulfilled") setDocuments(docResult.value.data ?? []); else setDocuments([]);
    if (rxResult.status === "fulfilled") setPrescriptions(rxResult.value.data ?? []); else setPrescriptions([]);
    if (referralResult.status === "fulfilled") setReferrals(referralResult.value.data ?? []); else setReferrals([]);

    const rejected = [docResult, rxResult, referralResult].find((result) => result.status === "rejected") as PromiseRejectedResult | undefined;
    if (rejected) setError(rejected.reason instanceof Error ? rejected.reason.message : "تعذر تحميل بعض السجلات");
    setLoading(false);
  }, [allowed, orthoCaseId, patientId]);

  useFocusEffect(useCallback(() => { setLoading(true); void load(); }, [load]));

  async function refresh() {
    setRefreshing(true);
    try { await load(); } finally { setRefreshing(false); }
  }

  async function openDocument(doc: PatientDocument) {
    if (!doc.fileUrl) return Alert.alert("لا يوجد ملف", "هذا السجل لا يحتوي ملفًا مرفوعًا.");
    const url = apiAssetUrl(doc.fileUrl);
    const supported = await Linking.canOpenURL(url);
    if (!supported) return Alert.alert("تعذر فتح الملف", "لا يوجد تطبيق قادر على فتح هذا الملف.");
    await Linking.openURL(url);
  }

  async function markSigned(doc: PatientDocument) {
    if (doc.signed || actingId) return;
    setActingId(doc.id);
    setError(null);
    try {
      await apiRequest(`/api/documents/${doc.id}`, { method: "PUT", body: JSON.stringify({ signed: true }) });
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر توقيع المستند");
    } finally {
      setActingId(null);
    }
  }

  async function referralAction(referral: InternalReferral, action: "accept" | "complete") {
    if (actingId) return;
    setActingId(referral.id);
    setError(null);
    try {
      await apiRequest(`/api/referrals/${referral.id}/${action}`, { method: "PUT" });
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحديث الإحالة");
    } finally {
      setActingId(null);
    }
  }

  if (!allowed) return <Screen><StateMessage title="غير مصرح" message="السجلات الطبية الإضافية متاحة للحسابات السريرية المصرح لها." /></Screen>;

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      <View>
        <Text style={styles.title}>السجلات الطبية</Text>
        <Text style={styles.subtitle}>{patientName || "ملف المريض"}</Text>
      </View>
      {error ? <StateMessage title="تنبيه" message={error} /> : null}
      <View style={styles.tabs}>
        <TabButton label={`المستندات (${documents.length})`} active={tab === "documents"} onPress={() => setTab("documents")} />
        <TabButton label={`الوصفات (${prescriptions.length})`} active={tab === "prescriptions"} onPress={() => setTab("prescriptions")} />
        <TabButton label={`الإحالات (${referrals.length})`} active={tab === "referrals"} onPress={() => setTab("referrals")} />
      </View>
      {loading ? <ActivityIndicator size="large" color={colors.primary} /> : null}

      {tab === "documents" ? (
        <>
          <PrimaryButton title="إضافة مستند" onPress={() => router.push({ pathname: "/(app)/document-new", params: { patientId, patientName, ...(orthoCaseId ? { orthoCaseId } : {}) } })} />
          <SectionTitle>المستندات</SectionTitle>
          {!loading && documents.length === 0 ? <StateMessage title="لا توجد مستندات مسجلة" /> : null}
          {documents.map((doc) => (
            <Card key={doc.id}>
              <Text style={styles.cardTitle}>{doc.title || doc.fileName || "مستند"}</Text>
              <Text style={styles.meta}>{DOCUMENT_TYPE_LABELS[doc.documentType || ""] || doc.documentType || "غير مصنف"} • {doc.createdAt}</Text>
              {doc.notes ? <Text style={styles.notes}>{doc.notes}</Text> : null}
              <Row label="التوقيع" value={doc.signed ? `موقّع${doc.signedAt ? ` • ${doc.signedAt}` : ""}` : "غير موقّع"} />
              {doc.fileName ? <Row label="الملف" value={doc.fileName} last /> : null}
              {doc.fileUrl ? <PrimaryButton title="فتح الملف" onPress={() => void openDocument(doc)} /> : null}
              {!doc.signed ? <PrimaryButton title="تسجيل المستند كموقّع" loading={actingId === doc.id} onPress={() => void markSigned(doc)} /> : null}
            </Card>
          ))}
        </>
      ) : null}

      {tab === "prescriptions" ? (
        <>
          <PrimaryButton title="وصفة طبية جديدة" onPress={() => router.push({ pathname: "/(app)/prescription-new", params: { patientId, patientName } })} />
          <SectionTitle>الوصفات الطبية</SectionTitle>
          {!loading && prescriptions.length === 0 ? <StateMessage title="لا توجد وصفات مسجلة" /> : null}
          {prescriptions.map((rx) => (
            <Pressable key={rx.id} onPress={() => router.push({ pathname: "/(app)/prescription-detail", params: { id: rx.id } })}>
              <Card>
                <Text style={styles.cardTitle}>{rx.diagnosis || "وصفة طبية"}</Text>
                <Text style={styles.meta}>{rx.createdAt} • {rx.doctorName ? `د. ${rx.doctorName}` : "طبيب غير محدد"}</Text>
                <Row label="عدد الأدوية" value={String(rx.drugCount)} />
                {rx.notes ? <Text style={styles.notes}>{rx.notes}</Text> : null}
              </Card>
            </Pressable>
          ))}
        </>
      ) : null}

      {tab === "referrals" ? (
        <>
          <PrimaryButton title="إحالة داخلية جديدة" onPress={() => router.push({ pathname: "/(app)/referral-new", params: { patientId, patientName } })} />
          <SectionTitle>الإحالات الداخلية</SectionTitle>
          {!loading && referrals.length === 0 ? <StateMessage title="لا توجد إحالات مسجلة" /> : null}
          {referrals.map((referral) => (
            <Card key={referral.id}>
              <View style={styles.refHeader}>
                <Text style={styles.status}>{REFERRAL_STATUS_LABELS[referral.status] ?? referral.status}</Text>
                <Text style={styles.cardTitle}>{referral.fromDoctorName} ← {referral.toDoctorName}</Text>
              </View>
              <Text style={styles.meta}>{referral.createdAt} • {REFERRAL_PRIORITY_LABELS[referral.priority || ""] || referral.priority || "عادي"}</Text>
              {referral.reason ? <Row label="السبب" value={referral.reason} /> : null}
              {referral.notes ? <Text style={styles.notes}>{referral.notes}</Text> : null}
              {referral.status === "pending" ? (
                <PrimaryButton title="قبول الإحالة — للطبيب المستقبِل فقط" loading={actingId === referral.id} onPress={() => void referralAction(referral, "accept")} />
              ) : null}
              {referral.status === "accepted" ? (
                <PrimaryButton title="إكمال الإحالة — للطبيب المستقبِل فقط" loading={actingId === referral.id} onPress={() => void referralAction(referral, "complete")} />
              ) : null}
            </Card>
          ))}
          <Text style={styles.disclaimer}>الخادم هو المرجع النهائي: قبول وإكمال الإحالة مسموح للطبيب المستقبِل أو Admin فقط.</Text>
        </>
      ) : null}
    </Screen>
  );
}

function first(value?: string | string[]): string { return Array.isArray(value) ? value[0] ?? "" : value ?? ""; }
function TabButton({ label, active, onPress }: { label: string; active: boolean; onPress: () => void }) {
  return <Pressable onPress={onPress} style={[styles.tab, active && styles.tabActive]}><Text style={[styles.tabText, active && styles.tabTextActive]}>{label}</Text></Pressable>;
}
function Row({ label, value, last = false }: { label: string; value: string; last?: boolean }) {
  return <View style={[styles.row, last && { borderBottomWidth: 0 }]}><Text style={styles.value}>{value}</Text><Text style={styles.label}>{label}</Text></View>;
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  tabs: { flexDirection: "row-reverse", flexWrap: "wrap", gap: spacing.xs },
  tab: { borderWidth: 1, borderColor: colors.border, borderRadius: 999, paddingHorizontal: spacing.sm, paddingVertical: 8, backgroundColor: colors.surface },
  tabActive: { borderColor: colors.primary, backgroundColor: colors.primarySoft },
  tabText: { color: colors.muted, fontSize: 12, fontWeight: "700" },
  tabTextActive: { color: colors.primary },
  cardTitle: { color: colors.text, fontSize: 17, fontWeight: "800", textAlign: "right", flex: 1 },
  meta: { color: colors.muted, marginTop: 4, marginBottom: spacing.sm, textAlign: "right", fontSize: 12 },
  notes: { color: colors.text, backgroundColor: colors.background, borderRadius: radius.sm, padding: spacing.sm, textAlign: "right", lineHeight: 22, marginBottom: spacing.sm },
  row: { minHeight: 42, flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.border },
  label: { color: colors.muted, textAlign: "right" },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" },
  refHeader: { flexDirection: "row", alignItems: "flex-start", gap: spacing.sm },
  status: { color: colors.primary, backgroundColor: colors.primarySoft, borderRadius: radius.sm, paddingHorizontal: spacing.sm, paddingVertical: 5, fontSize: 11, fontWeight: "800" },
  disclaimer: { color: colors.muted, fontSize: 12, textAlign: "right", lineHeight: 20 }
});
