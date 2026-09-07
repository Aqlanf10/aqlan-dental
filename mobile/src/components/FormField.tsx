import { Pressable, StyleSheet, TextInput, View, type TextInputProps } from 'react-native';

import { useLocale } from '@/i18n/LocaleProvider';
import { colors, radius, spacing, typography } from '@/theme/tokens';
import { AppText } from './AppText';

type Props = TextInputProps & {
  label: string;
  actionLabel?: string;
  onAction?: () => void;
};

export function FormField({ label, actionLabel, onAction, style, ...props }: Props) {
  const { isRtl } = useLocale();
  return (
    <View style={styles.group}>
      <AppText variant="label">{label}</AppText>
      <View style={[styles.inputShell, { flexDirection: isRtl ? 'row-reverse' : 'row' }]}>
        <TextInput
          {...props}
          placeholderTextColor="#94A3B8"
          style={[
            styles.input,
            { textAlign: isRtl ? 'right' : 'left', writingDirection: isRtl ? 'rtl' : 'ltr' },
            style,
          ]}
        />
        {actionLabel && onAction ? (
          <Pressable accessibilityRole="button" hitSlop={8} onPress={onAction} style={styles.action}>
            <AppText variant="caption" color={colors.navy700}>{actionLabel}</AppText>
          </Pressable>
        ) : null}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  group: { gap: spacing.sm },
  inputShell: {
    minHeight: 54,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    backgroundColor: colors.white,
  },
  input: { ...typography.body, flex: 1, color: colors.ink, paddingHorizontal: spacing.lg, paddingVertical: 14 },
  action: { paddingHorizontal: spacing.lg, paddingVertical: spacing.md },
});
