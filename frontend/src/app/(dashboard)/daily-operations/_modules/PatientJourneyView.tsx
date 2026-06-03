"use client";

import {
  AlertTriangle,
  ArrowLeft,
  Banknote,
  CalendarCheck,
  CheckCircle2,
  ClipboardList,
  CreditCard,
  FlaskConical,
  LogIn,
  Receipt,
  Search,
  Stethoscope,
  UserCheck,
  UserX,
} from "lucide-react";

import type { TodayJourneyItem } from "../_lib/constants";
import { NAVY, BLUE, ORANGE } from "../_lib/constants";
import { getFinancialGate, resolveVisitTypeRule } from "../_lib/visitTypeRules";

type JourneyStage = "appointment" | "payment" | "waiting" | "called" | "clinic" | "checkout" | "left" | "completed";

interface PatientJourneyViewProps {
  items: TodayJourneyItem[];
  loading: boolean;
  selectedPatientId?: string;
  onIntake: (item: TodayJourneyItem) => void;
  onSendToQueue: (item: TodayJourneyItem) => void;
  onCallPatient: (item: TodayJourneyItem) => void;
  onEnterRoom: (item: TodayJourneyItem) => void;
  onQuickPayment: (item: TodayJourneyItem) => void;
  onCreateDraftInvoice: (item: TodayJourneyItem) => void;
  onCompleteVisit: (item: TodayJourneyItem) => void;
  onLeftWithoutCompletion: (item: TodayJourneyItem) => void;
  onBookAppointment: (item: TodayJourneyItem) => void;
  onOpenSidePanel: (item: TodayJourneyItem) => void;
  onContextMenu?: (e: React.MouseEvent, item: TodayJourneyItem) => void;
  createDraftInvoicePending?: boolean;
}

const STAGES: { key: JourneyStage; label: string; icon: React.ElementType; color: string }[] = [
  { key: "appointment", label: "موعد", icon: CalendarCheck, color: BLUE },
  { key: "payment", label: "منتظر الدفع", icon: Banknote, color: "#dc2626" },
  { key: "waiting", label: "الانتظار", icon: ClipboardList, color: ORANGE },
  { key: "called", label: "تم النداء", icon: UserCheck, color: "#0891b2" },
  { key: "clinic", label: "داخل العيادة", icon: Stethoscope, color: "#7c3aed" },
  { key: "checkout", label: "جاهز للمحاسبة", icon: CreditCard, color: "#059669" },
  { key: "left", label: "خرج بدون إكمال", icon: UserX, color: "#dc2626" },
  { key: "completed", label: "مكتمل", icon: CheckCircle2, color: "#16a34a" },
];

export default function PatientJourneyView({
  items,
  loading,
  selectedPatientId,
  onIntake,
  onSendToQueue,
  onCallPatient,
  onEnterRoom,
  onQuickPayment,
  onCreateDraftInvoice,
  onCompleteVisit,
  onLeftWithoutCompletion,
  onBookAppointment,
  onOpenSidePanel,
  onContextMenu,
  createDraftInvoicePending,
}: PatientJourneyViewProps) {
  if (loading) {
    return (
      <div className="grid grid-cols-1 lg:grid-cols-3 xl:grid-cols-8 gap-3 p-4">
        {STAGES.map((stage) => (
          <div key={stage.key} className="rounded-lg border border-gray-100 bg-white p-3">
            <div className="h-4 w-24 rounded bg-gray-100 animate-pulse" />
            <div className="mt-3 space-y-2">
              <div className="h-28 rounded-lg bg-gray-100 animate-pulse" />
              <div className="h-24 rounded-lg bg-gray-100 animate-pulse" />
            </div>
          </div>
        ))}
      </div>
    );
  }

  if (items.length === 0) {
    return (
      <div className="h-full flex flex-col items-center justify-center text-center p-8 text-gray-400">
        <Search className="w-10 h-10 mb-3 text-gray-300" />
        <h3 className="text-sm font-bold text-gray-500">لا توجد رحلات مرضى اليوم</h3>
        <p className="text-xs mt-1">عند تسجيل وصول مريض أو إضافة موعد سيظهر هنا ضمن مسار التشغيل.</p>
      </div>
    );
  }

  const grouped = STAGES.map((stage) => ({
    ...stage,
    items: items.filter((item) => getJourneyStage(item) === stage.key),
  }));

  return (
    <div className="h-full overflow-auto bg-[#f8fafc] p-3">
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-8 gap-3 min-w-[1320px]">
        {grouped.map((stage) => {
          const StageIcon = stage.icon;
          return (
            <section key={stage.key} className="min-h-[520px] rounded-lg border border-gray-200 bg-white">
              <header className="flex items-center justify-between gap-2 border-b border-gray-100 px-3 py-2">
                <div className="flex items-center gap-2 min-w-0">
                  <span className="grid h-7 w-7 place-items-center rounded-md" style={{ background: `${stage.color}14`, color: stage.color }}>
                    <StageIcon className="w-4 h-4" />
                  </span>
                  <h3 className="truncate text-xs font-bold" style={{ color: NAVY }}>{stage.label}</h3>
                </div>
                <span className="rounded-full bg-gray-50 px-2 py-0.5 text-[11px] font-bold text-gray-500">
                  {stage.items.length}
                </span>
              </header>

              <div className="space-y-2 p-2">
                {stage.items.length === 0 ? (
                  <div className="rounded-lg border border-dashed border-gray-200 p-3 text-center text-[11px] text-gray-400">
                    لا توجد حالات
                  </div>
                ) : (
                  stage.items.map((item) => (
                    <JourneyCard
                      key={item.appointmentId}
                      item={item}
                      selected={selectedPatientId === item.patientId}
                      onIntake={onIntake}
                      onSendToQueue={onSendToQueue}
                      onCallPatient={onCallPatient}
                      onEnterRoom={onEnterRoom}
                      onQuickPayment={onQuickPayment}
                      onCreateDraftInvoice={onCreateDraftInvoice}
                      onCompleteVisit={onCompleteVisit}
                      onLeftWithoutCompletion={onLeftWithoutCompletion}
                      onBookAppointment={onBookAppointment}
                      onOpenSidePanel={onOpenSidePanel}
                      onContextMenu={onContextMenu}
                      createDraftInvoicePending={createDraftInvoicePending}
                    />
                  ))
                )}
              </div>
            </section>
          );
        })}
      </div>
    </div>
  );
}

