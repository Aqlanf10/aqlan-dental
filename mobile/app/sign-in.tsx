import { useSession } from "@/auth/SessionProvider";
import { useClinicBranding } from "@/brand";
import { ApiError } from "@/lib/api";
import { colors, radius, shadow, spacing } from "@/theme";
import { router } from "expo-router";
import React, { useState } from "react";
import {
  ActivityIndicator,
  Image,
  KeyboardAvoidingView,
  Platform,
  Pressable,
  SafeAreaView,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View
} from "react-native";

export default function SignInScreen() {
  const { signIn } = useSession();
  const brand = useClinicBranding();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  async function submit() {
    if (!username.trim() || !password) {
      setError("أدخل اسم المستخدم وكلمة المرور.");
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const user = await signIn(username.trim(), password);
      router.replace(user.mustChangePassword ? "/change-password" : "/(app)/home");
    } catch (err) {
      setError(
        err instanceof ApiError
          ? err.message
          : "تعذر الاتصال بالنظام. تحقق من الإنترنت وإعداد رابط الخادم."
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <SafeAreaView style={styles.safe}>
      <View pointerEvents="none" style={styles.orbOne} />
      <View pointerEvents="none" style={styles.orbTwo} />
      <KeyboardAvoidingView
        style={styles.container}
        behavior={Platform.OS === "ios" ? "padding" : undefined}
      >
        <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled" showsVerticalScrollIndicator={false}>
          <View style={styles.brand}>
            <View style={styles.logoCard}>
              <Image source={require("../assets/logo.png")} resizeMode="contain" style={styles.logo} />
            </View>
            <Text style={styles.product}>AQLAN DENTAL PRO</Text>
            <Text accessibilityRole="header" style={styles.clinicName}>{brand.clinicName}</Text>
            <Text style={styles.location}>{brand.address}</Text>
          </View>

          <View style={styles.form}>
            <View style={styles.formHeading}>
              <Text style={styles.formEyebrow}>بوابة فريق المركز</Text>
              <Text style={styles.title}>مرحباً بعودتك</Text>
              <Text style={styles.subtitle}>سجّل الدخول للوصول إلى يوم العيادة والملفات المصرح بها.</Text>
            </View>

            <View style={styles.field}>
              <Text style={styles.label}>اسم المستخدم</Text>
              <TextInput
                accessibilityLabel="اسم المستخدم"
                value={username}
                onChangeText={setUsername}
                autoCapitalize="none"
                autoCorrect={false}
                textContentType="username"
                style={styles.input}
                placeholder="أدخل اسم المستخدم"
                placeholderTextColor={colors.muted}
                returnKeyType="next"
              />
            </View>

            <View style={styles.field}>
              <Text style={styles.label}>كلمة المرور</Text>
              <View style={styles.passwordField}>
                <Pressable accessibilityRole="button" accessibilityLabel={showPassword ? "إخفاء كلمة المرور" : "إظهار كلمة المرور"} onPress={() => setShowPassword((value) => !value)} style={styles.passwordToggle}>
                  <Text style={styles.passwordToggleText}>{showPassword ? "إخفاء" : "إظهار"}</Text>
                </Pressable>
                <TextInput
                  accessibilityLabel="كلمة المرور"
                  value={password}
                  onChangeText={setPassword}
                  secureTextEntry={!showPassword}
                  textContentType="password"
                  style={styles.passwordInput}
                  placeholder="••••••••"
                  placeholderTextColor={colors.muted}
                  onSubmitEditing={() => void submit()}
                  returnKeyType="done"
                />
              </View>
            </View>

            {error ? <Text accessibilityRole="alert" style={styles.error}>{error}</Text> : null}

            <Pressable
              accessibilityRole="button"
              accessibilityState={{ disabled: loading, busy: loading }}
              onPress={() => void submit()}
              disabled={loading}
              style={({ pressed }) => [styles.button, loading && styles.disabled, pressed && !loading && styles.pressed]}
            >
              {loading ? <ActivityIndicator color={colors.white} /> : <Text style={styles.buttonText}>دخول آمن إلى النظام</Text>}
            </Pressable>

            <View style={styles.securityRow}>
              <View style={styles.securityDot} />
              <Text style={styles.security}>الجلسة مشفرة ومحفوظة في التخزين الآمن للجهاز. لا تُخزّن كلمة المرور.</Text>
            </View>
          </View>

          <Text style={styles.footer}>{brand.leadDoctor}</Text>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.primary },
  container: { flex: 1 },
  content: { flexGrow: 1, justifyContent: "center", padding: spacing.lg, paddingVertical: spacing.xl, gap: spacing.lg },
  orbOne: { position: "absolute", top: -90, right: -80, width: 240, height: 240, borderRadius: 120, backgroundColor: "rgba(61,122,181,0.30)" },
  orbTwo: { position: "absolute", bottom: -120, left: -100, width: 280, height: 280, borderRadius: 140, backgroundColor: "rgba(245,146,46,0.11)" },
  brand: { alignItems: "center", gap: spacing.xs },
  logoCard: { width: 158, height: 94, borderRadius: radius.lg, backgroundColor: colors.white, alignItems: "center", justifyContent: "center", marginBottom: spacing.xs, ...shadow.floating },
  logo: { width: 132, height: 76 },
  product: { color: colors.accent, fontSize: 11, fontWeight: "900", letterSpacing: 2.2 },
  clinicName: { color: colors.white, fontSize: 19, lineHeight: 29, fontWeight: "900", textAlign: "center", maxWidth: 340 },
  location: { color: "rgba(255,255,255,0.68)", fontSize: 13, textAlign: "center" },
  form: {
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: "rgba(255,255,255,0.52)",
    borderRadius: radius.lg,
    padding: spacing.lg,
    gap: spacing.md,
    ...shadow.floating
  },
  formHeading: { alignItems: "flex-end", gap: spacing.xxs, marginBottom: spacing.xs },
  formEyebrow: { color: colors.secondary, fontSize: 12, fontWeight: "900" },
  title: { color: colors.text, fontSize: 25, fontWeight: "900", textAlign: "right" },
  subtitle: { color: colors.muted, fontSize: 13, lineHeight: 21, textAlign: "right" },
  field: { gap: spacing.xs },
  label: {
    textAlign: "right",
    color: colors.text,
    fontSize: 14,
    fontWeight: "700",
    marginTop: spacing.xxs
  },
  input: {
    minHeight: 54,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.sm,
    paddingHorizontal: spacing.md,
    color: colors.text,
    backgroundColor: colors.surfaceMuted,
    textAlign: "right"
  },
  passwordField: { minHeight: 54, flexDirection: "row", alignItems: "center", borderWidth: 1, borderColor: colors.border, borderRadius: radius.sm, backgroundColor: colors.surfaceMuted, overflow: "hidden" },
  passwordInput: { flex: 1, minHeight: 52, paddingHorizontal: spacing.md, color: colors.text, textAlign: "right" },
  passwordToggle: { minHeight: 52, justifyContent: "center", paddingHorizontal: spacing.md },
  passwordToggleText: { color: colors.secondary, fontSize: 12, fontWeight: "900" },
  error: {
    color: colors.danger,
    backgroundColor: colors.dangerSoft,
    borderRadius: radius.sm,
    padding: spacing.sm,
    textAlign: "right"
  },
  button: {
    minHeight: 50,
    marginTop: spacing.xs,
    borderRadius: radius.sm,
    backgroundColor: colors.accent,
    alignItems: "center",
    justifyContent: "center",
    ...shadow.card
  },
  buttonText: { color: colors.white, fontSize: 16, fontWeight: "900" },
  disabled: { opacity: 0.6 },
  pressed: { opacity: 0.84 },
  securityRow: { flexDirection: "row-reverse", justifyContent: "center", alignItems: "flex-start", gap: spacing.xs },
  securityDot: { width: 8, height: 8, borderRadius: 4, backgroundColor: colors.success, marginTop: 5 },
  security: {
    color: colors.muted,
    fontSize: 12,
    textAlign: "right",
    lineHeight: 19,
    flexShrink: 1
  },
  footer: { color: "rgba(255,255,255,0.56)", textAlign: "center", fontSize: 12, fontWeight: "700" }
});
