import { useState } from 'react';
import { KeyboardAvoidingView, Modal, Platform, Pressable, ScrollView, StyleSheet, TextInput, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { useLocale } from '@/i18n/LocaleProvider';
import { colors, radius, spacing } from '@/theme/tokens';
import type { ClinicRoom, DailyPatient } from '@/types/dailyOperations';
import { AppText } from './AppText';
import { AlertBanner } from './AlertBanner';
import { PrimaryButton } from './PrimaryButton';

type JourneyStep = 'intake' | 'send-to-queue';

type Props = {
  busy: boolean;
  errorMessage?: string | null;
  item: DailyPatient;
  mode: JourneyStep;
  rooms: ClinicRoom[];
  onClose: () => void;
  onSubmit: (input: { roomId?: string; notes?: string }) => void;
};

export function JourneyStepModal({ busy, errorMessage, item, mode, rooms, onClose, onSubmit }: Props) {
  const { isRtl, t } = useLocale();
  const [roomId, setRoomId] = useState(item.roomId ?? '');
  const [notes, setNotes] = useState('');
  const title = mode === 'intake' ? t('ops.intakeTitle') : t('ops.queueTitle');
  const description = mode === 'intake' ? t('ops.intakeDescription') : t('ops.queueDescription');
  const submitLabel = mode === 'intake' ? t('ops.action.intake') : t('ops.action.sendToQueue');

  return (
    <Modal animationType="slide" onRequestClose={busy ? undefined : onClose} transparent visible>
      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={styles.overlay}>
        <SafeAreaView edges={['bottom']} style={styles.sheet}>
          <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled" showsVerticalScrollIndicator={false}>
            <View style={[styles.headingRow, { flexDirection: isRtl ? 'row-reverse' : 'row' }]}>
              <View style={styles.headingCopy}>
                <AppText variant="heading">{title}</AppText>
                <AppText color={colors.muted}>{description}</AppText>
              </View>
              <Pressable
                accessibilityLabel={t('common.cancel')}
                accessibilityRole="button"
                disabled={busy}
                onPress={onClose}
                style={[styles.closeButton, busy && styles.disabled]}
              >
                <AppText variant="heading" color={colors.navy900} style={styles.center}>×</AppText>
              </Pressable>
            </View>

            <View style={styles.patientCard}>
              <AppText variant="caption" color={colors.orange600}>{t('ops.patientDetails')}</AppText>
              <AppText variant="subheading">{item.patientName}</AppText>
              <AppText variant="caption" color={colors.muted}>{item.appointmentTime || '—'} · {item.doctorName}</AppText>
            </View>

            {errorMessage ? <AlertBanner message={errorMessage} /> : null}

            <View style={styles.field}>
              <AppText variant="label">{t('ops.roomOptional')}</AppText>
              <View style={[styles.roomOptions, { flexDirection: isRtl ? 'row-reverse' : 'row' }]}>
                <RoomOption active={!roomId} label={t('ops.noRoom')} onPress={() => setRoomId('')} />
                {rooms.map((room) => (
                  <RoomOption active={roomId === room.id} key={room.id} label={room.arabicName} onPress={() => setRoomId(room.id)} />
                ))}
              </View>
            </View>

            <View style={styles.field}>
              <AppText variant="label">{t('ops.notesOptional')}</AppText>
              <TextInput
                accessibilityLabel={t('ops.notesOptional')}
                editable={!busy}
                maxLength={500}
                multiline
                onChangeText={setNotes}
                placeholder={t('ops.notesPlaceholder')}
                placeholderTextColor={colors.muted}
                style={[styles.notesInput, { textAlign: isRtl ? 'right' : 'left', writingDirection: isRtl ? 'rtl' : 'ltr' }]}
                value={notes}
              />
            </View>

            <View style={styles.actions}>
              <PrimaryButton
                busy={busy}
                label={submitLabel}
                onPress={() => onSubmit({ roomId: roomId || undefined, notes: notes.trim() || undefined })}
              />
              <PrimaryButton disabled={busy} label={t('common.cancel')} onPress={onClose} tone="secondary" />
            </View>
          </ScrollView>
        </SafeAreaView>
      </KeyboardAvoidingView>
    </Modal>
  );
}

function RoomOption({ active, label, onPress }: { active: boolean; label: string; onPress: () => void }) {
  return (
    <Pressable
      accessibilityRole="radio"
      accessibilityState={{ checked: active }}
      onPress={onPress}
      style={[styles.roomOption, active && styles.activeRoom]}
    >
      <AppText variant="caption" color={active ? colors.white : colors.navy900} style={styles.center}>{label}</AppText>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  overlay: { flex: 1, justifyContent: 'flex-end', backgroundColor: 'rgba(7, 23, 51, 0.55)' },
  sheet: { maxHeight: '88%', borderTopLeftRadius: radius.xl, borderTopRightRadius: radius.xl, backgroundColor: colors.white },
  content: { gap: spacing.xl, padding: spacing.xl, paddingBottom: spacing.xxxl },
  headingRow: { alignItems: 'flex-start', justifyContent: 'space-between', gap: spacing.md },
  headingCopy: { flex: 1, gap: spacing.xs },
  closeButton: { width: 42, height: 42, alignItems: 'center', justifyContent: 'center', borderRadius: 21, backgroundColor: colors.blue100 },
  center: { textAlign: 'center' },
  disabled: { opacity: 0.5 },
  patientCard: { gap: spacing.xs, padding: spacing.lg, borderRadius: radius.md, backgroundColor: colors.orange100 },
  field: { gap: spacing.sm },
  roomOptions: { flexWrap: 'wrap', gap: spacing.sm },
  roomOption: { minHeight: 42, justifyContent: 'center', paddingHorizontal: spacing.md, borderRadius: radius.pill, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.blue50 },
  activeRoom: { borderColor: colors.navy900, backgroundColor: colors.navy900 },
  notesInput: { minHeight: 110, padding: spacing.md, borderRadius: radius.md, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.blue50, color: colors.ink, fontSize: 15, textAlignVertical: 'top' },
  actions: { gap: spacing.sm },
});
