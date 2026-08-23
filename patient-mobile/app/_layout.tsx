import { PatientSessionProvider } from "@/auth/SessionProvider";
import { Stack } from "expo-router";
import { StatusBar } from "expo-status-bar";
import { I18nManager } from "react-native";
import "react-native-gesture-handler";
import "react-native-reanimated";

I18nManager.allowRTL(true);

export default function RootLayout() {
  return (
    <PatientSessionProvider>
      <StatusBar style="dark" />
      <Stack screenOptions={{ headerTitleAlign: "center", contentStyle: { backgroundColor: "#f4f8fb" } }}>
        <Stack.Screen name="index" options={{ headerShown: false }} />
        <Stack.Screen name="sign-in" options={{ headerShown: false }} />
        <Stack.Screen name="change-password" options={{ title: "تغيير كلمة المرور", headerBackVisible: false }} />
        <Stack.Screen name="(app)" options={{ headerShown: false }} />
      </Stack>
    </PatientSessionProvider>
  );
}
