import { ActivityIndicator, Pressable, StyleSheet } from 'react-native';

import { colors, radius, spacing } from '@/theme/tokens';
import { AppText } from './AppText';

type Props = {
  label: string;
  onPress: () => void;
  busy?: boolean;
  disabled?: boolean;
  tone?: 'primary' | 'secondary';
};

export function PrimaryButton({ label, onPress, busy = false, disabled = false, tone = 'primary' }: Props) {
  const inactive = busy || disabled;
  const secondary = tone === 'secondary';
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ disabled: inactive, busy }}
      disabled={inactive}
      onPress={onPress}
      style={({ pressed }) => [
        styles.button,
        secondary && styles.secondary,
        inactive && styles.inactive,
        pressed && styles.pressed,
      ]}
    >
      {busy ? (
        <ActivityIndicator color={secondary ? colors.navy900 : colors.white} />
      ) : (
        <AppText variant="label" color={secondary ? colors.navy900 : colors.white} style={styles.label}>
          {label}
        </AppText>
      )}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  button: {
    minHeight: 52,
    borderRadius: radius.md,
    paddingHorizontal: spacing.xl,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.navy900,
    shadowColor: colors.shadow,
    shadowOpacity: 0.16,
    shadowRadius: 12,
    shadowOffset: { width: 0, height: 5 },
    elevation: 3,
  },
  secondary: { backgroundColor: colors.white, borderWidth: 1, borderColor: colors.border, elevation: 0 },
  inactive: { opacity: 0.55 },
  pressed: { transform: [{ scale: 0.99 }], opacity: 0.9 },
  label: { textAlign: 'center' },
});
