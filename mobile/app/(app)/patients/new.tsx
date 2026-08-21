import { useSession } from "@/auth/SessionProvider";
import { PatientEditor } from "@/components/PatientEditor";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import type { PatientMutationInput, PatientProfile } from "@/lib/types";
import { colors, spacing } from "@/theme";
import { router } from "expo-router";
import React, { useState } from "react";
import { StyleSheet, Text } from "react-native";

type CreatedPatient = PatientProfile & {
  portalUsername?: string | null;
  portalTemporaryPassword?: string | null;
};

export default function NewPatientScreen() {
  const { user } = useSession();
  const [created, setCreated] = useState<CreatedPatient | null>(null);
  const canCreate = user?.role === "Admin" || user?.role === "Reception";

  if (!canCreate) {
    return (
      <Screen>
        <StateMessage
          title="غير مصرح"
          message="إضافة المرضى متاحة للمدير والاستقبال وفق سياسة الخادم."
        />
      </Screen>
    );
  }

  if (created) {
    return (
      <Screen>
        <SectionTitle>تم إنشاء المريض</SectionTitle>
        <Card>
          <Text style={styles.name}>
            {[created.firstName, created.middleName, created.lastName].filter(Boolean).join(" ")}
          </Text>
          <Text style={styles.number}>{created.patientNumber}</Text>
        </Card>

        {created.portalUsername ? (
          <Card>
            <Text style={styles.warningTitle}>بيانات بوابة المريض — تظهر مرة واحدة</Text>
            <Text style={styles.credential}>اسم المستخدم: {created.portalUsername}</Text>
            {created.portalTemporaryPassword ? (
              <Text style={styles.credential}>
                كلمة المرور المؤقتة: {created.portalTemporaryPassword}
              </Text>
            ) : null}
            <Text style={styles.hint}>
              سلّم البيانات للمريض بطريقة آمنة ولا تحفظ كلمة المرور المؤقتة في ملاحظات عامة.
            </Text>
          </Card>
        ) : null}

        <PrimaryButton
          title="فتح ملف المريض"
          onPress={() =>
            router.replace({ pathname: "/(app)/patients/[id]", params: { id: created.id } })
          }
        />
      </Screen>
    );
  }

  async function createPatient(input: PatientMutationInput) {
    const result = await apiRequest<CreatedPatient>("/api/patients", {
      method: "POST",
      body: JSON.stringify(input)
    });
    setCreated(result);
  }

  return (
    <Screen>
      <SectionTitle>إضافة مريض جديد</SectionTitle>
      <Text style={styles.hint}>
        تُطبق قواعد منع تكرار الهاتف والواتساب ورقم الملف في الخادم نفسه.
      </Text>
      <PatientEditor submitLabel="حفظ المريض" onSubmit={createPatient} />
    </Screen>
  );
}

const styles = StyleSheet.create({
  name: { color: colors.text, fontSize: 20, fontWeight: "800", textAlign: "right" },
  number: { color: colors.primary, fontWeight: "800", marginTop: spacing.xs, textAlign: "right" },
  warningTitle: { color: colors.warning, fontSize: 16, fontWeight: "800", textAlign: "right" },
  credential: { color: colors.text, marginTop: spacing.sm, textAlign: "right", fontWeight: "700" },
  hint: { color: colors.muted, textAlign: "right", lineHeight: 21, marginTop: spacing.xs }
});
