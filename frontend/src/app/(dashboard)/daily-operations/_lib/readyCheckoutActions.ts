import api from "@/lib/api";

export interface ReadyCheckoutItemRef {
  appointmentId: string;
  visitId?: string | null;
  checkoutStatus?: string | null;
  nextAction?: string | null;
}

export interface ReadyCheckoutResult {
  ok: boolean;
  action: "draftInvoice" | "checkout";
  skipped?: boolean;
  reason?: string;
  data?: unknown;
}

export function isReadyForReceptionCheckout(item: ReadyCheckoutItemRef): boolean {
  return item.checkoutStatus === "ReadyForCheckout" || item.nextAction === "Checkout";
}

export async function createDraftInvoiceForReadyCheckout(item: ReadyCheckoutItemRef): Promise<ReadyCheckoutResult> {
  if (!item.visitId) {
    return {
      ok: false,
      action: "draftInvoice",
      skipped: true,
      reason: "missing_visit_id",
    };
  }

  const { data } = await api.post(`/api/patient-journey/${item.visitId}/create-draft-invoice`);
  return {
    ok: true,
    action: "draftInvoice",
    data,
  };
}

export async function completeReceptionCheckout(
  item: ReadyCheckoutItemRef,
  body: {
    paymentAmount?: number;
    paymentMethod?: string;
    notes?: string;
    nextAppointmentDate?: string;
    nextServiceId?: string;
  },
): Promise<ReadyCheckoutResult> {
  if (!isReadyForReceptionCheckout(item)) {
    return {
      ok: false,
      action: "checkout",
      skipped: true,
      reason: "not_ready_for_checkout",
    };
  }

  const { data } = await api.post(`/api/patient-journey/${item.appointmentId}/checkout`, body);
  return {
    ok: true,
    action: "checkout",
    data,
  };
}
