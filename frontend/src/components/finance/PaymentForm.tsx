"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Save, Printer } from "lucide-react";
import type { PatientListItem } from "@/types/patient";
import type { Contract, CreatePaymentRequest, Payment } from "@/types/finance";
import api from "@/lib/api";
import { cn, formatYemeniRiyal } from "@/lib/utils";
import { ReceiptView } from "./ReceiptView";
import { PatientCombobox } from "@/components/shared/PatientCombobox";

const schema = z.object({
  patientId:          z.string().min(1, "اختر مريضاً"),
  contractId:         z.string().optional(),
  amount:             z.number().min(1, "المبلغ مطلوب"),
  paymentMethod:      z.enum(["cash", "bank_transfer", "card"]),
  serviceDescription: z.string().optional(),
  specialty:          z.string().optional(),
  notes:              z.string().optional(),
});
type FormData = z.infer<typeof schema>;

const inputCls = (err?: string) => cn(
  "w-full px-3 py-2 text-sm rounded-lg border-[1.5px] bg-[#f7fafd] focus:outline-none focus:ring-2 focus:ring-accent-blue",
  err ? "border-[#ef4444]" : "border-[#dce8f5]"
);

interface PaymentFormProps {
  defaultContractId?: string;
  defaultPatientId?:  string;
  defaultPatientName?: string;
}

export function PaymentForm({ defaultContractId, defaultPatientId, defaultPatientName }: PaymentFormProps) {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [serverError, setServerError] = useState("");
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [savedPayment, setSavedPayment] = useState<Payment | null>(null);

  const { register, handleSubmit, setValue, watch, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      paymentMethod: "cash",
      patientId:  defaultPatientId  ?? "",
      contractId: defaultContractId ?? "",
    },
  });

  const selectedPatientId = watch("patientId");

  // Pre-fill patient if passed via props
  useEffect(() => {
    if (defaultPatientId) setValue("patientId", defaultPatientId);
  }, [defaultPatientId, setValue]);

  useEffect(() => {
    if (!selectedPatientId) { setContracts([]); return; }
    api.get<{ items: Contract[]; totalCount: number }>(`/api/contracts?patientId=${selectedPatientId}&status=active`)
      .then((r) => {
        setContracts(r.data.items);
        if (defaultContractId) setValue("contractId", defaultContractId);
      })
      .catch(() => {});
  }, [selectedPatientId, defaultContractId, setValue]);

  const onSubmit = async (data: FormData) => {
    setSaving(true);
    setServerError("");
    try {
      const req: CreatePaymentRequest = {
        patientId: data.patientId,
        contractId: data.contractId || undefined,
        amount: data.amount,
        paymentMethod: data.paymentMethod,
        serviceDescription: data.serviceDescription,
        specialty: data.specialty,
        notes: data.notes,
      };
      const { data: payment } = await api.post<Payment>("/api/payments", req);
      setSavedPayment(payment);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setServerError(msg ?? "حدث خطأ أثناء الحفظ");
    } finally {
      setSaving(false);
    }
  };

  if (savedPayment) {
    return (
      <div className="space-y-4">
        <div className="bg-[#22c55e18] border border-[#22c55e30] text-[#22c55e] rounded-lg p-4 text-sm font-medium">
          ✓ تم تسجيل الدفعة بنجاح — رقم السند: {savedPayment.receiptNumber}
        </div>
        <ReceiptView payment={savedPayment} />
        <div className="flex gap-3">
          <button
            onClick={() => window.print()}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg border border-[#e8f0f9] hover:bg-[#f7fafd] transition"
          >
            <Printer className="w-4 h-4" />
            طباعة السند
          </button>
          <button
            onClick={() => { setSavedPayment(null); router.push("/finance"); }}
            className="px-4 py-2 text-sm font-medium rounded-lg bg-accent-blue text-white hover:bg-blue-hover transition"
          >
            العودة إلى المالية
          </button>
        </div>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      {serverError && (
        <div className="bg-[#ef444418] border border-[#ef444430] text-[#ef4444] rounded-lg p-3 text-sm">{serverError}</div>
      )}

      <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-5 grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Patient */}
        <div className="md:col-span-2">
          <label className="block text-sm font-medium text-[#0d2137] mb-1.5">المريض <span className="text-[#ef4444]">*</span></label>
          <PatientCombobox
            defaultDisplayValue={defaultPatientName ?? ""}
            onSelect={(p: PatientListItem) => setValue("patientId", p.id)}
            error={errors.patientId?.message}
          />
          <input type="hidden" {...register("patientId")} />
          {errors.patientId && <p className="mt-1 text-xs text-[#ef4444]">{errors.patientId.message}</p>}
        </div>

        {/* Contract */}
        {contracts.length > 0 && (
          <div className="md:col-span-2">
            <label className="block text-sm font-medium text-[#0d2137] mb-1.5">ربط بعقد</label>
            <select {...register("contractId")} className={inputCls()}>
              <option value="">بدون ربط بعقد</option>
              {contracts.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.specialty ?? "عام"} — متبقٍ: {formatYemeniRiyal(c.remainingAmount)}
                </option>
              ))}
            </select>
          </div>
        )}

        {/* Amount */}
        <div>
          <label className="block text-sm font-medium text-[#0d2137] mb-1.5">المبلغ (ريال) <span className="text-[#ef4444]">*</span></label>
          <input {...register("amount", { valueAsNumber: true })} type="number" min={1} className={inputCls(errors.amount?.message)} dir="ltr" />
          {errors.amount && <p className="mt-1 text-xs text-[#ef4444]">{errors.amount.message}</p>}
        </div>

        {/* Payment method */}
        <div>
          <label className="block text-sm font-medium text-[#0d2137] mb-1.5">طريقة الدفع</label>
          <select {...register("paymentMethod")} className={inputCls()}>
            <option value="cash">نقداً</option>
            <option value="bank_transfer">تحويل بنكي</option>
            <option value="card">بطاقة</option>
          </select>
        </div>

        {/* Service description */}
        <div className="md:col-span-2">
          <label className="block text-sm font-medium text-[#0d2137] mb-1.5">وصف الخدمة</label>
          <input {...register("serviceDescription")} className={inputCls()} placeholder="تقويم، حشو، فحص..." />
        </div>

        {/* Notes */}
        <div className="md:col-span-2">
          <label className="block text-sm font-medium text-[#0d2137] mb-1.5">ملاحظات</label>
          <textarea {...register("notes")} rows={2} className={inputCls()} />
        </div>
      </div>

      <div className="flex justify-end gap-3 pb-4">
        <button type="button" onClick={() => router.back()}
          className="px-5 py-2 text-sm rounded-lg border border-[#e8f0f9] text-[#64748b] hover:bg-[#f7fafd] transition"
        >إلغاء</button>
        <button type="submit" disabled={saving}
          className="flex items-center gap-2 px-6 py-2 text-sm font-medium rounded-lg bg-accent-blue text-white hover:bg-blue-hover disabled:opacity-60 transition"
        >
          <Save className="w-4 h-4" />
          {saving ? "جارٍ التسجيل..." : "تسجيل الدفعة"}
        </button>
      </div>
    </form>
  );
}
