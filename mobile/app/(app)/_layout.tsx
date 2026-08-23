import { useSession } from "@/auth/SessionProvider";
import { BrandLoading } from "@/components/ui";
import { colors } from "@/theme";
import { Redirect, Stack } from "expo-router";
import React from "react";

export default function AppStackLayout() {
  const { isLoading, user } = useSession();

  if (isLoading) return <BrandLoading />;
  if (!user) return <Redirect href="/sign-in" />;
  if (user.mustChangePassword) return <Redirect href="/change-password" />;

  return (
    <Stack
      screenOptions={{
        headerTitleAlign: "center",
        headerStyle: { backgroundColor: colors.primary },
        headerTintColor: colors.white,
        headerTitleStyle: { fontWeight: "900" },
        headerShadowVisible: false,
        contentStyle: { backgroundColor: colors.background }
      }}
    >
      <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
      <Stack.Screen name="journey" options={{ title: "تشغيل اليوم" }} />
      <Stack.Screen name="journey-summary" options={{ title: "ملخص رحلة المريض" }} />
      <Stack.Screen name="journey-handoff" options={{ title: "تسليم الزيارة للاستقبال" }} />
      <Stack.Screen name="appointments-new" options={{ title: "حجز موعد" }} />
      <Stack.Screen name="appointments-recall" options={{ title: "قائمة الاستدعاء" }} />
      <Stack.Screen name="message-detail" options={{ title: "المحادثة" }} />
      <Stack.Screen name="notifications" options={{ title: "الإشعارات" }} />
      <Stack.Screen name="diagnostics" options={{ title: "تشخيص التطبيق" }} />
      <Stack.Screen name="settings" options={{ title: "الإعدادات والحالة" }} />
      <Stack.Screen name="reports" options={{ title: "التقارير والإدارة" }} />
      <Stack.Screen name="inventory" options={{ title: "المخزون" }} />
    </Stack>
  );
}
