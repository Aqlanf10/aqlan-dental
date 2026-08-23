import { PatientSessionProvider } from "@/auth/SessionProvider";
import { Stack } from "expo-router";
import { StatusBar } from "expo-status-bar";

export default function RootLayout() {
  return (
    <PatientSessionProvider>
      <StatusBar style="dark" />
      <Stack screenOptions={{ headerTitleAlign: "center", headerBackTitle: "رجوع" }}>
        <Stack.Screen name="index" options={{ headerShown: false }} />
        <Stack.Screen name="sign-in" options={{ title: "دخول المريض" }} />
        <Stack.Screen name="change-password" options={{ title: "تغيير كلمة المرور" }} />
        <Stack.Screen name="(app)" options={{ headerShown: false }} />
      </Stack>
    </PatientSessionProvider>
  );
}
