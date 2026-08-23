import { useSession } from "@/auth/SessionProvider";
import { useClinicBranding } from "@/brand";
import { PrimaryButton } from "@/components/ui";
import { ApiError } from "@/lib/api";
import { colors, radius, shadow, spacing } from "@/theme";
import { Redirect, router } from "expo-router";
import React, { useState } from "react";
import {
  Image,
  KeyboardAvoidingView,
  Platform,
  Pressable,
  SafeAreaView,
  StyleSheet,
  Text,
  TextInput,
  View
} from "react-native";

export default function ChangePasswordScreen() {
  const { isLoading, user, changePassword, signOut } = useSession();
  const brand = useClinicBranding();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  if (!isLoading && !user) return <Redirect href="/sign-in" />;

  async function submit() {
    if (!currentPassword || !newPassword || !confirmPassword) return setError("أكمل جميع الحقول.");
    if (newPassword !== confirmPassword) return setError("كلمة المرور الجديدة وتأكيدها غير متطابقين.");
    if (newPassword === currentPassword) return setError("كلمة المرور الجديدة يجب أن تكون مختلفة عن الحالية.");

    setSubmitting(true);
    setError(null);
    try {
      await changePassword(currentPassword, newPassword);
      router.replace("/(app)/home");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "تعذر تغيير كلمة المرور الآن.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <SafeAreaView style={styles.safe}>
      <KeyboardAvoidingView style={styles.container} behavior={Platform.OS === "ios" ? "padding" : undefined}>
        <View style={styles.card}>
          <View style={styles.logoCard}><Image source={require("../assets/logo.png")} resizeMode="contain" style={styles.logo} /></View>
          <Text style={styles.eyebrow}>حماية حسابك</Text>
          <Text style={styles.title}>تغيير كلمة المرور</Text>
          <Text style={styles.description}>
            {user?.mustChangePassword
              ? "يجب تغيير كلمة المرور المؤقتة قبل استخدام نظام العيادة من الهاتف."
              : "أدخل كلمة المرور الحالية ثم اختر كلمة مرور جديدة قوية. يطبق الخادم سياسة التعقيد المعتمدة للمركز."}
          </Text>

          <Text style={styles.label}>كلمة المرور الحالية</Text>
          <TextInput value={currentPassword} onChangeText={setCurrentPassword} secureTextEntry autoCapitalize="none" textContentType="password" style={styles.input} />

          <Text style={styles.label}>كلمة المرور الجديدة</Text>
          <TextInput value={newPassword} onChangeText={setNewPassword} secureTextEntry autoCapitalize="none" textContentType="newPassword" style={styles.input} />

          <Text style={styles.label}>تأكيد كلمة المرور الجديدة</Text>
          <TextInput value={confirmPassword} onChangeText={setConfirmPassword} secureTextEntry autoCapitalize="none" textContentType="newPassword" style={styles.input} onSubmitEditing={() => void submit()} />

          {error ? <Text style={styles.error}>{error}</Text> : null}

          <PrimaryButton title="حفظ والمتابعة" variant="accent" loading={submitting} disabled={submitting} onPress={() => void submit()} />

          {user?.mustChangePassword ? (
            <Pressable onPress={() => void signOut()} disabled={submitting} style={styles.secondaryButton}>
              <Text style={styles.secondaryText}>تسجيل الخروج</Text>
            </Pressable>
          ) : (
            <Pressable onPress={() => router.back()} disabled={submitting} style={styles.secondaryButton}>
              <Text style={styles.secondaryText}>رجوع بدون تغيير</Text>
            </Pressable>
          )}
        </View>
        <Text numberOfLines={2} style={styles.clinic}>{brand.clinicName}</Text>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.primary },
  container: { flex: 1, justifyContent: "center", padding: spacing.lg },
  card: { backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border, borderRadius: radius.lg, padding: spacing.lg, gap: spacing.sm, ...shadow.floating },
  logoCard: { width: 126, height: 74, alignSelf: "center", borderRadius: radius.md, backgroundColor: colors.white, alignItems: "center", justifyContent: "center", marginBottom: spacing.xs },
  logo: { width: 108, height: 62 },
  eyebrow: { color: colors.secondary, fontSize: 12, fontWeight: "900", textAlign: "right" },
  title: { color: colors.text, fontSize: 24, fontWeight: "900", textAlign: "right" },
  description: { color: colors.muted, lineHeight: 22, textAlign: "right", marginBottom: spacing.sm },
  label: { color: colors.text, fontWeight: "700", textAlign: "right", marginTop: spacing.xs },
  input: { minHeight: 52, borderWidth: 1, borderColor: colors.border, borderRadius: radius.sm, paddingHorizontal: spacing.md, color: colors.text, backgroundColor: colors.surfaceMuted, textAlign: "right" },
  error: { color: colors.danger, backgroundColor: colors.dangerSoft, borderRadius: radius.sm, padding: spacing.sm, textAlign: "right" },
  secondaryButton: { minHeight: 44, alignItems: "center", justifyContent: "center" },
  secondaryText: { color: colors.muted, fontWeight: "800" },
  clinic: { color: "rgba(255,255,255,0.64)", fontSize: 11, lineHeight: 18, textAlign: "center", marginTop: spacing.md }
});
