import { SessionProvider } from "@/auth/SessionProvider";
import { BrandProvider } from "@/brand";
import { AppErrorBoundary } from "@/components/AppErrorBoundary";
import { colors } from "@/theme";
import { Stack } from "expo-router";
import React from "react";
import { I18nManager, StyleSheet } from "react-native";
import { GestureHandlerRootView } from "react-native-gesture-handler";
import { SafeAreaProvider } from "react-native-safe-area-context";
import { StatusBar } from "expo-status-bar";
import "react-native-gesture-handler";
import "react-native-reanimated";

I18nManager.allowRTL(true);

export default function RootLayout() {
  return (
    <GestureHandlerRootView style={styles.root}>
      <SafeAreaProvider>
        <AppErrorBoundary>
          <BrandProvider>
            <SessionProvider>
              <StatusBar style="light" />
              <Stack
                screenOptions={{
                  headerStyle: { backgroundColor: colors.primary },
                  headerTintColor: colors.white,
                  headerTitleAlign: "center",
                  headerShadowVisible: false,
                  contentStyle: { backgroundColor: colors.background }
                }}
              >
                <Stack.Screen name="index" options={{ headerShown: false }} />
                <Stack.Screen name="sign-in" options={{ headerShown: false }} />
                <Stack.Screen name="change-password" options={{ title: "تغيير كلمة المرور", headerBackVisible: false }} />
                <Stack.Screen name="(app)" options={{ headerShown: false }} />
              </Stack>
            </SessionProvider>
          </BrandProvider>
        </AppErrorBoundary>
      </SafeAreaProvider>
    </GestureHandlerRootView>
  );
}

const styles = StyleSheet.create({ root: { flex: 1 } });
