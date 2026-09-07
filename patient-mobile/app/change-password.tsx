import { usePatientSession } from "@/auth/SessionProvider";
import { router } from "expo-router";
import { useState } from "react";
import { Pressable, StyleSheet, Text, TextInput, View } from "react-native";

export default function ChangePassword() {
  const { changePassword } = usePatientSession();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [message, setMessage] = useState("");
  const submit = async () => {
    setMessage("");
    try {
      await changePassword(currentPassword, newPassword);
      router.replace("/(app)/home");
    } catch (value) {
      setMessage(value instanceof Error ? value.message : "تعذر تغيير كلمة المرور");
    }
  };
  return (
    <View style={styles.page}>
      <Text style={styles.note}>لأمان حسابك غيّر كلمة المرور المؤقتة قبل المتابعة.</Text>
      <TextInput style={styles.input} placeholder="كلمة المرور الحالية" secureTextEntry textAlign="right" value={currentPassword} onChangeText={setCurrentPassword} />
      <TextInput style={styles.input} placeholder="كلمة المرور الجديدة" secureTextEntry textAlign="right" value={newPassword} onChangeText={setNewPassword} />
      {message ? <Text style={styles.error}>{message}</Text> : null}
      <Pressable style={styles.button} onPress={() => void submit()}><Text style={styles.buttonText}>حفظ والمتابعة</Text></Pressable>
    </View>
  );
}
const styles = StyleSheet.create({
  page: { flex: 1, padding: 24, justifyContent: "center", gap: 14, backgroundColor: "#f4f8fb" },
  note: { textAlign: "right", color: "#123b5d", fontSize: 16 },
  input: { backgroundColor: "#fff", borderWidth: 1, borderColor: "#c9d8e4", borderRadius: 12, padding: 14 },
  error: { color: "#b42318", textAlign: "right" },
  button: { backgroundColor: "#0d6b78", borderRadius: 12, padding: 15 },
  buttonText: { color: "#fff", textAlign: "center", fontWeight: "700" }
});