function JourneyCard({
  item,
  selected,
  onIntake,
  onSendToQueue,
  onCallPatient,
  onEnterRoom,
  onQuickPayment,
  onCreateDraftInvoice,
  onCompleteVisit,
  onLeftWithoutCompletion,
  onBookAppointment,
  onOpenSidePanel,
  onContextMenu,
  createDraftInvoicePending,
}: {
  item: TodayJourneyItem;
  selected: boolean;
  onIntake: (item: TodayJourneyItem) => void;
  onSendToQueue: (item: TodayJourneyItem) => void;
  onCallPatient: (item: TodayJourneyItem) => void;
  onEnterRoom: (item: TodayJourneyItem) => void;
  onQuickPayment: (item: TodayJourneyItem) => void;
  onCreateDraftInvoice: (item: TodayJourneyItem) => void;
  onCompleteVisit: (item: TodayJourneyItem) => void;
  onLeftWithoutCompletion: (item: TodayJourneyItem) => void;
  onBookAppointment: (item: TodayJourneyItem) => void;
  onOpenSidePanel: (item: TodayJourneyItem) => void;
  onContextMenu?: (e: React.MouseEvent, item: TodayJourneyItem) => void;
  createDraftInvoicePending?: boolean;
}) {
  const rule = resolveVisitTypeRule(item.appointmentType);
  const gate = getFinancialGate(item);
  const stage = getJourneyStage(item);

  return (
    <article
      onClick={() => onOpenSidePanel(item)}
      onContextMenu={(event) => onContextMenu?.(event, item)}
      className="cursor-pointer rounded-lg border bg-white p-2.5 shadow-sm transition hover:shadow-md"
      style={{ borderColor: selected ? BLUE : "#e5e7eb", boxShadow: selected ? `0 0 0 2px ${BLUE}22` : undefined }}
    >
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <h4 className="truncate text-xs font-bold" style={{ color: NAVY }}>{item.patientName}</h4>
          <div className="mt-0.5 flex flex-wrap items-center gap-1 text-[10px] text-gray-500">
            <span className="font-mono">{item.patientNumber ?? "بدون رقم"}</span>
            <span>•</span>
            <span>{item.appointmentTime}</span>
          </div>
        </div>
        <span className="rounded-full bg-gray-50 px-1.5 py-0.5 text-[10px] font-bold text-gray-500">
          {item.nextAction === "None" ? "—" : item.nextAction}
        </span>
      </div>

      <div className="mt-2 space-y-1.5 text-[11px] text-gray-600">
        <InfoLine label="الطبيب" value={item.doctorName || "غير محدد"} />
        <InfoLine label="نوع الزيارة" value={rule.arabicName} />
        <InfoLine label="الخدمة" value={item.serviceName ?? "لم تحدد خدمة مالية"} />
      </div>

      <div className="mt-2 flex flex-wrap gap-1">
        <GateBadge gate={gate} />
        {rule.isMultiSession && <MiniBadge label="خطة متعددة" color="#7c3aed" />}
        {rule.requiresPricedService && <MiniBadge label="خدمة مسعرة" color={BLUE} />}
        {(item.hasLabOrder || rule.code.includes("lab")) && (
          <MiniBadge label={item.labOrderStatus === "Ready" ? "عمل المعمل جاهز" : "يوجد عمل معمل"} color="#8b5cf6" icon={<FlaskConical className="w-3 h-3" />} />
        )}
        {stage === "left" && <MiniBadge label="خرج بدون إكمال" color="#dc2626" icon={<UserX className="w-3 h-3" />} />}
      </div>

      {gate.reason && (
        <p className="mt-2 line-clamp-2 rounded-md bg-gray-50 px-2 py-1 text-[10px] leading-4 text-gray-500">
          {gate.reason}
        </p>
      )}

      <div className="mt-2 flex flex-wrap gap-1">
        {stage === "appointment" && (
          <ActionButton label="تسجيل وصول" icon={<LogIn className="w-3 h-3" />} onClick={() => onIntake(item)} />
        )}
        {stage === "payment" && (
          <ActionButton label="تحصيل" icon={<Banknote className="w-3 h-3" />} onClick={() => onQuickPayment(item)} emphasis />
        )}
        {stage === "waiting" && (
          <ActionButton label="نداء" icon={<UserCheck className="w-3 h-3" />} onClick={() => onCallPatient(item)} />
        )}
        {stage === "called" && (
          <ActionButton label="إدخال" icon={<ArrowLeft className="w-3 h-3" />} onClick={() => onEnterRoom(item)} />
        )}
        {item.nextAction === "SendToQueue" && (
          <ActionButton label="إلى الانتظار" icon={<ClipboardList className="w-3 h-3" />} onClick={() => onSendToQueue(item)} />
        )}
        {stage === "checkout" && (
          <>
            <ActionButton label="مسودة" icon={<Receipt className="w-3 h-3" />} onClick={() => onCreateDraftInvoice(item)} disabled={createDraftInvoicePending} />
            <ActionButton label="تحصيل" icon={<CreditCard className="w-3 h-3" />} onClick={() => onQuickPayment(item)} emphasis />
            <ActionButton label="إغلاق" icon={<CheckCircle2 className="w-3 h-3" />} onClick={() => onCompleteVisit(item)} />
          </>
        )}
        {item.visitId && stage !== "completed" && stage !== "left" && (
          <ActionButton label="خرج بدون إكمال" icon={<UserX className="w-3 h-3" />} onClick={() => onLeftWithoutCompletion(item)} />
        )}
        {stage === "completed" && (
          <ActionButton label="موعد قادم" icon={<CalendarCheck className="w-3 h-3" />} onClick={() => onBookAppointment(item)} />
        )}
      </div>
    </article>
  );
}

