"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Save, Search } from "lucide-react";
import type { PatientListItem } from "@/types/patient";
import type { CreateContractRequest } from "@/types/finance";
import api from "@/lib/api";
import { cn } from "@/lib/utils";

const schema = z.object({
  patientId:       z.string().min(1, "اختر مريضاً"),
  specialty:       z.string().optional(),
  totalAmount:     z.number().min(1, "المبلغ مطلوب"),
  downPayment:     z.number().min(0).default(0),
  installmentsCount: z.number().min(1).default(1),
  installmentAmount: z.number().optional(),
  startDate:       z.string().optional(),
  discountAmount:  z.number().min(0).default(0),
  discountReason:  z.string().optional(),
  notes:           z.string().optional(),
});
type FormData = z.infer<typeof schema>;

const inputCls = (err?: string) => cn(
  "w-full px-3 py-2 text-sm rounded-lg border bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal",
  err ? "border-red-400" : "border-gray-300"
);

export function ContractForm() {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [serverError, setServerError] = useState("");
  const [patientSearch, setPatientSearch] = useState("");
  const [patientResults, setPatientResults] = useState<PatientListItem[]>([]);
  const [showPatientDropdown, setShowPatientDropdown] = useState(false);

  const { register, handleSubmit, setValue, watch, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { downPayment: 0, installmentsCount: 1, discountAmount: 0, startDate: new Date().toISOString().slice(0, 10) }
  });

  const totalAmount = watch("totalAmount") || 0;
  const installmentsCount = watch("installmentsCount") || 1;
  const discountAmount = watch("discountAmount") || 0;
  const downPayment = watch("downPayment") || 0;

  // Calculate installment amount automatically
  const netAmount = totalAmount - discountAmount - downPayment;
  const calculatedInstallment = installmentsCount > 0 && netAmount > 0
    ? Math.ceil(netAmount / installmentsCount)
    : 0;

  useEffect(() => {
    if (patientSearch.length < 2) { setPatientResults([]); return; }
    const t = setTimeout(() => {
      api.get<{ items: PatientListItem[] }>(`/api/patients?search=${encodeURIComponent(patientSearch)}&pageSize=8`)
        .then((r) => setPatientResults(r.data.items))
        .catch(() => {});
    }, 300);
    return () => clearTimeout(t);
  }, [patientSearch]);

  const selectPatient = (p: PatientListItem) => {
    setValue("patientId", p.id);
    setPatientSearch(`${p.fullName} (${p.patientNumber})`);
    setShowPatientDropdown(false);
  };

  const onSubmit = async (data: FormData) => {
    setSaving(true);
    setServerError("");
    try {
      const req: CreateContractRequest = {
        patientId: data.patientId,
        specialty: data.specialty,
        totalAmount: data.totalAmount,
        downPayment: data.downPayment,
        installmentsCount: data.installmentsCount,
        installmentAmount: calculatedInstallment || data.installmentAmount,
        startDate: data.startDate,
        discountAmount: data.discountAmount,
        discountReason: data.discountReason,
        notes: data.notes,
      };
      await api.post("/api/contracts", req);
      router.push("/finance/contracts");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setServerError(msg ?? "حدث خطأ أثناء الحفظ");
    } finally {
      setSaving(false);
    }
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      {serverError && (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">{serverError}</div>
      )}

      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Patient */}
        <div className="md:col-span-2">
          <label className="block text-sm font-medium text-gray-700 mb-1.5">المريض <span className="text-red-500">*</span></label>
          <div className="relative">
            <Search className="absolute right-3 top-2.5 w-4 h-4 text-gray-400" />
            <input
              value={patientSearch}
              onChange={(e) => { setPatientSearch(e.target.value); setShowPatientDropdown(true); }}
              onFocus={() => patientSearch.length >= 2 && setShowPatientDropdown(true)}
              onBlur={() => setTimeout(() => setShowPatientDropdown(false), 150)}
              placeholder="ابحث بالاسم أو رقم المريض..."
              className={cn(inputCls(errors.patientId?.message), "pe-9")}
              autoComplete="off"
            />
            {showPatientDropdown && patientResults.length > 0 && (
              <div className="absolute z-10 w-full mt-1 bg-white rounded-lg border border-gray-200 shadow-lg max-h-48 overflow-y-auto">
                {patientResults.map((p) => (
                  <button key={p.id} type="button" onMouseDown={() => selectPatient(p)}
                    className="w-full text-start px-3 py-2.5 text-sm hover:bg-gray-50 flex items-center justify-between"
                  >
                    <span className="font-medium">{p.fullName}</span>
                    <span className="text-xs text-gray-400 font-mono">{p.patientNumber}</span>
                  </button>
                ))}
              </div>
            )}
          </div>
          <input type="hidden" {...register("patientId")} />
          {errors.patientId && <p className="mt-1 text-xs text-red-600">{errors.patientId.message}</p>}
        </div>

        {/* Specialty */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">التخصص</label>
          <select {...register("specialty")} className={inputCls()}>
            <option value="">اختر...</option>
            <option value="orthodontics">تقويم</option>
            <option value="general">أسنان عام</option>
            <option value="surgery">جراحة</option>
            <option value="other">أخرى</option>
          </select>
        </div>

        {/* Start date */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">تاريخ العقد</label>
          <input {...register("startDate")} type="date" className={inputCls()} />
        </div>

        {/* Total amount */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">المبلغ الإجمالي (ريال) <span className="text-red-500">*</span></label>
          <input {...register("totalAmount", { valueAsNumber: true })} type="number" min={0} className={inputCls(errors.totalAmount?.message)} dir="ltr" />
          {errors.totalAmount && <p className="mt-1 text-xs text-red-600">{errors.totalAmount.message}</p>}
        </div>

        {/* Discount */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">الخصم (ريال)</label>
          <input {...register("discountAmount", { valueAsNumber: true })} type="number" min={0} className={inputCls()} dir="ltr" />
        </div>

        {/* Down payment */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">الدفعة الأولى (ريال)</label>
          <input {...register("downPayment", { valueAsNumber: true })} type="number" min={0} className={inputCls()} dir="ltr" />
        </div>

        {/* Installments */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">عدد الأقساط</label>
          <input {...register("installmentsCount", { valueAsNumber: true })} type="number" min={1} max={60} className={inputCls()} dir="ltr" />
        </div>

        {/* Summary */}
        {totalAmount > 0 && (
          <div className="md:col-span-2 bg-teal-50 rounded-lg p-3 text-sm">
            <div className="grid grid-cols-3 gap-3 text-center">
              <div>
                <p className="text-xs text-gray-500">صافي المبلغ</p>
                <p className="font-bold text-gray-900">{(totalAmount - discountAmount).toLocaleString()} ر.ي</p>
              </div>
              <div>
                <p className="text-xs text-gray-500">بعد الدفعة الأولى</p>
                <p className="font-bold text-teal-700">{netAmount.toLocaleString()} ر.ي</p>
              </div>
              <div>
                <p className="text-xs text-gray-500">القسط الشهري</p>
                <p className="font-bold text-blue-700">{calculatedInstallment.toLocaleString()} ر.ي</p>
              </div>
            </div>
          </div>
        )}

        {/* Notes */}
        <div className="md:col-span-2">
          <label className="block text-sm font-medium text-gray-700 mb-1.5">ملاحظات</label>
          <textarea {...register("notes")} rows={2} className={inputCls()} />
        </div>
      </div>

      <div className="flex justify-end gap-3 pb-4">
        <button type="button" onClick={() => router.back()}
          className="px-5 py-2 text-sm rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
        >إلغاء</button>
        <button type="submit" disabled={saving}
          className="flex items-center gap-2 px-6 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
        >
          <Save className="w-4 h-4" />
          {saving ? "جارٍ الحفظ..." : "حفظ العقد"}
        </button>
      </div>
    </form>
  );
}
