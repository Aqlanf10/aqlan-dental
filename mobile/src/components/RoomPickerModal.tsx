import { FlatList, Modal, Pressable, StyleSheet, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { useLocale } from '@/i18n/LocaleProvider';
import { colors, radius, spacing } from '@/theme/tokens';
import type { ClinicRoom } from '@/types/dailyOperations';
import { AppText } from './AppText';

type Props = {
  visible: boolean;
  rooms: ClinicRoom[];
  onClose: () => void;
  onSelect: (roomName?: string) => void;
};

export function RoomPickerModal({ visible, rooms, onClose, onSelect }: Props) {
  const { isRtl, t } = useLocale();
  return (
    <Modal animationType="slide" onRequestClose={onClose} transparent visible={visible}>
      <View style={styles.overlay}>
        <SafeAreaView style={styles.sheet} edges={['bottom']}>
          <View style={[styles.headingRow, { flexDirection: isRtl ? 'row-reverse' : 'row' }]}>
            <View style={styles.headingCopy}>
              <AppText variant="heading">{t('ops.selectRoom')}</AppText>
              <AppText color={colors.muted}>{t('ops.selectRoomDescription')}</AppText>
            </View>
            <Pressable accessibilityLabel={t('common.cancel')} accessibilityRole="button" onPress={onClose} style={styles.closeButton}>
              <AppText variant="heading" color={colors.navy900} style={styles.center}>×</AppText>
            </Pressable>
          </View>

          <Pressable accessibilityRole="button" onPress={() => onSelect()} style={styles.roomButton}>
            <AppText variant="label" color={colors.navy900}>{t('ops.noRoom')}</AppText>
          </Pressable>
          <FlatList
            contentContainerStyle={styles.roomList}
            data={rooms}
            keyExtractor={(room) => room.id}
            renderItem={({ item }) => (
              <Pressable accessibilityRole="button" onPress={() => onSelect(item.arabicName)} style={styles.roomButton}>
                <AppText variant="label" color={colors.navy900}>{item.arabicName}</AppText>
              </Pressable>
            )}
          />
        </SafeAreaView>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  overlay: { flex: 1, justifyContent: 'flex-end', backgroundColor: 'rgba(7, 23, 51, 0.5)' },
  sheet: {
    maxHeight: '70%',
    padding: spacing.xl,
    gap: spacing.md,
    borderTopLeftRadius: radius.xl,
    borderTopRightRadius: radius.xl,
    backgroundColor: colors.white,
  },
  headingRow: { alignItems: 'flex-start', justifyContent: 'space-between', gap: spacing.md },
  headingCopy: { flex: 1, gap: spacing.xs },
  closeButton: { width: 42, height: 42, alignItems: 'center', justifyContent: 'center', borderRadius: 21, backgroundColor: colors.blue100 },
  center: { textAlign: 'center' },
  roomList: { gap: spacing.sm, paddingBottom: spacing.md },
  roomButton: { minHeight: 50, justifyContent: 'center', paddingHorizontal: spacing.lg, borderRadius: radius.md, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.blue50 },
});
