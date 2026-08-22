import { useSession } from "@/auth/SessionProvider";
import { colors } from "@/theme";
import { Redirect, Tabs } from "expo-router";
import React from "react";
import { ActivityIndicator, Text, View, type ColorValue } from "react-native";

const icon = (value: string) => ({ color }: { color: ColorValue }) => (
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
    <Tabs screenOptions={{ headerTitleAlign: "center", headerStyle: { backgroundColor: colors.surface }, headerTintColor: colors.text, tabBarActiveTintColor: colors.primary, tabBarInactiveTintColor: colors.muted, tabBarStyle: { backgroundColor: colors.surface } }}>
      <Tabs.Screen name="home" options={{ title: "الرئيسية", tabBarIcon: icon("⌂") }} />
      <Tabs.Screen name="patients" options={{ title: "المرضى", tabBarIcon: icon("♙") }} />
      <Tabs.Screen name="appointments" options={{ title: "المواعيد", tabBarIcon: icon("◷") }} />
      <Tabs.Screen name="messages" options={{ title: "الرسائل", tabBarIcon: icon("✉") }} />
      <Tabs.Screen name="account" options={{ title: "حسابي", tabBarIcon: icon("●") }} />
      <Tabs.Screen name="appointments-new" options={{ href: null, title: "حجز موعد" }} />
      <Tabs.Screen name="appointments-recall" options={{ href: null, title: "قائمة الاستدعاء" }} />
      <Tabs.Screen name="message-detail" options={{ href: null, title: "المحادثة" }} />
      <Tabs.Screen name="notifications" options={{ href: null, title: "الإشعارات" }} />
      <Tabs.Screen name="visits" options={{ href: null, title: "الزيارات السريرية" }} />
      <Tabs.Screen name="visit-detail" options={{ href: null, title: "تفاصيل الزيارة" }} />
      <Tabs.Screen name="visit-editor" options={{ href: null, title: "السجل السريري" }} />
      <Tabs.Screen name="journey" options={{ href: null, title: "تشغيل اليوم" }} />
      <Tabs.Screen name="journey-summary" options={{ href: null, title: "ملخص رحلة المريض" }} />
      <Tabs.Screen name="journey-handoff" options={{ href: null, title: "تسليم الزيارة للاستقبال" }} />
      <Tabs.Screen name="patient-finance" options={{ href: null, title: "مالية المريض" }} />
      <Tabs.Screen name="payment-new" options={{ href: null, title: "إضافة دفعة" }} />
      <Tabs.Screen name="patient-ortho" options={{ href: null, title: "تقويم الأسنان" }} />
      <Tabs.Screen name="ortho-case" options={{ href: null, title: "حالة التقويم" }} />
      <Tabs.Screen name="ortho-visit-new" options={{ href: null, title: "زيارة تقويمية" }} />
      <Tabs.Screen name="patient-general" options={{ href: null, title: "الأسنان العامة" }} />
      <Tabs.Screen name="general-tooth" options={{ href: null, title: "حالة السن" }} />
      <Tabs.Screen name="general-treatment-new" options={{ href: null, title: "تسجيل علاج عام" }} />
      <Tabs.Screen name="general-plan-new" options={{ href: null, title: "خطة علاج عام" }} />
      <Tabs.Screen name="general-perio-new" options={{ href: null, title: "سجل اللثة" }} />
      <Tabs.Screen name="patient-surgery" options={{ href: null, title: "جراحة الفم" }} />
      <Tabs.Screen name="surgery-new" options={{ href: null, title: "حالة جراحية جديدة" }} />
      <Tabs.Screen name="surgery-case" options={{ href: null, title: "الحالة الجراحية" }} />
      <Tabs.Screen name="surgery-preop" options={{ href: null, title: "ما قبل الجراحة" }} />
      <Tabs.Screen name="surgery-operative" options={{ href: null, title: "تقرير الجراحة" }} />
      <Tabs.Screen name="surgery-postop" options={{ href: null, title: "ما بعد الجراحة" }} />
      <Tabs.Screen name="surgery-referral-new" options={{ href: null, title: "إحالة مستشفى" }} />
      <Tabs.Screen name="patient-media" options={{ href: null, title: "الصور والأشعة" }} />
      <Tabs.Screen name="media-photo-new" options={{ href: null, title: "صورة سريرية" }} />
      <Tabs.Screen name="media-xray-new" options={{ href: null, title: "إضافة أشعة" }} />
      <Tabs.Screen name="patient-records" options={{ href: null, title: "السجلات الطبية" }} />
      <Tabs.Screen name="document-new" options={{ href: null, title: "إضافة مستند" }} />
      <Tabs.Screen name="prescription-new" options={{ href: null, title: "وصفة طبية جديدة" }} />
      <Tabs.Screen name="prescription-detail" options={{ href: null, title: "تفاصيل الوصفة" }} />
      <Tabs.Screen name="referral-new" options={{ href: null, title: "إحالة داخلية" }} />
      <Tabs.Screen name="patient-lab" options={{ href: null, title: "طلبات المعمل" }} />
      <Tabs.Screen name="lab-order-new" options={{ href: null, title: "طلب معمل جديد" }} />
      <Tabs.Screen name="lab-order-detail" options={{ href: null, title: "تفاصيل طلب المعمل" }} />
      <Tabs.Screen name="lab-order-transition" options={{ href: null, title: "تغيير حالة طلب المعمل" }} />
      <Tabs.Screen name="inventory" options={{ href: null, title: "المخزون" }} />
      <Tabs.Screen name="inventory-item" options={{ href: null, title: "تفاصيل المادة" }} />
      <Tabs.Screen name="inventory-item-editor" options={{ href: null, title: "بيانات المادة" }} />
      <Tabs.Screen name="inventory-adjust" options={{ href: null, title: "تعديل المخزون" }} />
      <Tabs.Screen name="lab-order-consume-inventory" options={{ href: null, title: "صرف مواد للمعمل" }} />
      <Tabs.Screen name="reports" options={{ href: null, title: "التقارير والإدارة" }} />
      <Tabs.Screen name="settings" options={{ href: null, title: "الإعدادات والحالة" }} />
    </Tabs>
  );
}
