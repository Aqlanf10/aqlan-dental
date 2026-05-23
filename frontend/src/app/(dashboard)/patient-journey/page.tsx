"use client";
import { useEffect, useState, useCallback } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  Users, UserCheck, Clock, DoorOpen, CreditCard, CheckCircle2,
  Plus, X, Save, Search, Filter, RefreshCw, Stethoscope,
  ArrowRight, Phone, CalendarDays, ChevronDown, FileText
} from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import { useAuthStore } from "@/stores/authStore";
import { toast } from "@/stores/toastStore";
import { WorkflowNav, WORKFLOW_LINKS } from "@/components/shared/WorkflowNav";

// ─── Types ────────────────────────────────────────────────────────────────────

interface JourneyItem {
  appointmentId: string;
  patientId: string;
  patientName: string;
  patientPhone?: string;
  appointmentTime: string;
  appointmentStatus: string;
  doctorId: string;
  doctorName: string;
  serviceId?: string;
  serviceName?: string;
  roomName?: string;
  queueItemId?: string;
  queueStatus?: string;
  visitId?: string;
  visitStatus?: string;
  consultationFeeRequired: boolean;
  consultationFeePaid: boolean;
  checkoutStatus?: string;
  nextAction: string;
}

interface ServiceOption {
  id: string;
  arabicName: string;
  code: string;
  defaultPrice: number;
  requiresConsultationFee: boolean;
}

interface RoomOption {
  id: string;
  arabicName: string;
  code: string;
  roomType: string;
}

// ─── Constants ────────────────────────────────────────────────────────────────

const STATUS_LABELS: Record<string, string> = {
  Scheduled: "مجدول",
  Confirmed: "مؤكد",
  Arrived: "وصل",
  Waiting: "في الانتظار",
  Called: "تم النداء",
  InRoom: "داخل الغرفة",
  InProgress: "قيد المعالجة",
  Completed: "مكتمل",
  Cancelled: "ملغي",
  NoShow: "لم يحضر",
};

const STATUS_COLORS: Record<string, string> = {
  Scheduled: "bg-blue-50 text-blue-700",
  Confirmed: "bg-indigo-50 text-indigo-700",
  Arrived: "bg-amber-50 text-amber-700",
  Waiting: "bg-orange-50 text-orange-700",
  Called: "bg-purple-50 text-purple-700",
  InRoom: "bg-cyan-50 text-cyan-700",
  InProgress: "bg-emerald-50 text-emerald-700",
  Completed: "bg-green-50 text-green-700",
  Cancelled: "bg-gray-100 text-gray-500",
  NoShow: "bg-red-50 text-red-700",
};

const ACTION_LABELS: Record<string, string> = {
  Intake: "تسجيل الوصول",
  SendToQueue: "إدخال للطابور",
  CallPatient: "نداء المريض",
  EnterRoom: "إدخال الغرفة",
  StartVisit: "بدء الزيارة",
  InProgress: "عند الطبيب",
  Checkout: "إنهاء الحساب",
  None: "—",
};

const ACTION_COLORS: Record<string, string> = {
  Intake: "bg-amber-500 hover:bg-amber-600",
  SendToQueue: "bg-blue-500 hover:bg-blue-600",
  CallPatient: "bg-purple-500 hover:bg-purple-600",
  EnterRoom: "bg-cyan-500 hover:bg-cyan-600",
  StartVisit: "bg-emerald-500 hover:bg-emerald-600",
  InProgress: "bg-gray-400",
  Checkout: "bg-green-600 hover:bg-green-700",
  None: "bg-gray-300",
};

const PAYMENT_METHODS = [
  { value: "cash", label: "نقدي" },
  { value: "transfer", label: "تحويل" },
  { value: "card", label: "بطاقة" },
];

const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue";

// ─── Main Page ────────────────────────────────────────────────────────────────

