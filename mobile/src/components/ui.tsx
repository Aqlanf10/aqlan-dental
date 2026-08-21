import { colors, radius, spacing } from "@/theme";
import type { PropsWithChildren, ReactNode } from "react";
import React from "react";
import {
  ActivityIndicator,
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
  return <Text style={styles.sectionTitle}>{children}</Text>;
}

export function PrimaryButton({
  title,
  onPress,
  disabled = false,
  loading = false
}: {
  title: string;
  onPress: () => void;
  disabled?: boolean;
  loading?: boolean;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      onPress={onPress}
      disabled={disabled || loading}
      style={({ pressed }) => [
        styles.primaryButton,
        (disabled || loading) && styles.disabled,
        pressed && !disabled && !loading && styles.pressed
      ]}
    >
      {loading ? (
        <ActivityIndicator color="#fff" />
      ) : (
        <Text style={styles.primaryButtonText}>{title}</Text>
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
    <Card>
      <Text style={styles.stateTitle}>{title}</Text>
      {message ? <Text style={styles.stateMessage}>{message}</Text> : null}
      {action ? <View style={{ marginTop: spacing.md }}>{action}</View> : null}
    </Card>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  screen: {
    flexGrow: 1,
    padding: spacing.md,
    gap: spacing.md,
    backgroundColor: colors.background
  },
  card: {
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    padding: spacing.md
  },
  sectionTitle: {
    color: colors.text,
    fontSize: 20,
    fontWeight: "700",
    textAlign: "right"
  },
  primaryButton: {
    minHeight: 48,
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: colors.primary,
    borderRadius: radius.sm,
    paddingHorizontal: spacing.md
  },
  primaryButtonText: { color: "#fff", fontSize: 16, fontWeight: "700" },
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
