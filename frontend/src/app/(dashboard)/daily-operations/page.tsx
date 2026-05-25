"use client";

import { useState, useCallback, useMemo } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  Calendar, ClipboardList, CreditCard, Clock, CheckCircle,
  Stethoscope, AlertTriangle, Search, RefreshCw, ArrowLeft,
  UserCheck, Globe, Plus, CalendarClock, Route,
  Wallet,
} from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import { hasPermission, PERMISSION_KEYS } from "@/hooks/usePermissions";
import { toast } from "@/stores/toastStore";
import { WorkflowNav, WORKFLOW_LINKS } from "@/components/shared/WorkflowNav";

import {
  NAVY, BLUE, ORANGE,
  TABS,
  fmtDate, getTodayStr, fmtRial,
  computeDayStats, filterByTab,
  isDoctorRole,
  type TodayJourneyItem, type TabKey,
} from "./_lib/constants";

import {
  useTodayJourneyItems,
  usePatientSummary,
  useDoctors,
  useBranches,
  useRooms,
  useServices,
  useClinicSettings,
  useDashboardStats,
  useFinanceSummary,
  useIntake,
  useSendToQueue,
  useCallPatient,
  useEnterRoom,
  useUpdateAppointmentStatus,
  useCreatePayment,
  useCheckout,
  useHandoff,
  useCancelQueue,
  useChangeRoom,
  useCreateAppointment,
  useCompleteVisit,
} from "./_lib/hooks";

import AppointmentsTable from "./_components/AppointmentsTable";
import {
  QuickPaymentModal,
  CompleteVisitModal,
  BookAppointmentModal,
  ConfirmDialog,
  WhatsAppMenu,
  ChangeRoomModal,
} from "./_components/Modals";

/* ═══════════════════════════════════════════════════════════════════════════
   Quick Link items for sidebar-style navigation at bottom
   ═══════════════════════════════════════════════════════════════════════════ */
const QUICK_LINKS = [
  { href: "/booking-requests",    label: "طلبات الحجز",   icon: Globe,         color: BLUE,   perm: PERMISSION_KEYS.BOOKING_REQUESTS_VIEW },
  { href: "/appointments/new",    label: "موعد جديد",     icon: CalendarClock, color: "#a855f7", perm: PERMISSION_KEYS.APPOINTMENTS_CREATE },
  { href: "/clinic-queue",        label: "الطابور",       icon: ClipboardList, color: ORANGE, perm: PERMISSION_KEYS.CLINIC_QUEUE_VIEW },
  { href: "/patient-journey",     label: "رحلة المريض",   icon: Route,         color: "#22c55e", perm: PERMISSION_KEYS.PATIENT_JOURNEY_VIEW },
  { href: "/finance/payments",    label: "المدفوعات",     icon: CreditCard,    color: "#22c55e", perm: PERMISSION_KEYS.PAYMENTS_VIEW },
  { href: "/patients/new",        label: "مريض جديد",     icon: Plus,          color: BLUE,   perm: PERMISSION_KEYS.PATIENTS_CREATE },
];

/* ═══════════════════════════════════════════════════════════════════════════
   Summary Card
   ═══════════════════════════════════════════════════════════════════════════ */
