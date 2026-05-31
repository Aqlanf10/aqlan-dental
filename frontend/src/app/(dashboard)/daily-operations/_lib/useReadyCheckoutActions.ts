"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  completeReceptionCheckout,
  createDraftInvoiceForReadyCheckout,
  isReadyForReceptionCheckout,
  type ReadyCheckoutItemRef,
} from "./readyCheckoutActions";

export function useReadyCheckoutActions() {
  const queryClient = useQueryClient();

  const invalidateReadyCheckout = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] }),
      queryClient.invalidateQueries({ queryKey: ["patient-journey"] }),
      queryClient.invalidateQueries({ queryKey: ["finance"] }),
      queryClient.invalidateQueries({ queryKey: ["payments"] }),
      queryClient.invalidateQueries({ queryKey: ["invoices"] }),
    ]);
  };

  const createDraftInvoiceMutation = useMutation({
    mutationFn: async (item: ReadyCheckoutItemRef) => createDraftInvoiceForReadyCheckout(item),
    onSuccess: invalidateReadyCheckout,
  });

  const completeCheckoutMutation = useMutation({
    mutationFn: async (params: {
      item: ReadyCheckoutItemRef;
      body: {
        paymentAmount?: number;
        paymentMethod?: string;
        notes?: string;
        nextAppointmentDate?: string;
        nextServiceId?: string;
      };
    }) => completeReceptionCheckout(params.item, params.body),
    onSuccess: invalidateReadyCheckout,
  });

  return {
    isReadyForReceptionCheckout,
    createDraftInvoice: createDraftInvoiceMutation.mutateAsync,
    completeReceptionCheckout: completeCheckoutMutation.mutateAsync,
    createDraftInvoicePending: createDraftInvoiceMutation.isPending,
    completeCheckoutPending: completeCheckoutMutation.isPending,
  };
}
