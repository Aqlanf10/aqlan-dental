import { usePatientSession } from "@/auth/SessionProvider";
import { router } from "expo-router";
import { useState } from "react";
import { Pressable, StyleSheet, Text, TextInput, View } from "react-native";

export default function SignIn() {
  const { signIn } = usePatientSession();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    setBusy(true); setError("");
    try {
      const session = await signIn(username.trim(), password);
      router.replace(session.mustChangePassword ? "/change-password" : "/(app)/home");
    } catch (value) {
      setError(value instanceof Error ? value.message : "تعذر تسجيل الدخول");
    } finally {
      setBusy(false);
    }
  };

  return (
    <View style={styles.page}>
      <Text style={styles.title}>بوابة مرضى مركز عقلان</Text>
      <TextInput style={styles.input} placeholder="اسم المستخدم" value={username} onChangeText={setUsername} textAlign="right" autoCapitalize="none" />
      <TextInput style={styles.input} placeholder="كلمة المرور" value={password} onChangeText={setPassword} textAlign="right" secureTextEntry />
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <Pressable style={styles.button} onPress={() => void submit()} disabled={busy || !username || !password}>
        <Text style={styles.buttonText}>{busy ? "جاري الدخول..." : "تسجيل الدخول"}</Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  page: { flex: 1, justifyContent: "center", padding: 24, gap: 14, backgroundColor: "#f4f8fb" },
  title: { fontSize: 26, fontWeight: "700", textAlign: "center", color: "#123b5d", marginBottom: 18 },
  input: { backgroundColor: "#fff", borderWidth: 1, borderColor: "#c9d8e4", borderRadius: 12, padding: 14, fontSize: 16 },
  error: { color: "#b42318", textAlign: "right" },
  button: { backgroundColor: "#0d6b78", borderRadius: 12, padding: 15 },
  buttonText: { color: "#fff", textAlign: "center", fontWeight: "700", fontSize: 16 }
});
