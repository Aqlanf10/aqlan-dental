import { useSession } from "@/auth/SessionProvider";
import { FormField, SelectList } from "@/components/forms";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { isoDateLocal } from "@/lib/format";
import type {
  Appointment,
  AppointmentMutationInput,
  DoctorSummary,
  PaginatedResponse,
  PatientListItem
} from "@/lib/types";
import { colors, radius, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useEffect, useMemo, useState } from "react";
import { Pressable, StyleSheet, Text, TextInput, View } from "react-native";

export default function NewAppointmentScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ patientId?: string; patientName?: string }>();
  const fixedPatientId = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const fixedPatientName = Array.isArray(params.patientName)
    ? params.patientName[0]
    : params.patientName;

  const [patientId, setPatientId] = useState<string | null>(fixedPatientId ?? null);
  const [patientName, setPatientName] = useState(fixedPatientName ?? "");
  const [patientSearch, setPatientSearch] = useState("");
  const [patientResults, setPatientResults] = useState<PatientListItem[]>([]);
  const [doctors, setDoctors] = useState<DoctorSummary[]>([]);
  const [doctorId, setDoctorId] = useState<string | null>(user?.doctorId ?? null);
  const [date, setDate] = useState(() => isoDateLocal(new Date()));
  const [startTime, setStartTime] = useState("09:00");
  const [duration, setDuration] = useState("30");
  const [appointmentType, setAppointmentType] = useState("مراجعة");
  const [notes, setNotes] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [created, setCreated] = useState<Appointment | null>(null);

  useEffect(() => {
    let cancelled = false;
    async function loadDoctors() {
      try {
        const query = new URLSearchParams({ status: "active" });
        if (user?.role !== "Admin" && user?.branchId) query.set("branchId", user.branchId);
        const result = await apiRequest<DoctorSummary[]>(`/api/doctors?${query.toString()}`);
        if (!cancelled) setDoctors(result ?? []);
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : "تعذر تحميل الأطباء");
      }
    }
    void loadDoctors();
    return () => {
      cancelled = true;
    };
  }, [user?.branchId, user?.role]);

  useEffect(() => {
    if (fixedPatientId || patientSearch.trim().length < 2) {
      setPatientResults([]);
      return;
    }
    let cancelled = false;
    const handle = setTimeout(async () => {
      try {
        const query = new URLSearchParams({
          search: patientSearch.trim(),
          page: "1",
          pageSize: "10",
          status: "active"
        });
        const result = await apiRequest<PaginatedResponse<PatientListItem>>(
          `/api/patients?${query.toString()}`
        );
        if (!cancelled) setPatientResults(result.data ?? []);
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : "تعذر البحث عن المريض");
      }
    }, 300);
    return () => {
      cancelled = true;
      clearTimeout(handle);
    };
  }, [fixedPatientId, patientSearch]);

  const doctorOptions = useMemo(
    () =>
      doctors.map((doctor) => ({
        value: doctor.id,
        label: doctor.name,
        subtitle: doctor.specialty || doctor.branchName || null
      })),
    [doctors]
  );

  async function submit() {
    const minutes = Number.parseInt(duration, 10);
    if (!patientId) {
      setError("اختر المريض أولاً.");
      return;
    }
    if (!doctorId) {
      setError("اختر الطبيب.");
      return;
    }
    if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) {
      setError("التاريخ يجب أن يكون بصيغة YYYY-MM-DD.");
      return;
    }
    if (!/^([01]\d|2[0-3]):[0-5]\d$/.test(startTime)) {
      setError("الوقت يجب أن يكون بصيغة HH:mm مثل 09:30.");
      return;
    }
    if (!Number.isFinite(minutes) || minutes < 5 || minutes > 240) {
      setError("مدة الموعد يجب أن تكون بين 5 و240 دقيقة.");
      return;
    }
    if (!appointmentType.trim()) {
      setError("نوع الموعد مطلوب.");
      return;
    }

    const request: AppointmentMutationInput = {
      patientId,
      doctorId,
      appointmentDate: date,
      startTime,
      durationMinutes: minutes,
      appointmentType: appointmentType.trim(),
      notes: notes.trim() || null
    };

    setSubmitting(true);
    setError(null);
    try {
      const result = await apiRequest<Appointment>("/api/appointments", {
        method: "POST",
        body: JSON.stringify(request)
      });
      setCreated(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر إنشاء الموعد");
    } finally {
      setSubmitting(false);
    }
  }

  if (created) {
    return (
      <Screen>
        <SectionTitle>تم حجز الموعد</SectionTitle>
        <Card>
          <Text style={styles.successName}>{created.patientName || patientName}</Text>
          <Text style={styles.successMeta}>{created.appointmentDate}</Text>
          <Text style={styles.successMeta}>
            {created.startTime} – {created.endTime}
          </Text>
          <Text style={styles.successMeta}>د. {created.doctorName}</Text>
        </Card>
        <PrimaryButton
          title="عرض مواعيد اليوم"
          onPress={() =>
            router.replace({
              pathname: "/(app)/appointments",
              params: { patientId: created.patientId, patientName: created.patientName }
            })
          }
        />
      </Screen>
    );
  }

  return (
    <Screen>
      <SectionTitle>حجز موعد جديد</SectionTitle>
      {error ? <StateMessage title="تعذر الحجز" message={error} /> : null}

      {fixedPatientId ? (
        <Card>
          <Text style={styles.selectedLabel}>المريض</Text>
          <Text style={styles.selectedValue}>{patientName || fixedPatientId}</Text>
        </Card>
      ) : (
        <View style={{ gap: spacing.sm }}>
          <Text style={styles.fieldLabel}>المريض *</Text>
          {patientId ? (
            <Pressable
              onPress={() => {
                setPatientId(null);
                setPatientName("");
                setPatientSearch("");
              }}
              style={styles.selectedPatient}
            >
              <Text style={styles.selectedValue}>{patientName}</Text>
              <Text style={styles.change}>تغيير</Text>
            </Pressable>
          ) : (
            <>
              <TextInput
                value={patientSearch}
                onChangeText={setPatientSearch}
                placeholder="ابحث بالاسم أو رقم الملف أو الهاتف"
                placeholderTextColor={colors.muted}
                textAlign="right"
                style={styles.search}
              />
              {patientResults.map((patient) => (
                <Pressable
                  key={patient.id}
                  onPress={() => {
                    setPatientId(patient.id);
                    setPatientName(patient.fullName);
                    setPatientResults([]);
                  }}
                  style={styles.patientOption}
                >
                  <Text style={styles.optionName}>{patient.fullName}</Text>
                  <Text style={styles.optionMeta}>{patient.patientNumber}</Text>
                </Pressable>
              ))}
            </>
          )}
        </View>
      )}

      <SelectList
        label="الطبيب *"
        value={doctorId}
        options={doctorOptions}
        onChange={setDoctorId}
        emptyLabel="اختر الطبيب"
      />
      <FormField label="التاريخ *" value={date} onChangeText={setDate} placeholder="YYYY-MM-DD" />
      <FormField label="وقت البداية *" value={startTime} onChangeText={setStartTime} placeholder="09:00" />
      <FormField
        label="المدة بالدقائق *"
        value={duration}
        onChangeText={setDuration}
        keyboardType="number-pad"
      />
      <FormField label="نوع الموعد *" value={appointmentType} onChangeText={setAppointmentType} />
      <FormField label="ملاحظات" value={notes} onChangeText={setNotes} multiline />

      <Text style={styles.hint}>
        فحص التعارض النهائي يتم داخل معاملة الخادم عند الحفظ؛ لذلك لا يمكن للهاتف تجاوز قواعد منع الحجز المزدوج.
      </Text>
      <PrimaryButton
        title="تأكيد الحجز"
        loading={submitting}
        disabled={submitting}
        onPress={() => void submit()}
      />
    </Screen>
  );
}

