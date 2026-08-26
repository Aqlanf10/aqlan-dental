import { Component, type ErrorInfo, type PropsWithChildren, type ReactNode } from 'react';
import { StyleSheet, View } from 'react-native';

import { AppScreen } from '@/components/AppScreen';
import { AppText } from '@/components/AppText';
import { BrandLockup } from '@/components/BrandLockup';
import { PrimaryButton } from '@/components/PrimaryButton';
import { useLocale } from '@/i18n/LocaleProvider';
import { colors, radius, spacing } from '@/theme/tokens';

type BoundaryProps = PropsWithChildren<{
  fallback: (reset: () => void) => ReactNode;
}>;

class Boundary extends Component<BoundaryProps, { failed: boolean }> {
  state = { failed: false };

  static getDerivedStateFromError() {
    return { failed: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Unhandled mobile UI error', error, info.componentStack);
  }

  reset = () => this.setState({ failed: false });

  render() {
    if (this.state.failed) return this.props.fallback(this.reset);
    return this.props.children;
  }
}

export function AppErrorBoundary({ children }: PropsWithChildren) {
  const { t } = useLocale();
  return (
    <Boundary fallback={(reset) => (
      <AppScreen>
        <View style={styles.center}>
          <BrandLockup compact />
          <View style={styles.card}>
            <View style={styles.marker} />
            <AppText variant="heading">{t('error.title')}</AppText>
            <AppText color={colors.muted}>{t('error.description')}</AppText>
            <PrimaryButton label={t('error.restart')} onPress={reset} />
          </View>
        </View>
      </AppScreen>
    )}>
      {children}
    </Boundary>
  );
}

const styles = StyleSheet.create({
  center: { flex: 1, justifyContent: 'center', gap: spacing.xxl },
  card: {
    gap: spacing.lg,
    padding: spacing.xxl,
    borderRadius: radius.xl,
    backgroundColor: colors.white,
    borderWidth: 1,
    borderColor: colors.border,
  },
  marker: { width: 44, height: 5, borderRadius: 3, backgroundColor: colors.orange500 },
});
