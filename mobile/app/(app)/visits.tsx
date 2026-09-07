import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { canAccessClinicalRecords, canWriteClinicalRecords } from "@/lib/roles";
import type { ClinicalVisit, VisitListResponse } from "@/lib/types";
import { colors, radius, spacing } from "@/theme";
import { router, useFocusEffect, useLocalSearchParams } from "expo-router";
import React, { useCallback, useState } from "react";
import { ActivityIndicator, Pressable, RefreshControl, StyleSheet, Text, View } from "react-native";

export default function VisitsScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ patientId?: string; patientName?: string }>();
  const patientId = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const [visits, setVisits] = useState<ClinicalVisit[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canRead = canAccessClinicalRecords(user);
  const canWrite = canWriteClinicalRecords(user);

  const load = useCallback(async () => {
    if (!patientId || !canRead) {
      setLoading(false);
      return;
    }

    setError(null);
    try {
      const response = await apiRequest<VisitListResponse>(
        `/api/visits?patientId=${encodeURIComponent(patientId)}&page=1&pageSize=100`
      );
      setVisits(response.data ?? []);
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحميل الزيارات السريرية");
    } finally {
      setLoading(false);
    }
  }, [canRead, patientId]);

  useFocusEffect(
    useCallback(() => {
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

  if (!canRead) {
    return (
      <Screen>
        <StateMessage
          title="السجل السريري غير متاح لهذا الحساب"
          message="الزيارات السريرية متاحة للطبيب أو مدير النظام فقط."
        />
      </Screen>
    );
  }

  if (!patientId) {
    return (
      <Screen>
        <StateMessage title="تعذر فتح السجل السريري" message="معرّف المريض غير موجود." />
      </Screen>
    );
  }

  return (
    <Screen
      refreshControl={
        <RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />
      }
    >
      <View>
        <Text style={styles.title}>الزيارات السريرية</Text>
        <Text style={styles.patient}>{patientName || "المريض"}</Text>
      </View>

      {canWrite ? (
        <PrimaryButton
          title="إضافة زيارة سريرية"
          onPress={() =>
            router.push({
              pathname: "/(app)/visit-editor",
              params: { patientId, patientName: patientName || "" }
            })
          }
        />
      ) : null}

      <SectionTitle>السجل</SectionTitle>

      {loading ? <ActivityIndicator size="large" color={colors.primary} /> : null}
      {error ? <StateMessage title="تعذر تحميل الزيارات" message={error} /> : null}

      {!loading && !error && visits.length === 0 ? (
        <Card>
          <Text style={styles.empty}>لا توجد زيارات سريرية مسجلة لهذا المريض بعد.</Text>
        </Card>
      ) : null}

      <View style={styles.list}>
        {visits.map((visit) => (
          <VisitCard key={visit.id} visit={visit} patientName={patientName || ""} />
        ))}
      </View>
    </Screen>
  );
}

function VisitCard({ visit, patientName }: { visit: ClinicalVisit; patientName: string }) {
  return (
    <Pressable
      accessibilityRole="button"
      onPress={() =>
        router.push({
          pathname: "/(app)/visit-detail",
          params: { id: visit.id, patientName }
        })
      }
      style={({ pressed }) => [styles.card, pressed && { opacity: 0.82 }]}
    >
      <View style={styles.cardTop}>
        <Text style={styles.doctor}>{visit.doctorName || "طبيب غير محدد"}</Text>
        <Text style={styles.date}>{visit.visitDate}</Text>
      </View>
      <Text style={styles.kind}>{visit.visitType || specialtyLabel(visit.specialty)}</Text>
      {visit.chiefComplaint ? (
        <Text numberOfLines={2} style={styles.summary}>
          الشكوى: {visit.chiefComplaint}
        </Text>
      ) : null}
      {visit.diagnosis ? (
        <Text numberOfLines={2} style={styles.summary}>
          التشخيص: {visit.diagnosis}
        </Text>
      ) : null}
      {visit.treatmentDone ? (
        <Text numberOfLines={2} style={styles.summary}>
          العلاج: {visit.treatmentDone}
        </Text>
      ) : null}
      {visit.nextVisitDate ? (
        <Text style={styles.next}>الزيارة القادمة: {visit.nextVisitDate}</Text>
      ) : null}
    </Pressable>
  );
}

function specialtyLabel(value?: string | null): string {
  switch (value) {
    case "Orthodontics":
      return "تقويم الأسنان";
    case "GeneralDentistry":
      return "طب الأسنان العام";
    case "OralSurgery":
      return "جراحة الفم";
    default:
      return value || "زيارة سريرية";
  }
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  patient: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  empty: { color: colors.muted, textAlign: "center", lineHeight: 22 },
  list: { gap: spacing.sm },
  card: {
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.surface,
    borderRadius: radius.md,
    padding: spacing.md,
    gap: spacing.xs
  },
  cardTop: {
    flexDirection: "row-reverse",
    alignItems: "center",
    justifyContent: "space-between",
    gap: spacing.sm
  },
  doctor: { color: colors.text, fontWeight: "800", flex: 1, textAlign: "right" },
  date: { color: colors.primary, fontWeight: "800" },
  kind: { color: colors.muted, textAlign: "right" },
  summary: { color: colors.text, textAlign: "right", lineHeight: 21 },
  next: {
    color: colors.primary,
    backgroundColor: colors.primarySoft,
    borderRadius: radius.sm,
    padding: spacing.xs,
    marginTop: spacing.xs,
    textAlign: "right",
    fontWeight: "700"
  }
});
