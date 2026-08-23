import { Pressable, StyleSheet, View } from 'react-native';

import { useLocale } from '@/i18n/LocaleProvider';
import { colors, radius, spacing } from '@/theme/tokens';
import { AppText } from './AppText';

export function LanguageSwitch() {
  const { locale, setLocale, t } = useLocale();
  return (
    <View style={styles.container} accessibilityRole="radiogroup">
      {(['ar', 'en'] as const).map((item) => {
        const selected = item === locale;
        return (
          <Pressable
            accessibilityLabel={item === 'ar' ? t('language.switchToArabic') : t('language.switchToEnglish')}
            accessibilityRole="radio"
            accessibilityState={{ checked: selected }}
            key={item}
            onPress={() => setLocale(item)}
            style={({ pressed }) => [styles.option, selected && styles.selected, pressed && styles.pressed]}
          >
            <AppText variant="caption" color={selected ? colors.white : colors.navy800} style={styles.centerText}>
              {item === 'ar' ? t('language.arabic') : t('language.english')}
            </AppText>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    alignSelf: 'center',
    flexDirection: 'row',
    padding: 3,
    borderRadius: radius.pill,
    backgroundColor: colors.blue100,
    borderWidth: 1,
    borderColor: colors.border,
    gap: spacing.xs,
  },
  option: { minWidth: 80, paddingHorizontal: spacing.md, paddingVertical: 8, borderRadius: radius.pill },
  selected: { backgroundColor: colors.navy900 },
  pressed: { opacity: 0.75 },
  centerText: { textAlign: 'center' },
});
