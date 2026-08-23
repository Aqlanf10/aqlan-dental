import { Redirect, router } from 'expo-router';
import { StyleSheet, View } from 'react-native';

import { useAuth } from '@/auth/AuthProvider';
import { AppScreen } from '@/components/AppScreen';
import { AppText } from '@/components/AppText';
import { BrandLockup } from '@/components/BrandLockup';
import { LanguageSwitch } from '@/components/LanguageSwitch';
import { PrimaryButton } from '@/components/PrimaryButton';
import { useLocale } from '@/i18n/LocaleProvider';
import type { TranslationKey } from '@/i18n';
import { colors, radius, spacing } from '@/theme/tokens';

const roleKeys: Record<string, TranslationKey> = {
  Admin: 'role.Admin',
  Reception: 'role.Reception',
  Doctor: 'role.Doctor',
  Accountant: 'role.Accountant',
  Assistant: 'role.Assistant',
  Nurse: 'role.Nurse',
};

export default function HomeScreen() {
  const { busy, permissions, signOut, user } = useAuth();
  const { isRtl, t } = useLocale();

  if (!user) return <Redirect href="/sign-in" />;

  const logout = async () => {
    await signOut();
    router.replace('/sign-in');
  };

  const roleKey = roleKeys[user.role];
  const role = roleKey ? t(roleKey) : user.role;

  return (
    <AppScreen>
      <View style={styles.page}>
        <View style={styles.headerCard}>
          <BrandLockup compact inverse />
          <LanguageSwitch />
          <View style={styles.headerCopy}>
            <AppText variant="title" color={colors.white}>{t('home.greeting', { name: user.doctorName || user.username })}</AppText>
            <AppText color="#C8D8E9">{t('home.workspace')}</AppText>
          </View>
          <View style={[styles.connected, { flexDirection: isRtl ? 'row-reverse' : 'row' }]}>
            <View style={styles.connectedDot} />
            <AppText variant="caption" color="#C6F2E5">{t('status.connected')}</AppText>
          </View>
        </View>

        <View style={styles.card}>
          <View style={styles.orangeMarker} />
          <AppText variant="heading">{t('home.foundationReady')}</AppText>
          <AppText color={colors.muted}>{t('home.foundationDescription')}</AppText>
        </View>

        <View style={styles.card}>
          <AppText variant="subheading">{t('home.account')}</AppText>
          <InfoRow label={t('auth.username')} value={user.username} />
          <InfoRow label={t('home.role')} value={role} />
          <InfoRow label={t('home.branch')} value={user.branchId || t('common.notAvailable')} />
          <InfoRow label={t('home.permissions')} value={String(permissions?.permissions.length ?? 0)} />
        </View>

        <View style={styles.nextCard}>
          <AppText variant="caption" color={colors.orange600}>{t('home.nextUnit')}</AppText>
          <AppText variant="heading" color={colors.navy900}>{t('home.nextUnitName')}</AppText>
          <AppText color={colors.muted}>{t('home.nextUnitDescription')}</AppText>
        </View>

        <PrimaryButton busy={busy} label={t('auth.signOut')} onPress={logout} tone="secondary" />
      </View>
    </AppScreen>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  const { isRtl } = useLocale();
  return (
    <View style={[styles.infoRow, { flexDirection: isRtl ? 'row-reverse' : 'row' }]}>
      <AppText variant="label" color={colors.muted} style={styles.infoLabel}>{label}</AppText>
      <AppText variant="label" color={colors.navy900} style={styles.infoValue} numberOfLines={1}>{value}</AppText>
    </View>
  );
}

const styles = StyleSheet.create({
  page: { gap: spacing.lg, paddingBottom: spacing.xxxl },
  headerCard: {
    marginHorizontal: -20,
    marginTop: -16,
    paddingHorizontal: spacing.xl,
    paddingTop: spacing.xl,
    paddingBottom: spacing.xxxl,
    gap: spacing.xl,
    backgroundColor: colors.navy950,
    borderBottomLeftRadius: radius.xl,
    borderBottomRightRadius: radius.xl,
  },
  headerCopy: { gap: spacing.xs },
  connected: { alignSelf: 'flex-start', alignItems: 'center', gap: spacing.sm, paddingHorizontal: spacing.md, paddingVertical: 7, borderRadius: radius.pill, backgroundColor: '#163F50' },
  connectedDot: { width: 8, height: 8, borderRadius: 4, backgroundColor: '#4DD4A8' },
  card: { gap: spacing.md, padding: spacing.xl, borderRadius: radius.lg, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.white },
  orangeMarker: { width: 44, height: 5, borderRadius: 3, backgroundColor: colors.orange500 },
  infoRow: { alignItems: 'center', justifyContent: 'space-between', gap: spacing.lg, paddingTop: spacing.md, borderTopWidth: 1, borderTopColor: colors.blue100 },
  infoLabel: { flex: 1 },
  infoValue: { flex: 1, textAlign: 'auto' },
  nextCard: { gap: spacing.sm, padding: spacing.xl, borderRadius: radius.lg, backgroundColor: colors.orange100, borderWidth: 1, borderColor: '#FFD7A3' },
});
