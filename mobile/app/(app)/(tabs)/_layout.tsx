import { colors } from "@/theme";
import { markRuntimeAction } from "@/lib/runtimeDiagnostics";
import { Tabs } from "expo-router";
import React from "react";
import { StyleSheet, Text, View, type ColorValue } from "react-native";

const icon = (value: string) => ({ color, focused }: { color: ColorValue; focused: boolean }) => (
  <View style={[styles.tabIcon, focused && styles.tabIconActive]}>
    <Text style={[styles.tabIconText, { color }]}>{value}</Text>
  </View>
);

export default function MainTabsLayout() {
  return (
    <Tabs
      initialRouteName="home"
      backBehavior="initialRoute"
      screenListeners={{
        tabPress: (event) => markRuntimeAction("تغيير التبويب", event.target)
      }}
      screenOptions={{
        headerTitleAlign: "center",
        headerStyle: { backgroundColor: colors.primary },
        headerTintColor: colors.white,
        headerTitleStyle: { fontWeight: "900" },
        headerShadowVisible: false,
        tabBarActiveTintColor: colors.accent,
        tabBarInactiveTintColor: colors.muted,
        tabBarLabelStyle: { fontSize: 11, fontWeight: "800", marginTop: 2 },
        tabBarStyle: styles.tabBar,
        tabBarItemStyle: styles.tabItem
      }}
    >
      <Tabs.Screen name="home" options={{ title: "الرئيسية", headerShown: false, tabBarIcon: icon("ر") }} />
      <Tabs.Screen name="patients" options={{ title: "المرضى", tabBarIcon: icon("م") }} />
      <Tabs.Screen name="appointments" options={{ title: "المواعيد", tabBarIcon: icon("ع") }} />
      <Tabs.Screen name="messages" options={{ title: "الرسائل", tabBarIcon: icon("ل") }} />
      <Tabs.Screen name="account" options={{ title: "حسابي", tabBarIcon: icon("ح") }} />
    </Tabs>
  );
}

const styles = StyleSheet.create({
  tabBar: {
    height: 74,
    paddingTop: 8,
    paddingBottom: 9,
    backgroundColor: colors.surface,
    borderTopWidth: 0,
    shadowColor: "#102a43",
    shadowOffset: { width: 0, height: -4 },
    shadowOpacity: 0.1,
    shadowRadius: 12,
    elevation: 14
  },
  tabItem: { borderRadius: 14 },
  tabIcon: {
    width: 28,
    height: 28,
    borderRadius: 9,
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: colors.surfaceMuted
  },
  tabIconActive: { backgroundColor: colors.accentSoft },
  tabIconText: { fontSize: 14, fontWeight: "900" }
});
