import { useSession } from "@/auth/SessionProvider";
import { BrandLoading } from "@/components/ui";
import { colors } from "@/theme";
import { Redirect, Tabs } from "expo-router";
import React from "react";
import { StyleSheet, Text, View, type ColorValue } from "react-native";

const icon = (value: string) => ({ color, focused }: { color: ColorValue; focused: boolean }) => (
  <View style={[styles.tabIcon, focused && styles.tabIconActive]}>
    <Text style={[styles.tabIconText, { color }]}>{value}</Text>
  </View>
);

export default function AppTabsLayout() {
  const { isLoading, user } = useSession();

  if (isLoading) {
    return <BrandLoading />;
  }

  if (!user) return <Redirect href="/sign-in" />;
  if (user.mustChangePassword) return <Redirect href="/change-password" />;

  return (
    <Tabs screenOptions={{ headerTitleAlign: "center", headerStyle: { backgroundColor: colors.primary }, headerTintColor: colors.white, headerTitleStyle: { fontWeight: "900" }, headerShadowVisible: false, tabBarActiveTintColor: colors.accent, tabBarInactiveTintColor: colors.muted, tabBarLabelStyle: { fontSize: 11, fontWeight: "800", marginTop: 2 }, tabBarStyle: styles.tabBar, tabBarItemStyle: styles.tabItem }}>
      <Tabs.Screen name="home" options={{ title: "الرئيسية", headerShown: false, tabBarIcon: icon("ر") }} />
      <Tabs.Screen name="patients" options={{ title: "المرضى", tabBarIcon: icon("م") }} />
      <Tabs.Screen name="appointments" options={{ title: "المواعيد", tabBarIcon: icon("ع") }} />
      <Tabs.Screen name="messages" options={{ title: "الرسائل", tabBarIcon: icon("ل") }} />
      <Tabs.Screen name="account" options={{ title: "حسابي", tabBarIcon: icon("ح") }} />
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
      <Tabs.Screen name="ortho-surgical" options={{ href: null, title: "التخطيط التقويمي الجراحي" }} />
      <Tabs.Screen name="ortho-surgical-new" options={{ href: null, title: "خطة جراحية تقويمية جديدة" }} />
      <Tabs.Screen name="ortho-surgical-case" options={{ href: null, title: "الخطة الجراحية التقويمية" }} />
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
      <Tabs.Screen name="diagnostics" options={{ href: null, title: "تشخيص التطبيق" }} />
    </Tabs>
  );
}

const styles = StyleSheet.create({
  tabBar: { height: 74, paddingTop: 8, paddingBottom: 9, backgroundColor: colors.surface, borderTopWidth: 0, shadowColor: "#102a43", shadowOffset: { width: 0, height: -4 }, shadowOpacity: 0.1, shadowRadius: 12, elevation: 14 },
  tabItem: { borderRadius: 14 },
  tabIcon: { width: 28, height: 28, borderRadius: 9, alignItems: "center", justifyContent: "center", backgroundColor: colors.surfaceMuted },
  tabIconActive: { backgroundColor: colors.accentSoft },
  tabIconText: { fontSize: 14, fontWeight: "900" }
});
