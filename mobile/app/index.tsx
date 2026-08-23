import { Redirect } from 'expo-router';
import { ActivityIndicator, StyleSheet, View } from 'react-native';

import { useAuth } from '@/auth/AuthProvider';
import { useLocale } from '@/i18n/LocaleProvider';
import { colors } from '@/theme/tokens';

export default function IndexScreen() {
  const { initializing, user } = useAuth();
  const { ready } = useLocale();

  if (initializing || !ready) {
    return (
      <View style={styles.loading}>
        <ActivityIndicator color={colors.orange500} size="large" />
      </View>
    );
  }

  return <Redirect href={user ? '/home' : '/sign-in'} />;
}

const styles = StyleSheet.create({
  loading: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.blue50 },
});
