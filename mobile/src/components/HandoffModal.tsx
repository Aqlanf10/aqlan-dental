import { useEffect, useState } from 'react';
import { KeyboardAvoidingView, Modal, Platform, Pressable, ScrollView, StyleSheet, TextInput, View } from 'react-native';

import { useLocale } from '@/i18n/LocaleProvider';
import { colors, radius, spacing } from '@/theme/tokens';
import type { DailyOperationInput, DailyPatient } from '@/types/dailyOperations';
import { AppText } from './AppText';

type Props = {
  allowAmount: boolean;
  busy: boolean;
  errorMessage: string | null;
  item: DailyPatient;
  onClose: () => void;
  onSubmit: (input: DailyOperationInput) => void;
  visible: boolean;
};

export function HandoffModal({ allowAmount, busy, errorMessage, item, onClose, onSubmit, visible }: Props) {
  const { isRtl, t } = useLocale();
  const [treatmentDone, setTreatmentDone] = useState('');
  const [diagnosis, setDiagnosis] = useState('');
  const [proposedProcedure, setProposedProcedure] = useState('');
  const [amount, setAmount] = useState('');
  const [notes, setNotes] = useState('');
  const [validationError, setValidationError] = useState(false);

  useEffect(() => {
    if (!visible) return;
    setTreatmentDone(item.treatmentDone ?? item.serviceName ?? '');
    setDiagnosis('');
    setProposedProcedure(item.proposedProcedure ?? '');
    setAmount(allowAmount && item.amountDueReference ? String(item.amountDueReference) : '');
    setNotes('');
    setValidationError(false);
  }, [allowAmount, item, visible]);

  const textStyle = { textAlign: isRtl ? 'right' as const : 'left' as const, writingDirection: isRtl ? 'rtl' as const : 'ltr' as const };

  const submit = () => {
    const treatment = treatmentDone.trim();
    if (!treatment) {
      setValidationError(true);
      return;
    }
    const parsedAmount = allowAmount && amount.trim() ? Number(amount) : undefined;
    onSubmit({
      treatmentDone: treatment,
      diagnosis: diagnosis.trim() || undefined,
      proposedProcedure: proposedProcedure.trim() || undefined,
      amountDue: parsedAmount && parsedAmount > 0 ? parsedAmount : undefined,
      notes: notes.trim() || undefined,
    });
  };

  return (
    <Modal animationType="slide" onRequestClose={busy ? undefined : onClose} transparent visible={visible}>
      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={styles.overlay}>
        <View style={styles.sheet}>
          <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
            <AppText variant="heading">{t('ops.handoffTitle')}</AppText>
            <AppText color={colors.muted}>{t('ops.handoffDescription', { patient: item.patientName })}</AppText>

            <Field label={t('ops.treatmentDone')} required>
              <TextInput
                accessibilityLabel={t('ops.treatmentDone')}
                editable={!busy}
                multiline
                onChangeText={(value) => { setTreatmentDone(value); setValidationError(false); }}
                placeholder={t('ops.treatmentDonePlaceholder')}
                placeholderTextColor={colors.muted}
                style={[styles.input, styles.multiline, textStyle]}
                value={treatmentDone}
              />
            </Field>
            {validationError ? <AppText variant="caption" color={colors.danger}>{t('ops.treatmentRequired')}</AppText> : null}

            <Field label={t('ops.diagnosis')}>
              <TextInput accessibilityLabel={t('ops.diagnosis')} editable={!busy} onChangeText={setDiagnosis} placeholder={t('ops.optional')} placeholderTextColor={colors.muted} style={[styles.input, textStyle]} value={diagnosis} />
            </Field>
            <Field label={t('ops.proposedProcedure')}>
              <TextInput accessibilityLabel={t('ops.proposedProcedure')} editable={!busy} onChangeText={setProposedProcedure} placeholder={t('ops.optional')} placeholderTextColor={colors.muted} style={[styles.input, textStyle]} value={proposedProcedure} />
            </Field>
            {allowAmount ? (
              <Field label={t('ops.referenceAmount')}>
                <TextInput accessibilityLabel={t('ops.referenceAmount')} editable={!busy} keyboardType="decimal-pad" onChangeText={setAmount} placeholder={t('ops.optional')} placeholderTextColor={colors.muted} style={[styles.input, textStyle]} value={amount} />
              </Field>
            ) : null}
            <Field label={t('ops.handoffNotes')}>
              <TextInput accessibilityLabel={t('ops.handoffNotes')} editable={!busy} multiline onChangeText={setNotes} placeholder={t('ops.optional')} placeholderTextColor={colors.muted} style={[styles.input, styles.multiline, textStyle]} value={notes} />
            </Field>

            {errorMessage ? <View accessibilityRole="alert" style={styles.error}><AppText variant="caption" color={colors.danger}>{errorMessage}</AppText></View> : null}
            <View style={[styles.actions, { flexDirection: isRtl ? 'row-reverse' : 'row' }]}>
              <Pressable accessibilityRole="button" disabled={busy} onPress={onClose} style={[styles.button, styles.secondary, busy && styles.disabled]}>
                <AppText variant="label" color={colors.navy900}>{t('common.cancel')}</AppText>
              </Pressable>
              <Pressable accessibilityRole="button" disabled={busy} onPress={submit} style={[styles.button, styles.primary, busy && styles.disabled]}>
                <AppText variant="label" color={colors.white}>{busy ? t('ops.handingOff') : t('ops.confirmHandoff')}</AppText>
              </Pressable>
            </View>
          </ScrollView>
        </View>
      </KeyboardAvoidingView>
    </Modal>
  );
}

function Field({ children, label, required = false }: { children: React.ReactNode; label: string; required?: boolean }) {
  return (
    <View style={styles.field}>
      <AppText variant="label">{label}{required ? ' *' : ''}</AppText>
      {children}
    </View>
  );
}

const styles = StyleSheet.create({
  overlay: { flex: 1, justifyContent: 'flex-end', backgroundColor: 'rgba(7, 23, 51, 0.55)' },
  sheet: { maxHeight: '92%', borderTopLeftRadius: radius.xl, borderTopRightRadius: radius.xl, backgroundColor: colors.white },
  content: { gap: spacing.lg, padding: spacing.xl, paddingBottom: spacing.xxxl },
  field: { gap: spacing.sm },
  input: { minHeight: 48, paddingHorizontal: spacing.md, borderWidth: 1, borderColor: colors.border, borderRadius: radius.md, backgroundColor: colors.blue50, color: colors.ink, fontSize: 15 },
  multiline: { minHeight: 88, paddingTop: spacing.md, textAlignVertical: 'top' },
  error: { padding: spacing.md, borderRadius: radius.md, backgroundColor: colors.dangerSoft },
  actions: { gap: spacing.sm },
  button: { flex: 1, minHeight: 48, alignItems: 'center', justifyContent: 'center', borderRadius: radius.md },
  primary: { backgroundColor: colors.orange600 },
  secondary: { borderWidth: 1, borderColor: colors.border, backgroundColor: colors.white },
  disabled: { opacity: 0.5 },
});
