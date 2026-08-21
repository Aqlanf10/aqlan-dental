import { useSession } from "@/auth/SessionProvider";
import { FormField, SelectList } from "@/components/forms";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import { isoDateLocal } from "@/lib/format";
import { canWriteClinicalRecords } from "@/lib/roles";
import type { ClinicalVisit, DoctorSummary, VisitMutationInput } from "@/lib/types";
import { colors, spacing } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useEffect, useMemo, useState } from "react";
import { ActivityIndicator, StyleSheet, Text } from "react-native";

type CreateVisitResponse = { id: string; message?: string };

const specialtyOptions = [
  { value: "Orthodontics", label: "تقويم الأسنان" },
  { value: "GeneralDentistry", label: "طب الأسنان العام" },
  { value: "OralSurgery", label: "جراحة الفم" }
];

export default function VisitEditorScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ id?: string; patientId?: string; patientName?: string }>();
  const id = Array.isArray(params.id) ? params.id[0] : params.id;
  const patientIdParam = Array.isArray(params.patientId) ? params.patientId[0] : params.patientId;
  const patientName = Array.isArray(params.patientName) ? params.patientName[0] : params.patientName;
  const isEditing = Boolean(id);

  const [patientId, setPatientId] = useState(patientIdParam ?? "");
  const [doctors, setDoctors] = useState<DoctorSummary[]>([]);
  const [doctorId, setDoctorId] = useState<string | null>(user?.doctorId ?? null);
  const [visitDate, setVisitDate] = useState(isoDateLocal(new Date()));
  const [visitType, setVisitType] = useState("مراجعة");
  const [specialty, setSpecialty] = useState<string | null>(defaultSpecialty(user?.role));
  const [chiefComplaint, setChiefComplaint] = useState("");
  const [diagnosis, setDiagnosis] = useState("");
  const [clinicalNotes, setClinicalNotes] = useState("");
  const [treatmentDone, setTreatmentDone] = useState("");
  const [instructions, setInstructions] = useState("");
  const [nextVisitPlan, setNextVisitPlan] = useState("");
  const [nextVisitDate, setNextVisitDate] = useState("");
  const [loading, setLoading] = useState(isEditing);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canWrite = canWriteClinicalRecords(user);

  useEffect(() => {
    if (!canWrite) return;
    let cancelled = false;

    async function loadDoctors() {
      try {
        const query = new URLSearchParams({ status: "active" });
        if (user?.role !== "Admin" && user?.branchId) query.set("branchId", user.branchId);
        const result = await apiRequest<DoctorSummary[]>(`/api/doctors?${query.toString()}`);
        if (!cancelled) setDoctors(result ?? []);
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : "تعذر تحميل قائمة الأطباء");
      }
    }

    void loadDoctors();
    return () => {
      cancelled = true;
    };
  }, [canWrite, user?.branchId, user?.role]);

  useEffect(() => {
    if (!id || !canWrite) return;
    let cancelled = false;

    async function loadVisit() {
      setLoading(true);
      setError(null);
      try {
        const visit = await apiRequest<ClinicalVisit>(`/api/visits/${id}`);
        if (cancelled) return;
        setPatientId(visit.patientId);
        setDoctorId(visit.doctorId ?? null);
        setVisitDate(visit.visitDate);
        setVisitType(visit.visitType || "مراجعة");
        setSpecialty(visit.specialty ?? null);
        setChiefComplaint(visit.chiefComplaint || "");
        setDiagnosis(visit.diagnosis || "");
        setClinicalNotes(visit.clinicalNotes || "");
        setTreatmentDone(visit.treatmentDone || "");
        setInstructions(visit.instructions || "");
        setNextVisitPlan(visit.nextVisitPlan || "");
        setNextVisitDate(visit.nextVisitDate || "");
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : "تعذر تحميل الزيارة");
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    void loadVisit();
    return () => {
      cancelled = true;
    };
  }, [canWrite, id]);

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
    if (!patientId) {
      setError("معرّف المريض غير موجود.");
      return;
    }
    if (!/^\d{4}-\d{2}-\d{2}$/.test(visitDate)) {
      setError("تاريخ الزيارة يجب أن يكون بصيغة YYYY-MM-DD.");
      return;
    }
    if (nextVisitDate && !/^\d{4}-\d{2}-\d{2}$/.test(nextVisitDate)) {
      setError("تاريخ الزيارة القادمة يجب أن يكون بصيغة YYYY-MM-DD.");
      return;
    }
    if (!visitType.trim()) {
      setError("نوع الزيارة مطلوب.");
      return;
    }
    if (!specialty) {
      setError("اختر التخصص السريري.");
      return;
    }
    if (!doctorId) {
      setError("اختر الطبيب المسؤول عن الزيارة.");
      return;
    }

    const clinicalContentPresent = [
      chiefComplaint,
      diagnosis,
      clinicalNotes,
      treatmentDone,
      instructions,
      nextVisitPlan
    ].some((value) => value.trim().length > 0);

    if (!clinicalContentPresent) {
      setError("سجّل معلومة سريرية واحدة على الأقل قبل الحفظ.");
      return;
    }

    const request: VisitMutationInput = {
      visitDate,
      visitType: visitType.trim(),
      specialty,
      doctorId,
      chiefComplaint: nullable(chiefComplaint),
      diagnosis: nullable(diagnosis),
      clinicalNotes: nullable(clinicalNotes),
      treatmentDone: nullable(treatmentDone),
      instructions: nullable(instructions),
      nextVisitPlan: nullable(nextVisitPlan),
      nextVisitDate: nextVisitDate || null
    };

    setSubmitting(true);
    setError(null);
    try {
      let savedId = id;
      if (id) {
        await apiRequest<unknown>(`/api/visits/${id}`, {
          method: "PUT",
          body: JSON.stringify(request)
        });
      } else {
        const created = await apiRequest<CreateVisitResponse>("/api/visits", {
          method: "POST",
          body: JSON.stringify({ ...request, patientId })
        });
        savedId = created.id;
      }

      if (!savedId) throw new Error("لم يُرجع الخادم معرّف الزيارة المحفوظة.");
      router.replace({
        pathname: "/(app)/visit-detail",
        params: { id: savedId, patientName: patientName || "" }
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر حفظ الزيارة");
    } finally {
      setSubmitting(false);
    }
  }

  if (!canWrite) {
    return (
      <Screen>
        <StateMessage
          title="لا تملك صلاحية تعديل السجل السريري"
          message="إضافة وتعديل الزيارات متاح للطبيب أو مدير النظام فقط."
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

  return (
    <Screen>
      <SectionTitle>{isEditing ? "تعديل الزيارة السريرية" : "إضافة زيارة سريرية"}</SectionTitle>
      {patientName ? (
        <Card>
          <Text style={styles.patientLabel}>المريض</Text>
          <Text style={styles.patientName}>{patientName}</Text>
        </Card>
      ) : null}
      {error ? <StateMessage title="تعذر الحفظ" message={error} /> : null}

      <FormField
        label="تاريخ الزيارة *"
        value={visitDate}
        onChangeText={setVisitDate}
        placeholder="YYYY-MM-DD"
      />
      <FormField label="نوع الزيارة *" value={visitType} onChangeText={setVisitType} />
      <SelectList
        label="التخصص *"
        value={specialty}
        options={specialtyOptions}
        onChange={setSpecialty}
        emptyLabel="اختر التخصص"
      />
      <SelectList
        label="الطبيب *"
        value={doctorId}
        options={doctorOptions}
        onChange={setDoctorId}
        emptyLabel="اختر الطبيب"
      />

      <SectionTitle>المعلومات السريرية</SectionTitle>
      <FormField
        label="الشكوى الرئيسية"
        value={chiefComplaint}
        onChangeText={setChiefComplaint}
        multiline
      />
      <FormField label="التشخيص" value={diagnosis} onChangeText={setDiagnosis} multiline />
      <FormField
        label="الملاحظات السريرية"
        value={clinicalNotes}
        onChangeText={setClinicalNotes}
        multiline
      />
      <FormField
        label="العلاج المنفذ"
        value={treatmentDone}
        onChangeText={setTreatmentDone}
        multiline
      />
      <FormField
        label="تعليمات المريض"
        value={instructions}
        onChangeText={setInstructions}
        multiline
      />
      <FormField
        label="خطة الزيارة القادمة"
        value={nextVisitPlan}
        onChangeText={setNextVisitPlan}
        multiline
      />
      <FormField
        label="تاريخ الزيارة القادمة"
        value={nextVisitDate}
        onChangeText={setNextVisitDate}
        placeholder="YYYY-MM-DD"
      />

      <Text style={styles.hint}>
        الصلاحية النهائية والوصول للمريض يتحققان مرة أخرى داخل الخادم عند الحفظ.
      </Text>
      <PrimaryButton
        title={isEditing ? "حفظ التعديلات" : "حفظ الزيارة"}
        loading={submitting}
        disabled={submitting}
        onPress={() => void submit()}
      />
    </Screen>
  );
}

function nullable(value: string): string | null {
  const trimmed = value.trim();
  return trimmed || null;
}

function defaultSpecialty(role?: string): string | null {
  switch (role) {
    case "Orthodontist":
      return "Orthodontics";
    case "GeneralDentist":
      return "GeneralDentistry";
    case "OralSurgeon":
      return "OralSurgery";
    default:
      return null;
  }
}

const styles = StyleSheet.create({
  patientLabel: { color: colors.muted, textAlign: "right", fontSize: 12 },
  patientName: { color: colors.text, textAlign: "right", fontWeight: "800", fontSize: 18 },
  hint: { color: colors.muted, textAlign: "right", lineHeight: 21, marginTop: spacing.xs }
});
