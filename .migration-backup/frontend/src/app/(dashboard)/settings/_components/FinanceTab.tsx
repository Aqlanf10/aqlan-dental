"use client";
// Sprint 11A — extracted from the former monolithic settings/page.tsx.
// Behavior unchanged: same UI, same API calls, same state management.

import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  Save, Banknote, Clock, CreditCard, UserCog, FileSearch,
  ShieldAlert, AlertTriangle,
} from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import { toast } from "@/stores/toastStore";
import api from "@/lib/api";
import { extractErrorMessage } from "@/lib/errors";
import { inputCls } from "./_shared";
import { FieldWithBadge } from "./FieldWithBadge";

// ─── Finance Tab (FIN-SETTINGS) ──────────────────────────────────────────────
// Configurable finance defaults stored under the finance.* Settings namespace.
// All 9 keys default to the current production behavior, so the clinic owner
// can change values WITHOUT any silent money-behavior change until they do.
// Permissions: Admin can manage; Accountant can view (read-only badge).
const FINANCE_DEFAULTS: Record<string, string> = {
  "finance.default_consultation_fee": "5000",
  "finance.max_discount_percentage": "100",
  "finance.cashier_session.default_opening_balance": "0",
  "finance.payment_methods.default_visibility": "all",
  "finance.commission.default_recognition_mode": "OnPaymentCollection",
  "finance.commission.default_doctor_percentage": "40",
  "finance.commission.default_base_rule": "AfterDiscountAndCosts",
  "finance.receipt.footer_text": "",
  "finance.receipt.show_lead_doctor": "true",
};

const financeSchema = z.object({
  "finance.default_consultation_fee": z.string().refine((v) => v.trim() === "" || !isNaN(Number(v)), {
    message: "القيمة يجب أن تكون رقمًا",
  }),
  "finance.max_discount_percentage": z.string().refine((v) => {
    const n = Number(v);
    return !isNaN(n) && n >= 0 && n <= 100;
  }, { message: "النسبة يجب أن تكون بين 0 و 100" }),
  "finance.cashier_session.default_opening_balance": z.string().refine((v) => v.trim() === "" || !isNaN(Number(v)), {
    message: "القيمة يجب أن تكون رقمًا",
  }),
  "finance.payment_methods.default_visibility": z.string().min(1, { message: "القيمة مطلوبة" }),
  "finance.commission.default_recognition_mode": z.enum(["OnPaymentCollection", "OnInvoiceIssuance"], {
    message: "وضع الاحتساب غير صالح",
  }),
  "finance.commission.default_doctor_percentage": z.string().refine((v) => {
    const n = Number(v);
    return !isNaN(n) && n >= 0 && n <= 100;
  }, { message: "النسبة يجب أن تكون بين 0 و 100" }),
  "finance.commission.default_base_rule": z.enum(
    ["AfterDiscountAndCosts", "AfterDiscount", "Gross"],
    { message: "قاعدة الاحتساب غير صالحة" }
  ),
  "finance.receipt.footer_text": z.string().max(200, { message: "الحد الأقصى 200 حرف" }),
  "finance.receipt.show_lead_doctor": z.enum(["true", "false"], {
    message: "القيمة يجب أن تكون true أو false",
  }),
});
type FinanceFormData = z.infer<typeof financeSchema>;

