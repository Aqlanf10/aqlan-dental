import { useSession } from "@/auth/SessionProvider";
import { ChoiceRow, FormField, SelectList } from "@/components/forms";
import { PrimaryButton, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import type { DoctorSummary, PatientMutationInput, PatientProfile } from "@/lib/types";
import { spacing } from "@/theme";
import React, { useEffect, useMemo, useState } from "react";
import { View } from "react-native";

function nullable(value: string): string | null {
  const trimmed = value.trim();
  return trimmed ? trimmed : null;
}

export function PatientEditor({
  initial,
  submitLabel,
  onSubmit
}: {
  initial?: PatientProfile | null;
  submitLabel: string;
  onSubmit: (input: PatientMutationInput) => Promise<void>;
}) {
  const { user } = useSession();
  const [firstName, setFirstName] = useState(initial?.firstName ?? "");
  const [middleName, setMiddleName] = useState(initial?.middleName ?? "");
  const [lastName, setLastName] = useState(initial?.lastName ?? "");
  const [dateOfBirth, setDateOfBirth] = useState(initial?.dateOfBirth ?? "");
  const [gender, setGender] = useState<string | null>(initial?.gender ?? null);
  const [phone, setPhone] = useState(initial?.phone ?? "");
  const [whatsApp, setWhatsApp] = useState(initial?.whatsApp ?? "");
  const [email, setEmail] = useState(initial?.email ?? "");
  const [address, setAddress] = useState(initial?.address ?? "");
  const [occupation, setOccupation] = useState(initial?.occupation ?? "");
  const [referralSource, setReferralSource] = useState(initial?.referralSource ?? "");
  const [primaryDoctorId, setPrimaryDoctorId] = useState<string | null>(
    initial?.primaryDoctorId ?? null
  );
  const [doctors, setDoctors] = useState<DoctorSummary[]>([]);
  const [doctorsError, setDoctorsError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    let cancelled = false;
    async function loadDoctors() {
      try {
        const params = new URLSearchParams({ status: "active" });
        if (user?.role !== "Admin" && user?.branchId) params.set("branchId", user.branchId);
        const result = await apiRequest<DoctorSummary[]>(`/api/doctors?${params.toString()}`);
        if (!cancelled) {
          setDoctors(result ?? []);
          setDoctorsError(null);
        }
      } catch (err) {
        if (!cancelled) {
          setDoctorsError(err instanceof Error ? err.message : "تعذر تحميل قائمة الأطباء");
        }
      }
    }
    void loadDoctors();
    return () => {
      cancelled = true;
    };
  }, [user?.branchId, user?.role]);

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
    if (!firstName.trim() || !lastName.trim()) {
      setError("الاسم الأول واسم العائلة مطلوبان.");
      return;
    }
    if (dateOfBirth.trim() && !/^\d{4}-\d{2}-\d{2}$/.test(dateOfBirth.trim())) {
      setError("تاريخ الميلاد يجب أن يكون بصيغة YYYY-MM-DD.");
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      await onSubmit({
        firstName: firstName.trim(),
        middleName: nullable(middleName),
        lastName: lastName.trim(),
        dateOfBirth: nullable(dateOfBirth),
        gender,
        phone: nullable(phone),
        whatsApp: nullable(whatsApp),
        email: email.trim(),
        address: nullable(address),
        occupation: nullable(occupation),
        referralSource: nullable(referralSource),
        primaryDoctorId,
        medicalHistory: initial?.medicalHistory ?? undefined,
        dentalHistory: initial?.dentalHistory ?? undefined
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر حفظ بيانات المريض");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <View style={{ gap: spacing.md }}>
      {error ? <StateMessage title="تعذر الحفظ" message={error} /> : null}

      <FormField label="الاسم الأول *" value={firstName} onChangeText={setFirstName} />
      <FormField label="الاسم الأوسط" value={middleName} onChangeText={setMiddleName} />
      <FormField label="اسم العائلة *" value={lastName} onChangeText={setLastName} />
      <FormField
        label="تاريخ الميلاد"
        value={dateOfBirth}
        onChangeText={setDateOfBirth}
        placeholder="YYYY-MM-DD"
        keyboardType="numbers-and-punctuation"
      />
      <ChoiceRow
        label="الجنس"
        value={gender}
        options={[
          { label: "ذكر", value: "Male" },
          { label: "أنثى", value: "Female" }
        ]}
        onChange={setGender}
      />
      <FormField label="الهاتف" value={phone} onChangeText={setPhone} keyboardType="phone-pad" />
      <FormField
        label="واتساب"
        value={whatsApp}
        onChangeText={setWhatsApp}
        keyboardType="phone-pad"
      />
      <FormField
        label="البريد الإلكتروني"
        value={email}
        onChangeText={setEmail}
        keyboardType="email-address"
        autoCapitalize="none"
      />
      <FormField label="العنوان" value={address} onChangeText={setAddress} multiline />
      <FormField label="المهنة" value={occupation} onChangeText={setOccupation} />
      <FormField label="مصدر الإحالة" value={referralSource} onChangeText={setReferralSource} />

      {doctorsError ? (
        <StateMessage title="تعذر تحميل الأطباء" message={doctorsError} />
      ) : (
        <SelectList
          label="الطبيب الأساسي"
          value={primaryDoctorId}
          options={doctorOptions}
          onChange={setPrimaryDoctorId}
          emptyLabel="بدون طبيب أساسي"
        />
      )}

      <PrimaryButton
        title={submitLabel}
        loading={submitting}
        disabled={submitting}
        onPress={() => void submit()}
      />
    </View>
  );
}
