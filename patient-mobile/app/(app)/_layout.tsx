import { usePatientSession } from "@/auth/SessionProvider";
import { Redirect, Tabs } from "expo-router";

export default function AppLayout() {
  const { loading, profile, mustChangePassword } = usePatientSession();
  if (!loading && !profile) return <Redirect href="/sign-in" />;
  if (!loading && mustChangePassword) return <Redirect href="/change-password" />;
  return (
    <Tabs screenOptions={{ headerTitleAlign: "center", tabBarActiveTintColor: "#0d6b78" }}>
      <Tabs.Screen name="home" options={{ title: "الرئيسية" }} />
      <Tabs.Screen name="appointments" options={{ title: "مواعيدي" }} />
    </Tabs>
  );
}