export function FinanceTab() {
  const user = useAuthStore((s) => s.user);
  const isAdmin = user?.role === "Admin";
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const {
    register,
    handleSubmit,
    reset,
    watch,
    formState: { errors },
  } = useForm<FinanceFormData>({
    resolver: zodResolver(financeSchema),
    defaultValues: FINANCE_DEFAULTS as FinanceFormData,
  });

  useEffect(() => {
    api.get<Record<string, string | null>>("/api/settings/finance")
      .then((r) => {
        const data = r.data ?? {};
        reset({
          "finance.default_consultation_fee": data["finance.default_consultation_fee"] ?? FINANCE_DEFAULTS["finance.default_consultation_fee"],
          "finance.max_discount_percentage": data["finance.max_discount_percentage"] ?? FINANCE_DEFAULTS["finance.max_discount_percentage"],
          "finance.cashier_session.default_opening_balance": data["finance.cashier_session.default_opening_balance"] ?? FINANCE_DEFAULTS["finance.cashier_session.default_opening_balance"],
          "finance.payment_methods.default_visibility": data["finance.payment_methods.default_visibility"] ?? FINANCE_DEFAULTS["finance.payment_methods.default_visibility"],
          "finance.commission.default_recognition_mode": (data["finance.commission.default_recognition_mode"] ?? FINANCE_DEFAULTS["finance.commission.default_recognition_mode"]) as "OnPaymentCollection" | "OnInvoiceIssuance",
          "finance.commission.default_doctor_percentage": data["finance.commission.default_doctor_percentage"] ?? FINANCE_DEFAULTS["finance.commission.default_doctor_percentage"],
          "finance.commission.default_base_rule": (data["finance.commission.default_base_rule"] ?? FINANCE_DEFAULTS["finance.commission.default_base_rule"]) as "AfterDiscountAndCosts" | "AfterDiscount" | "Gross",
          "finance.receipt.footer_text": data["finance.receipt.footer_text"] ?? "",
          "finance.receipt.show_lead_doctor": (data["finance.receipt.show_lead_doctor"] ?? FINANCE_DEFAULTS["finance.receipt.show_lead_doctor"]) as "true" | "false",
        });
      })
      .catch(() => {
        toast.error("تعذّر تحميل إعدادات المالية");
      })
      .finally(() => setLoading(false));
  }, [reset]);

  const onSubmit = handleSubmit(async (formData) => {
    setSaving(true);
    try {
      await api.put("/api/settings/finance", formData);
      toast.success("تم حفظ إعدادات المالية بنجاح");
    } catch (err) {
      toast.error(extractErrorMessage(err) ?? "فشل حفظ إعدادات المالية");
    } finally {
      setSaving(false);
    }
  });

  if (loading) {
    return (
      <div className="animate-pulse space-y-3">
        {Array.from({ length: 6 }).map((_, i) => (
          <div key={i} className="h-10 bg-gray-100 rounded-lg" />
        ))}
      </div>
    );
  }

  // Watch values for default/custom badges.
  const watched = watch();
  const isDefault = (key: keyof FinanceFormData) =>
    (watched[key] ?? "") === (FINANCE_DEFAULTS[key as string] ?? "");

  return (
    <form className="space-y-5" onSubmit={onSubmit}>
      {/* Helper banner */}
      <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 flex items-start gap-2">
        <AlertTriangle className="w-4 h-4 text-amber-600 mt-0.5 flex-shrink-0" />
        <p className="text-xs text-amber-800">
          هذه القيم تؤثر على التحصيل والخصومات والعمولات المستقبلية فقط. لا يتم تعديل أي
          قيمة موجودة مسبقًا (الأسعار والعمولات لكل خدمة تبقى كما هي).
        </p>
      </div>

      {!isAdmin && (
        <div className="rounded-lg border border-gray-200 bg-gray-50 p-3 flex items-center gap-2">
          <ShieldAlert className="w-4 h-4 text-gray-600" />
          <p className="text-xs text-gray-700">
            للقراءة فقط — يحتاج صلاحية المدير لتعديل إعدادات المالية.
          </p>
        </div>
      )}

      {/* ── Group 1: الرسوم والخصومات ─────────────────────────────────── */}
      <div className="rounded-xl border border-gray-200 p-4 space-y-4">
        <div className="flex items-center gap-2">
          <Banknote className="w-4 h-4 text-clinic-blue" />
          <h3 className="text-sm font-bold text-gray-900">الرسوم والخصومات</h3>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <FieldWithBadge
            label="رسوم المعاينة الافتراضية (ر.ي)"
            isDefault={isDefault("finance.default_consultation_fee")}
            error={errors["finance.default_consultation_fee"]?.message}
          >
            <input
              {...register("finance.default_consultation_fee")}
              className={inputCls}
              dir="ltr"
              disabled={!isAdmin}
              placeholder="5000"
            />
          </FieldWithBadge>
          <FieldWithBadge
            label="نسبة الخصم القصوى المسموحة (%)"
            isDefault={isDefault("finance.max_discount_percentage")}
            error={errors["finance.max_discount_percentage"]?.message}
            hint="100 = لا قيد. خفضها لتقييد الخصومات على الفواتير والعقود."
          >
            <input
              {...register("finance.max_discount_percentage")}
              className={inputCls}
              dir="ltr"
              disabled={!isAdmin}
              placeholder="100"
            />
          </FieldWithBadge>
        </div>
      </div>

      {/* ── Group 2: جلسة أمين الصندوق ────────────────────────────────── */}
      <div className="rounded-xl border border-gray-200 p-4 space-y-4">
        <div className="flex items-center gap-2">
          <Clock className="w-4 h-4 text-clinic-blue" />
          <h3 className="text-sm font-bold text-gray-900">جلسة أمين الصندوق</h3>
        </div>
        <FieldWithBadge
          label="رصيد الافتتاح الافتراضي للوردية (ر.ي)"
          isDefault={isDefault("finance.cashier_session.default_opening_balance")}
          error={errors["finance.cashier_session.default_opening_balance"]?.message}
          hint="0 = لا حد أدنى. يُستخدم كقيمة افتراضية في نموذج فتح وردية جديدة."
        >
          <input
            {...register("finance.cashier_session.default_opening_balance")}
            className={inputCls}
            dir="ltr"
            disabled={!isAdmin}
            placeholder="0"
          />
        </FieldWithBadge>
      </div>

      {/* ── Group 3: طرق الدفع ─────────────────────────────────────────── */}
      <div className="rounded-xl border border-gray-200 p-4 space-y-4">
        <div className="flex items-center gap-2">
          <CreditCard className="w-4 h-4 text-clinic-blue" />
          <h3 className="text-sm font-bold text-gray-900">طرق الدفع</h3>
        </div>
        <FieldWithBadge
          label="الرؤية الافتراضية لطرق الدفع"
          isDefault={isDefault("finance.payment_methods.default_visibility")}
          error={errors["finance.payment_methods.default_visibility"]?.message}
          hint="معلومتي — جميع طرق الدفع النشطة تظهر افتراضيًا. أدِر الطرق من شاشة طرق الدفع."
        >
          <input
            {...register("finance.payment_methods.default_visibility")}
            className={inputCls}
            disabled={!isAdmin}
            placeholder="all"
          />
        </FieldWithBadge>
      </div>

      {/* ── Group 4: العمولات ──────────────────────────────────────────── */}
      <div className="rounded-xl border border-gray-200 p-4 space-y-4">
        <div className="flex items-center gap-2">
          <UserCog className="w-4 h-4 text-clinic-blue" />
          <h3 className="text-sm font-bold text-gray-900">العمولات (القيم الافتراضية للخدمات الجديدة)</h3>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <FieldWithBadge
            label="وضع الاحتساب"
            isDefault={isDefault("finance.commission.default_recognition_mode")}
            error={errors["finance.commission.default_recognition_mode"]?.message}
          >
            <select {...register("finance.commission.default_recognition_mode")} className={inputCls} disabled={!isAdmin}>
              <option value="OnPaymentCollection">عند التحصيل</option>
              <option value="OnInvoiceIssuance">عند إصدار الفاتورة</option>
            </select>
          </FieldWithBadge>
          <FieldWithBadge
            label="نسبة عمولة الطبيب الافتراضية (%)"
            isDefault={isDefault("finance.commission.default_doctor_percentage")}
            error={errors["finance.commission.default_doctor_percentage"]?.message}
          >
            <input
              {...register("finance.commission.default_doctor_percentage")}
              className={inputCls}
              dir="ltr"
              disabled={!isAdmin}
              placeholder="40"
            />
          </FieldWithBadge>
          <FieldWithBadge
            label="قاعدة احتساب العمولة"
            isDefault={isDefault("finance.commission.default_base_rule")}
            error={errors["finance.commission.default_base_rule"]?.message}
          >
            <select {...register("finance.commission.default_base_rule")} className={inputCls} disabled={!isAdmin}>
              <option value="AfterDiscountAndCosts">بعد الخصم والتكاليف</option>
              <option value="AfterDiscount">بعد الخصم فقط</option>
              <option value="Gross">الإجمالي</option>
            </select>
          </FieldWithBadge>
        </div>
        <p className="text-xs text-gray-500">
          هذه قيم افتراضية للخدمات الجديدة فقط. القيم الموجودة لكل خدمة تبقى كما هي.
        </p>
      </div>

      {/* ── Group 5: الإيصالات ─────────────────────────────────────────── */}
      <div className="rounded-xl border border-gray-200 p-4 space-y-4">
        <div className="flex items-center gap-2">
          <FileSearch className="w-4 h-4 text-clinic-blue" />
          <h3 className="text-sm font-bold text-gray-900">الإيصالات</h3>
        </div>
        <FieldWithBadge
          label="نص مخصص في تذييل الإيصال"
          isDefault={isDefault("finance.receipt.footer_text")}
          error={errors["finance.receipt.footer_text"]?.message}
          hint="اتركه فارغًا لاستخدام النص الافتراضي (شكراً لثقتكم بنا — نتمنى لكم دوام الصحة والعافية)."
        >
          <textarea
            {...register("finance.receipt.footer_text")}
            className={inputCls}
            rows={2}
            disabled={!isAdmin}
            placeholder="مثال: شكراً لزيارتكم"
          />
        </FieldWithBadge>
        <div className="flex items-center justify-between rounded-lg border border-gray-100 bg-gray-50 p-3">
          <div>
            <p className="text-sm font-medium text-gray-800">إظهار بيانات الطبيب في الإيصال</p>
            <p className="text-xs text-gray-500">يتحكم في عرض كتلة اسم الطبيب والتخصص في ترويسة الإيصال.</p>
          </div>
          <label className="inline-flex items-center cursor-pointer">
            <input
              type="checkbox"
              {...register("finance.receipt.show_lead_doctor")}
              value="true"
              className="sr-only peer"
              disabled={!isAdmin}
            />
            <div className="relative w-11 h-6 bg-gray-200 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-[-100%] rtl:peer-checked:after:translate-x-[-100%] peer-checked:after:translate-x-0 peer-checked:bg-clinic-blue after:content-[''] after:absolute after:top-[2px] after:right-[2px] after:bg-white after:rounded-full after:h-5 after:w-5 after:transition-transform" />
          </label>
        </div>
        <p className="text-xs text-gray-500">
          القيمة الحالية: {watched["finance.receipt.show_lead_doctor"] === "true" ? "مُفعّل" : "مُعطّل"}
        </p>
      </div>

      <div className="flex items-center gap-3 pt-2">
        <button
          type="submit"
          disabled={saving || !isAdmin}
          className="flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 disabled:cursor-not-allowed transition"
        >
          <Save className="w-4 h-4" />
          {saving ? "جارٍ الحفظ..." : "حفظ إعدادات المالية"}
        </button>
        {!isAdmin && (
          <span className="text-xs text-gray-500">للقراءة فقط — يلزم صلاحية المدير</span>
        )}
      </div>
    </form>
  );
}
