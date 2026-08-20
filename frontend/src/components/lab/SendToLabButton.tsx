"use client";

import { useState } from "react";
import { MessageCircle } from "lucide-react";
import api from "@/lib/api";
import { extractErrorMessage } from "@/lib/errors";
import { toast } from "@/stores/toastStore";
import { formatPhoneForWhatsApp } from "@/lib/utils";

/**
 * LABINV-REQ-009 — send a lab order's details to the lab over WhatsApp.
 *
 * The step this removes: after raising an order, someone phones the lab or retypes the
 * case details into WhatsApp by hand. Retyping is where the tooth number and the shade
 * get transposed, and it happens on every single order.
 *
 * The message is composed by the server so the clinic's name, phones and lead doctor come
 * from `Settings` rather than from literals in this file. This component only opens
 * WhatsApp with what the server returned — the clinic's WhatsApp account never touches
 * the backend.
 */

interface Props {
  orderId: string;
  className?: string;
}

interface DispatchPayload {
  phone: string;
  labName: string | null;
  message: string;
}

export function SendToLabButton({ orderId, className }: Props) {
  const [isPreparing, setIsPreparing] = useState(false);

  const send = async () => {
    if (isPreparing) return;
    setIsPreparing(true);
    try {
      const { data } = await api.get<DispatchPayload>(
        `/api/lab-orders/${orderId}/whatsapp-message`,
      );

      const phone = formatPhoneForWhatsApp(data.phone);
      if (!phone) {
        // Defence in depth: the server already refuses an order with no lab number, but a
        // number that survives the server and normalises to nothing here would otherwise
        // open WhatsApp to no one and look like the message went out.
        toast.error("رقم المعمل غير صالح — راجعه من الإعدادات ← المعامل");
        return;
      }

      window.open(
        `https://wa.me/${phone}?text=${encodeURIComponent(data.message)}`,
        "_blank",
        "noopener,noreferrer",
      );
    } catch (err) {
      toast.error(extractErrorMessage(err));
    } finally {
      setIsPreparing(false);
    }
  };

  return (
    <button
      type="button"
      onClick={() => void send()}
      disabled={isPreparing}
      title="إرسال تفاصيل الطلب للمعمل عبر واتساب"
      className={className ?? "text-xs text-emerald-700 hover:text-emerald-900 font-medium disabled:opacity-50"}
    >
      <MessageCircle className="w-3.5 h-3.5 inline" aria-hidden /> واتساب
    </button>
  );
}
