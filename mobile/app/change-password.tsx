import { useSession } from "@/auth/SessionProvider";
import { ApiError } from "@/lib/api";
import { colors, radius, spacing } from "@/theme";
import { Redirect, router } from "expo-router";
import React, { useState } from "react";
import {
  ActivityIndicator,
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
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  if (!isLoading && !user) return <Redirect href="/sign-in" />;

  async function submit() {
    if (!currentPassword || !newPassword || !confirmPassword) {
      setError("أكمل جميع الحقول.");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("كلمة المرور الجديدة وتأكيدها غير متطابقين.");
      return;
    }

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
          <Text style={styles.title}>تغيير كلمة المرور</Text>
          <Text style={styles.description}>
            يجب تغيير كلمة المرور المؤقتة قبل استخدام نظام العيادة من الهاتف.
          </Text>

          <Text style={styles.label}>كلمة المرور الحالية</Text>
          <TextInput value={currentPassword} onChangeText={setCurrentPassword} secureTextEntry style={styles.input} />

          <Text style={styles.label}>كلمة المرور الجديدة</Text>
          <TextInput value={newPassword} onChangeText={setNewPassword} secureTextEntry style={styles.input} />

          <Text style={styles.label}>تأكيد كلمة المرور الجديدة</Text>
          <TextInput
            value={confirmPassword}
            onChangeText={setConfirmPassword}
            secureTextEntry
            style={styles.input}
            onSubmitEditing={() => void submit()}
          />

          {error ? <Text style={styles.error}>{error}</Text> : null}

          <Pressable onPress={() => void submit()} disabled={submitting} style={styles.primaryButton}>
            {submitting ? <ActivityIndicator color="#fff" /> : <Text style={styles.primaryText}>حفظ والمتابعة</Text>}
          </Pressable>

          <Pressable onPress={() => void signOut()} disabled={submitting} style={styles.secondaryButton}>
            <Text style={styles.secondaryText}>تسجيل الخروج</Text>
          </Pressable>
        </View>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  container: { flex: 1, justifyContent: "center", padding: spacing.lg },
  card: {
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.lg,
    padding: spacing.lg,
    gap: spacing.sm
  },
  title: { color: colors.text, fontSize: 24, fontWeight: "800", textAlign: "right" },
  description: { color: colors.muted, lineHeight: 22, textAlign: "right", marginBottom: spacing.sm },
  label: { color: colors.text, fontWeight: "700", textAlign: "right", marginTop: spacing.xs },
  input: {
    minHeight: 50,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.sm,
    paddingHorizontal: spacing.md,
    color: colors.text,
    textAlign: "right"
  },
  error: { color: colors.danger, backgroundColor: colors.dangerSoft, borderRadius: radius.sm, padding: spacing.sm, textAlign: "right" },
  primaryButton: {
    minHeight: 50,
    borderRadius: radius.sm,
    backgroundColor: colors.primary,
    alignItems: "center",
    justifyContent: "center",
    marginTop: spacing.sm
  },
  primaryText: { color: "#fff", fontWeight: "800", fontSize: 16 },
  secondaryButton: { minHeight: 44, alignItems: "center", justifyContent: "center" },
  secondaryText: { color: colors.muted, fontWeight: "700" }
});
