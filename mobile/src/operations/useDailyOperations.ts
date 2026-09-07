import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { ApiError } from '@/api/client';
import { createDailyOperationsApi } from '@/api/dailyOperationsApi';
import { useAuth } from '@/auth/AuthProvider';
import type { ClinicRoom, DailyOperationAction, DailyOperationInput, DailyPatient } from '@/types/dailyOperations';

type BusyAction = { itemId: string; action: DailyOperationAction } | null;

export function useDailyOperations(enabled = true) {
  const { request } = useAuth();
  const api = useMemo(() => createDailyOperationsApi(request), [request]);
  const sequence = useRef(0);
  const mounted = useRef(true);
  const actionInFlight = useRef(false);
  const [items, setItems] = useState<DailyPatient[]>([]);
  const [rooms, setRooms] = useState<ClinicRoom[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [notice, setNotice] = useState<DailyOperationAction | null>(null);
  const [busyAction, setBusyAction] = useState<BusyAction>(null);

  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; };
  }, []);

  // `keepError` exists for the conflict path: re-reading the list must not silently wipe the
  // message explaining why the row just changed under the user's finger.
  const load = useCallback(async (initial = false, keepError = false) => {
    if (!enabled) {
      setLoading(false);
      setRefreshing(false);
      return;
    }
    const requestSequence = ++sequence.current;
    if (initial) setLoading(true);
    else setRefreshing(true);
    if (!keepError) setError(null);
    try {
      const [nextItems, nextRooms] = await Promise.all([
        api.today(),
        api.rooms().catch(() => [] as ClinicRoom[]),
      ]);
      if (!mounted.current || requestSequence !== sequence.current) return;
      setItems(nextItems);
      setRooms(nextRooms);
    } catch (nextError) {
      if (mounted.current && requestSequence === sequence.current) setError(nextError);
    } finally {
      if (mounted.current && requestSequence === sequence.current) {
        setLoading(false);
        setRefreshing(false);
      }
    }
  }, [api, enabled]);

  useEffect(() => { void load(true); }, [load]);

  const runAction = useCallback(async (
    item: DailyPatient,
    action: DailyOperationAction,
    input: DailyOperationInput = {},
  ) => {
    if (actionInFlight.current) return false;
    actionInFlight.current = true;
    const itemId = item.queueItemId ?? item.appointmentId ?? item.patientId;
    setBusyAction({ itemId, action });
    setError(null);
    setNotice(null);
    try {
      if (action === 'intake' && item.appointmentId) {
        await api.intake(item.appointmentId, {
          roomId: input.roomId,
          notes: input.notes,
          serviceId: item.serviceId ?? undefined,
        });
      }
      else if (action === 'send-to-queue' && item.appointmentId) {
        await api.sendToQueue(item.appointmentId, { roomId: input.roomId, notes: input.notes });
      }
      else if (action === 'call' && item.queueItemId) await api.call(item.queueItemId, input.roomName);
      else if (action === 'recall' && item.queueItemId) await api.recall(item.queueItemId, input.roomName);
      else if (action === 'enter-room' && item.queueItemId) await api.enterRoom(item.queueItemId);
      else if (action === 'start-visit' && item.appointmentId) await api.startVisit(item.appointmentId);
      else if (action === 'handoff' && item.visitId) {
        await api.handoff(item.visitId, {
          treatmentDone: input.treatmentDone,
          diagnosis: input.diagnosis,
          proposedProcedure: input.proposedProcedure,
          amountDue: input.amountDue,
          notes: input.notes,
        });
      }
      else if (action === 'create-draft-invoice' && item.visitId) await api.createDraftInvoice(item.visitId);
      else throw new Error('Missing operation identifier');
      if (!mounted.current) return false;
      setNotice(action);
      await load(false);
      return true;
    } catch (nextError) {
      if (mounted.current) setError(nextError);
      // A 409 means another device already moved this patient on. Showing the message while
      // leaving the stale row on screen invites the user to tap again at something that no
      // longer exists, so the list is re-read to show what actually happened. Only on 409:
      // a network failure should not trigger a second request while the first may still land.
      if (nextError instanceof ApiError && nextError.status === 409) await load(false, true);
      return false;
    } finally {
      actionInFlight.current = false;
      if (mounted.current) setBusyAction(null);
    }
  }, [api, load]);

  const refresh = useCallback(() => load(false), [load]);

  return {
    items,
    rooms,
    loading,
    refreshing,
    error,
    notice,
    busyAction,
    refresh,
    runAction,
  };
}
