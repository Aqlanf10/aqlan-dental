import type { PropsWithChildren } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { colors } from '@/theme/tokens';

type Props = PropsWithChildren<{
  keyboardAware?: boolean;
}>;

export function AppScreen({ children, keyboardAware = false }: Props) {
  const content = (
    <ScrollView
      contentContainerStyle={styles.content}
      keyboardShouldPersistTaps="handled"
      showsVerticalScrollIndicator={false}
    >
      {children}
    </ScrollView>
  );

  return (
    <SafeAreaView style={styles.safeArea}>
      <View pointerEvents="none" style={styles.orangeGlow} />
      <View pointerEvents="none" style={styles.blueGlow} />
      {keyboardAware ? (
        <KeyboardAvoidingView
          behavior={Platform.OS === 'ios' ? 'padding' : undefined}
          style={styles.flex}
        >
          {content}
        </KeyboardAvoidingView>
      ) : content}
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safeArea: { flex: 1, backgroundColor: colors.blue50, overflow: 'hidden' },
  flex: { flex: 1 },
  content: { flexGrow: 1, paddingHorizontal: 20, paddingVertical: 16 },
  orangeGlow: {
    position: 'absolute',
    width: 230,
    height: 230,
    borderRadius: 115,
    backgroundColor: colors.orange100,
    opacity: 0.7,
    top: -115,
    right: -85,
  },
  blueGlow: {
    position: 'absolute',
    width: 270,
    height: 270,
    borderRadius: 135,
    backgroundColor: colors.blue100,
    opacity: 0.75,
    bottom: -150,
    left: -120,
  },
});
