import { usePatientSession } from "@/auth/SessionProvider";
import { portalRequest } from "@/lib/api";
import type { PatientDashboard } from "@/lib/types";
import { useEffect, useState } from "react";
import { Pressable, ScrollView, StyleSheet, Text, View } from "react-native";

export default function Home() {
  const { profile, signOut } = usePatientSession();
  const [data, setData] = useState<PatientDashboard | null>(null);
  const [error, setError] = useState("");
  useEffect(() => {
    portalRequest<PatientDashboard>("/dashboard").then(setData).catch(value => setError(value instanceof Error ? value.message : "تعذر تحميل البيانات"));
  }, []);
  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.greeting}>مرحباً {profile?.fullName}</Text>
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <View style={styles.card}><Text style={styles.label}>الموعد القادم</Text><Text style={styles.value}>{data?.nextAppointment ? `${data.nextAppointment.appointmentDate} — ${data.nextAppointment.startTime}` : "لا يوجد موعد قادم"}</Text></View>
      <View style={styles.row}>
        <View style={styles.stat}><Text style={styles.number}>{data?.upcomingAppointments ?? "—"}</Text><Text>مواعيد قادمة</Text></View>
        <View style={styles.stat}><Text style={styles.number}>{data?.completedTreatments ?? "—"}</Text><Text>علاجات مكتملة</Text></View>
      </View>
      <View style={styles.card}><Text style={styles.label}>الرصيد المتبقي</Text><Text style={styles.value}>{data ? data.finance.totalOutstanding.toLocaleString("ar-YE") : "—"}</Text></View>
      <Pressable style={styles.logout} onPress={() => void signOut()}><Text style={styles.logoutText}>تسجيل الخروج</Text></Pressable>
    </ScrollView>
  );
}
const styles = StyleSheet.create({
  page: { padding: 20, gap: 14, backgroundColor: "#f4f8fb", minHeight: "100%" },
  greeting: { fontSize: 24, fontWeight: "700", color: "#123b5d", textAlign: "right" },
  card: { backgroundColor: "#fff", padding: 18, borderRadius: 14, gap: 8 },
  label: { color: "#5b7284", textAlign: "right" },
  value: { color: "#123b5d", fontWeight: "700", fontSize: 18, textAlign: "right" },
  row: { flexDirection: "row-reverse", gap: 12 },
  stat: { flex: 1, backgroundColor: "#dff3f2", padding: 16, borderRadius: 14, alignItems: "center" },
  number: { fontSize: 24, fontWeight: "800", color: "#0d6b78" },
  error: { color: "#b42318", textAlign: "right" },
  logout: { padding: 14 }, logoutText: { color: "#b42318", textAlign: "center" }
});
