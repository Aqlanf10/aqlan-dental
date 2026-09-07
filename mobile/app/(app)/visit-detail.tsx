import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { canAccessClinicalRecords, canWriteClinicalRecords } from "@/lib/roles";
import type { ClinicalVisit } from "@/lib/types";
import { colors, spacing } from "@/theme";
import { router, useFocusEffect, useLocalSearchParams } from "expo-router";
import React, { useCallback, useState } from "react";
import { ActivityIndicator, RefreshControl, StyleSheet, Text, View } from "react-native";

export default function VisitDetailScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ id?: string; patientName?: string }>();
  const id = Array.isArray(params.id) ? params.id[0] : params.id;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const [visit, setVisit] = useState<ClinicalVisit | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canRead = canAccessClinicalRecords(user);
  const canWrite = canWriteClinicalRecords(user);

  const load = useCallback(async () => {
    if (!id || !canRead) {
      setLoading(false);
      return;
    }

    setError(null);
    try {
      setVisit(await apiRequest<ClinicalVisit>(`/api/visits/${id}`));
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحميل الزيارة");
    } finally {
      setLoading(false);
    }
  }, [canRead, id]);

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
          message="تفاصيل الزيارة متاحة للطبيب أو مدير النظام فقط."
        />
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

  if (!visit) {
    return (
      <Screen>
        <StateMessage
          title="تعذر فتح الزيارة"
          message={error ?? "الزيارة غير موجودة أو لا تملك صلاحية الوصول إليها."}
          action={<PrimaryButton title="إعادة المحاولة" onPress={() => void load()} />}
        />
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
        <Text style={styles.title}>{patientName || "تفاصيل الزيارة"}</Text>
        <Text style={styles.date}>{visit.visitDate}</Text>
      </View>

      {canWrite && visit.isActive ? (
        <PrimaryButton
          title="تعديل الزيارة"
          onPress={() =>
            router.push({
              pathname: "/(app)/visit-editor",
              params: {
                id: visit.id,
                patientId: visit.patientId,
                patientName: patientName || ""
              }
            })
          }
        />
      ) : null}

      <SectionTitle>ملخص الزيارة</SectionTitle>
      <Card>
        <Row label="نوع الزيارة" value={visit.visitType || "—"} />
        <Row label="التخصص" value={specialtyLabel(visit.specialty)} />
        <Row label="الطبيب" value={visit.doctorName || "—"} />
        {visit.appointment ? (
          <Row
            label="الموعد المرتبط"
            value={`${visit.appointment.appointmentDate} ${visit.appointment.appointmentTime}`}
            last
          />
        ) : (
          <Row label="الموعد المرتبط" value="زيارة بدون موعد مرتبط" last />
        )}
      </Card>

      <ClinicalSection title="الشكوى الرئيسية" value={visit.chiefComplaint} />
      <ClinicalSection title="التشخيص" value={visit.diagnosis} />
      <ClinicalSection title="الملاحظات السريرية" value={visit.clinicalNotes} />
      <ClinicalSection title="العلاج المنفذ" value={visit.treatmentDone} />
      <ClinicalSection title="تعليمات المريض" value={visit.instructions} />
      <ClinicalSection title="خطة الزيارة القادمة" value={visit.nextVisitPlan} />

      <SectionTitle>المتابعة</SectionTitle>
      <Card>
        <Row label="تاريخ الزيارة القادمة" value={visit.nextVisitDate || "غير محدد"} />
        <Row label="حالة الزيارة" value={visit.isActive ? "نشطة" : "محذوفة"} last />
      </Card>
    </Screen>
  );
}

function ClinicalSection({ title, value }: { title: string; value?: string | null }) {
  if (!value?.trim()) return null;
  return (
    <>
      <SectionTitle>{title}</SectionTitle>
      <Card>
        <Text style={styles.paragraph}>{value}</Text>
      </Card>
    </>
  );
}

function Row({ label, value, last = false }: { label: string; value: string; last?: boolean }) {
  return (
    <View style={[styles.row, last && { borderBottomWidth: 0 }]}>
      <Text style={styles.value}>{value}</Text>
      <Text style={styles.label}>{label}</Text>
    </View>
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
      return value || "—";
  }
}

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 24, fontWeight: "800", textAlign: "right" },
  date: { color: colors.primary, marginTop: 4, fontWeight: "800", textAlign: "right" },
  row: {
    minHeight: 48,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    gap: spacing.md
  },
  label: { color: colors.muted, textAlign: "right" },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" },
  paragraph: { color: colors.text, textAlign: "right", lineHeight: 24 }
});
