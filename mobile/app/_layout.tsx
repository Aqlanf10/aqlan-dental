import { SessionProvider } from "@/auth/SessionProvider";
import { colors } from "@/theme";
import { Stack } from "expo-router";
import React from "react";
import { I18nManager } from "react-native";
import "react-native-gesture-handler";
import "react-native-reanimated";

I18nManager.allowRTL(true);

export default function RootLayout() {
  return (
    <SessionProvider>
      <Stack
        screenOptions={{
          headerStyle: { backgroundColor: colors.surface },
          headerTintColor: colors.text,
          headerTitleAlign: "center",
          contentStyle: { backgroundColor: colors.background }
        }}
      >
        <Stack.Screen name="index" options={{ headerShown: false }} />
        <Stack.Screen name="sign-in" options={{ headerShown: false }} />
        <Stack.Screen name="change-password" options={{ title: "تغيير كلمة المرور", headerBackVisible: false }} />
        <Stack.Screen name="(app)" options={{ headerShown: false }} />
      </Stack>
    </SessionProvider>
  );
}
