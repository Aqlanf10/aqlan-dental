import { portalRequest } from "@/lib/api";
import type { PatientAppointment } from "@/lib/types";
import { useEffect, useState } from "react";
import { FlatList, StyleSheet, Text, View } from "react-native";

export default function Appointments() {
  const [items, setItems] = useState<PatientAppointment[]>([]);
  const [error, setError] = useState("");
  useEffect(() => {
    portalRequest<PatientAppointment[]>("/appointments").then(setItems).catch(value => setError(value instanceof Error ? value.message : "تعذر تحميل المواعيد"));
  }, []);
  return (
    <View style={styles.page}>
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <FlatList
        data={items}
        keyExtractor={item => item.id}
        ListEmptyComponent={<Text style={styles.empty}>لا توجد مواعيد</Text>}
        renderItem={({ item }) => (
          <View style={styles.card}>
            <Text style={styles.title}>{item.appointmentType}</Text>
            <Text style={styles.text}>{item.appointmentDate} — {item.startTime}</Text>
            <Text style={styles.text}>{item.doctorName}</Text>
            <Text style={styles.status}>{item.status}</Text>
          </View>
        )}
      />
    </View>
  );
}
const styles = StyleSheet.create({
  page: { flex: 1, padding: 16, backgroundColor: "#f4f8fb" },
  card: { backgroundColor: "#fff", padding: 16, borderRadius: 14, marginBottom: 12 },
  title: { fontWeight: "700", fontSize: 17, color: "#123b5d", textAlign: "right" },
  text: { color: "#4a6070", textAlign: "right", marginTop: 5 },
  status: { color: "#0d6b78", textAlign: "right", marginTop: 8, fontWeight: "700" },
  empty: { textAlign: "center", marginTop: 40, color: "#5b7284" },
  error: { color: "#b42318", textAlign: "right", marginBottom: 12 }
});
