import { Redirect, router } from 'expo-router';
import { useState } from 'react';
import { Platform, StyleSheet, View } from 'react-native';

import { ApiError } from '@/api/client';
import { useAuth } from '@/auth/AuthProvider';
import { AlertBanner } from '@/components/AlertBanner';
import { AppScreen } from '@/components/AppScreen';
import { AppText } from '@/components/AppText';
import { BrandLockup } from '@/components/BrandLockup';
import { FormField } from '@/components/FormField';
import { LanguageSwitch } from '@/components/LanguageSwitch';
import { PrimaryButton } from '@/components/PrimaryButton';
import { useLocale } from '@/i18n/LocaleProvider';
import { colors, radius, spacing } from '@/theme/tokens';

export default function SignInScreen() {
  const { busy, signIn, user } = useAuth();
  const { locale, t } = useLocale();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [passwordVisible, setPasswordVisible] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canSubmit = username.trim().length > 0 && password.length > 0;
  if (user) return <Redirect href="/home" />;

  const submit = async () => {
    if (!canSubmit) {
      setError(t('auth.required'));
      return;
    }
    setError(null);
    try {
      await signIn(username, password);
      router.replace('/home');
    } catch (caught) {
      if (caught instanceof ApiError) {
        if (caught.kind === 'network' || caught.kind === 'timeout') setError(t('auth.networkError'));
        else if (caught.kind === 'invalid-response') setError(t('auth.invalidResponse'));
        else if (caught.status === 401) setError(t('auth.invalidCredentials'));
        else if (caught.status === 429) setError(locale === 'ar' && caught.message ? caught.message : t('auth.accountLocked'));
        else setError(t('auth.genericError'));
      } else {
        setError(t('auth.genericError'));
      }
    }
  };

  return (
    <AppScreen keyboardAware>
      <View style={styles.page}>
        <View style={styles.topBar}>
          <BrandLockup compact />
          <LanguageSwitch />
        </View>

        <View style={styles.hero}>
          <View style={styles.eyebrow} />
          <AppText variant="title" color={colors.navy950}>{t('auth.welcome')}</AppText>
          <AppText color={colors.muted}>{t('auth.subtitle')}</AppText>
        </View>

        <View style={styles.card}>
          <FormField
            autoCapitalize="none"
            autoComplete="username"
            editable={!busy}
            label={t('auth.username')}
            onChangeText={setUsername}
            placeholder={t('auth.usernamePlaceholder')}
            returnKeyType="next"
            textContentType="username"
            value={username}
          />
          <FormField
            actionLabel={passwordVisible ? t('auth.hidePassword') : t('auth.showPassword')}
            autoCapitalize="none"
            autoComplete="current-password"
            editable={!busy}
            label={t('auth.password')}
            onAction={() => setPasswordVisible((value) => !value)}
            onChangeText={setPassword}
            onSubmitEditing={submit}
            placeholder={t('auth.passwordPlaceholder')}
            returnKeyType="go"
            secureTextEntry={!passwordVisible}
            textContentType="password"
            value={password}
          />
          {error ? <AlertBanner message={error} /> : null}
          <PrimaryButton
            busy={busy}
            disabled={!canSubmit}
            label={busy ? t('auth.signingIn') : t('auth.signIn')}
            onPress={submit}
          />
          <View style={styles.securityLine}>
            <View style={styles.securityDot} />
            <AppText variant="caption" color={colors.success} style={styles.securityCopy}>
              {t('common.secure')}
            </AppText>
          </View>
        </View>

        <View style={styles.footer}>
          <AppText variant="caption" color={colors.muted} style={styles.centerText}>{t('brand.address')}</AppText>
          <AppText variant="caption" color={colors.muted} style={styles.centerText}>{t('brand.phone')}</AppText>
        </View>
      </View>
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  page: { flex: 1, justifyContent: 'center', gap: spacing.xxl, paddingVertical: Platform.OS === 'android' ? spacing.lg : 0 },
  topBar: { gap: spacing.xl },
  hero: { gap: spacing.sm },
  eyebrow: { width: 52, height: 5, borderRadius: 3, backgroundColor: colors.orange500 },
  card: {
    gap: spacing.lg,
    padding: spacing.xl,
    backgroundColor: colors.white,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.xl,
    shadowColor: colors.shadow,
    shadowOpacity: 0.1,
    shadowRadius: 24,
    shadowOffset: { width: 0, height: 10 },
    elevation: 4,
  },
  securityLine: { flexDirection: 'row', justifyContent: 'center', alignItems: 'center', gap: spacing.sm },
  securityDot: { width: 7, height: 7, borderRadius: 4, backgroundColor: colors.success },
  securityCopy: { textAlign: 'center' },
  footer: { alignItems: 'center', gap: 2 },
  centerText: { textAlign: 'center' },
});
