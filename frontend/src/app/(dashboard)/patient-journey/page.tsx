"use client";

import { useEffect, useState, useCallback, useMemo, useRef } from "react";
import Link from "next/link";
import {
  UserCheck, Clock, DoorOpen, CreditCard, CheckCircle2,
  Stethoscope, CalendarDays, RefreshCw, Users,
  Filter, AlertTriangle, ExternalLink, Megaphone,
  CircleDot,
  Ban, UserX,
  ArrowRightLeft, Timer, MessageSquare, MessageCircle, Pill, Download, Printer,
  Route, Upload, Undo2,
} from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import { useAuthStore } from "@/stores/authStore";
import { toast } from "@/stores/toastStore";
import { WorkflowNav, WORKFLOW_LINKS } from "@/components/shared/WorkflowNav";
import { useHasPermission, PERMISSION_KEYS } from "@/hooks/usePermissions";
import {
  useDailyJourneySummary,
  useJourneyIntake,
  useJourneySendToQueue,
  useJourneyStartVisit,
  useJourneyHandoff,
  useJourneyCheckout,
  useJourneyCreateDraftInvoice,
  useJourneyCancelQueue,
  useJourneyChangeRoom,
  useJourneyUpdateAppointmentStatus,
  useJourneyCreatePayment,
  useJourneyIssueInvoice,
  useJourneyUpdateVisit,
  useJourneySendSms,
  useJourneySendAppointmentReminder,
  useJourneyCreatePrescription,
  useJourneyCreateAppointment,
  useQueueEstimatedWait,
} from "@/hooks/usePatientJourney";
import {
  STATUS_LABELS, STATUS_COLORS, ACTION_LABELS, ACTION_COLORS,
  fmtDate, fmtTime, isDoctorRole, isAccountantRole, isReceptionRole,
} from "./_lib/constants";
import type { JourneyItem, ServiceOption, RoomOption } from "./_lib/constants";
import {
  PatientHeaderCard, TodaysAppointmentCard, QueueStatusCard,
  MedicalAlertsCard, JourneyActionsPanel, TodaysVisitCard,
  FinanceSummaryCard, ActiveOrthoCard, RecentVisitsCard, TimelineCard,
} from "./_components/Cards";
import {
  ConfirmDialog, RecordPaymentModal, SendSmsModal,
  PrescriptionModal, EditVisitModal, BookAppointmentModal,
  UploadDocumentModal,
} from "./_components/Modals";

// ─── Main Page ────────────────────────────────────────────────────────────────