const styles = StyleSheet.create({
  fieldLabel: { color: colors.text, fontWeight: "700", textAlign: "right" },
  search: {
    minHeight: 48,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.sm,
    backgroundColor: colors.surface,
    color: colors.text,
    paddingHorizontal: spacing.md
  },
  patientOption: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.sm,
    backgroundColor: colors.surface,
    padding: spacing.md
  },
  optionName: { color: colors.text, fontWeight: "700", textAlign: "right" },
  optionMeta: { color: colors.muted, marginTop: 3, textAlign: "right" },
  selectedPatient: {
    flexDirection: "row-reverse",
    justifyContent: "space-between",
    alignItems: "center",
    borderWidth: 1,
    borderColor: colors.primary,
    borderRadius: radius.sm,
    backgroundColor: colors.primarySoft,
    padding: spacing.md
  },
  selectedLabel: { color: colors.muted, textAlign: "right" },
  selectedValue: { color: colors.text, fontWeight: "800", textAlign: "right" },
  change: { color: colors.primary, fontWeight: "700" },
  hint: { color: colors.muted, textAlign: "right", lineHeight: 21 },
  successName: { color: colors.text, fontSize: 20, fontWeight: "800", textAlign: "right" },
  successMeta: { color: colors.text, marginTop: spacing.sm, textAlign: "right" }
});
