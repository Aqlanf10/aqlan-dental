import { usePatientSession } from "@/auth/SessionProvider";
import { Redirect } from "expo-router";
import { ActivityIndicator, View } from "react-native";

export default function Index() {
  const { loading, profile, mustChangePassword } = usePatientSession();
  if (loading) return <View style={{ flex: 1, justifyContent: "center" }}><ActivityIndicator /></View>;
  if (!profile) return <Redirect href="/sign-in" />;
  if (mustChangePassword) return <Redirect href="/change-password" />;
  return <Redirect href="/(app)/home" />;
}