export default function PatientJourneyPage() {
  const { user } = useAuthStore();
  const userRole = user?.role ?? "";
  const isDoctor = isDoctorRole(userRole);
  const isAccountant = isAccountantRole(userRole);
  const isReception = isReceptionRole(userRole);

  // Permissions
  const canViewJourney = useHasPermission(PERMISSION_KEYS.PATIENT_JOURNEY_VIEW);
  const canEditJourney = useHasPermission(PERMISSION_KEYS.PATIENT_JOURNEY_EDIT);
  const canViewVisits = useHasPermission(PERMISSION_KEYS.VISITS_VIEW);
  const canViewFinance = useHasPermission(PERMISSION_KEYS.PAYMENTS_VIEW);
  const canViewCheckout = useHasPermission(PERMISSION_KEYS.CHECKOUT_VIEW);
  const canViewInvoices = useHasPermission(PERMISSION_KEYS.INVOICES_VIEW);

  // ─── Left Panel State ─────────────────────────────────────────────────────
  const [items, setItems] = useState<JourneyItem[]>([]);
  const [services, setServices] = useState<ServiceOption[]>([]);
  const [rooms, setRooms] = useState<RoomOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // Filters
  const [filterDate, setFilterDate] = useState(
    new Date().toISOString().split("T")[0]
  );
  const [filterStatus, setFilterStatus] = useState("");
  const [filterService, setFilterService] = useState("");
  const [filterRoom, setFilterRoom] = useState("");

  // Selected patient
  const [selectedPatientId, setSelectedPatientId] = useState<string | null>(null);

  // ─── Right Panel: Daily Summary ────────────────────────────────────────────
  const { data: summary, isLoading: summaryLoading, error: summaryError, refetch: refetchSummary } =
    useDailyJourneySummary(selectedPatientId);

  // ─── Action Mutations ──────────────────────────────────────────────────────
  const intakeMutation = useJourneyIntake();
  const sendToQueueMutation = useJourneySendToQueue();
  const startVisitMutation = useJourneyStartVisit();
  const handoffMutation = useJourneyHandoff();
  const checkoutMutation = useJourneyCheckout();
  const draftInvoiceMutation = useJourneyCreateDraftInvoice();

  // ─── New Action Mutations ──────────────────────────────────────────────────
  const cancelQueueMutation = useJourneyCancelQueue();
  const changeRoomMutation = useJourneyChangeRoom();
  const updateAptStatusMutation = useJourneyUpdateAppointmentStatus();
  const createPaymentMutation = useJourneyCreatePayment();
  const issueInvoiceMutation = useJourneyIssueInvoice();
  const updateVisitMutation = useJourneyUpdateVisit();
  const sendSmsMutation = useJourneySendSms();
  const sendReminderMutation = useJourneySendAppointmentReminder();
  const createPrescriptionMutation = useJourneyCreatePrescription();
  const createAppointmentMutation = useJourneyCreateAppointment();

  // ─── Intake Form State ─────────────────────────────────────────────────────
  const [intakeService, setIntakeService] = useState("");
  const [intakeComplaint, setIntakeComplaint] = useState("");
  const [intakeRoom, setIntakeRoom] = useState("");
  const [intakeConsultFee, setIntakeConsultFee] = useState(false);
  const [intakeConsultAmount, setIntakeConsultAmount] = useState(0);
  const [intakeNotes, setIntakeNotes] = useState("");

  // ─── Handoff Form State ────────────────────────────────────────────────────
  const [handoffForm, setHandoffForm] = useState({
    treatmentDone: "",
    diagnosis: "",
    nextVisitPlan: "",
    instructions: "",
    amountDue: 0,
    notes: "",
  });

  // ─── Checkout Form State ───────────────────────────────────────────────────
  const [checkoutAmount, setCheckoutAmount] = useState(0);
  const [checkoutPayment, setCheckoutPayment] = useState("cash");
  const [checkoutNextDate, setCheckoutNextDate] = useState("");
  const [checkoutNextService, setCheckoutNextService] = useState("");
  const [checkoutNotes, setCheckoutNotes] = useState("");
  const [checkoutSuccess, setCheckoutSuccess] = useState(false);
  const [draftInvoiceResult, setDraftInvoiceResult] = useState<{ invoiceId: string; invoiceNumber: string } | null>(null);

  // ─── Cancel Appointment/NoShow Dialog ──────────────────────────────────────
  const [cancelDialogOpen, setCancelDialogOpen] = useState(false);
  const [cancelDialogType, setCancelDialogType] = useState<"Cancelled" | "NoShow" | "CancelQueue" | "ChangeRoom">("Cancelled");
  const [pendingChangeRoomName, setPendingChangeRoomName] = useState("");

  // ─── Record Payment Modal ──────────────────────────────────────────────────
  const [recordPaymentOpen, setRecordPaymentOpen] = useState(false);
  const [paymentAmount, setPaymentAmount] = useState(0);
  const [paymentMethod, setPaymentMethod] = useState("cash");
  const [paymentServiceDesc, setPaymentServiceDesc] = useState("");
  const [paymentNotes, setPaymentNotes] = useState("");

  // ─── Send SMS Modal ────────────────────────────────────────────────────────
  const [smsModalOpen, setSmsModalOpen] = useState(false);
  const [smsTo, setSmsTo] = useState("");
  const [smsMessage, setSmsMessage] = useState("");

  // ─── Prescription Modal ────────────────────────────────────────────────────
  const [prescriptionModalOpen, setPrescriptionModalOpen] = useState(false);
  const [prescDiagnosis, setPrescDiagnosis] = useState("");
  const [prescNotes, setPrescNotes] = useState("");
  const [prescItems, setPrescItems] = useState<Array<{
    name: string; dose: string; frequency: string; duration: string; notes: string;
  }>>([{ name: "", dose: "", frequency: "", duration: "", notes: "" }]);

  // ─── Edit Visit Modal ──────────────────────────────────────────────────────
  const [editVisitModalOpen, setEditVisitModalOpen] = useState(false);
  const [editVisitForm, setEditVisitForm] = useState({
    chiefComplaint: "", clinicalNotes: "", treatmentDone: "", diagnosis: "", instructions: "", nextVisitPlan: "", cost: 0,
  });

  // ─── Book Appointment Modal ────────────────────────────────────────────────
  const [bookAppointmentModalOpen, setBookAppointmentModalOpen] = useState(false);
  const [bookAptDate, setBookAptDate] = useState("");
  const [bookAptStartTime, setBookAptStartTime] = useState("");
  const [bookAptEndTime, setBookAptEndTime] = useState("");
  const [bookAptDoctorId, setBookAptDoctorId] = useState("");
  const [bookAptServiceId, setBookAptServiceId] = useState("");
  const [bookAptType, setBookAptType] = useState("FollowUp");
  const [bookAptNotes, setBookAptNotes] = useState("");

  // ─── Change Room Inline ────────────────────────────────────────────────────
  const [changeRoomSelectedRoom, setChangeRoomSelectedRoom] = useState("");

  // ─── PDF Download Loading ────────────────────────────────────────────────────
  const [pdfDownloading, setPdfDownloading] = useState(false);

  // ─── Upload Document Modal ──────────────────────────────────────────────────
  const [uploadDocModalOpen, setUploadDocModalOpen] = useState(false);

  // ─── Clinic Name (for WhatsApp) ─────────────────────────────────────────────
  const [clinicName, setClinicName] = useState("العيادة");

  // ─── Undo State ─────────────────────────────────────────────────────────────
  const [undoInfo, setUndoInfo] = useState<{
    type: "Cancelled" | "NoShow" | "CancelQueue";
    previousStatus: string;
    appointmentId?: string;
    queueItemId?: string;
  } | null>(null);
  const undoTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // ─── Data Loading ──────────────────────────────────────────────────────────

  const loadJourney = useCallback(() => {
    setLoading(true);
    setError("");
    const params = new URLSearchParams();
    if (filterDate) params.set("date", filterDate);
    if (filterStatus) params.set("status", filterStatus);
    if (filterService) params.set("serviceId", filterService);
    if (filterRoom) params.set("roomId", filterRoom);
    api
      .get<JourneyItem[]>(`/api/patient-journey/today?${params.toString()}`)
      .then((r) => {
        setItems(r.data);
        // Auto-select first patient if none selected
        if (r.data.length > 0 && !selectedPatientId) {
          setSelectedPatientId(r.data[0].patientId);
        }
      })
      .catch((err) => {
        const msg = err?.response?.data?.message ?? "فشل تحميل البيانات";
        setError(msg);
      })
      .finally(() => setLoading(false));
  }, [filterDate, filterStatus, filterService, filterRoom, selectedPatientId]);

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
    // Load clinic name from settings for WhatsApp
    api
      .get<{ clinicName?: string }>("/api/settings")
      .then((r) => {
        if (r.data.clinicName) setClinicName(r.data.clinicName);
      })
      .catch(() => {});
  }, []);

  // Reset form states when patient changes
  useEffect(() => {
    setCheckoutSuccess(false);
    setDraftInvoiceResult(null);
    setIntakeService("");
    setIntakeComplaint("");
    setIntakeRoom("");
    setIntakeConsultFee(false);
    setIntakeConsultAmount(0);
    setIntakeNotes("");
    setHandoffForm({ treatmentDone: "", diagnosis: "", nextVisitPlan: "", instructions: "", amountDue: 0, notes: "" });
    setCheckoutAmount(0);
    setCheckoutPayment("cash");
    setCheckoutNextDate("");
    setCheckoutNextService("");
    setCheckoutNotes("");
    // Reset new modal states
    setCancelDialogOpen(false);
    setPendingChangeRoomName("");
    setPdfDownloading(false);
    setRecordPaymentOpen(false);
    setPaymentAmount(0);
    setPaymentMethod("cash");
    setPaymentServiceDesc("");
    setPaymentNotes("");
    setSmsModalOpen(false);
    setSmsTo("");
    setSmsMessage("");
    setPrescriptionModalOpen(false);
    setPrescDiagnosis("");
    setPrescNotes("");
    setPrescItems([{ name: "", dose: "", frequency: "", duration: "", notes: "" }]);
    setEditVisitModalOpen(false);
    setEditVisitForm({ chiefComplaint: "", clinicalNotes: "", treatmentDone: "", diagnosis: "", instructions: "", nextVisitPlan: "", cost: 0 });
    setBookAppointmentModalOpen(false);
    setBookAptDate("");
    setBookAptStartTime("");
    setBookAptEndTime("");
    setBookAptDoctorId("");
    setBookAptServiceId("");
    setBookAptType("FollowUp");
    setBookAptNotes("");
    setChangeRoomSelectedRoom("");
  }, [selectedPatientId]);

  // ─── Auto-fill Checkout Amount ──────────────────────────────────────────────
  useEffect(() => {
    if (summary?.todayVisit?.amountDueReference && checkoutAmount === 0) {
      setCheckoutAmount(summary.todayVisit.amountDueReference);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [summary?.todayVisit?.amountDueReference]);

  // ─── Summary Cards ─────────────────────────────────────────────────────────

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

  // ─── Selected JourneyItem ──────────────────────────────────────────────────

  const selectedItem = useMemo(
    () => items.find((i) => i.patientId === selectedPatientId) ?? null,
    [items, selectedPatientId]
  );

  // ─── Queue Estimated Wait ──────────────────────────────────────────────────
  const queueItemId = selectedItem?.queueItemId || summary?.queueStatus?.id || null;
  const { data: estimatedWait } = useQueueEstimatedWait(queueItemId);

  // ─── Action Handlers ──────────────────────────────────────────────────────

  const handleSimpleAction = useCallback(async (item: JourneyItem) => {
    try {
      if (item.nextAction === "SendToQueue") {
        await sendToQueueMutation.mutateAsync({ appointmentId: item.appointmentId });
        toast.success("تم إضافة المريض للانتظار");
      } else if (item.nextAction === "CallPatient") {
        await api.post(`/api/clinic-queue/${item.queueItemId}/call`, {});
        toast.success("تم نداء المريض");
      } else if (item.nextAction === "EnterRoom") {
        await api.post(`/api/clinic-queue/${item.queueItemId}/enter-room`);
        toast.success("تم تسجيل دخول الغرفة");
      } else if (item.nextAction === "StartVisit") {
        await startVisitMutation.mutateAsync(item.appointmentId);
        toast.success("تم بدء الزيارة");
      }
      loadJourney();
      refetchSummary();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [sendToQueueMutation, startVisitMutation, loadJourney, refetchSummary]);

  const handleIntakeSubmit = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedItem) return;
    try {
      await intakeMutation.mutateAsync({
        appointmentId: selectedItem.appointmentId,
        body: {
          serviceId: intakeService || undefined,
          chiefComplaint: intakeComplaint || undefined,
          roomId: intakeRoom || undefined,
          requiresConsultationFee: intakeConsultFee || undefined,
          consultationFeeAmount: intakeConsultFee ? intakeConsultAmount : undefined,
          notes: intakeNotes || undefined,
        },
      });
      // Also send to queue
      await sendToQueueMutation.mutateAsync({
        appointmentId: selectedItem.appointmentId,
        body: { roomId: intakeRoom || undefined },
      });
      toast.success("تم تسجيل الوصول وإدخال الانتظار");
      loadJourney();
      refetchSummary();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [selectedItem, intakeService, intakeComplaint, intakeRoom, intakeConsultFee, intakeConsultAmount, intakeNotes, intakeMutation, sendToQueueMutation, loadJourney, refetchSummary]);

  const handleHandoffSubmit = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!summary?.todayVisit?.id) return;
    try {
      await handoffMutation.mutateAsync({
        visitId: summary.todayVisit.id,
        body: {
          treatmentDone: handoffForm.treatmentDone || undefined,
          diagnosis: handoffForm.diagnosis || undefined,
          nextVisitPlan: handoffForm.nextVisitPlan || undefined,
          instructions: handoffForm.instructions || undefined,
          amountDue: handoffForm.amountDue || undefined,
          notes: handoffForm.notes || undefined,
        },
      });
      toast.success("تم تسليم المريض للاستقبال");
      loadJourney();
      refetchSummary();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [summary?.todayVisit?.id, handoffForm, handoffMutation, loadJourney, refetchSummary]);

  const handleCheckoutSubmit = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedItem) return;
    try {
      // Checkout only marks the appointment as completed.
      // Payment creation is handled separately via the payment flow,
      // not auto-created during checkout.
      await checkoutMutation.mutateAsync({
        appointmentId: selectedItem.appointmentId,
        body: {
          nextAppointmentDate: checkoutNextDate || undefined,
          nextServiceId: checkoutNextService || undefined,
          notes: checkoutNotes || undefined,
        },
      });
      setCheckoutSuccess(true);
      toast.success("تم إنهاء الحساب بنجاح");
      loadJourney();
      refetchSummary();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [selectedItem, checkoutNextDate, checkoutNextService, checkoutNotes, checkoutMutation, loadJourney, refetchSummary]);

  const handleCreateDraftInvoice = useCallback(async () => {
    if (!summary?.todayVisit?.id) {
      toast.error("لا يوجد زيارة مرتبطة لإنشاء فاتورة");
      return;
    }
    try {
      const res = await draftInvoiceMutation.mutateAsync(summary.todayVisit.id);
      setDraftInvoiceResult({ invoiceId: res.id, invoiceNumber: res.invoiceNumber });
      toast.success(`تم إنشاء الفاتورة ${res.invoiceNumber}`);
    } catch {
      toast.error("فشل إنشاء الفاتورة المسودة");
    }
  }, [summary?.todayVisit?.id, draftInvoiceMutation]);

  // ─── Confirm Action (Cancel/NoShow/CancelQueue/ChangeRoom) ────────────────
  const handleConfirmAction = useCallback(async () => {
    try {
      if (cancelDialogType === "Cancelled" || cancelDialogType === "NoShow") {
        if (!summary?.todayAppointment?.id) return;
        const previousStatus = summary.todayAppointment.status;
        await updateAptStatusMutation.mutateAsync({
          appointmentId: summary.todayAppointment.id,
          status: cancelDialogType,
        });
        toast.success(cancelDialogType === "Cancelled" ? "تم إلغاء الموعد" : "تم تسجيل عدم الحضور");
        // Enable undo for 5 seconds
        setUndoInfo({ type: cancelDialogType, previousStatus, appointmentId: summary.todayAppointment.id });
        if (undoTimerRef.current) clearTimeout(undoTimerRef.current);
        undoTimerRef.current = setTimeout(() => setUndoInfo(null), 5000);
      } else if (cancelDialogType === "CancelQueue") {
        if (!queueItemId) return;
        await cancelQueueMutation.mutateAsync(queueItemId);
        toast.success("تم إلغاء المريض من الانتظار");
        // Enable undo for 5 seconds
        setUndoInfo({ type: "CancelQueue", previousStatus: "Waiting", queueItemId: queueItemId ?? undefined });
        if (undoTimerRef.current) clearTimeout(undoTimerRef.current);
        undoTimerRef.current = setTimeout(() => setUndoInfo(null), 5000);
      } else if (cancelDialogType === "ChangeRoom") {
        if (!queueItemId || !pendingChangeRoomName) return;
        await changeRoomMutation.mutateAsync({ queueItemId, roomName: pendingChangeRoomName });
        toast.success("تم تغيير الغرفة");
        setChangeRoomSelectedRoom("");
      }
      setCancelDialogOpen(false);
      loadJourney();
      refetchSummary();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [cancelDialogType, summary?.todayAppointment?.id, summary?.todayAppointment?.status, queueItemId, pendingChangeRoomName, updateAptStatusMutation, cancelQueueMutation, changeRoomMutation, loadJourney, refetchSummary]);

  // ─── Undo Action ──────────────────────────────────────────────────────────
  const handleUndo = useCallback(async () => {
    if (!undoInfo) return;
    try {
      if (undoInfo.type === "Cancelled" || undoInfo.type === "NoShow") {
        if (!undoInfo.appointmentId) return;
        await updateAptStatusMutation.mutateAsync({
          appointmentId: undoInfo.appointmentId,
          status: undoInfo.previousStatus as "Scheduled" | "Confirmed" | "Arrived",
        });
        toast.success("تم التراجع عن الإلغاء");
      } else if (undoInfo.type === "CancelQueue") {
        // Re-add to queue via send-to-queue
        if (selectedItem?.appointmentId) {
          await sendToQueueMutation.mutateAsync({ appointmentId: selectedItem.appointmentId });
          toast.success("تم إعادة المريض للانتظار");
        }
      }
      setUndoInfo(null);
      if (undoTimerRef.current) clearTimeout(undoTimerRef.current);
      loadJourney();
      refetchSummary();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "فشل التراجع";
      toast.error(msg);
    }
  }, [undoInfo, updateAptStatusMutation, sendToQueueMutation, selectedItem?.appointmentId, loadJourney, refetchSummary]);

  // ─── Request Cancel from Queue (shows confirmation) ────────────────────────
  const handleRequestCancelQueue = useCallback(() => {
    setCancelDialogType("CancelQueue");
    setCancelDialogOpen(true);
  }, []);

  // ─── Request Change Room (shows confirmation) ───────────────────────────────
  const handleRequestChangeRoom = useCallback((roomId: string) => {
    const targetRoom = rooms.find((r) => r.id === roomId);
    setChangeRoomSelectedRoom(roomId);
    setCancelDialogType("ChangeRoom");
    setCancelDialogOpen(true);
    // Store target room name for dialog display
    setPendingChangeRoomName(targetRoom?.arabicName ?? "");
  }, [rooms]);

  // ─── Record Payment ────────────────────────────────────────────────────────
  const handleRecordPayment = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedPatientId || paymentAmount <= 0) {
      toast.error("يرجى إدخال مبلغ صحيح");
      return;
    }
    try {
      await createPaymentMutation.mutateAsync({
        patientId: selectedPatientId,
        amount: paymentAmount,
        paymentMethod: paymentMethod || undefined,
        serviceDescription: paymentServiceDesc || undefined,
        notes: paymentNotes || undefined,
        doctorId: selectedItem?.doctorId || undefined,
      });
      toast.success("تم تسجيل الدفعة بنجاح");
      setRecordPaymentOpen(false);
      setPaymentAmount(0);
      setPaymentServiceDesc("");
      setPaymentNotes("");
      loadJourney();
      refetchSummary();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [selectedPatientId, paymentAmount, paymentMethod, paymentServiceDesc, paymentNotes, selectedItem?.doctorId, createPaymentMutation, loadJourney, refetchSummary]);

  // ─── Issue Invoice ─────────────────────────────────────────────────────────
  const handleIssueInvoice = useCallback(async () => {
    if (!draftInvoiceResult?.invoiceId) return;
    try {
      await issueInvoiceMutation.mutateAsync(draftInvoiceResult.invoiceId);
      toast.success("تم إصدار الفاتورة");
      refetchSummary();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [draftInvoiceResult?.invoiceId, issueInvoiceMutation, refetchSummary]);

  // ─── Download PDF ──────────────────────────────────────────────────────────
  const handleDownloadPdf = useCallback(async (url: string, filename: string) => {
    setPdfDownloading(true);
    try {
      const res = await api.get(url, { responseType: "blob" });
      const blob = new Blob([res.data as BlobPart], { type: "application/pdf" });
      const linkUrl = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = linkUrl;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(linkUrl);
      toast.success("تم تحميل الملف بنجاح");
    } catch {
      toast.error("فشل تحميل الملف");
    } finally {
      setPdfDownloading(false);
    }
  }, []);

  // ─── Send SMS ──────────────────────────────────────────────────────────────
  const handleSendSms = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!smsMessage) {
      toast.error("يرجى إدخال الرسالة");
      return;
    }
    try {
      if (!selectedPatientId) {
        toast.error("يرجى اختيار مريض أولاً");
        return;
      }
      await sendSmsMutation.mutateAsync({
        patientId: selectedPatientId,
        message: smsMessage,
      });
      toast.success("تم إرسال الرسالة");
      setSmsModalOpen(false);
      setSmsMessage("");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [smsMessage, selectedPatientId, sendSmsMutation]);

  // ─── Send Appointment Reminder ─────────────────────────────────────────────
  const handleSendReminder = useCallback(async () => {
    if (!summary?.todayAppointment?.id) return;
    try {
      await sendReminderMutation.mutateAsync(summary.todayAppointment.id);
      toast.success("تم إرسال التذكير");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [summary?.todayAppointment?.id, sendReminderMutation]);

  // ─── WhatsApp Reminder ─────────────────────────────────────────────────────
  const handleOpenWhatsApp = useCallback(() => {
    const phone = summary?.patient.phone;
    if (!phone) {
      toast.error("لا يوجد رقم هاتف للمريض");
      return;
    }
    // Ensure country code: if starts with 0, replace with 967 (Yemen)
    let cleanPhone = phone.replace(/[^0-9+]/g, "");
    if (cleanPhone.startsWith("0")) {
      cleanPhone = "967" + cleanPhone.substring(1);
    } else if (!cleanPhone.startsWith("+") && !cleanPhone.startsWith("967") && cleanPhone.length <= 9) {
      cleanPhone = "967" + cleanPhone;
    }
    const aptTime = summary?.todayAppointment?.startTime
      ? fmtTime(summary.todayAppointment.startTime)
      : "";
    const aptDate = summary?.todayAppointment?.appointmentDate
      ? fmtDate(summary.todayAppointment.appointmentDate)
      : "";
    const doctorName = summary?.todayAppointment?.doctorName ?? "الطبيب";
    const msg = `تذكير من ${clinicName}: لديكم موعد يوم ${aptDate} الساعة ${aptTime} مع ${doctorName}`;
    const encoded = encodeURIComponent(msg);
    window.open(`https://wa.me/${cleanPhone}?text=${encoded}`, "_blank");
  }, [summary?.patient.phone, summary?.todayAppointment?.startTime, summary?.todayAppointment?.appointmentDate, summary?.todayAppointment?.doctorName, clinicName]);

  // ─── Create Prescription ───────────────────────────────────────────────────
  const handleCreatePrescription = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedPatientId) return;
    const validItems = prescItems.filter((item) => item.name.trim());
    if (validItems.length === 0) {
      toast.error("يرجى إضافة دواء واحد على الأقل");
      return;
    }
    try {
      await createPrescriptionMutation.mutateAsync({
        patientId: selectedPatientId,
        doctorId: selectedItem?.doctorId || undefined,
        diagnosis: prescDiagnosis || undefined,
        notes: prescNotes || undefined,
        drugs: validItems.map((item) => ({
          name: item.name,
          dose: item.dose || undefined,
          frequency: item.frequency || undefined,
          duration: item.duration || undefined,
          notes: item.notes || undefined,
        })),
      });
      toast.success("تم إنشاء الوصفة الطبية");
      setPrescriptionModalOpen(false);
      setPrescDiagnosis("");
      setPrescNotes("");
      setPrescItems([{ name: "", dose: "", frequency: "", duration: "", notes: "" }]);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [selectedPatientId, prescDiagnosis, prescNotes, prescItems, selectedItem?.doctorId, createPrescriptionMutation]);

  // ─── Update Visit ──────────────────────────────────────────────────────────
  const handleUpdateVisit = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!summary?.todayVisit?.id) return;
    try {
      await updateVisitMutation.mutateAsync({
        visitId: summary.todayVisit.id,
        body: {
          chiefComplaint: editVisitForm.chiefComplaint || undefined,
          clinicalNotes: editVisitForm.clinicalNotes || undefined,
          treatmentDone: editVisitForm.treatmentDone || undefined,
          diagnosis: editVisitForm.diagnosis || undefined,
          instructions: editVisitForm.instructions || undefined,
          nextVisitPlan: editVisitForm.nextVisitPlan || undefined,
          cost: editVisitForm.cost || undefined,
        },
      });
      toast.success("تم تحديث ملاحظات الزيارة");
      setEditVisitModalOpen(false);
      refetchSummary();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [summary?.todayVisit?.id, editVisitForm, updateVisitMutation, refetchSummary]);

  // ─── Book Next Appointment ─────────────────────────────────────────────────
  const handleBookAppointment = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedPatientId || !bookAptDoctorId || !bookAptDate || !bookAptStartTime) {
      toast.error("يرجى ملء جميع الحقول المطلوبة");
      return;
    }
    try {
      await createAppointmentMutation.mutateAsync({
        patientId: selectedPatientId,
        doctorId: bookAptDoctorId,
        appointmentDate: bookAptDate,
        startTime: bookAptStartTime,
        endTime: bookAptEndTime || undefined,
        serviceId: bookAptServiceId || undefined,
        appointmentType: bookAptType || undefined,
        notes: bookAptNotes || undefined,
      });
      toast.success("تم حجز الموعد بنجاح");
      setBookAppointmentModalOpen(false);
      setBookAptDate("");
      setBookAptStartTime("");
      setBookAptEndTime("");
      setBookAptServiceId("");
      setBookAptNotes("");
      loadJourney();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "حدث خطأ";
      toast.error(msg);
    }
  }, [selectedPatientId, bookAptDoctorId, bookAptDate, bookAptStartTime, bookAptEndTime, bookAptServiceId, bookAptType, bookAptNotes, createAppointmentMutation, loadJourney]);

  // ─── Keyboard Shortcuts ────────────────────────────────────────────────────
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Don't trigger in inputs/textareas
      const tag = (e.target as HTMLElement)?.tagName;
      if (tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT") return;

      const ctrl = e.ctrlKey || e.metaKey;
      if (ctrl && e.key === "k" && selectedItem?.nextAction === "CallPatient") {
        e.preventDefault();
        handleSimpleAction(selectedItem);
      } else if (ctrl && e.key === "e" && selectedItem?.nextAction === "EnterRoom") {
        e.preventDefault();
        handleSimpleAction(selectedItem);
      } else if (ctrl && e.key === "r") {
        e.preventDefault();
        loadJourney();
        refetchSummary();
      } else if (ctrl && e.key === "z" && undoInfo) {
        e.preventDefault();
        handleUndo();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedItem, undoInfo]);

  // ─── Render ───────────────────────────────────────────────────────────────

  return (
    <div className="flex flex-col lg:flex-row gap-4 h-[calc(100vh-5rem)]">
      {/* ═══ LEFT PANEL (~40%) ═══ */}
      <div className="w-full lg:w-[40%] flex flex-col gap-3 overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between flex-shrink-0">
          <div>
            <h1 className="text-xl font-extrabold text-[#1a3a5c]">مركز القيادة</h1>
            <p className="text-xs text-gray-500 mt-0.5">
              رحلة المرضى — من الوصول حتى إنهاء الحساب
            </p>
          </div>
          <button
            onClick={() => { loadJourney(); refetchSummary(); }}
            className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
          >
            <RefreshCw className="w-3.5 h-3.5" />
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
        <div className="grid grid-cols-3 md:grid-cols-6 gap-2 flex-shrink-0">
          {summaryCards.map((card) => (
            <div
              key={card.label}
              className="rounded-xl border border-gray-200 bg-white p-2.5 flex items-center gap-2"
            >
              <div
                className={cn(
                  "w-8 h-8 rounded-lg flex items-center justify-center flex-shrink-0",
                  card.color
                )}
              >
                <card.icon className="w-4 h-4 text-white" />
              </div>
              <div>
                <p className="text-lg font-extrabold text-gray-900 leading-tight">{card.count}</p>
                <p className="text-[9px] text-gray-500 whitespace-nowrap leading-tight">{card.label}</p>
              </div>
            </div>
          ))}
        </div>

        {/* Filters */}
        <div className="flex flex-wrap items-end gap-2 bg-gray-50 rounded-xl border border-gray-200 p-3 flex-shrink-0">
          <div className="flex items-center gap-1 text-xs font-semibold text-gray-700">
            <Filter className="w-3.5 h-3.5" />
            تصفية
          </div>
          <div>
            <label className="block text-[10px] font-medium text-gray-600 mb-0.5">التاريخ</label>
            <input
              type="date"
              value={filterDate}
              onChange={(e) => setFilterDate(e.target.value)}
              className="w-full px-2 py-1 text-xs rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-1 focus:ring-[#3d7ab5]"
              dir="ltr"
            />
          </div>
          <div>
            <label className="block text-[10px] font-medium text-gray-600 mb-0.5">الحالة</label>
            <select
              value={filterStatus}
              onChange={(e) => setFilterStatus(e.target.value)}
              className="w-full px-2 py-1 text-xs rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-1 focus:ring-[#3d7ab5]"
            >
              <option value="">الكل</option>
              {Object.entries(STATUS_LABELS).map(([k, v]) => (
                <option key={k} value={k}>{v}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-[10px] font-medium text-gray-600 mb-0.5">الخدمة</label>
            <select
              value={filterService}
              onChange={(e) => setFilterService(e.target.value)}
              className="w-full px-2 py-1 text-xs rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-1 focus:ring-[#3d7ab5]"
            >
              <option value="">الكل</option>
              {services.map((s) => (
                <option key={s.id} value={s.id}>{s.arabicName}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-[10px] font-medium text-gray-600 mb-0.5">الغرفة</label>
            <select
              value={filterRoom}
              onChange={(e) => setFilterRoom(e.target.value)}
              className="w-full px-2 py-1 text-xs rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-1 focus:ring-[#3d7ab5]"
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
          <div className="text-xs text-red-600 bg-red-50 px-3 py-2 rounded-lg flex-shrink-0">{error}</div>
        )}

        {/* Loading */}
        {loading && (
          <div className="animate-pulse space-y-2 flex-1">
            {Array.from({ length: 6 }).map((_, i) => (
              <div key={i} className="h-16 bg-gray-100 rounded-lg" />
            ))}
          </div>
        )}

        {/* Patient Card List */}
        {!loading && items.length > 0 && (
          <div className="flex-1 overflow-y-auto space-y-1.5 min-h-0 pr-1">
            {items.map((item) => {
              const isSelected = item.patientId === selectedPatientId;
              return (
                <button
                  key={item.appointmentId}
                  onClick={() => setSelectedPatientId(item.patientId)}
                  className={cn(
                    "w-full text-start rounded-xl border p-3 transition-all",
                    isSelected
                      ? "border-[#3d7ab5] bg-[#f0f6fc] ring-1 ring-[#3d7ab5]/20"
                      : "border-gray-200 bg-white hover:bg-gray-50 hover:border-gray-300"
                  )}
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-1.5">
                        <span className={cn(
                          "text-sm font-bold truncate",
                          isSelected ? "text-[#1a3a5c]" : "text-gray-900"
                        )}>
                          {item.patientName}
                        </span>
                        <Link
                          href={`/patient-journey/${item.patientId}`}
                          onClick={(e) => e.stopPropagation()}
                          className="text-[#3d7ab5] hover:text-[#2d5e8e] flex-shrink-0"
                          title="عرض صفحة المريض الكاملة"
                        >
                          <ExternalLink className="w-3 h-3" />
                        </Link>
                      </div>
                      <div className="flex items-center gap-2 mt-0.5">
                        <span className="text-[11px] text-gray-500 font-mono" dir="ltr">
                          {item.appointmentTime}
                        </span>
                        <span className="text-gray-300">|</span>
                        <span className="text-[11px] text-gray-600">{item.doctorName}</span>
                      </div>
                    </div>
                    <div className="flex flex-col items-end gap-1 flex-shrink-0">
                      <span
                        className={cn(
                          "text-[10px] px-2 py-0.5 rounded-full font-medium",
                          STATUS_COLORS[item.appointmentStatus] ?? "bg-gray-50 text-gray-700"
                        )}
                      >
                        {STATUS_LABELS[item.appointmentStatus] ?? item.appointmentStatus}
                      </span>
                      {item.nextAction && item.nextAction !== "None" && item.nextAction !== "InProgress" && (
                        <span
                          className={cn(
                            "text-[9px] px-1.5 py-0.5 rounded-full font-medium text-white",
                            ACTION_COLORS[item.nextAction] ?? "bg-gray-300"
                          )}
                        >
                          {ACTION_LABELS[item.nextAction] ?? item.nextAction}
                        </span>
                      )}
                      {item.nextAction === "InProgress" && (
                        <span className="text-[9px] px-1.5 py-0.5 rounded-full font-medium bg-emerald-100 text-emerald-700 flex items-center gap-0.5">
                          <Stethoscope className="w-2.5 h-2.5" />
                          عند الطبيب
                        </span>
                      )}
                    </div>
                  </div>
                </button>
              );
            })}
          </div>
        )}

        {/* Empty State for List */}
        {!loading && items.length === 0 && (
          <div className="text-center py-12 flex-1 flex flex-col items-center justify-center">
            <Users className="w-10 h-10 text-gray-300 mx-auto mb-2" />
            <p className="text-gray-500 font-medium text-sm">لا توجد رحلات مرضى لهذا اليوم</p>
            <p className="text-xs text-gray-400 mt-1">اختر تاريخًا آخر أو أضف مواعيد</p>
          </div>
        )}
      </div>

      {/* ═══ RIGHT PANEL (~60%) ═══ */}
      <div className="w-full lg:w-[60%] flex flex-col bg-white rounded-2xl border border-gray-200 overflow-hidden">
        {/* No patient selected or list empty */}
        {!selectedPatientId && !loading && (
          <div className="flex-1 flex flex-col items-center justify-center text-center p-8">
            <Route className="w-14 h-14 text-gray-200 mb-3" />
            <p className="text-gray-500 font-bold">اختر مريضًا من القائمة</p>
            <p className="text-xs text-gray-400 mt-1">سيتم عرض تفاصيل رحلة المريض هنا</p>
          </div>
        )}

        {/* Loading Summary */}
        {selectedPatientId && summaryLoading && (
          <div className="flex-1 flex flex-col items-center justify-center p-8">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-[#3d7ab5] mb-3" />
            <p className="text-sm text-gray-500">جارٍ تحميل بيانات المريض...</p>
          </div>
        )}

        {/* Error loading summary */}
        {selectedPatientId && summaryError && !summaryLoading && (
          <div className="flex-1 flex flex-col items-center justify-center p-8">
            <AlertTriangle className="w-10 h-10 text-[#ba7517] mb-3" />
            <p className="text-sm font-bold text-[#1a3a5c]">فشل تحميل البيانات</p>
            <p className="text-xs text-gray-500 mt-1">
              {(summaryError as Error)?.message ?? "ليس لديك صلاحية أو حدث خطأ"}
            </p>
            <button
              onClick={() => refetchSummary()}
              className="mt-3 px-3 py-1.5 text-xs font-semibold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition"
            >
              إعادة المحاولة
            </button>
          </div>
        )}

        {/* ═══ Patient Detail Content ═══ */}
        {selectedPatientId && summary && !summaryLoading && (
          <div className="flex-1 overflow-y-auto p-4 space-y-4">
            {/* ─── 1. Patient Header Card ─── */}
            <PatientHeaderCard summary={summary} isDoctor={isDoctor} />

            {/* ─── 2. Today's Appointment ─── */}
            {summary.todayAppointment && (
              <div>
                <TodaysAppointmentCard summary={summary} />
                {/* Phase 1a: Cancel / NoShow Buttons */}
                {canEditJourney && ["Scheduled", "Confirmed"].includes(summary.todayAppointment.status) && (
                  <div className="flex gap-2 mt-2">
                    <button
                      onClick={() => { setCancelDialogType("Cancelled"); setCancelDialogOpen(true); }}
                      className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-red-50 text-red-700 border border-red-200 hover:bg-red-100 transition"
                    >
                      <Ban className="w-3 h-3" />
                      إلغاء الموعد
                    </button>
                    <button
                      onClick={() => { setCancelDialogType("NoShow"); setCancelDialogOpen(true); }}
                      className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-orange-50 text-orange-700 border border-orange-200 hover:bg-orange-100 transition"
                    >
                      <UserX className="w-3 h-3" />
                      لم يحضر
                    </button>
                  </div>
                )}
              </div>
            )}

            {/* ─── 3. Queue Status (Enhanced) ─── */}
            {summary.queueStatus && (
              <div>
                <QueueStatusCard summary={summary} />
                {/* Phase 1b: Cancel from Queue */}
                {canEditJourney && ["Waiting", "Called"].includes(summary.queueStatus.status) && (
                  <div className="flex flex-wrap items-center gap-2 mt-2">
                    <button
                      onClick={handleRequestCancelQueue}
                      className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-red-50 text-red-700 border border-red-200 hover:bg-red-100 transition"
                    >
                      <Ban className="w-3 h-3" />
                      إلغاء من الانتظار
                    </button>
                    {/* Phase 1c: Change Room */}
                    <div className="flex items-center gap-1.5">
                      <ArrowRightLeft className="w-3 h-3 text-gray-500" />
                      <select
                        value={changeRoomSelectedRoom}
                        onChange={(e) => {
                          if (e.target.value) handleRequestChangeRoom(e.target.value);
                        }}
                        disabled={changeRoomMutation.isPending}
                        className="px-2 py-1.5 text-[11px] rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-1 focus:ring-[#3d7ab5] disabled:opacity-50"
                      >
                        <option value="">تغيير الغرفة</option>
                        {rooms.map((r) => (
                          <option key={r.id} value={r.id}>{r.arabicName}</option>
                        ))}
                      </select>
                    </div>
                  </div>
                )}
                {/* Phase 1d: Estimated Wait Time */}
                {summary.queueStatus.status === "Waiting" && estimatedWait && (
                  <div className="mt-2 flex items-center gap-2 text-[11px] text-[#1a3a5c] bg-blue-50 border border-blue-100 rounded-lg px-3 py-1.5">
                    <Timer className="w-3.5 h-3.5 text-[#3d7ab5]" />
                    <span>الوقت المتوقع: <strong>{estimatedWait.estimatedMinutes} دقيقة</strong></span>
                    <span className="text-gray-300">|</span>
                    <span>الترتيب: <strong>{estimatedWait.position}</strong></span>
                  </div>
                )}
              </div>
            )}

            {/* ─── 4. Medical Safety Alerts ─── */}
            {summary.medicalAlerts && summary.medicalAlerts.length > 0 && !isAccountant && (
              <MedicalAlertsCard alerts={summary.medicalAlerts} />
            )}

            {/* ─── 5. Journey Actions Panel ─── */}
            {canEditJourney && summary.nextAction && summary.nextAction !== "None" && selectedItem && (
              <JourneyActionsPanel
                summary={summary}
                selectedItem={selectedItem}
                isDoctor={isDoctor}
                services={services}
                rooms={rooms}
                intakeService={intakeService}
                setIntakeService={setIntakeService}
                intakeComplaint={intakeComplaint}
                setIntakeComplaint={setIntakeComplaint}
                intakeRoom={intakeRoom}
                setIntakeRoom={setIntakeRoom}
                intakeConsultFee={intakeConsultFee}
                setIntakeConsultFee={setIntakeConsultFee}
                intakeConsultAmount={intakeConsultAmount}
                setIntakeConsultAmount={setIntakeConsultAmount}
                intakeNotes={intakeNotes}
                setIntakeNotes={setIntakeNotes}
                handoffForm={handoffForm}
                setHandoffForm={setHandoffForm}
                checkoutAmount={checkoutAmount}
                setCheckoutAmount={setCheckoutAmount}
                checkoutPayment={checkoutPayment}
                setCheckoutPayment={setCheckoutPayment}
                checkoutNextDate={checkoutNextDate}
                setCheckoutNextDate={setCheckoutNextDate}
                checkoutNextService={checkoutNextService}
                setCheckoutNextService={setCheckoutNextService}
                checkoutNotes={checkoutNotes}
                setCheckoutNotes={setCheckoutNotes}
                checkoutSuccess={checkoutSuccess}
                draftInvoiceResult={draftInvoiceResult}
                intakeMutation={intakeMutation}
                sendToQueueMutation={sendToQueueMutation}
                startVisitMutation={startVisitMutation}
                handoffMutation={handoffMutation}
                checkoutMutation={checkoutMutation}
                draftInvoiceMutation={draftInvoiceMutation}
                onSimpleAction={handleSimpleAction}
                onIntakeSubmit={handleIntakeSubmit}
                onHandoffSubmit={handleHandoffSubmit}
                onCheckoutSubmit={handleCheckoutSubmit}
                onCreateDraftInvoice={handleCreateDraftInvoice}
                onIssueInvoice={handleIssueInvoice}
                issueInvoiceMutation={issueInvoiceMutation}
                onDownloadPdf={handleDownloadPdf}
              />
            )}

            {/* ─── 6. Today's Visit ─── */}
            {summary.todayVisit && canViewVisits && (
              <div>
                <TodaysVisitCard summary={summary} isReception={isReception} isAccountant={isAccountant} />
                {/* Phase 4b: Edit Visit Notes - Doctors only */}
                {canEditJourney && isDoctor && summary.todayVisit.id && (
                  <button
                    onClick={() => {
                      setEditVisitForm({
                        chiefComplaint: summary.todayVisit?.chiefComplaint ?? "",
                        clinicalNotes: summary.todayVisit?.clinicalNotes ?? "",
                        treatmentDone: summary.todayVisit?.treatmentDone ?? "",
                        diagnosis: summary.todayVisit?.diagnosis ?? "",
                        instructions: summary.todayVisit?.instructions ?? "",
                        nextVisitPlan: summary.todayVisit?.nextVisitPlan ?? "",
                        cost: summary.todayVisit?.cost ?? 0,
                      });
                      setEditVisitModalOpen(true);
                    }}
                    className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-[#3d7ab5]/10 text-[#3d7ab5] border border-[#3d7ab5]/20 hover:bg-[#3d7ab5]/20 transition mt-2"
                  >
                    <Stethoscope className="w-3 h-3" />
                    تعديل ملاحظات الزيارة
                  </button>
                )}
              </div>
            )}

            {/* ─── 7. Finance Summary ─── */}
            {(canViewFinance || canViewCheckout) && !isDoctor && summary.financeSummary && (
              <div>
                <FinanceSummaryCard summary={summary} isAccountant={isAccountant} canViewFinance={canViewFinance} canViewInvoices={canViewInvoices} />
                {/* Phase 2a: Record Payment Button */}
                {canEditJourney && (canViewFinance || canViewCheckout) && (
                  <button
                    onClick={() => {
                      setPaymentAmount(summary.financeSummary?.outstandingBalance ?? 0);
                      setRecordPaymentOpen(true);
                    }}
                    className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-green-50 text-green-700 border border-green-200 hover:bg-green-100 transition mt-2"
                  >
                    <CreditCard className="w-3 h-3" />
                    تسجيل دفعة
                  </button>
                )}
                {/* Phase 2c: Download Payment Receipt PDF */}
                {summary.financeSummary?.latestPayment && canViewFinance && (
                  <button
                    onClick={() => handleDownloadPdf(`/api/payments/${summary.financeSummary!.latestPayment!.id}/pdf`, `receipt-${summary.financeSummary!.latestPayment!.receiptNumber ?? summary.financeSummary!.latestPayment!.id}.pdf`)}
                    disabled={pdfDownloading}
                    className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-gray-50 text-gray-600 border border-gray-200 hover:bg-gray-100 transition mt-1 mr-2 disabled:opacity-50"
                  >
                    {pdfDownloading ? <RefreshCw className="w-3 h-3 animate-spin" /> : <Download className="w-3 h-3" />}
                    {pdfDownloading ? "جارٍ التحميل..." : "تحميل إيصال الدفعة"}
                  </button>
                )}
              </div>
            )}

            {/* ─── 8. Active Ortho Case ─── */}
            {summary.activeOrthoCase && canViewVisits && !isAccountant && (
              <ActiveOrthoCard summary={summary} />
            )}

            {/* ─── 9. Recent Visits ─── */}
            {summary.recentVisits && summary.recentVisits.length > 0 && canViewVisits && !isAccountant && (
              <RecentVisitsCard visits={summary.recentVisits.slice(0, 3)} isReception={isReception} isAccountant={isAccountant} />
            )}

            {/* ─── 10. Timeline ─── */}
            {summary.timeline && summary.timeline.length > 0 && canViewJourney && (
              <TimelineCard events={summary.timeline.slice(0, 5)} />
            )}

            {/* ─── 11. Quick Actions Bar ─── */}
            {canEditJourney && (
              <div className="rounded-xl border border-[#3d7ab5]/20 bg-[#f0f6fc] p-3">
                <div className="flex items-center gap-2 mb-2">
                  <CircleDot className="w-4 h-4 text-[#f5922e]" />
                  <span className="text-sm font-bold text-[#1a3a5c]">إجراءات سريعة</span>
                </div>
                <div className="flex flex-wrap gap-2">
                  {/* SMS */}
                  <button
                    onClick={() => {
                      setSmsTo(summary.patient.phone ?? "");
                      setSmsModalOpen(true);
                    }}
                    className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-white text-[#3d7ab5] border border-[#3d7ab5]/20 hover:bg-[#3d7ab5]/10 transition"
                  >
                    <MessageSquare className="w-3 h-3" />
                    إرسال رسالة
                  </button>
                  {/* Appointment Reminder */}
                  {summary.todayAppointment && (
                    <button
                      onClick={handleSendReminder}
                      disabled={sendReminderMutation.isPending}
                      className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-white text-amber-700 border border-amber-200 hover:bg-amber-50 transition disabled:opacity-50"
                    >
                      <Megaphone className="w-3 h-3" />
                      {sendReminderMutation.isPending ? "جارٍ..." : "تذكير بالموعد"}
                    </button>
                  )}
                  {/* WhatsApp */}
                  <button
                    onClick={handleOpenWhatsApp}
                    className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-white text-green-700 border border-green-200 hover:bg-green-50 transition"
                  >
                    <MessageCircle className="w-3 h-3" />
                    واتساب
                  </button>
                  {/* Prescription - Doctors only */}
                  {isDoctor && (
                    <button
                      onClick={() => setPrescriptionModalOpen(true)}
                      className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-white text-purple-700 border border-purple-200 hover:bg-purple-50 transition"
                    >
                      <Pill className="w-3 h-3" />
                      وصفة طبية
                    </button>
                  )}
                  {/* Edit Visit - Doctors only */}
                  {isDoctor && summary.todayVisit?.id && (
                    <button
                      onClick={() => {
                        setEditVisitForm({
                          chiefComplaint: summary.todayVisit?.chiefComplaint ?? "",
                          clinicalNotes: summary.todayVisit?.clinicalNotes ?? "",
                          treatmentDone: summary.todayVisit?.treatmentDone ?? "",
                          diagnosis: summary.todayVisit?.diagnosis ?? "",
                          instructions: summary.todayVisit?.instructions ?? "",
                          nextVisitPlan: summary.todayVisit?.nextVisitPlan ?? "",
                          cost: summary.todayVisit?.cost ?? 0,
                        });
                        setEditVisitModalOpen(true);
                      }}
                      className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-white text-[#3d7ab5] border border-[#3d7ab5]/20 hover:bg-[#3d7ab5]/10 transition"
                    >
                      <Stethoscope className="w-3 h-3" />
                      تعديل الزيارة
                    </button>
                  )}
                  {/* Book Appointment */}
                  <button
                    onClick={() => {
                      setBookAptDoctorId(selectedItem?.doctorId ?? "");
                      setBookAptServiceId(selectedItem?.serviceId ?? "");
                      setBookAppointmentModalOpen(true);
                    }}
                    className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-white text-[#1a3a5c] border border-gray-200 hover:bg-gray-50 transition"
                  >
                    <CalendarDays className="w-3 h-3" />
                    حجز موعد
                  </button>
                  {/* Upload Document */}
                  <button
                    onClick={() => setUploadDocModalOpen(true)}
                    className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-white text-gray-700 border border-gray-200 hover:bg-gray-50 transition"
                  >
                    <Upload className="w-3 h-3" />
                    رفع مستند
                  </button>
                  {/* Print */}
                  <button
                    onClick={() => window.print()}
                    className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-bold rounded-lg bg-white text-gray-600 border border-gray-200 hover:bg-gray-50 transition"
                  >
                    <Printer className="w-3 h-3" />
                    طباعة
                  </button>
                </div>

                {/* Undo Bar */}
                {undoInfo && (
                  <div className="mt-2 flex items-center gap-2 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
                    <Undo2 className="w-3.5 h-3.5 text-amber-700" />
                    <span className="text-[11px] text-amber-800 font-medium">
                      {undoInfo.type === "Cancelled" ? "تم إلغاء الموعد" :
                       undoInfo.type === "NoShow" ? "تم تسجيل عدم الحضور" :
                       "تم الإلغاء من الانتظار"}
                    </span>
                    <button
                      onClick={handleUndo}
                      className="px-2.5 py-1 text-[10px] font-bold rounded-lg bg-amber-600 text-white hover:bg-amber-700 transition"
                    >
                      تراجع
                    </button>
                    <span className="text-[9px] text-amber-600 mr-auto">Ctrl+Z</span>
                  </div>
                )}

                {/* Keyboard Shortcuts Hint */}
                <div className="mt-2 flex flex-wrap gap-x-3 gap-y-1 text-[9px] text-gray-400">
                  <span>Ctrl+K: نداء المريض</span>
                  <span>Ctrl+E: دخول الغرفة</span>
                  <span>Ctrl+R: تحديث</span>
                  <span>Ctrl+Z: تراجع</span>
                </div>
              </div>
            )}
          </div>
        )}
      </div>

      {/* ═══ MODAL OVERLAYS ═══ */}

      <ConfirmDialog
        open={cancelDialogOpen}
        type={cancelDialogType}
        patientName={summary?.patient.fullName ?? ""}
        pendingChangeRoomName={pendingChangeRoomName}
        isPending={updateAptStatusMutation.isPending || cancelQueueMutation.isPending || changeRoomMutation.isPending}
        onConfirm={handleConfirmAction}
        onClose={() => { setCancelDialogOpen(false); setChangeRoomSelectedRoom(""); }}
      />

      <RecordPaymentModal
        open={recordPaymentOpen}
        outstandingBalance={summary?.financeSummary?.outstandingBalance}
        paymentAmount={paymentAmount}
        setPaymentAmount={setPaymentAmount}
        paymentMethod={paymentMethod}
        setPaymentMethod={setPaymentMethod}
        paymentServiceDesc={paymentServiceDesc}
        setPaymentServiceDesc={setPaymentServiceDesc}
        paymentNotes={paymentNotes}
        setPaymentNotes={setPaymentNotes}
        isPending={createPaymentMutation.isPending}
        onSubmit={handleRecordPayment}
        onClose={() => setRecordPaymentOpen(false)}
      />

      <SendSmsModal
        open={smsModalOpen}
        smsTo={smsTo}
        setSmsTo={setSmsTo}
        smsMessage={smsMessage}
        setSmsMessage={setSmsMessage}
        isPending={sendSmsMutation.isPending}
        onSubmit={handleSendSms}
        onClose={() => setSmsModalOpen(false)}
      />

      <PrescriptionModal
        open={prescriptionModalOpen}
        prescDiagnosis={prescDiagnosis}
        setPrescDiagnosis={setPrescDiagnosis}
        prescNotes={prescNotes}
        setPrescNotes={setPrescNotes}
        prescItems={prescItems}
        setPrescItems={setPrescItems}
        isPending={createPrescriptionMutation.isPending}
        onSubmit={handleCreatePrescription}
        onClose={() => setPrescriptionModalOpen(false)}
      />

      <EditVisitModal
        open={editVisitModalOpen}
        form={editVisitForm}
        setForm={setEditVisitForm}
        isPending={updateVisitMutation.isPending}
        onSubmit={handleUpdateVisit}
        onClose={() => setEditVisitModalOpen(false)}
      />

      <BookAppointmentModal
        open={bookAppointmentModalOpen}
        bookAptDate={bookAptDate}
        setBookAptDate={setBookAptDate}
        bookAptStartTime={bookAptStartTime}
        setBookAptStartTime={setBookAptStartTime}
        bookAptEndTime={bookAptEndTime}
        setBookAptEndTime={setBookAptEndTime}
        bookAptDoctorId={bookAptDoctorId}
        doctorName={selectedItem?.doctorName ?? ""}
        bookAptServiceId={bookAptServiceId}
        setBookAptServiceId={setBookAptServiceId}
        bookAptType={bookAptType}
        setBookAptType={setBookAptType}
        bookAptNotes={bookAptNotes}
        setBookAptNotes={setBookAptNotes}
        services={services}
        isPending={createAppointmentMutation.isPending}
        onSubmit={handleBookAppointment}
        onClose={() => setBookAppointmentModalOpen(false)}
      />

      <UploadDocumentModal
        open={uploadDocModalOpen}
        patientId={selectedPatientId}
        onClose={() => setUploadDocModalOpen(false)}
        onUploaded={() => { loadJourney(); refetchSummary(); }}
      />
    </div>
  );
}
