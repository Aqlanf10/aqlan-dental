import 'react-native-gesture-handler';

import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';

import { AuthProvider } from '@/auth/AuthProvider';
import { AppErrorBoundary } from '@/errors/AppErrorBoundary';
import { LocaleProvider } from '@/i18n/LocaleProvider';

export default function RootLayout() {
  return (
    <LocaleProvider>
      <AppErrorBoundary>
        <AuthProvider>
          <StatusBar style="dark" />
          <Stack screenOptions={{ headerShown: false, animation: 'fade' }}>
            <Stack.Screen name="index" />
            <Stack.Screen name="sign-in" />
            <Stack.Screen name="home" />
          </Stack>
        </AuthProvider>
      </AppErrorBoundary>
    </LocaleProvider>
  );
}
