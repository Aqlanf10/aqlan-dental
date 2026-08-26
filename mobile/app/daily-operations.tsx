import { Redirect, router } from 'expo-router';
import { memo, useCallback, useMemo, useState } from 'react';
import { ActivityIndicator, FlatList, Pressable, StyleSheet, TextInput, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { ApiError, SessionRenewedError } from '@/api/client';
import { useAuth } from '@/auth/AuthProvider';
import { AlertBanner } from '@/components/AlertBanner';
import { AppText } from '@/components/AppText';
import { HandoffModal } from '@/components/HandoffModal';
import { JourneyStepModal } from '@/components/JourneyStepModal';
import { LanguageSwitch } from '@/components/LanguageSwitch';
import { ReadyForAccountingCard } from '@/components/ReadyForAccountingCard';
import { RoomPickerModal } from '@/components/RoomPickerModal';
import { useLocale } from '@/i18n/LocaleProvider';
import type { TranslationKey } from '@/i18n';
import { useDailyOperations } from '@/operations/useDailyOperations';
import { colors, radius, spacing } from '@/theme/tokens';
import type { DailyOperationAction, DailyPatient } from '@/types/dailyOperations';

type Tab = 'arrivals' | 'waiting' | 'accounting';
type RoomRequest = { item: DailyPatient; action: 'call' | 'recall' } | null;
type JourneyRequest = { item: DailyPatient; action: 'intake' | 'send-to-queue' } | null;

const statusKeys: Record<string, TranslationKey> = {
  scheduled: 'ops.status.scheduled',
  confirmed: 'ops.status.confirmed',
  arrived: 'ops.status.arrived',
  waiting: 'ops.status.waiting',
  called: 'ops.status.called',
  inroom: 'ops.status.inRoom',
  inprogress: 'ops.status.inProgress',
  completed: 'ops.status.completed',
  noshow: 'ops.status.noShow',
  cancelled: 'ops.status.cancelled',
  canceled: 'ops.status.cancelled',
};

const actionKeys: Record<DailyOperationAction, TranslationKey> = {
  intake: 'ops.action.intake',
  'send-to-queue': 'ops.action.sendToQueue',
  call: 'ops.action.call',
  recall: 'ops.action.recall',
  'enter-room': 'ops.action.enterRoom',
  'start-visit': 'ops.action.startVisit',
  handoff: 'ops.action.handoff',
  'create-draft-invoice': 'ops.action.createDraft',
};

function normalizedStatus(item: DailyPatient) {
  return (item.queueStatus || item.visitStatus || item.appointmentStatus).replaceAll(/[-_\s]/g, '').toLowerCase();
}

function patientKey(item: DailyPatient) {
  return `${item.patientId}:${item.queueItemId ?? item.appointmentId ?? 'patient'}`;
}

export default function DailyOperationsScreen() {
  const { hasPermission, user } = useAuth();
  const { isRtl, t } = useLocale();
  const canView = hasPermission('patient_journey.view') || hasPermission('clinic_queue.view');
  const operations = useDailyOperations(canView);
  const runAction = operations.runAction;
  const [tab, setTab] = useState<Tab>('arrivals');
  const [query, setQuery] = useState('');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [roomRequest, setRoomRequest] = useState<RoomRequest>(null);
  const [journeyRequest, setJourneyRequest] = useState<JourneyRequest>(null);
  const [handoffRequest, setHandoffRequest] = useState<DailyPatient | null>(null);
  const canViewAmount = hasPermission('finance.patient_balance.view') || hasPermission('finance.view');
  const isFinanceRole = user?.role === 'Admin' || user?.role === 'Reception' || user?.role === 'Accountant';
  const canCreateDraft = isFinanceRole && hasPermission('finance.view');

  const filtered = useMemo(() => {
    const normalizedQuery = query.trim().toLocaleLowerCase();
    return operations.items.filter((item) => {
      const status = normalizedStatus(item);
      const inTab = tab === 'arrivals'
        ? !item.queueItemId || ['scheduled', 'confirmed', 'arrived'].includes(status)
        : tab === 'waiting'
          ? Boolean(item.queueItemId) && !['completed', 'cancelled', 'canceled', 'noshow'].includes(status)
          : item.checkoutStatus === 'ReadyForCheckout';
      if (!inTab) return false;
      if (!normalizedQuery) return true;
      return [item.patientName, item.patientNumber, item.patientPhone, item.doctorName, item.roomName]
        .some((value) => value?.toLocaleLowerCase().includes(normalizedQuery));
    });
  }, [operations.items, query, tab]);

  const selected = useMemo(() => (
    filtered.find((item) => patientKey(item) === selectedId) ?? filtered[0] ?? null
  ), [filtered, selectedId]);
  const selectedKey = selected ? patientKey(selected) : null;
  const summary = useMemo(() => {
    let arrivals = 0;
    let waiting = 0;
    let accounting = 0;
    for (const item of operations.items) {
      const status = normalizedStatus(item);
      if (item.checkoutStatus === 'ReadyForCheckout') accounting += 1;
      else if (!item.queueItemId) arrivals += 1;
      else if (!['completed', 'cancelled', 'canceled'].includes(status)) waiting += 1;
    }
    return { arrivals, waiting, accounting };
  }, [operations.items]);

  const runRoomAction = useCallback((roomName?: string) => {
    if (!roomRequest) return;
    const pending = roomRequest;
    setRoomRequest(null);
    void runAction(pending.item, pending.action, { roomName });
  }, [roomRequest, runAction]);

  const requestAction = useCallback((item: DailyPatient, action: DailyOperationAction) => {
    if (action === 'call' || action === 'recall') setRoomRequest({ item, action });
    else if (action === 'intake' || action === 'send-to-queue') setJourneyRequest({ item, action });
    else if (action === 'handoff') setHandoffRequest(item);
    else void runAction(item, action);
  }, [runAction]);

  const submitJourneyStep = useCallback(async (input: { roomId?: string; notes?: string }) => {
    if (!journeyRequest) return;
    const succeeded = await runAction(journeyRequest.item, journeyRequest.action, input);
    if (succeeded) setJourneyRequest(null);
  }, [journeyRequest, runAction]);

  const submitHandoff = useCallback(async (input: Parameters<typeof runAction>[2]) => {
    if (!handoffRequest) return;
    const succeeded = await runAction(handoffRequest, 'handoff', input);
    if (succeeded) setHandoffRequest(null);
  }, [handoffRequest, runAction]);

  const createDraft = useCallback((item: DailyPatient) => {
    void runAction(item, 'create-draft-invoice');
  }, [runAction]);

  if (!user) return <Redirect href="/sign-in" />;

  const errorMessage = operations.error ? describeError(operations.error, t) : null;
  const noticeMessage = operations.notice ? t('ops.actionSuccess', { action: t(actionKeys[operations.notice]) }) : null;

  return (
    <SafeAreaView style={styles.screen}>
      <FlatList
        contentContainerStyle={styles.content}
        data={canView && !operations.loading ? filtered : []}
        keyExtractor={patientKey}
        keyboardShouldPersistTaps="handled"
        ListEmptyComponent={canView && !operations.loading ? <EmptyState query={query} tab={tab} /> : null}
        ListHeaderComponent={(
          <View style={styles.headerContent}>
            <View style={[styles.topBar, { flexDirection: isRtl ? 'row-reverse' : 'row' }]}>
              <Pressable accessibilityLabel={t('common.back')} accessibilityRole="button" onPress={() => router.back()} style={styles.backButton}>
                <AppText variant="heading" color={colors.white} style={styles.center}>{isRtl ? '→' : '←'}</AppText>
              </Pressable>
              <View style={styles.titleCopy}>
                <AppText variant="heading" color={colors.white}>{t('ops.title')}</AppText>
                <AppText variant="caption" color="#C8D8E9">{t('ops.subtitle')}</AppText>
              </View>
              <LanguageSwitch compact />
            </View>

            <View style={[styles.summaryRow, { flexDirection: isRtl ? 'row-reverse' : 'row' }]}>
              <Summary value={summary.arrivals} label={t('ops.arrivals')} />
              <Summary value={summary.waiting} label={t('ops.waiting')} />
              <Summary value={summary.accounting} label={t('ops.readyForAccounting')} />
            </View>

            {!canView ? <AlertBanner message={t('ops.noAccess')} /> : null}
            {errorMessage ? <AlertBanner message={errorMessage} /> : null}
            {noticeMessage ? <NoticeBanner message={noticeMessage} /> : null}

            <View style={styles.tabs}>
              <View style={[styles.tabRow, { flexDirection: isRtl ? 'row-reverse' : 'row' }]}>
                <TabButton active={tab === 'arrivals'} label={t('ops.arrivals')} onPress={() => setTab('arrivals')} />
                <TabButton active={tab === 'waiting'} label={t('ops.waiting')} onPress={() => setTab('waiting')} />
                <TabButton active={tab === 'accounting'} label={t('ops.readyForAccounting')} onPress={() => setTab('accounting')} />
              </View>
              <View style={[styles.searchRow, { flexDirection: isRtl ? 'row-reverse' : 'row' }]}>
                <TextInput
                  accessibilityLabel={t('ops.search')}
                  onChangeText={setQuery}
                  placeholder={t('ops.searchPlaceholder')}
                  placeholderTextColor={colors.muted}
                  style={[styles.searchInput, { textAlign: isRtl ? 'right' : 'left', writingDirection: isRtl ? 'rtl' : 'ltr' }]}
                  value={query}
                />
                <Pressable
                  accessibilityLabel={t('ops.refresh')}
                  accessibilityRole="button"
                  disabled={operations.refreshing}
                  onPress={() => void operations.refresh()}
                  style={[styles.refreshButton, operations.refreshing && styles.disabled]}
                >
                  {operations.refreshing ? <ActivityIndicator color={colors.navy900} size="small" /> : <AppText variant="label" color={colors.navy900}>{t('ops.refresh')}</AppText>}
                </Pressable>
              </View>
            </View>

            {operations.loading && canView ? (
              <View style={styles.loadingBox}>
                <ActivityIndicator color={colors.orange600} />
                <AppText color={colors.muted}>{t('ops.loading')}</AppText>
              </View>
            ) : null}

            {selected && tab !== 'accounting' && canView && !operations.loading ? (
              <PatientDetails
                busyAction={operations.busyAction}
                item={selected}
                onAction={requestAction}
              />
            ) : null}

            {canView && !operations.loading ? <AppText variant="subheading">{t('ops.patientList')}</AppText> : null}
          </View>
        )}
        renderItem={({ item }) => tab === 'accounting' ? (
          <ReadyForAccountingCard
            busy={operations.busyAction?.itemId === (item.queueItemId ?? item.appointmentId ?? item.patientId)}
            canCreateDraft={canCreateDraft}
            canViewAmount={canViewAmount}
            item={item}
            onCreateDraft={createDraft}
          />
        ) : (
          <PatientCard item={item} onSelect={setSelectedId} selected={selectedKey === patientKey(item)} />
        )}
        showsVerticalScrollIndicator={false}
      />
      <RoomPickerModal
        onClose={() => setRoomRequest(null)}
        onSelect={runRoomAction}
        rooms={operations.rooms}
        visible={roomRequest !== null}
      />
      {journeyRequest ? (
        <JourneyStepModal
          busy={operations.busyAction?.action === journeyRequest.action}
          errorMessage={errorMessage}
          item={journeyRequest.item}
          key={`${patientKey(journeyRequest.item)}:${journeyRequest.action}`}
          mode={journeyRequest.action}
          onClose={() => setJourneyRequest(null)}
          onSubmit={(input) => { void submitJourneyStep(input); }}
          rooms={operations.rooms}
        />
      ) : null}
      {handoffRequest ? (
        <HandoffModal
          allowAmount={canViewAmount}
          busy={operations.busyAction?.action === 'handoff'}
          errorMessage={errorMessage}
          item={handoffRequest}
          onClose={() => setHandoffRequest(null)}
          onSubmit={(input) => { void submitHandoff(input); }}
          visible
        />
      ) : null}
    </SafeAreaView>
  );
}

function Summary({ value, label }: { value: number; label: string }) {
  return (
    <View style={styles.summaryCard}>
      <AppText variant="title" color={colors.navy900}>{String(value)}</AppText>
      <AppText variant="caption" color={colors.muted}>{label}</AppText>
    </View>
  );
}

function TabButton({ active, label, onPress }: { active: boolean; label: string; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="tab" accessibilityState={{ selected: active }} onPress={onPress} style={[styles.tabButton, active && styles.activeTab]}>
      <AppText variant="label" color={active ? colors.white : colors.navy900} style={styles.center}>{label}</AppText>
    </Pressable>
  );
}

const PatientCard = memo(function PatientCard({ item, selected, onSelect }: { item: DailyPatient; selected: boolean; onSelect: (id: string) => void }) {
  const { isRtl, t } = useLocale();
  const statusKey = statusKeys[normalizedStatus(item)];
  return (
    <Pressable
      accessibilityLabel={`${item.patientName}, ${statusKey ? t(statusKey) : item.queueStatus || item.appointmentStatus}`}
      accessibilityRole="button"
      onPress={() => onSelect(patientKey(item))}
      style={[styles.patientCard, selected && styles.selectedCard]}
    >
      <View style={[styles.patientHeading, { flexDirection: isRtl ? 'row-reverse' : 'row' }]}>
        <View style={styles.avatar}><AppText variant="subheading" color={colors.white} style={styles.center}>{item.patientName.trim().charAt(0)}</AppText></View>
        <View style={styles.patientCopy}>
          <AppText variant="subheading" numberOfLines={1}>{item.patientName}</AppText>
          <AppText variant="caption" color={colors.muted}>{item.patientNumber || item.patientPhone || t('common.notAvailable')}</AppText>
        </View>
        <View style={styles.statusPill}><AppText variant="caption" color={colors.navy900}>{statusKey ? t(statusKey) : item.queueStatus || item.appointmentStatus}</AppText></View>
      </View>
      <View style={[styles.metaRow, { flexDirection: isRtl ? 'row-reverse' : 'row' }]}>
        <AppText variant="caption" color={colors.muted}>{item.appointmentTime || '—'}</AppText>
        <AppText variant="caption" color={colors.muted} numberOfLines={1}>{item.doctorName}</AppText>
        {item.roomName ? <AppText variant="caption" color={colors.orange600}>{item.roomName}</AppText> : null}
      </View>
    </Pressable>
  );
});

function PatientDetails({ item, busyAction, onAction }: {
  item: DailyPatient;
  busyAction: { itemId: string; action: DailyOperationAction } | null;
  onAction: (item: DailyPatient, action: DailyOperationAction) => void;
}) {
  const { isRtl, t } = useLocale();
  const { hasPermission, user } = useAuth();
  const status = normalizedStatus(item);
  const itemId = item.queueItemId ?? item.appointmentId ?? item.patientId;
  const isBusy = busyAction?.itemId === itemId;
  const actions: DailyOperationAction[] = [];
  const canCreateDaily = hasPermission('daily_operations.create');
  const paymentBlocked = item.paymentBeforeEntryRequired
    || item.financialEntryStatus === 'WaitingForPayment'
    || item.canEnterWithoutPayment === false;
  if ((status === 'scheduled' || status === 'confirmed') && item.appointmentId && canCreateDaily) actions.push('intake');
  if ((status === 'arrived' || status === 'waiting') && item.appointmentId && !item.queueItemId && canCreateDaily) actions.push('send-to-queue');
  if (status === 'waiting' && item.queueItemId && hasPermission('clinic_queue.create')) actions.push('call');
  if (status === 'called' && item.queueItemId && hasPermission('clinic_queue.edit')) actions.push('recall');
  if (status === 'called' && item.queueItemId && hasPermission('clinic_queue.approve') && !paymentBlocked) actions.push('enter-room');
  if (status === 'inroom' && item.appointmentId && (user?.role === 'Doctor' || hasPermission('visits.edit'))) actions.push('start-visit');
  const isDoctorRole = user?.role === 'Admin' || user?.role === 'Orthodontist' || user?.role === 'GeneralDentist' || user?.role === 'OralSurgeon';
  const shouldHandoff = status === 'inprogress' || item.nextAction === 'HandoffToReception';
  if (shouldHandoff && item.visitId && isDoctorRole) actions.push('handoff');

  return (
    <View style={styles.detailsCard}>
      <View style={[styles.detailHeading, { flexDirection: isRtl ? 'row-reverse' : 'row' }]}>
        <View style={styles.detailHeadingCopy}>
          <AppText variant="caption" color={colors.orange600}>{t('ops.patientDetails')}</AppText>
          <AppText variant="heading" color={colors.white}>{item.patientName}</AppText>
        </View>
        {item.hasMedicalAlerts ? <View style={styles.alertPill}><AppText variant="caption" color={colors.danger}>{t('ops.medicalAlert')}</AppText></View> : null}
      </View>
      <View style={styles.detailGrid}>
        <DetailRow label={t('ops.patientNumber')} value={item.patientNumber} />
        <DetailRow label={t('ops.phone')} value={item.patientPhone} />
        <DetailRow label={t('ops.doctor')} value={item.doctorName} />
        <DetailRow label={t('ops.service')} value={item.serviceName || item.proposedProcedure} />
        <DetailRow label={t('ops.room')} value={item.roomName} />
        <DetailRow label={t('ops.visitCount')} value={item.visitCount === null ? null : String(item.visitCount)} />
      </View>
      {paymentBlocked ? (
        <View style={styles.financialBlocked}>
          <AppText variant="label" color={colors.danger}>{t('ops.paymentRequired')}</AppText>
          <AppText variant="caption" color={colors.danger}>{item.financialEntryReason || t('ops.paymentRequiredDescription')}</AppText>
        </View>
      ) : item.consultationFeeRequired && item.consultationFeePaid ? (
        <View style={styles.financialClear}>
          <AppText variant="caption" color={colors.success}>{t('ops.paymentClear')}</AppText>
        </View>
      ) : null}
      {actions.length ? (
        <View style={[styles.actionRow, { flexDirection: isRtl ? 'row-reverse' : 'row' }]}>
          {actions.map((action) => (
            <Pressable
              accessibilityRole="button"
              disabled={Boolean(busyAction)}
              key={action}
              onPress={() => onAction(item, action)}
              style={[styles.actionButton, Boolean(busyAction) && styles.disabled]}
            >
              {isBusy && busyAction?.action === action ? <ActivityIndicator color={colors.white} size="small" /> : <AppText variant="label" color={colors.white} style={styles.center}>{t(actionKeys[action])}</AppText>}
            </Pressable>
          ))}
        </View>
      ) : <AppText variant="caption" color={colors.muted}>{t('ops.noAvailableActions')}</AppText>}
    </View>
  );
}

function DetailRow({ label, value }: { label: string; value: string | null }) {
  const { isRtl, t } = useLocale();
  return (
    <View style={[styles.detailRow, { flexDirection: isRtl ? 'row-reverse' : 'row' }]}>
      <AppText variant="caption" color={colors.muted}>{label}</AppText>
      <AppText variant="label" color={colors.navy900} numberOfLines={1}>{value || t('common.notAvailable')}</AppText>
    </View>
  );
}

function EmptyState({ query, tab }: { query: string; tab: Tab }) {
  const { t } = useLocale();
  return (
    <View style={styles.emptyBox}>
      <AppText variant="heading" style={styles.center}>{query ? t('ops.noSearchResults') : t('ops.empty')}</AppText>
      <AppText color={colors.muted} style={styles.center}>
        {query ? t('ops.changeSearch') : tab === 'accounting' ? t('ops.readyForAccountingEmpty') : t('ops.emptyDescription')}
      </AppText>
    </View>
  );
}

function NoticeBanner({ message }: { message: string }) {
  return (
    <View accessibilityRole="alert" style={styles.noticeBanner}>
      <AppText variant="caption" color={colors.success}>{message}</AppText>
    </View>
  );
}

function describeError(error: unknown, t: (key: TranslationKey) => string) {
  // Distinct from an expired session: the staff member is still signed in, and the action
  // simply was not sent. Telling them to sign in again here would be wrong and alarming.
  if (error instanceof SessionRenewedError) return t('auth.sessionRenewed');
  if (error instanceof ApiError) {
    if (error.status === 401) return t('auth.sessionExpired');
    if (error.status === 403) return t('ops.forbidden');
    if (error.status === 409) return t('ops.conflict');
    if (error.status === 400) return t('ops.invalidTransition');
    if (error.kind === 'network' || error.kind === 'timeout') return t('ops.networkError');
    if (error.kind === 'invalid-response') return t('ops.invalidData');
  }
  return t('ops.genericError');
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: colors.blue50 },
  content: { paddingBottom: spacing.xxxl, gap: spacing.md },
  headerContent: { gap: spacing.lg, paddingBottom: spacing.lg },
  topBar: { alignItems: 'center', gap: spacing.md, paddingHorizontal: spacing.xl, paddingVertical: spacing.xl, backgroundColor: colors.navy950 },
  backButton: { width: 44, height: 44, alignItems: 'center', justifyContent: 'center', borderRadius: 22, backgroundColor: colors.navy800 },
  center: { textAlign: 'center' },
  titleCopy: { flex: 1, gap: spacing.xs },
  summaryRow: { flexWrap: 'wrap', gap: spacing.md, paddingHorizontal: spacing.xl, marginTop: -6 },
  summaryCard: { flexGrow: 1, flexBasis: 100, gap: spacing.xs, padding: spacing.lg, borderRadius: radius.lg, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.white },
  tabs: { gap: spacing.md, paddingHorizontal: spacing.xl },
  tabRow: { padding: 4, borderRadius: radius.md, backgroundColor: colors.blue100 },
  tabButton: { flex: 1, minHeight: 44, alignItems: 'center', justifyContent: 'center', paddingHorizontal: spacing.sm, borderRadius: radius.sm },
  activeTab: { backgroundColor: colors.navy900 },
  searchRow: { gap: spacing.sm },
  searchInput: { flex: 1, minHeight: 48, paddingHorizontal: spacing.lg, borderRadius: radius.md, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.white, color: colors.ink, fontSize: 15 },
  refreshButton: { minWidth: 92, minHeight: 48, alignItems: 'center', justifyContent: 'center', paddingHorizontal: spacing.md, borderRadius: radius.md, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.white },
  disabled: { opacity: 0.5 },
  loadingBox: { marginHorizontal: spacing.xl, alignItems: 'center', gap: spacing.md, padding: spacing.xxxl, borderRadius: radius.lg, backgroundColor: colors.white },
  patientCard: { gap: spacing.md, marginHorizontal: spacing.xl, padding: spacing.lg, borderRadius: radius.lg, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.white },
  selectedCard: { borderWidth: 2, borderColor: colors.orange500 },
  patientHeading: { alignItems: 'center', gap: spacing.md },
  avatar: { width: 44, height: 44, alignItems: 'center', justifyContent: 'center', borderRadius: 22, backgroundColor: colors.navy800 },
  patientCopy: { flex: 1, gap: 2 },
  statusPill: { maxWidth: 100, paddingHorizontal: spacing.sm, paddingVertical: 5, borderRadius: radius.pill, backgroundColor: colors.orange100 },
  metaRow: { alignItems: 'center', justifyContent: 'space-between', gap: spacing.sm, paddingTop: spacing.sm, borderTopWidth: 1, borderTopColor: colors.blue100 },
  detailsCard: { gap: spacing.lg, marginHorizontal: spacing.xl, padding: spacing.xl, borderRadius: radius.lg, backgroundColor: colors.navy900 },
  detailHeading: { alignItems: 'flex-start', justifyContent: 'space-between', gap: spacing.md },
  detailHeadingCopy: { flex: 1, gap: spacing.xs },
  alertPill: { paddingHorizontal: spacing.sm, paddingVertical: 5, borderRadius: radius.pill, backgroundColor: colors.dangerSoft },
  detailGrid: { gap: spacing.sm, padding: spacing.md, borderRadius: radius.md, backgroundColor: colors.white },
  detailRow: { alignItems: 'center', justifyContent: 'space-between', gap: spacing.md },
  financialBlocked: { gap: spacing.xs, padding: spacing.md, borderRadius: radius.md, backgroundColor: colors.dangerSoft },
  financialClear: { padding: spacing.md, borderRadius: radius.md, backgroundColor: colors.successSoft },
  actionRow: { flexWrap: 'wrap', gap: spacing.sm },
  actionButton: { minHeight: 46, minWidth: 120, flexGrow: 1, alignItems: 'center', justifyContent: 'center', paddingHorizontal: spacing.md, borderRadius: radius.md, backgroundColor: colors.orange600 },
  emptyBox: { gap: spacing.sm, marginHorizontal: spacing.xl, padding: spacing.xxxl, alignItems: 'center', borderRadius: radius.lg, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.white },
  noticeBanner: { marginHorizontal: spacing.xl, padding: spacing.md, borderRadius: radius.md, backgroundColor: colors.successSoft },
});