function SummaryCard({
  icon: Icon, label, value, color, subValue,
}: {
  icon: React.ElementType; label: string; value: number | string;
  color: string; subValue?: string;
}) {
  return (
    <div className="rounded-xl p-3.5 flex items-center gap-3 transition-shadow hover:shadow-md"
      style={{ background: color + "08", border: `1.5px solid ${color}20` }}>
      <div className="w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0"
        style={{ background: color + "15" }}>
        <Icon className="w-5 h-5" style={{ color }} />
      </div>
      <div>
        <div className="text-xl font-extrabold leading-tight" style={{ color }}>{value}</div>
        <div className="text-[11px] font-medium" style={{ color: "#64748b" }}>{label}</div>
        {subValue && <div className="text-[10px] font-semibold mt-0.5" style={{ color: "#94a3b8" }}>{subValue}</div>}
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   MAIN PAGE
   ═══════════════════════════════════════════════════════════════════════════ */
export default function DailyOperationsPage() {
  const router = useRouter();
  const { user } = useAuthStore();
  // toast is imported directly from the store
  const userRole = user?.role ?? "";
  const isDoctor = isDoctorRole(userRole);

  // ── Filters ──
  const [filterDate, setFilterDate] = useState(getTodayStr());
  const [filterDoctor, setFilterDoctor] = useState("");
  const [filterBranch, setFilterBranch] = useState("");
  const [filterStatus, setFilterStatus] = useState("");
  const [searchQuery, setSearchQuery] = useState("");
  const [activeTab, setActiveTab] = useState<TabKey>("appointments");

  // ── Data ──
  const { data: items = [], isLoading: itemsLoading, refetch: refetchItems } = useTodayJourneyItems({
    date: filterDate,
    status: filterStatus || undefined,
    doctorId: filterDoctor || undefined,
  });

  useDashboardStats();
  const { data: doctors = [] } = useDoctors();
  const { data: branches = [] } = useBranches();
  const { data: rooms = [] } = useRooms();
  const { data: services = [] } = useServices();
  const { data: clinicSettings } = useClinicSettings();
  const { data: financeSummary } = useFinanceSummary();
  const clinicName = clinicSettings?.clinicName ?? "مركز الدكتور عقلان";

  // ── Mutations ──
  const intakeMutation = useIntake();
  const sendToQueueMutation = useSendToQueue();
  const callPatientMutation = useCallPatient();
  const enterRoomMutation = useEnterRoom();
  const updateStatusMutation = useUpdateAppointmentStatus();
  const createPaymentMutation = useCreatePayment();
  const checkoutMutation = useCheckout();
  const handoffMutation = useHandoff();
  const cancelQueueMutation = useCancelQueue();
  const changeRoomMutation = useChangeRoom();
  const createAppointmentMutation = useCreateAppointment();
  const completeVisitMutation = useCompleteVisit();

  // ── Modal state ──
  const [selectedItem, setSelectedItem] = useState<TodayJourneyItem | null>(null);
  const [paymentModalOpen, setPaymentModalOpen] = useState(false);
  const [completeVisitModalOpen, setCompleteVisitModalOpen] = useState(false);
  const [bookAppointmentModalOpen, setBookAppointmentModalOpen] = useState(false);
  const [confirmDialogOpen, setConfirmDialogOpen] = useState(false);
  const [confirmDialogType, setConfirmDialogType] = useState<"Cancel" | "NoShow" | "CancelQueue" | "ChangeRoom" | "Complete">("Cancel");
  const [whatsAppMenuOpen, setWhatsAppMenuOpen] = useState(false);
  const [changeRoomModalOpen, setChangeRoomModalOpen] = useState(false);

  // ── Selected patient summary ──
  const { data: selectedSummary } = usePatientSummary(selectedItem?.patientId ?? null);

  // ── Computed ──
  const dayStats = useMemo(() => computeDayStats(items, financeSummary), [items, financeSummary]);

  const filteredItems = useMemo(() => {
    let result = filterByTab(items, activeTab);
    if (searchQuery.trim()) {
      const q = searchQuery.trim().toLowerCase();
      result = result.filter(i =>
        i.patientName.toLowerCase().includes(q) ||
        (i.patientPhone && i.patientPhone.includes(q)) ||
        i.doctorName.toLowerCase().includes(q) ||
        (i.serviceName && i.serviceName.toLowerCase().includes(q))
      );
    }
    return result;
  }, [items, activeTab, searchQuery]);

  // ── Tab counts ──
  const tabCounts = useMemo(() => ({
    appointments: items.length,
    queue: items.filter(i => i.queueStatus === "Waiting" || i.queueStatus === "Called" || (i.appointmentStatus === "Waiting" && !i.queueStatus)).length,
    inClinic: items.filter(i => i.appointmentStatus === "InRoom" || i.appointmentStatus === "InProgress" || i.queueStatus === "InRoom" || i.queueStatus === "InProgress").length,
    completed: items.filter(i => i.appointmentStatus === "Completed").length,
    payments: items.filter(i => i.checkoutStatus === "ReadyForCheckout" || i.nextAction === "Checkout" || i.appointmentStatus === "Completed").length,
    overdue: items.filter(i => i.appointmentStatus === "NoShow" || i.appointmentStatus === "Cancelled").length,
  }), [items]);

  // ── Action handlers ──
  const handleIntake = useCallback((item: TodayJourneyItem) => {
    intakeMutation.mutate(
      { appointmentId: item.appointmentId, body: {} },
      { onSuccess: () => toast.success("تم تسجيل وصول المريض"), onError: () => toast.error("فشل تسجيل الوصول") },
    );
  }, [intakeMutation]);

  const handleSendToQueue = useCallback((item: TodayJourneyItem) => {
    sendToQueueMutation.mutate(
      { appointmentId: item.appointmentId },
      { onSuccess: () => toast.success("تمت إضافة المريض للطابور"), onError: () => toast.error("فشل الإضافة للطابور") },
    );
  }, [sendToQueueMutation]);

  const handleCallPatient = useCallback((item: TodayJourneyItem) => {
    if (!item.queueItemId) return;
    callPatientMutation.mutate(
      item.queueItemId,
      { onSuccess: () => toast.success("تم نداء المريض"), onError: () => toast.error("فشل نداء المريض") },
    );
  }, [callPatientMutation]);

  const handleEnterRoom = useCallback((item: TodayJourneyItem) => {
    if (!item.queueItemId) return;
    enterRoomMutation.mutate(
      item.queueItemId,
      { onSuccess: () => toast.success("تم دخول الغرفة"), onError: () => toast.error("فشل دخول الغرفة") },
    );
  }, [enterRoomMutation]);

  const handleQuickPayment = useCallback((item: TodayJourneyItem) => {
    setSelectedItem(item);
    setPaymentModalOpen(true);
  }, []);

  const handleCompleteVisit = useCallback((item: TodayJourneyItem) => {
    setSelectedItem(item);
    setCompleteVisitModalOpen(true);
  }, []);

  const handleBookAppointment = useCallback((item: TodayJourneyItem) => {
    setSelectedItem(item);
    setBookAppointmentModalOpen(true);
  }, []);

  const handleWhatsApp = useCallback((item: TodayJourneyItem) => {
    setSelectedItem(item);
    setWhatsAppMenuOpen(true);
  }, []);

  const handleNoShow = useCallback((item: TodayJourneyItem) => {
    setSelectedItem(item);
    setConfirmDialogType("NoShow");
    setConfirmDialogOpen(true);
  }, []);

  const handleCancel = useCallback((item: TodayJourneyItem) => {
    setSelectedItem(item);
    setConfirmDialogType("Cancel");
    setConfirmDialogOpen(true);
  }, []);

  const handleViewPatient = useCallback((item: TodayJourneyItem) => {
    router.push(`/patients/${item.patientId}`);
  }, [router]);

  // ── Confirm action ──
  const handleConfirmAction = useCallback(async () => {
    if (!selectedItem) return;
    try {
      if (confirmDialogType === "NoShow") {
        await updateStatusMutation.mutateAsync({ appointmentId: selectedItem.appointmentId, status: "NoShow" });
        toast.success("تم تسجيل عدم الحضور");
      } else if (confirmDialogType === "Cancel") {
        await updateStatusMutation.mutateAsync({ appointmentId: selectedItem.appointmentId, status: "Cancelled" });
        toast.success("تم إلغاء الموعد");
      } else if (confirmDialogType === "CancelQueue") {
        if (!selectedItem.queueItemId) return;
        await cancelQueueMutation.mutateAsync(selectedItem.queueItemId);
        toast.success("تم إلغاء المريض من الطابور");
      } else if (confirmDialogType === "Complete") {
        if (!selectedItem.queueItemId) return;
        await completeVisitMutation.mutateAsync({ queueItemId: selectedItem.queueItemId });
        toast.success("تم إنهاء الزيارة");
      }
    } catch {
      toast.error("فشل تنفيذ الإجراء");
    }
    setConfirmDialogOpen(false);
  }, [selectedItem, confirmDialogType, updateStatusMutation, cancelQueueMutation, completeVisitMutation]);

  // ── Payment confirm ──
  const handlePaymentConfirm = useCallback(async (amount: number, method: string, desc: string, notes: string) => {
    if (!selectedItem) return;
    try {
      await createPaymentMutation.mutateAsync({
        patientId: selectedItem.patientId,
        amount,
        paymentMethod: method,
        serviceDescription: desc || undefined,
        doctorId: selectedItem.doctorId,
        notes: notes || undefined,
      });
      toast.success("تم تسجيل الدفعة بنجاح");
      setPaymentModalOpen(false);
    } catch {
      toast.error("فشل تسجيل الدفعة");
    }
  }, [selectedItem, createPaymentMutation]);

  // ── Complete visit confirm ──
  const handleCompleteVisitConfirm = useCallback(async (data: {
    serviceDesc: string; amountDue: number; isPaid: boolean;
    needsFollowUp: boolean; nextDate: string; notes: string;
    diagnosis: string; instructions: string;
  }) => {
    if (!selectedItem) return;
    try {
      // If handoff available (visit exists)
      if (selectedItem.visitId) {
        await handoffMutation.mutateAsync({
          visitId: selectedItem.visitId,
          body: {
            treatmentDone: data.serviceDesc || undefined,
            diagnosis: data.diagnosis || undefined,
            instructions: data.instructions || undefined,
            amountDue: data.amountDue || undefined,
            notes: data.notes || undefined,
            followUpDate: data.needsFollowUp ? data.nextDate : undefined,
          },
        });
      }
      // Then checkout
      await checkoutMutation.mutateAsync({
        appointmentId: selectedItem.appointmentId,
        body: {
          paymentAmount: data.isPaid ? data.amountDue : 0,
          paymentMethod: "Cash",
          notes: data.notes || undefined,
          nextAppointmentDate: data.needsFollowUp ? data.nextDate : undefined,
        },
      });
      toast.success("تم إنهاء الزيارة بنجاح");
      setCompleteVisitModalOpen(false);
    } catch {
      toast.error("فشل إنهاء الزيارة");
    }
  }, [selectedItem, handoffMutation, checkoutMutation]);

  // ── Checkout shortcut ──
  const handleCheckoutConfirm = useCallback(async (data: {
    paymentAmount: number; paymentMethod: string; notes: string;
    nextDate?: string; nextServiceId?: string;
  }) => {
    if (!selectedItem) return;
    try {
      await checkoutMutation.mutateAsync({
        appointmentId: selectedItem.appointmentId,
        body: data,
      });
      toast.success("تم إنهاء الزيارة بنجاح");
      setCompleteVisitModalOpen(false);
    } catch {
      toast.error("فشل إنهاء الزيارة");
    }
  }, [selectedItem, checkoutMutation]);

  // ── Book appointment confirm ──
  const handleBookConfirm = useCallback(async (data: {
    doctorId: string; date: string; startTime: string; endTime: string;
    serviceId: string; type: string; notes: string;
  }) => {
    if (!selectedItem) return;
    try {
      await createAppointmentMutation.mutateAsync({
        patientId: selectedItem.patientId,
        doctorId: data.doctorId,
        appointmentDate: data.date,
        startTime: data.startTime,
        endTime: data.endTime,
        serviceId: data.serviceId || undefined,
        appointmentType: data.type,
        notes: data.notes || undefined,
      });
      toast.success("تم حجز الموعد بنجاح");
      setBookAppointmentModalOpen(false);
    } catch {
      toast.error("فشل حجز الموعد");
    }
  }, [selectedItem, createAppointmentMutation]);

  // ── Visible quick links ──
  const visibleQuickLinks = useMemo(() =>
    QUICK_LINKS.filter(l => hasPermission(user, l.perm)),
  [user]);

  // ── Available status filters ──
  const statusFilters = [
    { value: "", label: "الكل" },
    { value: "Scheduled", label: "مجدول" },
    { value: "Confirmed", label: "مؤكد" },
    { value: "Arrived", label: "وصل" },
    { value: "Waiting", label: "في الانتظار" },
    { value: "InRoom", label: "داخل الغرفة" },
    { value: "InProgress", label: "جاري العلاج" },
    { value: "Completed", label: "مكتمل" },
    { value: "NoShow", label: "لم يحضر" },
    { value: "Cancelled", label: "ملغى" },
  ];

  // ── Render ──
  return (
    <div className="space-y-4 page-content">
      {/* ═══ Header ═══ */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-extrabold" style={{ color: NAVY }}>التشغيل اليومي</h1>
          <p className="text-sm mt-1" style={{ color: "#64748b" }}>
            رحلة الاستقبال اليومية — من الموعد حتى الدفع والمتابعة
          </p>
        </div>
        <div className="flex items-center gap-2">
          <button onClick={() => refetchItems()}
            className="w-9 h-9 rounded-lg flex items-center justify-center transition hover:bg-gray-100"
            style={{ border: "1px solid #e2e8f0" }}
            title="تحديث">
            <RefreshCw className="w-4 h-4" style={{ color: NAVY }} />
          </button>
          <Link href="/"
            className="flex items-center gap-1.5 px-3.5 py-2 rounded-lg text-sm font-semibold transition"
            style={{ background: NAVY + "0d", color: NAVY, border: `1px solid ${NAVY}20` }}
            onMouseEnter={e => (e.currentTarget.style.background = NAVY + "1a")}
            onMouseLeave={e => (e.currentTarget.style.background = NAVY + "0d")}>
            <ArrowLeft className="w-4 h-4" />
            لوحة التحكم
          </Link>
        </div>
      </div>

      {/* ═══ Workflow Nav ═══ */}
      <WorkflowNav
        links={[
          WORKFLOW_LINKS.bookingRequests(),
          WORKFLOW_LINKS.appointments(),
          WORKFLOW_LINKS.newAppointment(),
          WORKFLOW_LINKS.clinicQueue(),
          WORKFLOW_LINKS.patientJourney(),
          WORKFLOW_LINKS.checkout(),
          WORKFLOW_LINKS.payments(),
        ]}
        currentPage="/daily-operations"
      />

      {/* ═══ Top Bar: Date + Filters ═══ */}
      <div className="bg-white rounded-xl border p-4 flex flex-wrap items-center gap-3"
        style={{ borderColor: "#e8f0f9" }}>
        {/* Date */}
        <div className="flex items-center gap-2">
          <Calendar className="w-4 h-4" style={{ color: NAVY }} />
          <input type="date" value={filterDate} onChange={e => setFilterDate(e.target.value)}
            className="text-sm font-semibold rounded-lg border px-2.5 py-1.5 outline-none focus:border-[#3d7ab5]"
            style={{ borderColor: "#e2e8f0", color: NAVY }} />
        </div>

        {/* Branch filter */}
        {!isDoctor && branches.length > 1 && (
          <select value={filterBranch} onChange={e => setFilterBranch(e.target.value)}
            className="text-sm rounded-lg border px-2.5 py-1.5 outline-none focus:border-[#3d7ab5]"
            style={{ borderColor: "#e2e8f0", color: NAVY }}>
            <option value="">كل الفروع</option>
            {branches.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}
          </select>
        )}

        {/* Doctor filter */}
        {!isDoctor && (
          <select value={filterDoctor} onChange={e => setFilterDoctor(e.target.value)}
            className="text-sm rounded-lg border px-2.5 py-1.5 outline-none focus:border-[#3d7ab5]"
            style={{ borderColor: "#e2e8f0", color: NAVY }}>
            <option value="">كل الأطباء</option>
            {doctors.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
          </select>
        )}

        {/* Status filter */}
        <select value={filterStatus} onChange={e => setFilterStatus(e.target.value)}
          className="text-sm rounded-lg border px-2.5 py-1.5 outline-none focus:border-[#3d7ab5]"
          style={{ borderColor: "#e2e8f0", color: NAVY }}>
          {statusFilters.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
        </select>

        {/* Search */}
        <div className="flex-1 min-w-[200px] relative">
          <Search className="w-4 h-4 absolute top-1/2 right-3 -translate-y-1/2" style={{ color: "#94a3b8" }} />
          <input
            type="text"
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            placeholder="بحث باسم المريض / رقم الهاتف / الطبيب..."
            className="w-full text-sm rounded-lg border pl-3 pr-9 py-1.5 outline-none focus:border-[#3d7ab5]"
            style={{ borderColor: "#e2e8f0" }}
          />
        </div>

        {/* Date display */}
        <div className="text-sm font-bold" style={{ color: NAVY }}>
          {fmtDate(filterDate)}
        </div>
      </div>

      {/* ═══ Summary Cards ═══ */}
      <div className="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-8 gap-2.5">
        <SummaryCard icon={Calendar} label="مواعيد اليوم" value={dayStats.totalAppointments} color={ORANGE} />
        <SummaryCard icon={UserCheck} label="حضروا" value={dayStats.arrived} color="#16a34a" />
        <SummaryCard icon={Clock} label="في الانتظار" value={dayStats.waiting} color="#d97706" />
        <SummaryCard icon={Stethoscope} label="داخل العيادة" value={dayStats.inClinic} color="#9333ea" />
        <SummaryCard icon={CheckCircle} label="مكتمل" value={dayStats.completed} color="#22c55e" />
        <SummaryCard icon={AlertTriangle} label="لم يحضر" value={dayStats.noShow} color="#ef4444" />
        <SummaryCard icon={CreditCard} label="مدفوعات اليوم"
          value={fmtRial(dayStats.todayPayments)}
          color="#22c55e" />
        <SummaryCard icon={Wallet} label="المستحقات المتأخرة"
          value={fmtRial(dayStats.overdueAmount)}
          color="#ef4444"
          subValue={financeSummary?.overdueAmount ? "يحتاج متابعة" : undefined} />
      </div>

      {/* ═══ Tabs ═══ */}
      <div className="bg-white rounded-xl border overflow-hidden" style={{ borderColor: "#e8f0f9" }}>
        {/* Tab headers */}
        <div className="flex overflow-x-auto border-b" style={{ borderColor: "#e8f0f9" }}>
          {TABS.map(tab => {
            const count = tabCounts[tab.key];
            const isActive = activeTab === tab.key;
            return (
              <button key={tab.key} onClick={() => setActiveTab(tab.key)}
                className="flex items-center gap-1.5 px-4 py-3 text-sm font-bold whitespace-nowrap transition-colors relative"
                style={{
                  color: isActive ? tab.color : "#64748b",
                  borderBottom: isActive ? `2.5px solid ${tab.color}` : "2.5px solid transparent",
                }}>
                <span>{tab.label}</span>
                {count > 0 && (
                  <span className="text-[10px] font-extrabold px-1.5 py-0.5 rounded-full"
                    style={{ background: tab.color + "15", color: tab.color }}>
                    {count}
                  </span>
                )}
              </button>
            );
          })}
        </div>

        {/* Tab content */}
        <div className="p-4">
          <AppointmentsTable
            items={filteredItems}
            loading={itemsLoading}
            isDoctor={isDoctor}
            onIntake={handleIntake}
            onSendToQueue={handleSendToQueue}
            onCallPatient={handleCallPatient}
            onEnterRoom={handleEnterRoom}
            onQuickPayment={handleQuickPayment}
            onBookAppointment={handleBookAppointment}
            onWhatsApp={handleWhatsApp}
            onNoShow={handleNoShow}
            onCancel={handleCancel}
            onViewPatient={handleViewPatient}
            onCompleteVisit={handleCompleteVisit}
          />
        </div>
      </div>

      {/* ═══ Quick Links Bar ═══ */}
      {visibleQuickLinks.length > 0 && (
        <div className="bg-white rounded-xl border p-4" style={{ borderColor: "#e8f0f9" }}>
          <h3 className="text-xs font-bold mb-3" style={{ color: "#64748b" }}>روابط سريعة</h3>
          <div className="flex flex-wrap gap-2">
            {visibleQuickLinks.map(link => (
              <Link key={link.href} href={link.href}
                className="flex items-center gap-1.5 px-3 py-2 rounded-lg text-xs font-bold transition"
                style={{ background: link.color + "0d", color: link.color, border: `1px solid ${link.color}20` }}
                onMouseEnter={e => { e.currentTarget.style.background = link.color + "18"; }}
                onMouseLeave={e => { e.currentTarget.style.background = link.color + "0d"; }}>
                <link.icon className="w-3.5 h-3.5" />
                {link.label}
              </Link>
            ))}
          </div>
        </div>
      )}

      {/* ═══ Modals ═══ */}
      <QuickPaymentModal
        open={paymentModalOpen}
        onClose={() => setPaymentModalOpen(false)}
        item={selectedItem}
        summary={selectedSummary ?? null}
        isPending={createPaymentMutation.isPending}
        onConfirm={handlePaymentConfirm}
      />

      <CompleteVisitModal
        open={completeVisitModalOpen}
        onClose={() => setCompleteVisitModalOpen(false)}
        item={selectedItem}
        summary={selectedSummary ?? null}
        isPending={checkoutMutation.isPending || handoffMutation.isPending}
        onConfirm={handleCompleteVisitConfirm}
        onCheckout={handleCheckoutConfirm}
      />

      <BookAppointmentModal
        open={bookAppointmentModalOpen}
        onClose={() => setBookAppointmentModalOpen(false)}
        item={selectedItem}
        doctors={doctors}
        services={services}
        isPending={createAppointmentMutation.isPending}
        onConfirm={handleBookConfirm}
      />

      <ConfirmDialog
        open={confirmDialogOpen}
        onClose={() => setConfirmDialogOpen(false)}
        type={confirmDialogType}
        patientName={selectedItem?.patientName ?? ""}
        isPending={updateStatusMutation.isPending || cancelQueueMutation.isPending || completeVisitMutation.isPending}
        onConfirm={handleConfirmAction}
      />

      <WhatsAppMenu
        open={whatsAppMenuOpen}
        onClose={() => setWhatsAppMenuOpen(false)}
        item={selectedItem}
        summary={selectedSummary ?? null}
        clinicName={clinicName}
      />

      <ChangeRoomModal
        open={changeRoomModalOpen}
        onClose={() => setChangeRoomModalOpen(false)}
        rooms={rooms}
        isPending={changeRoomMutation.isPending}
        onConfirm={async (roomId: string) => {
          if (!selectedItem?.queueItemId) return;
          try {
            await changeRoomMutation.mutateAsync({ queueItemId: selectedItem.queueItemId, roomId });
            toast.success("تم تغيير الغرفة");
            setChangeRoomModalOpen(false);
          } catch {
            toast.error("فشل تغيير الغرفة");
          }
        }}
      />
    </div>
  );
}
