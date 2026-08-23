import { colors, radius, shadow, spacing } from "@/theme";
import type { PropsWithChildren, ReactNode } from "react";
import React from "react";
import { StatusBar } from "expo-status-bar";
import {
  ActivityIndicator,
  Image,
  Pressable,
  SafeAreaView,
  ScrollView,
  StyleSheet,
  Text,
  View,
  type RefreshControlProps,
  type ViewStyle
} from "react-native";

export function Screen({
  children,
  scroll = true,
  refreshControl
}: PropsWithChildren<{
  scroll?: boolean;
  refreshControl?: React.ReactElement<RefreshControlProps>;
}>) {
  if (!scroll) {
    return (
      <SafeAreaView style={styles.safe}>
        <View style={styles.screen}>{children}</View>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.safe}>
      <ScrollView
        contentContainerStyle={styles.screen}
        keyboardShouldPersistTaps="handled"
        keyboardDismissMode="interactive"
        automaticallyAdjustKeyboardInsets
        showsVerticalScrollIndicator={false}
        refreshControl={refreshControl}
      >
        {children}
      </ScrollView>
    </SafeAreaView>
  );
}

export function Card({
  children,
  style
}: PropsWithChildren<{ style?: ViewStyle | ViewStyle[] }>) {
  return <View style={[styles.card, style]}>{children}</View>;
}

export function SectionTitle({ children }: PropsWithChildren) {
  return (
    <View style={styles.sectionHeading}>
      <View style={styles.sectionLine} />
      <Text accessibilityRole="header" style={styles.sectionTitle}>{children}</Text>
    </View>
  );
}

export function PageHeader({ title, subtitle, eyebrow }: { title: string; subtitle?: string; eyebrow?: string }) {
  return (
    <View style={styles.pageHeader}>
      {eyebrow ? <Text style={styles.eyebrow}>{eyebrow}</Text> : null}
      <Text accessibilityRole="header" style={styles.pageTitle}>{title}</Text>
      {subtitle ? <Text style={styles.pageSubtitle}>{subtitle}</Text> : null}
    </View>
  );
}

export function BrandLoading({ message = "جارٍ تجهيز مساحة العمل…" }: { message?: string }) {
  return (
    <View style={styles.loadingScreen}>
      <StatusBar style="light" />
      <View style={styles.loadingLogoCard}>
        <Image source={require("../../assets/logo.png")} resizeMode="contain" style={styles.loadingLogo} />
      </View>
      <ActivityIndicator size="large" color={colors.accent} />
      <Text style={styles.loadingText}>{message}</Text>
    </View>
  );
}

export function PrimaryButton({
  title,
  onPress,
  disabled = false,
  loading = false,
  accessibilityHint,
  variant = "primary"
}: {
  title: string;
  onPress: () => void;
  disabled?: boolean;
  loading?: boolean;
  accessibilityHint?: string;
  variant?: "primary" | "accent" | "secondary" | "danger";
}) {
  const unavailable = disabled || loading;
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={title}
      accessibilityHint={accessibilityHint}
      accessibilityState={{ disabled: unavailable, busy: loading }}
      onPress={onPress}
      disabled={unavailable}
      style={({ pressed }) => [
        styles.primaryButton,
        variant === "accent" && styles.accentButton,
        variant === "secondary" && styles.secondaryButton,
        variant === "danger" && styles.dangerButton,
        unavailable && styles.disabled,
        pressed && !unavailable && styles.pressed
      ]}
    >
      {loading ? (
        <ActivityIndicator accessibilityLabel="جارٍ التنفيذ" color="#fff" />
      ) : (
        <Text style={[styles.primaryButtonText, variant === "secondary" && styles.secondaryButtonText]}>{title}</Text>
      )}
    </Pressable>
  );
}

export function StateMessage({
  title,
  message,
  action
}: {
  title: string;
  message?: string;
  action?: ReactNode;
}) {
  return (
    <View accessibilityRole="alert">
      <Card>
        <Text style={styles.stateTitle}>{title}</Text>
        {message ? <Text style={styles.stateMessage}>{message}</Text> : null}
        {action ? <View style={{ marginTop: spacing.md }}>{action}</View> : null}
      </Card>
    </View>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  screen: {
    flexGrow: 1,
    padding: spacing.md,
    paddingBottom: spacing.xxl,
    gap: spacing.md,
    backgroundColor: colors.background
  },
  card: {
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    padding: spacing.md,
    ...shadow.card
  },
  sectionHeading: { flexDirection: "row", alignItems: "center", justifyContent: "flex-end", gap: spacing.sm, marginTop: spacing.xs },
  sectionLine: { width: 4, height: 24, borderRadius: radius.pill, backgroundColor: colors.accent },
  sectionTitle: {
    color: colors.text,
    fontSize: 19,
    fontWeight: "900",
    textAlign: "right"
  },
  pageHeader: { alignItems: "flex-end", gap: spacing.xxs, paddingVertical: spacing.xs },
  eyebrow: { color: colors.secondary, fontSize: 12, fontWeight: "800" },
  pageTitle: { color: colors.text, fontSize: 27, fontWeight: "900", textAlign: "right" },
  pageSubtitle: { color: colors.muted, lineHeight: 22, textAlign: "right" },
  loadingScreen: { flex: 1, alignItems: "center", justifyContent: "center", backgroundColor: colors.primary, gap: spacing.md, padding: spacing.lg },
  loadingLogoCard: { width: 178, height: 104, backgroundColor: colors.white, borderRadius: radius.lg, alignItems: "center", justifyContent: "center", ...shadow.floating },
  loadingLogo: { width: 146, height: 86 },
  loadingText: { color: "rgba(255,255,255,0.76)", textAlign: "center", fontWeight: "700" },
  primaryButton: {
    minHeight: 52,
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: colors.primary,
    borderRadius: radius.sm,
    paddingHorizontal: spacing.md,
    ...shadow.card
  },
  accentButton: { backgroundColor: colors.accent },
  secondaryButton: { backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.borderStrong, shadowOpacity: 0, elevation: 0 },
  dangerButton: { backgroundColor: colors.danger },
  primaryButtonText: { color: colors.white, fontSize: 16, fontWeight: "800", textAlign: "center" },
  secondaryButtonText: { color: colors.primary },
  disabled: { opacity: 0.55 },
  pressed: { opacity: 0.85 },
  stateTitle: {
    color: colors.text,
    fontSize: 17,
    fontWeight: "700",
    textAlign: "right"
  },
  stateMessage: {
    color: colors.muted,
    marginTop: spacing.sm,
    lineHeight: 22,
    textAlign: "right"
  }
});
