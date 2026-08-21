import { useSession } from "@/auth/SessionProvider";
import { PatientEditor } from "@/components/PatientEditor";
import { Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import type { PatientMutationInput, PatientProfile } from "@/lib/types";
import { colors } from "@/theme";
import { router, useLocalSearchParams } from "expo-router";
import React, { useEffect, useState } from "react";
import { ActivityIndicator } from "react-native";

export default function EditPatientScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ id?: string }>();
  const id = Array.isArray(params.id) ? params.id[0] : params.id;
  const [patient, setPatient] = useState<PatientProfile | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const canEdit = user?.role === "Admin" || user?.role === "Reception";

  useEffect(() => {
    let cancelled = false;
    async function load() {
      if (!id) {
        setError("معرّف المريض غير موجود.");
        setLoading(false);
        return;
      }
      try {
        const result = await apiRequest<PatientProfile>(`/api/patients/${id}`);
        if (!cancelled) setPatient(result);
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : "تعذر تحميل المريض");
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    void load();
    return () => {
      cancelled = true;
    };
  }, [id]);

  if (!canEdit) {
    return (
      <Screen>
        <StateMessage
          title="غير مصرح"
          message="تعديل بيانات المريض متاح للمدير والاستقبال وفق سياسة الخادم."
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

  if (!patient || !id) {
    return (
      <Screen>
        <StateMessage title="تعذر فتح المريض" message={error ?? "المريض غير موجود"} />
      </Screen>
    );
  }

  async function save(input: PatientMutationInput) {
    await apiRequest<PatientProfile>(`/api/patients/${id}`, {
      method: "PUT",
      body: JSON.stringify(input)
    });
    router.replace({ pathname: "/(app)/patients/[id]", params: { id } });
  }

  return (
    <Screen>
      <SectionTitle>تعديل بيانات المريض</SectionTitle>
      <PatientEditor initial={patient} submitLabel="حفظ التعديلات" onSubmit={save} />
    </Screen>
  );
}
