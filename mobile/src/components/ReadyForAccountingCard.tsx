import { memo } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, View } from 'react-native';

import { useLocale } from '@/i18n/LocaleProvider';
import { colors, radius, spacing } from '@/theme/tokens';
import type { DailyPatient } from '@/types/dailyOperations';
import { AppText } from './AppText';

type Props = {
  busy: boolean;
  canCreateDraft: boolean;
  canViewAmount: boolean;
  item: DailyPatient;
  onCreateDraft: (item: DailyPatient) => void;
};

export const ReadyForAccountingCard = memo(function ReadyForAccountingCard({ busy, canCreateDraft, canViewAmount, item, onCreateDraft }: Props) {
  const { isRtl, locale, t } = useLocale();
  const procedure = item.treatmentDone || item.proposedProcedure || item.serviceName;
  const amount = item.amountDueReference === null
    ? t('common.notAvailable')
    : new Intl.NumberFormat(locale === 'ar' ? 'ar-YE' : 'en-US', { maximumFractionDigits: 2 }).format(item.amountDueReference);

  return (
    <View style={styles.card}>
      <View style={[styles.heading, { flexDirection: isRtl ? 'row-reverse' : 'row' }]}>
        <View style={styles.copy}>
          <AppText variant="subheading">{item.patientName}</AppText>
          <AppText variant="caption" color={colors.muted}>{item.doctorName}</AppText>
        </View>
        <View style={styles.readyPill}><AppText variant="caption" color={colors.success}>{t('ops.readyForAccounting')}</AppText></View>
      </View>
      <View style={styles.details}>
        <Row label={t('ops.serviceOrProcedure')} value={procedure} />
        {canViewAmount ? <Row label={t('ops.referenceAmount')} value={amount} /> : null}
        <Row label={t('ops.draftStatus')} value={t(item.hasDraftInvoice ? 'ops.draftExists' : 'ops.noDraft')} />
        <Row
          label={t('ops.nextStep')}
          value={item.hasDraftInvoice
            ? t('ops.reviewDraft')
            : canCreateDraft
              ? t('ops.createDraftNext')
              : t('ops.awaitFinanceUser')}
        />
      </View>
      {canCreateDraft && !item.hasDraftInvoice && item.visitId ? (
        <Pressable accessibilityRole="button" disabled={busy} onPress={() => onCreateDraft(item)} style={[styles.button, busy && styles.disabled]}>
          {busy ? <ActivityIndicator color={colors.white} size="small" /> : <AppText variant="label" color={colors.white}>{t('ops.action.createDraft')}</AppText>}
        </Pressable>
      ) : null}
    </View>
  );
});

function Row({ label, value }: { label: string; value: string | null }) {
  const { isRtl, t } = useLocale();
  return (
    <View style={[styles.row, { flexDirection: isRtl ? 'row-reverse' : 'row' }]}>
      <AppText variant="caption" color={colors.muted}>{label}</AppText>
      <AppText variant="label" numberOfLines={2}>{value || t('common.notAvailable')}</AppText>
    </View>
  );
}

const styles = StyleSheet.create({
  card: { gap: spacing.md, marginHorizontal: spacing.xl, padding: spacing.lg, borderWidth: 1, borderColor: colors.border, borderRadius: radius.lg, backgroundColor: colors.white },
  heading: { alignItems: 'center', gap: spacing.md },
  copy: { flex: 1, gap: 2 },
  readyPill: { paddingHorizontal: spacing.sm, paddingVertical: 5, borderRadius: radius.pill, backgroundColor: colors.successSoft },
  details: { gap: spacing.sm, paddingTop: spacing.sm, borderTopWidth: 1, borderTopColor: colors.blue100 },
  row: { alignItems: 'flex-start', justifyContent: 'space-between', gap: spacing.md },
  button: { minHeight: 46, alignItems: 'center', justifyContent: 'center', borderRadius: radius.md, backgroundColor: colors.orange600 },
  disabled: { opacity: 0.5 },
});
