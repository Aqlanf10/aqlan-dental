import { useSession } from "@/auth/SessionProvider";
import { ApiError } from "@/lib/api";
import { colors, radius, spacing } from "@/theme";
import { router } from "expo-router";
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

export default function SignInScreen() {
  const { signIn } = useSession();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

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
      <KeyboardAvoidingView
        style={styles.container}
        behavior={Platform.OS === "ios" ? "padding" : undefined}
      >
        <View style={styles.brand}>
          <Text style={styles.logo}>ADP</Text>
          <Text style={styles.title}>Aqlan Dental Pro</Text>
          <Text style={styles.subtitle}>نظام إدارة مركز د. عقلان الكامل</Text>
        </View>

        <View style={styles.form}>
          <Text style={styles.label}>اسم المستخدم</Text>
          <TextInput
            value={username}
            onChangeText={setUsername}
            autoCapitalize="none"
            autoCorrect={false}
            textContentType="username"
            style={styles.input}
            placeholder="اسم المستخدم"
            placeholderTextColor={colors.muted}
          />

          <Text style={styles.label}>كلمة المرور</Text>
          <TextInput
            value={password}
            onChangeText={setPassword}
            secureTextEntry
            textContentType="password"
            style={styles.input}
            placeholder="••••••••"
            placeholderTextColor={colors.muted}
            onSubmitEditing={() => void submit()}
          />

          {error ? <Text style={styles.error}>{error}</Text> : null}

          <Pressable
            accessibilityRole="button"
            onPress={() => void submit()}
            disabled={loading}
            style={({ pressed }) => [
              styles.button,
              loading && { opacity: 0.6 },
              pressed && !loading && { opacity: 0.85 }
            ]}
          >
            {loading ? (
              <ActivityIndicator color="#fff" />
            ) : (
              <Text style={styles.buttonText}>تسجيل الدخول</Text>
            )}
          </Pressable>
        </View>

        <Text style={styles.security}>
          جلسة الهاتف محفوظة في التخزين الآمن للجهاز ولا تُخزن كلمة المرور.
        </Text>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  container: {
    flex: 1,
    justifyContent: "center",
    padding: spacing.lg,
    gap: spacing.xl
  },
  brand: { alignItems: "center", gap: spacing.sm },
  logo: {
    width: 72,
    height: 72,
    borderRadius: 22,
    textAlign: "center",
    textAlignVertical: "center",
    backgroundColor: colors.primary,
    color: "#fff",
    fontSize: 24,
    fontWeight: "800",
    paddingTop: Platform.OS === "ios" ? 20 : 0
  },
  title: { color: colors.text, fontSize: 27, fontWeight: "800" },
  subtitle: { color: colors.muted, fontSize: 15, textAlign: "center" },
  form: {
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.lg,
    padding: spacing.lg,
    gap: spacing.sm
  },
  label: {
    textAlign: "right",
    color: colors.text,
    fontSize: 14,
    fontWeight: "700",
    marginTop: spacing.xs
  },
  input: {
    minHeight: 50,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.sm,
    paddingHorizontal: spacing.md,
    color: colors.text,
    backgroundColor: "#fff",
    textAlign: "right"
  },
  error: {
    color: colors.danger,
    backgroundColor: colors.dangerSoft,
    borderRadius: radius.sm,
    padding: spacing.sm,
    textAlign: "right"
  },
  button: {
    minHeight: 50,
    marginTop: spacing.sm,
    borderRadius: radius.sm,
    backgroundColor: colors.primary,
    alignItems: "center",
    justifyContent: "center"
  },
  buttonText: { color: "#fff", fontSize: 16, fontWeight: "800" },
  security: {
    color: colors.muted,
    fontSize: 12,
    textAlign: "center",
    lineHeight: 19
  }
});