export default function PatientJourneyPage() {
  const router = useRouter();
  const { user } = useAuthStore();
  const userRole = user?.role ?? "";

  const [items, setItems] = useState<JourneyItem[]>([]);
  const [services, setServices] = useState<ServiceOption[]>([]);
  const [rooms, setRooms] = useState<RoomOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // Filters
  const [filterDate, setFilterDate] = useState(
    new Date().toISOString().split("T")[0]
  );
  const [filterDoctor, setFilterDoctor] = useState("");
  const [filterService, setFilterService] = useState("");
  const [filterRoom, setFilterRoom] = useState("");
  const [filterStatus, setFilterStatus] = useState("");

  // Dialogs
  const [showIntake, setShowIntake] = useState(false);
  const [showCheckout, setShowCheckout] = useState(false);
  const [selectedItem, setSelectedItem] = useState<JourneyItem | null>(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [dialogError, setDialogError] = useState("");

  // Intake form
  const [intakeService, setIntakeService] = useState("");
  const [intakeComplaint, setIntakeComplaint] = useState("");
  const [intakeRoom, setIntakeRoom] = useState("");
  const [intakeConsultFee, setIntakeConsultFee] = useState(false);
  const [intakeConsultAmount, setIntakeConsultAmount] = useState(0);
  const [intakeNotes, setIntakeNotes] = useState("");

  // Checkout form
  const [checkoutAmount, setCheckoutAmount] = useState(0);
  const [checkoutPayment, setCheckoutPayment] = useState("cash");
  const [checkoutNextDate, setCheckoutNextDate] = useState("");
  const [checkoutNextService, setCheckoutNextService] = useState("");
  const [checkoutNotes, setCheckoutNotes] = useState("");

  // Checkout success state
  const [checkoutSuccess, setCheckoutSuccess] = useState(false);

  // Draft invoice creation
  const [draftInvoiceLoading, setDraftInvoiceLoading] = useState(false);
  const [draftInvoiceResult, setDraftInvoiceResult] = useState<{ invoiceId: string; invoiceNumber: string } | null>(null);

  // ─── Data Loading ─────────────────────────────────────────────────────────

  const loadJourney = useCallback(() => {
    setLoading(true);
    setError("");
    const params = new URLSearchParams();
    if (filterDate) params.set("date", filterDate);
    if (filterStatus) params.set("status", filterStatus);
    if (filterDoctor) params.set("doctorId", filterDoctor);
    if (filterService) params.set("serviceId", filterService);
    if (filterRoom) params.set("roomId", filterRoom);
    api
      .get<JourneyItem[]>(`/api/patient-journey/today?${params.toString()}`)
      .then((r) => setItems(r.data))
      .catch((err) => {
        const msg = err?.response?.data?.message ?? "فشل تحميل البيانات";
        setError(msg);
      })
      .finally(() => setLoading(false));
  }, [filterDate, filterStatus, filterDoctor, filterService, filterRoom]);

  useEffect(loadJourney, [loadJourney]);

  useEffect(() => {
    api
      .get<ServiceOption[]>("/api/settings/services/active")
      .then((r) => setServices(r.data))
      .catch(() => {});
    api
      .get<RoomOption[]>("/api/settings/rooms/active")
      .then((r) => setRooms(r.data))
      .catch(() => {});
  }, []);

  // ─── Summary Cards ────────────────────────────────────────────────────────

  const todayCount = items.length;
  const arrivedCount = items.filter((i) =>
    ["Arrived", "Waiting", "Called", "InRoom"].includes(i.appointmentStatus)
  ).length;
  const waitingCount = items.filter(
    (i) => i.appointmentStatus === "Waiting" || i.appointmentStatus === "Called"
  ).length;
  const inRoomCount = items.filter(
    (i) => i.appointmentStatus === "InRoom" || i.appointmentStatus === "InProgress"
  ).length;
  const checkoutCount = items.filter(
    (i) => i.checkoutStatus === "ReadyForCheckout"
  ).length;
  const completedCount = items.filter(
    (i) => i.appointmentStatus === "Completed"
  ).length;

  const summaryCards = [
    { label: "مواعيد اليوم", count: todayCount, icon: CalendarDays, color: "bg-blue-500" },
    { label: "وصلوا", count: arrivedCount, icon: UserCheck, color: "bg-amber-500" },
    { label: "في الانتظار", count: waitingCount, icon: Clock, color: "bg-orange-500" },
    { label: "داخل الغرفة", count: inRoomCount, icon: DoorOpen, color: "bg-cyan-500" },
    { label: "جاهز للحساب", count: checkoutCount, icon: CreditCard, color: "bg-green-600" },
    { label: "مكتمل", count: completedCount, icon: CheckCircle2, color: "bg-emerald-500" },
  ];

  // ─── Action Handlers ──────────────────────────────────────────────────────

  const handleAction = async (item: JourneyItem) => {
    if (item.nextAction === "Intake") {
      setSelectedItem(item);
      setIntakeService(item.serviceId ?? "");
      setIntakeComplaint("");
      setIntakeRoom("");
      setIntakeConsultFee(false);
      setIntakeConsultAmount(0);
      setIntakeNotes("");
      setDialogError("");
      setShowIntake(true);
      return;
    }

    if (item.nextAction === "Checkout") {
      setSelectedItem(item);
      setCheckoutAmount(0);
      setCheckoutPayment("cash");
      setCheckoutNextDate("");
      setCheckoutNextService("");
      setCheckoutNotes("");
      setDraftInvoiceLoading(false);
      setDraftInvoiceResult(null);
      setCheckoutSuccess(false);
      setDialogError("");
      setShowCheckout(true);
      return;
    }

    setActionLoading(true);
    try {
      if (item.nextAction === "SendToQueue") {
        await api.post(`/api/patient-journey/${item.appointmentId}/send-to-queue`, {});
      } else if (item.nextAction === "CallPatient") {
        await api.post(`/api/clinic-queue/${item.queueItemId}/call`, {});
      } else if (item.nextAction === "EnterRoom") {
        await api.post(`/api/clinic-queue/${item.queueItemId}/enter-room`);
      } else if (item.nextAction === "StartVisit") {
        await api.post(`/api/patient-journey/${item.appointmentId}/start-visit`);
      }
      loadJourney();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      setError(msg);
    } finally {
      setActionLoading(false);
    }
  };

  const handleIntakeSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedItem) return;
    setActionLoading(true);
    setDialogError("");
    try {
      await api.post(`/api/patient-journey/${selectedItem.appointmentId}/intake`, {
        serviceId: intakeService || null,
        chiefComplaint: intakeComplaint || null,
        roomId: intakeRoom || null,
        requiresConsultationFee: intakeConsultFee,
        consultationFeeAmount: intakeConsultFee ? intakeConsultAmount : null,
        notes: intakeNotes || null,
      });
      setShowIntake(false);
      // If also sending to queue right away
      await api.post(`/api/patient-journey/${selectedItem.appointmentId}/send-to-queue`, {
        roomId: intakeRoom || null,
      });
      loadJourney();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      setDialogError(msg);
    } finally {
      setActionLoading(false);
    }
  };

  const handleCheckoutSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedItem) return;
    setActionLoading(true);
    setDialogError("");
    try {
      await api.post(`/api/patient-journey/${selectedItem.appointmentId}/checkout`, {
        paymentAmount: checkoutAmount > 0 ? checkoutAmount : null,
        paymentMethod: checkoutPayment,
        nextAppointmentDate: checkoutNextDate || null,
        nextServiceId: checkoutNextService || null,
        notes: checkoutNotes || null,
      });
      setCheckoutSuccess(true);
      loadJourney();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      setDialogError(msg);
    } finally {
      setActionLoading(false);
    }
  };

  // ─── Render ───────────────────────────────────────────────────────────────

  return (
    <div className="space-y-5 max-w-7xl">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">رحلة المرضى اليوم</h1>
          <p className="text-sm text-gray-500 mt-1">
            مركز القيادة — من الوصول حتى إنهاء الحساب
          </p>
        </div>
        <button
          onClick={loadJourney}
          className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
        >
          <RefreshCw className="w-4 h-4" />
          تحديث
        </button>
      </div>

      {/* Workflow Navigation */}
      <WorkflowNav
        links={[
          WORKFLOW_LINKS.backToDailyOps(),
          WORKFLOW_LINKS.clinicQueue(),
          WORKFLOW_LINKS.payments(),
          WORKFLOW_LINKS.invoices(),
        ]}
        currentPage="/patient-journey"
      />

      {/* Summary Cards */}
      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
        {summaryCards.map((card) => (
          <div
            key={card.label}
            className="rounded-xl border border-gray-200 bg-white p-4 flex items-center gap-3"
          >
            <div
              className={cn(
                "w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0",
                card.color
              )}
            >
              <card.icon className="w-5 h-5 text-white" />
            </div>
            <div>
              <p className="text-2xl font-extrabold text-gray-900">{card.count}</p>
              <p className="text-[11px] text-gray-500 whitespace-nowrap">{card.label}</p>
            </div>
          </div>
        ))}
      </div>

      {/* Filters */}
      <div className="flex flex-wrap items-end gap-3 bg-gray-50 rounded-xl border border-gray-200 p-4">
        <div className="flex items-center gap-1 text-sm font-semibold text-gray-700">
          <Filter className="w-4 h-4" />
          تصفية
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">التاريخ</label>
          <input
            type="date"
            value={filterDate}
            onChange={(e) => setFilterDate(e.target.value)}
            className={inputCls}
            dir="ltr"
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">الحالة</label>
          <select
            value={filterStatus}
            onChange={(e) => setFilterStatus(e.target.value)}
            className={inputCls}
          >
            <option value="">الكل</option>
            {Object.entries(STATUS_LABELS).map(([k, v]) => (
              <option key={k} value={k}>{v}</option>
            ))}
          </select>
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">الخدمة</label>
          <select
            value={filterService}
            onChange={(e) => setFilterService(e.target.value)}
            className={inputCls}
          >
            <option value="">الكل</option>
            {services.map((s) => (
              <option key={s.id} value={s.id}>{s.arabicName}</option>
            ))}
          </select>
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">الغرفة</label>
          <select
            value={filterRoom}
            onChange={(e) => setFilterRoom(e.target.value)}
            className={inputCls}
          >
            <option value="">الكل</option>
            {rooms.map((r) => (
              <option key={r.id} value={r.id}>{r.arabicName}</option>
            ))}
          </select>
        </div>
      </div>

      {/* Error */}
      {error && (
        <div className="text-sm text-red-600 bg-red-50 px-4 py-3 rounded-lg">{error}</div>
      )}

      {/* Loading */}
      {loading && (
        <div className="animate-pulse space-y-2">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="h-16 bg-gray-100 rounded-lg" />
          ))}
        </div>
      )}

      {/* Empty State */}
      {!loading && items.length === 0 && (
        <div className="text-center py-16">
          <Users className="w-12 h-12 text-gray-300 mx-auto mb-3" />
          <p className="text-gray-500 font-medium">لا توجد رحلات مرضى لهذا اليوم</p>
          <p className="text-sm text-gray-400 mt-1">اختر تاريخًا آخر أو أضف مواعيد</p>
        </div>
      )}

      {/* Journey Table */}
      {!loading && items.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-gray-200">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                {[
                  "المريض",
                  "الموعد",
                  "الطبيب",
                  "الخدمة",
                  "الغرفة",
                  "الحالة",
                  "رسوم المعاينة",
                  "إجراء",
                ].map((h) => (
                  <th
                    key={h}
                    className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap"
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {items.map((item) => (
                <tr key={item.appointmentId} className="hover:bg-gray-50 transition">
                  <td className="px-4 py-3">
                    <div className="font-medium text-gray-900">{item.patientName}</div>
                    {item.patientPhone && (
                      <div className="text-xs text-gray-400 mt-0.5 flex items-center gap-1" dir="ltr">
                        <Phone className="w-3 h-3" />
                        {item.patientPhone}
                      </div>
                    )}
                  </td>
                  <td className="px-4 py-3 font-mono text-xs" dir="ltr">
                    {item.appointmentTime}
                  </td>
                  <td className="px-4 py-3 text-gray-700">{item.doctorName}</td>
                  <td className="px-4 py-3 text-gray-700">{item.serviceName ?? "—"}</td>
                  <td className="px-4 py-3 text-gray-700">{item.roomName ?? "—"}</td>
                  <td className="px-4 py-3">
                    <span
                      className={cn(
                        "text-xs px-2 py-0.5 rounded-full font-medium",
                        STATUS_COLORS[item.appointmentStatus] ?? "bg-gray-50 text-gray-700"
                      )}
                    >
                      {STATUS_LABELS[item.appointmentStatus] ?? item.appointmentStatus}
                    </span>
                    {item.checkoutStatus === "ReadyForCheckout" && (
                      <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-green-50 text-green-700 mr-1">
                        جاهز للحساب
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    {item.consultationFeeRequired ? (
                      item.consultationFeePaid ? (
                        <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-green-50 text-green-700">
                          مدفوعة
                        </span>
                      ) : (
                        <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-red-50 text-red-700">
                          مطلوبة
                        </span>
                      )
                    ) : (
                      <span className="text-xs text-gray-400">—</span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    {item.nextAction !== "None" && item.nextAction !== "InProgress" ? (
                      <button
                        onClick={() => handleAction(item)}
                        disabled={actionLoading}
                        className={cn(
                          "text-xs px-3 py-1.5 rounded-lg font-medium text-white transition disabled:opacity-60",
                          ACTION_COLORS[item.nextAction] ?? "bg-gray-400"
                        )}
                      >
                        {ACTION_LABELS[item.nextAction] ?? item.nextAction}
                      </button>
                    ) : item.nextAction === "InProgress" ? (
                      <span className="text-xs text-emerald-600 font-medium flex items-center gap-1">
                        <Stethoscope className="w-3 h-3" />
                        عند الطبيب
                      </span>
                    ) : (
                      <span className="text-xs text-gray-400">—</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* ─── Intake Dialog ─────────────────────────────────────────────── */}
      {showIntake && selectedItem && (
        <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4">
          <form
            onSubmit={handleIntakeSubmit}
            className="bg-white rounded-2xl shadow-xl w-full max-w-lg space-y-4 p-6"
          >
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-bold text-gray-900">تسجيل الوصول</h2>
              <button
                type="button"
                onClick={() => setShowIntake(false)}
                className="text-gray-400 hover:text-gray-700"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <p className="text-sm text-gray-600">
              المريض: <span className="font-semibold">{selectedItem.patientName}</span>
            </p>

            {dialogError && (
              <p className="text-xs text-red-600 bg-red-50 px-3 py-2 rounded-lg">{dialogError}</p>
            )}

            <div className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">الخدمة المطلوبة</label>
                <select
                  value={intakeService}
                  onChange={(e) => {
                    setIntakeService(e.target.value);
                    const svc = services.find((s) => s.id === e.target.value);
                    if (svc) {
                      setIntakeConsultFee(svc.requiresConsultationFee);
                      setIntakeConsultAmount(svc.defaultPrice);
                    }
                  }}
                  className={inputCls}
                >
                  <option value="">— اختر خدمة —</option>
                  {services.map((s) => (
                    <option key={s.id} value={s.id}>{s.arabicName}</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">الشكوى الرئيسية</label>
                <input
                  value={intakeComplaint}
                  onChange={(e) => setIntakeComplaint(e.target.value)}
                  className={inputCls}
                  placeholder="مثل: ألم في الضرس"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">الغرفة</label>
                <select
                  value={intakeRoom}
                  onChange={(e) => setIntakeRoom(e.target.value)}
                  className={inputCls}
                >
                  <option value="">— اختر غرفة —</option>
                  {rooms.map((r) => (
                    <option key={r.id} value={r.id}>{r.arabicName}</option>
                  ))}
                </select>
              </div>
              <div className="flex items-center gap-3">
                <label className="flex items-center gap-2 text-sm text-gray-700">
                  <input
                    type="checkbox"
                    checked={intakeConsultFee}
                    onChange={(e) => setIntakeConsultFee(e.target.checked)}
                    className="rounded border-gray-300"
                  />
                  هل توجد رسوم معاينة؟
                </label>
                {intakeConsultFee && (
                  <input
                    type="number"
                    value={intakeConsultAmount}
                    onChange={(e) => setIntakeConsultAmount(parseInt(e.target.value) || 0)}
                    className={cn(inputCls, "w-32")}
                    dir="ltr"
                    min={0}
                  />
                )}
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">ملاحظات</label>
                <textarea
                  value={intakeNotes}
                  onChange={(e) => setIntakeNotes(e.target.value)}
                  className={cn(inputCls, "h-20 resize-none")}
                  placeholder="ملاحظات إضافية"
                />
              </div>
            </div>

            <div className="flex justify-end gap-2 pt-2">
              <button
                type="button"
                onClick={() => setShowIntake(false)}
                className="px-4 py-2 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50"
              >
                إلغاء
              </button>
              <button
                type="submit"
                disabled={actionLoading}
                className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60"
              >
                <Save className="w-4 h-4" />
                {actionLoading ? "جارٍ الحفظ..." : "تسجيل الوصول وإدخال الطابور"}
              </button>
            </div>
          </form>
        </div>
      )}

      {/* ─── Checkout Dialog ───────────────────────────────────────────── */}
      {showCheckout && selectedItem && (
        <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg space-y-4 p-6">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-bold text-gray-900">
                {checkoutSuccess ? "تم إنهاء الحساب بنجاح" : "إنهاء الحساب"}
              </h2>
              <button
                type="button"
                onClick={() => setShowCheckout(false)}
                className="text-gray-400 hover:text-gray-700"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <p className="text-sm text-gray-600">
              المريض: <span className="font-semibold">{selectedItem.patientName}</span>
            </p>

            {dialogError && (
              <p className="text-xs text-red-600 bg-red-50 px-3 py-2 rounded-lg">{dialogError}</p>
            )}

            {/* ── Success State ── */}
            {checkoutSuccess && (
              <div className="space-y-3">
                <div className="flex items-center gap-2 bg-green-50 border border-green-200 rounded-lg px-4 py-3">
                  <CheckCircle2 className="w-5 h-5 text-green-600 flex-shrink-0" />
                  <span className="text-sm font-medium text-green-800">
                    تم إنهاء حساب الزيارة بنجاح
                  </span>
                </div>

                {/* Draft invoice link */}
                {draftInvoiceResult && (
                  <div className="border border-green-200 bg-green-50 rounded-lg p-3 space-y-1">
                    <p className="text-sm font-medium text-green-800">
                      الفاتورة: <span className="font-mono">{draftInvoiceResult.invoiceNumber}</span>
                    </p>
                    <Link
                      href={`/finance/invoices/${draftInvoiceResult.invoiceId}`}
                      className="text-sm text-[#3d7ab5] hover:underline flex items-center gap-1"
                    >
                      <FileText className="w-4 h-4" />
                      عرض الفاتورة
                    </Link>
                  </div>
                )}

                {/* Payments link */}
                <div className="border border-blue-200 bg-blue-50 rounded-lg p-3 space-y-1">
                  <p className="text-sm font-medium text-blue-800">تسجيل الدفع الفعلي</p>
                  <Link
                    href={`/finance/payments?patientId=${selectedItem.patientId}`}
                    className="text-sm text-[#3d7ab5] hover:underline flex items-center gap-1"
                  >
                    <CreditCard className="w-4 h-4" />
                    الانتقال إلى صفحة المدفوعات
                  </Link>
                </div>

                <div className="flex justify-end gap-2 pt-2">
                  <button
                    type="button"
                    onClick={() => setShowCheckout(false)}
                    className="px-4 py-2 text-sm font-medium rounded-lg bg-green-600 text-white hover:bg-green-700"
                  >
                    إغلاق
                  </button>
                </div>
              </div>
            )}

            {/* ── Pre-Checkout Form ── */}
            {!checkoutSuccess && (
              <form onSubmit={handleCheckoutSubmit} className="space-y-4">
                {/* Info banner — checkout is workflow-status only */}
                <div className="bg-blue-50 border border-blue-200 rounded-lg px-3 py-2 text-xs text-blue-700">
                  إنهاء الحساب هنا يغيّر حالة الزيارة فقط. لتسجيل الدّفع الفعلي، انتقل إلى صفحة المالية بعد الإنهاء.
                </div>

                {/* Create Draft Invoice */}
                {selectedItem.checkoutStatus === "ReadyForCheckout" && !draftInvoiceResult && (
                  <div className="border border-[#3d7ab5]/20 bg-[#f7fafd] rounded-lg p-3 space-y-2">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2 text-sm font-medium text-[#0d2137]">
                        <FileText className="w-4 h-4 text-[#3d7ab5]" />
                        إنشاء فاتورة مسودة
                      </div>
                      <button
                        type="button"
                        disabled={draftInvoiceLoading}
                        onClick={async () => {
                          if (!selectedItem.visitId) {
                            toast.error("لا يوجد زيارة مرتبطة لإنشاء فاتورة");
                            return;
                          }
                          setDraftInvoiceLoading(true);
                          try {
                            const res = await api.post<{ id: string; invoiceNumber: string }>(
                              `/api/patient-journey/${selectedItem.visitId}/create-draft-invoice`
                            );
                            setDraftInvoiceResult({
                              invoiceId: res.data.id,
                              invoiceNumber: res.data.invoiceNumber,
                            });
                            toast.success(`تم إنشاء الفاتورة ${res.data.invoiceNumber}`);
                          } catch {
                            toast.error("فشل إنشاء الفاتورة المسودة");
                          } finally {
                            setDraftInvoiceLoading(false);
                          }
                        }}
                        className="flex items-center gap-1.5 px-3 py-1.5 text-sm rounded-lg bg-[#3d7ab5] text-white hover:opacity-90 disabled:opacity-50 transition"
                      >
                        <FileText className="w-3.5 h-3.5" />
                        {draftInvoiceLoading ? "جارٍ الإنشاء..." : "إنشاء فاتورة مسودة"}
                      </button>
                    </div>
                    <p className="text-xs text-[#94a3b8]">الدفع يتم عبر صفحة المالية</p>
                  </div>
                )}

                {/* Draft invoice result */}
                {draftInvoiceResult && (
                  <div className="border border-green-200 bg-green-50 rounded-lg p-3 space-y-1">
                    <p className="text-sm font-medium text-green-800">
                      تم إنشاء الفاتورة: <span className="font-mono">{draftInvoiceResult.invoiceNumber}</span>
                    </p>
                    <Link
                      href={`/finance/invoices/${draftInvoiceResult.invoiceId}`}
                      className="text-xs text-[#3d7ab5] hover:underline flex items-center gap-1"
                    >
                      <FileText className="w-3 h-3" />
                      عرض الفاتورة
                    </Link>
                    <p className="text-xs text-[#94a3b8]">الدفع يتم عبر صفحة المالية</p>
                  </div>
                )}

                <div className="space-y-3">
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">المبلغ المطلوب (مرجعي)</label>
                    <input
                      type="number"
                      value={checkoutAmount}
                      onChange={(e) => setCheckoutAmount(parseInt(e.target.value) || 0)}
                      className={inputCls}
                      dir="ltr"
                      min={0}
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">طريقة الدفع (مرجعية)</label>
                    <select
                      value={checkoutPayment}
                      onChange={(e) => setCheckoutPayment(e.target.value)}
                      className={inputCls}
                    >
                      {PAYMENT_METHODS.map((m) => (
                        <option key={m.value} value={m.value}>{m.label}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">حجز موعد قادم</label>
                    <input
                      type="date"
                      value={checkoutNextDate}
                      onChange={(e) => setCheckoutNextDate(e.target.value)}
                      className={inputCls}
                      dir="ltr"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">الخدمة القادمة</label>
                    <select
                      value={checkoutNextService}
                      onChange={(e) => setCheckoutNextService(e.target.value)}
                      className={inputCls}
                    >
                      <option value="">— اختر خدمة —</option>
                      {services.map((s) => (
                        <option key={s.id} value={s.id}>{s.arabicName}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">ملاحظات</label>
                    <textarea
                      value={checkoutNotes}
                      onChange={(e) => setCheckoutNotes(e.target.value)}
                      className={cn(inputCls, "h-20 resize-none")}
                      placeholder="ملاحظات إضافية"
                    />
                  </div>
                </div>

                <div className="flex justify-end gap-2 pt-2">
                  <button
                    type="button"
                    onClick={() => setShowCheckout(false)}
                    className="px-4 py-2 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50"
                  >
                    إلغاء
                  </button>
                  <button
                    type="submit"
                    disabled={actionLoading}
                    className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-green-600 text-white hover:bg-green-700 disabled:opacity-60"
                  >
                    <CreditCard className="w-4 h-4" />
                    {actionLoading ? "جارٍ الإنهاء..." : "إنهاء الحساب"}
                  </button>
                </div>
              </form>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
