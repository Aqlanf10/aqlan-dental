import { clearTokens } from "@/auth/tokenStore";
import { colors, radius, shadow, spacing } from "@/theme";
import { router } from "expo-router";
import React, { Component, type ErrorInfo, type PropsWithChildren } from "react";
import { Image, Pressable, SafeAreaView, ScrollView, StyleSheet, Text, View } from "react-native";

type State = { error: Error | null };

export class AppErrorBoundary extends Component<PropsWithChildren, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    if (__DEV__) console.error("Mobile render error", error, info.componentStack);
  }

  private retry = () => {
    this.setState({ error: null });
    router.replace("/");
  };

  private signIn = async () => {
    await clearTokens();
    this.setState({ error: null });
    router.replace("/sign-in");
  };

  render() {
    const { error } = this.state;
    if (!error) return this.props.children;

    return (
      <SafeAreaView style={styles.safe}>
        <ScrollView contentContainerStyle={styles.screen}>
          <View style={styles.card}>
            <Image source={require("../../assets/logo.png")} resizeMode="contain" style={styles.logo} />
            <View style={styles.badge}><Text style={styles.badgeText}>تعذر فتح هذه الشاشة</Text></View>
            <Text accessibilityRole="header" style={styles.title}>التطبيق ما زال يعمل بأمان</Text>
            <Text style={styles.message}>
              حدث خطأ غير متوقع أثناء عرض الشاشة. لم تُرسل أي بيانات ولم تُكرّر أي عملية. يمكنك العودة للرئيسية أو بدء جلسة جديدة.
            </Text>
            <Text selectable style={styles.technical}>{error.message || "خطأ عرض غير معروف"}</Text>
            <Pressable accessibilityRole="button" onPress={this.retry} style={({ pressed }) => [styles.primary, pressed && styles.pressed]}>
              <Text style={styles.primaryText}>العودة الآمنة للرئيسية</Text>
            </Pressable>
            <Pressable accessibilityRole="button" onPress={() => void this.signIn()} style={({ pressed }) => [styles.secondary, pressed && styles.pressed]}>
              <Text style={styles.secondaryText}>تسجيل الدخول من جديد</Text>
            </Pressable>
          </View>
        </ScrollView>
      </SafeAreaView>
    );
  }
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  screen: { flexGrow: 1, justifyContent: "center", padding: spacing.lg },
  card: { backgroundColor: colors.surface, borderRadius: radius.lg, padding: spacing.lg, alignItems: "center", borderWidth: 1, borderColor: colors.border, ...shadow.floating },
  logo: { width: 132, height: 76, marginBottom: spacing.md },
  badge: { backgroundColor: colors.dangerSoft, borderRadius: radius.pill, paddingHorizontal: spacing.md, paddingVertical: spacing.xs },
  badgeText: { color: colors.danger, fontWeight: "800", fontSize: 12 },
  title: { color: colors.text, fontSize: 24, fontWeight: "900", textAlign: "center", marginTop: spacing.md },
  message: { color: colors.muted, lineHeight: 24, textAlign: "center", marginVertical: spacing.md },
  technical: { width: "100%", color: colors.danger, backgroundColor: colors.dangerSoft, borderRadius: radius.sm, padding: spacing.sm, textAlign: "left", marginBottom: spacing.md, fontSize: 12 },
  primary: { width: "100%", minHeight: 52, alignItems: "center", justifyContent: "center", backgroundColor: colors.primary, borderRadius: radius.sm },
  primaryText: { color: colors.white, fontWeight: "800", fontSize: 16 },
  secondary: { width: "100%", minHeight: 48, alignItems: "center", justifyContent: "center", marginTop: spacing.xs },
  secondaryText: { color: colors.primary, fontWeight: "800" },
  pressed: { opacity: 0.82 }
});