function InfoLine({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between gap-2">
      <span className="text-gray-400">{label}</span>
      <span className="truncate font-semibold text-gray-700">{value}</span>
    </div>
  );
}

function GateBadge({ gate }: { gate: ReturnType<typeof getFinancialGate> }) {
  const color = gate.tone === "danger" ? "#dc2626" : gate.tone === "success" ? "#16a34a" : gate.tone === "warning" ? ORANGE : "#64748b";
  const Icon = gate.tone === "danger" ? AlertTriangle : gate.tone === "success" ? CheckCircle2 : Banknote;
  return (
    <span className="inline-flex items-center gap-1 rounded-full px-1.5 py-0.5 text-[10px] font-bold" style={{ background: `${color}14`, color }}>
      <Icon className="w-3 h-3" />
      {gate.label}
    </span>
  );
}

function MiniBadge({ label, color, icon }: { label: string; color: string; icon?: React.ReactNode }) {
  return (
    <span className="inline-flex items-center gap-1 rounded-full px-1.5 py-0.5 text-[10px] font-bold" style={{ background: `${color}12`, color }}>
      {icon}
      {label}
    </span>
  );
}

function ActionButton({
  label,
  icon,
  onClick,
  emphasis,
  disabled,
}: {
  label: string;
  icon: React.ReactNode;
  onClick: () => void;
  emphasis?: boolean;
  disabled?: boolean;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={(event) => {
        event.stopPropagation();
        onClick();
      }}
      className="inline-flex items-center gap-1 rounded-md border px-1.5 py-1 text-[10px] font-bold transition disabled:opacity-50"
      style={{
        background: emphasis ? BLUE : "#fff",
        borderColor: emphasis ? BLUE : "#e5e7eb",
        color: emphasis ? "#fff" : NAVY,
      }}
    >
      {icon}
      {label}
    </button>
  );
}

function getJourneyStage(item: TodayJourneyItem): JourneyStage {
  if (isLeftWithoutCompletionStatus(item.checkoutStatus ?? item.visitStatus)) return "left";
  if (item.checkoutStatus === "CheckedOut" || item.appointmentStatus === "Completed") return "completed";
  if (item.checkoutStatus === "ReadyForCheckout" || item.nextAction === "Checkout") return "checkout";
  if (item.queueStatus === "InRoom" || item.queueStatus === "InProgress" || item.appointmentStatus === "InRoom" || item.appointmentStatus === "InProgress") return "clinic";
  if (item.queueStatus === "Called" || item.appointmentStatus === "Called") return "called";
  if (item.paymentBeforeEntryRequired || item.financialEntryStatus === "WaitingForPayment") return "payment";
  if (item.queueStatus === "Waiting" || item.appointmentStatus === "Waiting") return "waiting";
  return "appointment";
}

function isLeftWithoutCompletionStatus(status?: string | null) {
  return status === "LeftWithoutCompletion"
    || status === "CancelledAfterArrival"
    || status === "Incomplete"
    || status === "Abandoned";
}
