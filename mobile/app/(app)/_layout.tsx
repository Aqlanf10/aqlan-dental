import { useSession } from "@/auth/SessionProvider";
import { colors } from "@/theme";
import { Redirect, Tabs } from "expo-router";
import React from "react";
import { ActivityIndicator, Text, View } from "react-native";

const icon = (value: string) => ({ color }: { color: string }) => (
  <Text style={{ color, fontSize: 18 }}>{value}</Text>
);

export default function AppTabsLayout() {
  const { isLoading, user } = useSession();

  if (isLoading) {
    return (
      <View style={{ flex: 1, alignItems: "center", justifyContent: "center", backgroundColor: colors.background }}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  if (!user) return <Redirect href="/sign-in" />;
  if (user.mustChangePassword) return <Redirect href="/change-password" />;

  return (
    <Tabs
      screenOptions={{
        headerTitleAlign: "center",
        headerStyle: { backgroundColor: colors.surface },
        headerTintColor: colors.text,
        tabBarActiveTintColor: colors.primary,
        tabBarInactiveTintColor: colors.muted,
        tabBarStyle: { backgroundColor: colors.surface }
      }}
    >
      <Tabs.Screen name="home" options={{ title: "الرئيسية", tabBarIcon: icon("⌂") }} />
      <Tabs.Screen name="patients" options={{ title: "المرضى", tabBarIcon: icon("♙") }} />
      <Tabs.Screen name="appointments" options={{ title: "المواعيد", tabBarIcon: icon("◷") }} />
      <Tabs.Screen name="account" options={{ title: "حسابي", tabBarIcon: icon("●") }} />
    </Tabs>
  );
}
