import { Image, StyleSheet, View } from 'react-native';

import { useLocale } from '@/i18n/LocaleProvider';
import { useClinicIdentity } from '@/identity/ClinicIdentityProvider';
import { colors, spacing } from '@/theme/tokens';
import { AppText } from './AppText';

type Props = { compact?: boolean; inverse?: boolean };

export function BrandLockup({ compact = false, inverse = false }: Props) {
  const { isRtl, t } = useLocale();
  // اسم المركز من الإعدادات لا من الكود: تعديله في الإعدادات يغيّر الموقع وكل PDF،
  // وكان التطبيق وحده يبقى على الاسم القديم حتى تُبنى نسخة جديدة.
  const identity = useClinicIdentity();
  return (
    <View style={[styles.row, { flexDirection: isRtl ? 'row-reverse' : 'row' }]}>
      <View style={[styles.logoFrame, compact && styles.logoFrameCompact, inverse && styles.logoFrameInverse]}>
        <Image source={inverse ? require('../../assets/logo-white.png') : require('../../assets/logo.png')} style={styles.logo} />
      </View>
      <View style={styles.copy}>
        <AppText variant={compact ? 'subheading' : 'heading'} color={inverse ? colors.white : colors.navy900}>
          {t('app.name')}
        </AppText>
        <AppText variant="caption" color={inverse ? '#DDE8F5' : colors.muted} numberOfLines={compact ? 1 : 2}>
          {identity?.clinicName ?? t('brand.clinicName')}
        </AppText>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  row: { alignItems: 'center', gap: spacing.md },
  logoFrame: {
    width: 76,
    height: 76,
    borderRadius: 22,
    backgroundColor: colors.white,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1,
    borderColor: colors.border,
  },
  logoFrameCompact: { width: 56, height: 56, borderRadius: 17 },
  logoFrameInverse: { backgroundColor: colors.navy800, borderColor: '#31567F' },
  logo: { width: '84%', height: '84%', resizeMode: 'contain' },
  copy: { flex: 1, gap: 2 },
});
