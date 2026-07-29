"use client";

import { useState, type FormEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { X, RefreshCw } from "lucide-react";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";
import { extractErrorMessage } from "@/lib/errors";
import type { LabOrder, RemakeLabOrderRequest } from "@/types/lab";

interface RemakeLabOrderModalProps {
  order: LabOrder;
  onClose: () => void;
}

/**
 * CORE-LAB-007: the "returned → remake" button used to call the generic
 * PUT /status endpoint, which never reaches the dedicated POST /remake action —
 * so a rework's reason, free/paid choice and extra cost were never captured at all,
 * and the extra cost never entered the financial trail (supplier debt, expense,
 * doctor-commission deduction). This modal is the only UI path that calls the
 * real /remake endpoint.
 */
export function RemakeLabOrderModal({ order, onClose }: RemakeLabOrderModalProps) {
  const [reason, setReason] = useState("");
  const [isFreeRemake, setIsFreeRemake] = useState(true);
  const [remakeCost, setRemakeCost] = useState("");
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: async () => {
      const payload: RemakeLabOrderRequest = {
        reason,
        isFreeRemake,
        remakeCost: isFreeRemake ? undefined : Number(remakeCost),
      };
      await api.post(`/api/lab-orders/${order.id}/remake`, payload);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["lab-orders"] });
      queryClient.invalidateQueries({ queryKey: ["lab-orders-pending-count"] });
      toast.success("تم إرسال الطلب لإعادة الصناعة");
      onClose();
    },
    onError: (err) => toast.error(extractErrorMessage(err, "فشل إرسال إعادة الصناعة")),
  });

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (!reason.trim()) return;
    if (!isFreeRemake && (!remakeCost || Number(remakeCost) <= 0)) return;
    mutation.mutate();
  };

  return (
    <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-xl shadow-xl w-full max-w-md">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <h2 className="font-bold text-gray-900 flex items-center gap-2">
            <RefreshCw className="w-4 h-4 text-purple-600" />
            إعادة صناعة الطلب {order.orderNumber}
          </h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-5 space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">سبب إعادة الصناعة *</label>
            <textarea
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              required
              rows={2}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
              placeholder="مثال: عدم مطابقة اللون للمواصفات"
            />
          </div>

          <div className="flex items-center gap-4">
            <label className="flex items-center gap-2 text-sm text-gray-700">
              <input
                type="radio"
                checked={isFreeRemake}
                onChange={() => setIsFreeRemake(true)}
              />
              إعادة مجانية (خطأ المعمل)
            </label>
            <label className="flex items-center gap-2 text-sm text-gray-700">
              <input
                type="radio"
                checked={!isFreeRemake}
                onChange={() => setIsFreeRemake(false)}
              />
              إعادة بتكلفة إضافية
            </label>
          </div>

          {!isFreeRemake && (
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">التكلفة الإضافية *</label>
              <input
                type="number"
                min={0}
                step="0.01"
                value={remakeCost}
                onChange={(e) => setRemakeCost(e.target.value)}
                required
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
                placeholder="0.00"
              />
              <p className="text-xs text-gray-400 mt-1">
                تُضاف هذه التكلفة إلى تكلفة الطلب الإجمالية وتُسجَّل كمستحق إضافي للمعمل عند إعادة الإرسال.
              </p>
            </div>
          )}

          <div className="flex justify-end gap-2 pt-2">
            <button type="button" onClick={onClose} className="px-4 py-2 text-sm text-gray-600 hover:text-gray-800">
              إلغاء
            </button>
            <button
              type="submit"
              disabled={mutation.isPending || !reason.trim() || (!isFreeRemake && (!remakeCost || Number(remakeCost) <= 0))}
              className="px-4 py-2 text-sm bg-purple-600 text-white rounded-lg hover:bg-purple-700 disabled:opacity-50"
            >
              تأكيد إعادة الصناعة
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
