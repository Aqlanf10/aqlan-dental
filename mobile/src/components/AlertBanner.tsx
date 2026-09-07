import { StyleSheet, View } from 'react-native';

import { useLocale } from '@/i18n/LocaleProvider';
import { colors, radius, spacing } from '@/theme/tokens';
import { AppText } from './AppText';

export function AlertBanner({ message }: { message: string }) {
  const { isRtl } = useLocale();
  return (
    <View accessibilityRole="alert" style={[styles.banner, { flexDirection: isRtl ? 'row-reverse' : 'row' }]}>
      <View style={styles.dot} />
      <AppText variant="caption" color={colors.danger} style={styles.copy}>{message}</AppText>
    </View>
  );
}

const styles = StyleSheet.create({
  banner: { alignItems: 'flex-start', gap: spacing.sm, padding: spacing.md, borderRadius: radius.md, backgroundColor: colors.dangerSoft },
  dot: { width: 7, height: 7, marginTop: 5, borderRadius: 4, backgroundColor: colors.danger },
  copy: { flex: 1 },
});
