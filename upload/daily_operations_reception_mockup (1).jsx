import React, { useMemo, useState } from "react";
import {
  Activity,
  AlertTriangle,
  BarChart3,
  Bell,
  Building2,
  CalendarCheck,
  CalendarDays,
  CheckCircle2,
  ChevronLeft,
  Clock,
  CreditCard,
  DoorOpen,
  FileText,
  History,
  KeyRound,
  FlaskConical,
  LayoutDashboard,
  MessageCircle,
  Package,
  Printer,
  RefreshCw,
  Route,
  Search,
  Send,
  Settings,
  ShieldCheck,
  Stethoscope,
  User,
  UserCheck,
  UserPlus,
  UserRound,
  UserX,
  Users,
  Wallet,
  Zap,
} from "lucide-react";

function cn() {
  return Array.prototype.slice.call(arguments).filter(Boolean).join(" ");
}

const RECEPTION_MODULES = [
  { key: "dashboard", label: "لوحة التحكم", icon: LayoutDashboard },
  { key: "daily", label: "التشغيل اليومي", icon: Activity, badge: "اليوم" },
  { key: "patients", label: "المرضى", icon: UserRound },
  { key: "appointments", label: "المواعيد", icon: CalendarCheck },
  { key: "queue", label: "الطابور والنداء", icon: Bell },
  { key: "finance", label: "المالية والفواتير", icon: Wallet },
  { key: "messages", label: "الرسائل والواتساب", icon: MessageCircle },
  { key: "doctors", label: "الأطباء والموظفين", icon: Users },
  { key: "orthodontics", label: "ملفات التقويم", icon: Stethoscope },
  { key: "labs", label: "المعامل", icon: FlaskConical },
  { key: "inventory", label: "المخزون والمشتريات", icon: Package },
  { key: "reports", label: "التقارير", icon: BarChart3 },
  { key: "branches", label: "الفروع", icon: Building2 },
  { key: "permissions", label: "الصلاحيات", icon: ShieldCheck },
  { key: "settings", label: "الإعدادات", icon: Settings },
];

const DOCTOR_MODULES = [
  { key: "doctorClinic", label: "عيادة الطبيب", icon: Stethoscope, badge: "مرضاي" },
  { key: "doctorPatients", label: "مرضاي اليوم", icon: Users },
  { key: "doctorVisits", label: "تسجيل الزيارات", icon: FileText },
  { key: "doctorOrtho", label: "ملفات التقويم", icon: Zap },
  { key: "doctorMessages", label: "رسائل المرضى", icon: MessageCircle },
];

const SYSTEM_MODULES = RECEPTION_MODULES;

const DAILY_COMMAND_GROUPS = [
  {
    title: "ملف ومواعيد",
    hint: "إنشاء وفتح وتنظيم",
    commands: [
      { key: "walkIn", label: "مريض مشي", icon: UserPlus, accent: "#f5922e", primary: true },
      { key: "newPatient", label: "مريض جديد", icon: UserPlus, accent: "#3d7ab5" },
      { key: "archive", label: "تسجيل من الأرشيف", icon: FileText, accent: "#64748b" },
      { key: "newAppointment", label: "موعد جديد", icon: CalendarDays, accent: "#8b5cf6" },
      { key: "openFile", label: "فتح الملف", icon: UserRound, accent: "#1a3a5c" },
    ],
  },
  {
    title: "رحلة المريض",
    hint: "من الوصول حتى الطبيب",
    commands: [
      { key: "checkIn", label: "تسجيل حضور", icon: UserCheck, accent: "#16a34a", primary: true },
      { key: "sendQueue", label: "إرسال للطابور", icon: Route, accent: "#f5922e" },
      { key: "call", label: "نداء المريض", icon: Bell, accent: "#7c3aed" },
      { key: "enterRoom", label: "إدخال للغرفة", icon: DoorOpen, accent: "#0891b2" },
      { key: "startVisit", label: "بدء الزيارة", icon: Stethoscope, accent: "#4f46e5" },
      { key: "changeRoom", label: "تغيير الغرفة", icon: Building2, accent: "#0f766e" },
      { key: "cancelQueue", label: "إلغاء من الطابور", icon: UserX, accent: "#ef4444" },
    ],
  },
  {
    title: "الخروج والمالية",
    hint: "تحصيل وسند وفاتورة",
    commands: [
      { key: "finishVisit", label: "إلى الدفع", icon: CheckCircle2, accent: "#0f172a", primary: true },
      { key: "quickPay", label: "دفع سريع", icon: CreditCard, accent: "#22c55e" },
      { key: "draftInvoice", label: "فاتورة مسودة", icon: FileText, accent: "#3d7ab5" },
      { key: "receipt", label: "طباعة سند", icon: Printer, accent: "#64748b" },
      { key: "nextVisit", label: "موعد متابعة", icon: CalendarCheck, accent: "#f5922e" },
      { key: "completeCheckout", label: "إكمال الخروج", icon: CheckCircle2, accent: "#059669" },
    ],
  },
  {
    title: "التواصل والمتابعة",
    hint: "رسائل، تنبيهات، تقارير",
    commands: [
      { key: "whatsapp", label: "واتساب", icon: MessageCircle, accent: "#22c55e" },
      { key: "bulkReminders", label: "تذكيرات الغد", icon: Send, accent: "#3d7ab5" },
      { key: "noShow", label: "لم يحضر", icon: UserX, accent: "#ef4444" },
      { key: "dailyReport", label: "تقرير اليوم", icon: BarChart3, accent: "#1a3a5c" },
      { key: "printSchedule", label: "طباعة الجدول", icon: Printer, accent: "#64748b" },
      { key: "sound", label: "صوت النداء", icon: Bell, accent: "#f5922e" },
      { key: "shortcuts", label: "اختصارات", icon: Settings, accent: "#475569" },
    ],
  },
];

const DAILY_COMMANDS = DAILY_COMMAND_GROUPS.reduce(function (all, group) {
  return all.concat(group.commands);
}, []);

const DAILY_TABS = [
  { key: "journey", label: "رحلة اليوم" },
  { key: "schedule", label: "الجدول" },
  { key: "queue", label: "الطابور" },
  { key: "finance", label: "المالية" },
  { key: "messages", label: "الرسائل" },
  { key: "alerts", label: "التنبيهات" },
];

const PATIENT_TABS = [
  { key: "journey", label: "الرحلة" },
  { key: "visit", label: "تسجيل الزيارة" },
  { key: "ortho", label: "التقويم" },
  { key: "history", label: "التاريخ الطبي" },
  { key: "payment", label: "الدفع" },
  { key: "timeline", label: "السجل" },
];

const STATUS_LABELS = {
  Scheduled: "مجدول",
  Arrived: "وصل",
  Waiting: "ينتظر",
  Called: "نداء",
  InRoom: "الغرفة",
  InProgress: "عند الطبيب",
  Checkout: "خروج ودفع",
  Completed: "مكتمل",
  NoShow: "غياب",
};

const STATUS_COLORS = {
  Scheduled: "bg-blue-50 text-blue-700",
  Arrived: "bg-amber-50 text-amber-800",
  Waiting: "bg-orange-50 text-orange-800",
  Called: "bg-purple-50 text-purple-800",
  InRoom: "bg-cyan-50 text-cyan-800",
  InProgress: "bg-emerald-50 text-emerald-800",
  Checkout: "bg-teal-50 text-teal-800",
  Completed: "bg-green-50 text-green-800",
  NoShow: "bg-red-50 text-red-700",
};

const flowSteps = [
  { key: "Arrived", label: "وصل", icon: UserCheck },
  { key: "Waiting", label: "انتظار", icon: Clock },
  { key: "Called", label: "نداء", icon: Bell },
  { key: "InRoom", label: "الغرفة", icon: DoorOpen },
  { key: "InProgress", label: "الطبيب", icon: Stethoscope },
  { key: "Checkout", label: "الدفع", icon: CreditCard },
  { key: "Completed", label: "مكتمل", icon: CheckCircle2 },
];

const SERVICE_CATALOG = [
  { id: "svc-ortho-activation", category: "تقويم", name: "تفعيل تقويم شهري", price: 10000 },
  { id: "svc-wire-change", category: "تقويم", name: "تغيير سلك تقويم", price: 15000 },
  { id: "svc-bracket-rebond", category: "تقويم", name: "إعادة لصق براكت", price: 8000 },
  { id: "svc-scaling", category: "علاج عام", name: "تنظيف وتلميع", price: 25000 },
  { id: "svc-composite", category: "تجميل", name: "حشوة تجميلية", price: 30000 },
  { id: "svc-rct-session", category: "عصب", name: "جلسة علاج عصب", price: 35000 },
  { id: "svc-surgery-consult", category: "جراحة", name: "كشف جراحي", price: 15000 },
  { id: "svc-extraction", category: "جراحة", name: "خلع سن بسيط", price: 25000 },
];

const patientsSeed = [
  {
    appointmentId: "a1",
    patientId: "p1",
    patientName: "محمد علي أحمد",
    patientNumber: "8501",
    patientPhone: "773394704",
    appointmentTime: "08:30",
    appointmentStatus: "Scheduled",
    doctorName: "د. عقلان الكامل",
    doctorColor: "#3d7ab5",
    serviceName: "تقويم - متابعة شهرية",
    roomName: "—",
    consultationFeeRequired: true,
    consultationFeePaid: false,
    totalTreatmentCost: 450000,
    totalPaid: 280000,
    outstandingBalance: 170000,
    overdueAmount: 15000,
    chiefComplaint: "متابعة شهرية للتقويم وتغيير السلك",
  },
  {
    appointmentId: "a2",
    patientId: "p2",
    patientName: "أسماء عبدالجليل",
    patientNumber: "2940",
    patientPhone: "771234567",
    appointmentTime: "09:00",
    appointmentStatus: "Waiting",
    doctorName: "د. عائشة غازي",
    doctorColor: "#059669",
    serviceName: "حشوة تجميلية",
    roomName: "الانتظار",
    consultationFeeRequired: false,
    consultationFeePaid: true,
    totalTreatmentCost: 25000,
    totalPaid: 25000,
    outstandingBalance: 0,
    overdueAmount: 0,
    chiefComplaint: "كسر بسيط في الحشوة القديمة",
  },
  {
    appointmentId: "a3",
    patientId: "p3",
    patientName: "علي محمود سعيد",
    patientNumber: "6112",
    patientPhone: "777222111",
    appointmentTime: "09:30",
    appointmentStatus: "Called",
    doctorName: "د. هشام القدسي",
    doctorColor: "#7c3aed",
    serviceName: "جراحة - كشف",
    roomName: "غرفة 1",
    consultationFeeRequired: true,
    consultationFeePaid: true,
    totalTreatmentCost: 85000,
    totalPaid: 60000,
    outstandingBalance: 25000,
    overdueAmount: 0,
    chiefComplaint: "ألم متكرر في ضرس العقل",
  },
  {
    appointmentId: "a4",
    patientId: "p4",
    patientName: "سارة نبيل",
    patientNumber: "8502",
    patientPhone: "770888999",
    appointmentTime: "10:00",
    appointmentStatus: "InProgress",
    doctorName: "د. إيمان الكامل",
    doctorColor: "#dc2626",
    serviceName: "تنظيف وتلميع",
    roomName: "غرفة 2",
    consultationFeeRequired: false,
    consultationFeePaid: true,
    totalTreatmentCost: 35000,
    totalPaid: 30000,
    outstandingBalance: 5000,
    overdueAmount: 0,
    chiefComplaint: "تنظيف دوري وتصبغات بسيطة",
  },
  {
    appointmentId: "a5",
    patientId: "p5",
    patientName: "عبدالله غالب",
    patientNumber: "4801",
    patientPhone: "735111222",
    appointmentTime: "10:30",
    appointmentStatus: "Checkout",
    doctorName: "د. عقلان الكامل",
    doctorColor: "#3d7ab5",
    serviceName: "تقويم - تركيب سلك",
    roomName: "الخروج",
    consultationFeeRequired: false,
    consultationFeePaid: true,
    totalTreatmentCost: 520000,
    totalPaid: 500000,
    outstandingBalance: 20000,
    overdueAmount: 0,
    chiefComplaint: "تركيب سلك علوي وتحديد موعد بعد شهر",
  },
  {
    appointmentId: "a6",
    patientId: "p6",
    patientName: "منى خالد",
    patientNumber: "3321",
    patientPhone: "771555333",
    appointmentTime: "11:00",
    appointmentStatus: "Completed",
    doctorName: "د. عائشة غازي",
    doctorColor: "#059669",
    serviceName: "عصب - جلسة ثانية",
    roomName: "—",
    consultationFeeRequired: false,
    consultationFeePaid: true,
    totalTreatmentCost: 120000,
    totalPaid: 120000,
    outstandingBalance: 0,
    overdueAmount: 0,
    chiefComplaint: "تمت الجلسة الثانية وإرسال تعليمات ما بعد العلاج",
  },
];

const alertsSeed = [
  { id: "al1", type: "warning", patientName: "محمد علي أحمد", message: "استشارة مطلوبة ولم تسدد بعد" },
  { id: "al2", type: "danger", patientName: "عبدالله غالب", message: "يوجد متبقي قبل إكمال الخروج" },
  { id: "al3", type: "info", message: "يوجد 2 مرضى في الطابور بانتظار النداء" },
];

const activitySeed = [
  { id: "ac1", time: "09:05 ص", text: "تم فتح شاشة التشغيل اليومي", type: "info" },
  { id: "ac2", time: "09:10 ص", text: "تم تحديث قائمة مواعيد اليوم", type: "info" },
  { id: "ac3", time: "09:15 ص", text: "تم التنبيه على وجود متأخرات مالية", type: "warning" },
];

const receptionNotificationsSeed = [
  { id: "rn1", time: "09:12 ص", patientName: "سارة نبيل", message: "المريض خرج من عند الطبيب ويحتاج مراجعة الدفع", type: "info" },
];

function formatMoney(value) {
  return new Intl.NumberFormat("ar-YE").format(value || 0) + " ر.ي";
}

function getFlowIndex(status) {
  var map = { Arrived: 0, Waiting: 1, Called: 2, InRoom: 3, InProgress: 4, Checkout: 5, Completed: 6 };
  return typeof map[status] === "number" ? map[status] : -1;
}

function nextStatus(status) {
  if (status === "Scheduled") return "Arrived";
  if (status === "Arrived") return "Waiting";
  if (status === "Waiting") return "Called";
  if (status === "Called") return "InRoom";
  if (status === "InRoom") return "InProgress";
  if (status === "InProgress") return "Checkout";
  if (status === "Checkout") return "Completed";
  return null;
}

function nextActionText(status) {
  if (status === "Scheduled") return "تسجيل وصول";
  if (status === "Arrived") return "إرسال للطابور";
  if (status === "Waiting") return "نداء المريض";
  if (status === "Called") return "إدخال للغرفة";
  if (status === "InRoom") return "بدء الزيارة";
  if (status === "InProgress") return "إلى الخروج";
  if (status === "Checkout") return "إكمال الزيارة";
  return "مكتمل";
}

function runSmokeTests() {
  console.assert(patientsSeed.length === 6, "patients seed should contain 6 patients");
  console.assert(RECEPTION_MODULES.some(function (m) { return m.key === "daily"; }), "sidebar should contain daily operations module");
  console.assert(DOCTOR_MODULES.some(function (m) { return m.key === "doctorClinic"; }), "doctor sidebar should contain doctor clinic module");
  console.assert(DAILY_COMMANDS.length >= 10, "daily operations should contain all main commands");
  console.assert(DAILY_COMMAND_GROUPS.length === 4, "daily commands should be grouped into 4 workflow groups");
  console.assert(SERVICE_CATALOG.length >= 6, "doctor should use a priced service catalog");
  console.assert(getFlowIndex("Waiting") === 1, "waiting flow index should be 1");
  console.assert(nextStatus("Scheduled") === "Arrived", "scheduled should move to arrived");
  console.assert(nextStatus("Checkout") === "Completed", "checkout should move to completed");
}
runSmokeTests();

function SystemSidebar(props) {
  var modules = props.role === "doctor" ? DOCTOR_MODULES : RECEPTION_MODULES;
  var userLabel = props.role === "doctor" ? "د. عقلان الكامل" : "موظف الاستقبال";
  var roleLabel = props.role === "doctor" ? "حساب طبيب" : "حساب استقبال";

  return (
    <aside className="w-[260px] bg-[#102a43] text-white flex-shrink-0 flex flex-col border-l border-white/10">
      <div className="h-16 px-4 flex items-center gap-3 border-b border-white/10">
        <div className="w-10 h-10 rounded-xl bg-white flex items-center justify-center text-[#102a43]">
          <Stethoscope className="w-6 h-6" />
        </div>
        <div>
          <p className="font-black text-sm">Aqlan Dental Pro</p>
          <p className="text-[10px] text-blue-100">مركز الدكتور عقلان الكامل</p>
        </div>
      </div>

      <div className="p-3 border-b border-white/10">
        <div className="rounded-2xl bg-white/10 p-3">
          <p className="text-[10px] text-blue-100 mb-1">المستخدم الحالي</p>
          <p className="text-xs font-bold">{userLabel}</p>
          <p className="text-[10px] text-blue-100 mt-1">{roleLabel}</p>
        </div>
      </div>

      <nav className="flex-1 overflow-y-auto p-3 space-y-1">
        {modules.map(function (mod) {
          var Icon = mod.icon;
          var active = props.activeModule === mod.key;
          return (
            <button
              key={mod.key}
              type="button"
              onClick={function () { props.setActiveModule(mod.key); }}
              className={cn("w-full flex items-center justify-between gap-2 rounded-xl px-3 py-2.5 text-xs font-bold transition", active ? "bg-white text-[#102a43] shadow" : "text-blue-50 hover:bg-white/10")}
            >
              <span className="flex items-center gap-2">
                <Icon className="w-4 h-4" />
                {mod.label}
              </span>
              {mod.badge ? <span className={cn("rounded-full px-2 py-0.5 text-[9px]", active ? "bg-blue-100 text-blue-800" : "bg-orange-500 text-white")}>{mod.badge}</span> : null}
            </button>
          );
        })}
      </nav>

      <div className="p-3 border-t border-white/10">
        <button type="button" className="w-full flex items-center justify-center gap-2 rounded-xl bg-white/10 px-3 py-2.5 text-xs font-bold text-blue-50 hover:bg-white/15">
          <KeyRound className="w-4 h-4" /> تسجيل الخروج
        </button>
      </div>
    </aside>
  );
}

function PlaceholderModule(props) {
  var modules = props.role === "doctor" ? DOCTOR_MODULES : RECEPTION_MODULES;
  var mod = modules.find(function (m) { return m.key === props.moduleKey; }) || modules[0];
  var Icon = mod.icon;
  return (
    <div className="flex-1 bg-gray-100 p-6 overflow-y-auto">
      <div className="bg-white border border-gray-200 rounded-2xl p-8 text-center max-w-2xl mx-auto mt-16">
        <div className="w-16 h-16 rounded-2xl bg-blue-50 text-[#3d7ab5] flex items-center justify-center mx-auto mb-4">
          <Icon className="w-8 h-8" />
        </div>
        <h2 className="text-xl font-black text-[#1a3a5c]">{mod.label}</h2>
        <p className="text-sm text-gray-500 mt-2">هذه صفحة تجريبية حسب صلاحية المستخدم الحالي. الطبيب يرى وحداته الطبية فقط، والاستقبال يرى التشغيل اليومي والإدارة.</p>
      </div>
    </div>
  );
}

function StatChip(props) {
  return (
    <div className={cn("text-xs px-2.5 py-1 rounded-full flex items-center gap-1 border", props.danger ? "bg-red-900/30 border-red-500/30 text-red-300" : "bg-white/10 border-white/15 text-white/80")}>
      <span className={cn("font-bold", props.danger ? "text-red-300" : "text-white")}>{props.n}</span>
      <span className="text-[10px]">{props.label}</span>
    </div>
  );
}

function AlertBadge(props) {
  var colors = {
    danger: "bg-red-50 border-red-200 text-red-800",
    warning: "bg-amber-50 border-amber-200 text-amber-800",
    info: "bg-blue-50 border-blue-200 text-blue-800",
  };
  return (
    <div className={cn("flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[10px] font-semibold border flex-shrink-0", colors[props.type] || colors.info)}>
      <AlertTriangle className="w-3 h-3" />
      {props.text}
    </div>
  );
}

function PatientListRow(props) {
  var item = props.item;
  var stCls = STATUS_COLORS[item.appointmentStatus] || "bg-gray-100 text-gray-500";
  return (
    <button
      onClick={props.onSelect}
      className={cn("w-full flex items-center gap-2 px-3 py-2.5 text-right border-b border-gray-50 transition-all", props.selected ? "bg-blue-50 border-r-[3px] border-r-[#3d7ab5]" : "hover:bg-gray-50")}
    >
      <div className="w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold flex-shrink-0" style={{ background: item.doctorColor + "20", color: item.doctorColor }}>
        {item.patientName.slice(0, 2)}
      </div>
      <div className="flex-1 min-w-0 text-right">
        <p className={cn("text-xs font-semibold truncate", props.selected ? "text-[#3d7ab5]" : "text-[#1a3a5c]")}>{item.patientName}</p>
        <p className="text-[10px] text-gray-400">{item.appointmentTime} · {item.doctorName}</p>
      </div>
      <span className={cn("text-[9px] px-1.5 py-0.5 rounded-full font-bold flex-shrink-0", stCls)}>{STATUS_LABELS[item.appointmentStatus]}</span>
    </button>
  );
}

function Card(props) {
  return (
    <div className={cn("bg-white rounded-xl border border-gray-200 overflow-hidden", props.className)}>
      {props.title ? (
        <div className="flex items-center justify-between px-4 py-2.5 border-b border-gray-100">
          <p className="text-xs font-bold text-[#1a3a5c] flex items-center gap-1.5">{props.icon}{props.title}</p>
          {props.action}
        </div>
      ) : null}
      <div className="p-4">{props.children}</div>
    </div>
  );
}

function MiniBtn(props) {
  return (
    <button onClick={props.onClick} className={cn("flex items-center justify-center gap-1.5 rounded-lg text-xs font-bold px-3 py-2 transition", props.primary ? "bg-[#3d7ab5] text-white hover:bg-[#2d5e8e]" : "bg-white border border-gray-200 text-[#1a3a5c] hover:bg-gray-50", props.className)}>
      {props.children}
    </button>
  );
}

function CommandButton(props) {
  var Icon = props.icon;
  return (
    <button
      type="button"
      onClick={props.onClick}
      className={cn(
        "h-9 rounded-md px-2.5 text-[10px] font-black flex items-center justify-center gap-1.5 border transition whitespace-nowrap",
        props.primary ? "text-white shadow-sm" : "bg-white text-gray-700 hover:bg-gray-50"
      )}
      style={{
        background: props.primary ? props.accent : "#ffffff",
        borderColor: props.primary ? props.accent : props.accent + "33",
        color: props.primary ? "#ffffff" : props.accent,
      }}
    >
      <Icon className="w-3.5 h-3.5" />
      {props.label}
    </button>
  );
}

function FluentCommandGroup(props) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white overflow-hidden min-w-[235px]">
      <div className="px-2.5 py-1.5 border-b border-gray-100 bg-gray-50">
        <p className="text-[10px] font-black text-[#1a3a5c]">{props.group.title}</p>
        <p className="text-[9px] text-gray-500">{props.group.hint}</p>
      </div>
      <div className="p-2 grid grid-cols-2 gap-1.5">
        {props.group.commands.map(function (cmd) {
          return (
            <CommandButton
              key={cmd.key}
              icon={cmd.icon}
              label={cmd.label}
              accent={cmd.accent}
              primary={cmd.primary}
              onClick={function () { props.onCommand(cmd.key); }}
            />
          );
        })}
      </div>
    </div>
  );
}

function DailyCommandCenter(props) {
  return (
    <div className="bg-[#f8fafc] border-b border-gray-200 px-3 py-2 flex-shrink-0">
      <div className="flex items-center justify-between gap-3 mb-2">
        <div className="flex items-center gap-2">
          <div className="w-8 h-8 rounded-lg bg-[#1a3a5c] text-white flex items-center justify-center">
            <Activity className="w-4 h-4" />
          </div>
          <div>
            <p className="text-xs font-black text-[#1a3a5c]">شريط أوامر التشغيل اليومي</p>
            <p className="text-[10px] text-gray-500">بنمط Microsoft: أوامر أعلى الصفحة، مرتبة حسب سير العمل</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <button onClick={props.onRefresh} className="h-8 flex items-center gap-1 rounded-md border border-gray-200 bg-white px-2.5 text-[10px] font-bold text-gray-600 hover:bg-gray-50"><RefreshCw className="w-3 h-3" /> تحديث</button>
          <button onClick={function () { props.onCommand("printSchedule"); }} className="h-8 flex items-center gap-1 rounded-md border border-gray-200 bg-white px-2.5 text-[10px] font-bold text-gray-600 hover:bg-gray-50"><Printer className="w-3 h-3" /> طباعة</button>
        </div>
      </div>

      <div className="flex gap-2 overflow-x-auto pb-1">
        {DAILY_COMMAND_GROUPS.map(function (group) {
          return <FluentCommandGroup key={group.title} group={group} onCommand={props.onCommand} />;
        })}
      </div>
    </div>
  );
}

function SmartActionBar(props) {
  var item = props.item;
  if (!item) return null;
  var nStatus = nextStatus(item.appointmentStatus);
  var text = nextActionText(item.appointmentStatus);

  return (
    <div className="bg-white border-b border-gray-200 px-4 py-3 flex items-center gap-3">
      <div className="flex-1 min-w-0">
        <p className="text-[10px] font-bold text-gray-500">الإجراء الذكي للمريض المحدد</p>
        <p className="text-sm font-black text-[#1a3a5c] truncate">{item.patientName} — {STATUS_LABELS[item.appointmentStatus]}</p>
      </div>
      {nStatus ? (
        <button type="button" onClick={function () { props.onMove(item, nStatus); }} className="flex items-center gap-2 rounded-xl bg-[#3d7ab5] px-5 py-2.5 text-xs font-black text-white hover:bg-[#2d5e8e] shadow-sm">
          <CheckCircle2 className="w-4 h-4" />
          {text}
        </button>
      ) : (
        <button type="button" className="flex items-center gap-2 rounded-xl bg-green-50 px-5 py-2.5 text-xs font-black text-green-700 border border-green-200">
          <CheckCircle2 className="w-4 h-4" /> مكتمل
        </button>
      )}
      <button type="button" onClick={function () { props.onQuickPay(item); }} className="flex items-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-2.5 text-xs font-black text-emerald-700 hover:bg-emerald-100">
        <CreditCard className="w-4 h-4" /> دفع
      </button>
      <button type="button" onClick={function () { props.onNoShow(item); }} className="flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 text-xs font-black text-red-700 hover:bg-red-100">
        <UserX className="w-4 h-4" /> لم يحضر
      </button>
    </div>
  );
}

function ActivityLogPanel(props) {
  return (
    <Card title="آخر العمليات" icon={<History className="w-4 h-4" />} className="h-full">
      <div className="space-y-2 max-h-[260px] overflow-y-auto pr-1">
        {props.logs.map(function (log) {
          var cls = log.type === "danger" ? "bg-red-50 text-red-700" : log.type === "warning" ? "bg-amber-50 text-amber-700" : "bg-blue-50 text-blue-700";
          return (
            <div key={log.id} className="rounded-lg border border-gray-100 bg-white p-2 text-xs">
              <div className="flex items-center justify-between gap-2">
                <b className="text-[#1a3a5c]">{log.text}</b>
                <span className={cn("rounded-full px-2 py-0.5 text-[9px] font-bold", cls)}>{log.time}</span>
              </div>
            </div>
          );
        })}
      </div>
    </Card>
  );
}

function PatientJourneySection(props) {
  var item = props.item;
  var index = getFlowIndex(item.appointmentStatus);
  var nStatus = nextStatus(item.appointmentStatus);
  return (
    <div className="space-y-4">
      <Card title="مسار المريض اليوم" icon={<Route className="w-4 h-4" />}>
        <div className="grid grid-cols-7 gap-2">
          {flowSteps.map(function (step, i) {
            var Icon = step.icon;
            var active = i === index;
            var done = i < index || item.appointmentStatus === "Completed";
            return (
              <div key={step.key} className={cn("rounded-xl border p-3 text-center", active ? "bg-[#3d7ab5] border-[#3d7ab5] text-white" : done ? "bg-green-50 border-green-100 text-green-700" : "bg-gray-50 border-gray-100 text-gray-400")}>
                <Icon className="w-5 h-5 mx-auto mb-1" />
                <p className="text-[10px] font-bold">{step.label}</p>
              </div>
            );
          })}
        </div>
      </Card>

      <div className="grid grid-cols-4 gap-4">
        <Card title="إجراءات الاستقبال" icon={<Zap className="w-4 h-4" />}>
          <div className="space-y-2">
            {nStatus ? (
              <MiniBtn primary onClick={function () { props.onMove(item, nStatus); }} className="w-full">
                <CheckCircle2 className="w-4 h-4" /> {nextActionText(item.appointmentStatus)}
              </MiniBtn>
            ) : (
              <MiniBtn className="w-full"><CheckCircle2 className="w-4 h-4" /> مكتمل</MiniBtn>
            )}
            <MiniBtn onClick={function () { props.onNoShow(item); }} className="w-full text-red-600"><UserX className="w-4 h-4" /> لم يحضر</MiniBtn>
            <MiniBtn className="w-full"><CalendarDays className="w-4 h-4" /> حجز موعد قادم</MiniBtn>
          </div>
        </Card>

        <Card title="المالية السريعة" icon={<Wallet className="w-4 h-4" />}>
          <div className="space-y-2 text-sm">
            <div className="flex justify-between"><span className="text-gray-500">إجمالي الخطة</span><b>{formatMoney(item.totalTreatmentCost)}</b></div>
            <div className="flex justify-between"><span className="text-gray-500">المدفوع</span><b className="text-green-700">{formatMoney(item.totalPaid)}</b></div>
            <div className="flex justify-between"><span className="text-gray-500">المتبقي</span><b className="text-red-600">{formatMoney(item.outstandingBalance)}</b></div>
            <MiniBtn primary onClick={function () { props.onQuickPay(item); }} className="w-full mt-2"><CreditCard className="w-4 h-4" /> دفع سريع</MiniBtn>
          </div>
        </Card>

        <Card title="التواصل والطباعة" icon={<MessageCircle className="w-4 h-4" />}>
          <div className="space-y-2">
            <MiniBtn className="w-full text-green-700"><MessageCircle className="w-4 h-4" /> واتساب</MiniBtn>
            <MiniBtn className="w-full"><Printer className="w-4 h-4" /> سند قبض</MiniBtn>
            <MiniBtn className="w-full"><Send className="w-4 h-4" /> تعليمات بعد العلاج</MiniBtn>
          </div>
        </Card>
      
        <ActivityLogPanel logs={props.activityLogs || []} />
        <ReceptionNotificationsPanel notifications={props.receptionNotifications || []} />
      </div>

      <Card title="ملخص الحالة" icon={<FileText className="w-4 h-4" />}>
        <div className="grid grid-cols-2 gap-3 text-sm">
          <Info label="الشكوى" value={item.chiefComplaint} />
          <Info label="الخدمة" value={item.serviceName} />
          <Info label="الطبيب" value={item.doctorName} />
          <Info label="الغرفة" value={item.roomName} />
          <Info label="رسوم الاستشارة" value={item.consultationFeeRequired ? item.consultationFeePaid ? "مدفوعة" : "مطلوبة" : "غير مطلوبة"} />
          <Info label="الإجراء التالي" value={nextActionText(item.appointmentStatus)} />
        </div>
      </Card>
    </div>
  );
}

function Info(props) {
  return (
    <div className="rounded-lg bg-gray-50 p-3">
      <p className="text-[10px] text-gray-500 mb-1">{props.label}</p>
      <p className="text-xs font-bold text-[#1a3a5c]">{props.value || "—"}</p>
    </div>
  );
}

function VisitSection(props) {
  var item = props.item;
  var selectedServices = props.selectedServices || [];
  var selectedTotal = selectedServices.reduce(function (sum, service) { return sum + service.price; }, 0);

  return (
    <div className="grid grid-cols-2 gap-4">
      <Card title="تسجيل إجراءات اليوم من الخدمات المسعّرة" icon={<Stethoscope className="w-4 h-4" />}>
        <div className="space-y-3">
          <div className="rounded-lg bg-blue-50 border border-blue-100 p-3">
            <p className="text-[10px] font-bold text-blue-700">المريض</p>
            <p className="text-sm font-black text-[#1a3a5c]">{item ? item.patientName : "—"}</p>
          </div>

          <div className="grid grid-cols-2 gap-2 max-h-52 overflow-y-auto pr-1">
            {SERVICE_CATALOG.map(function (service) {
              var checked = selectedServices.some(function (x) { return x.id === service.id; });
              return (
                <button
                  key={service.id}
                  type="button"
                  onClick={function () { props.onToggleService(service); }}
                  className={cn("rounded-xl border p-3 text-right transition", checked ? "bg-[#3d7ab5] border-[#3d7ab5] text-white" : "bg-white border-gray-200 hover:bg-blue-50")}
                >
                  <div className="flex items-start justify-between gap-2">
                    <div>
                      <p className="text-xs font-black">{service.name}</p>
                      <p className={cn("text-[10px] mt-1", checked ? "text-blue-100" : "text-gray-500")}>{service.category}</p>
                    </div>
                    <b className="text-[10px] whitespace-nowrap">{formatMoney(service.price)}</b>
                  </div>
                </button>
              );
            })}
          </div>

          <textarea className="w-full rounded-lg border border-gray-200 px-3 py-2 text-xs min-h-20" placeholder="ملاحظات الطبيب على الإجراءات التي تمت اليوم" defaultValue="تم تنفيذ الإجراءات المحددة حسب الخطة العلاجية." />
          <textarea className="w-full rounded-lg border border-gray-200 px-3 py-2 text-xs min-h-16" placeholder="تعليمات للمريض بعد العلاج" />
        </div>
      </Card>

      <Card title="فاتورة الزيارة الطبية قبل الإرسال للاستقبال" icon={<Wallet className="w-4 h-4" />}>
        <div className="space-y-3">
          <div className="rounded-xl border border-gray-100 overflow-hidden">
            {selectedServices.length === 0 ? (
              <div className="p-5 text-center text-xs text-gray-400">اختر إجراءً واحداً أو أكثر من قائمة الخدمات المسعّرة.</div>
            ) : selectedServices.map(function (service) {
              return (
                <div key={service.id} className="flex items-center justify-between border-b border-gray-100 px-3 py-2 text-xs last:border-b-0">
                  <span className="font-bold text-[#1a3a5c]">{service.name}</span>
                  <span className="font-black text-gray-700">{formatMoney(service.price)}</span>
                </div>
              );
            })}
          </div>

          <div className="rounded-xl bg-gray-50 p-3 text-sm flex items-center justify-between">
            <span className="font-bold text-gray-600">إجمالي إجراءات اليوم</span>
            <b className="text-[#1a3a5c]">{formatMoney(selectedTotal)}</b>
          </div>

          <div className="rounded-xl bg-amber-50 border border-amber-100 p-3 text-xs text-amber-800 leading-6">
            الطبيب لا يستلم المبلغ هنا؛ فقط يحدد الإجراءات المنفذة من الخدمات المسعّرة، ثم يرسل المريض للاستقبال للتحصيل والسند.
          </div>

          <MiniBtn primary onClick={function () { props.onSaveDoctorVisit(item); }} className="w-full">
            <CheckCircle2 className="w-4 h-4" /> حفظ زيارة اليوم
          </MiniBtn>
          <MiniBtn onClick={function () { props.onFinishDoctorVisit(item); }} className="w-full bg-emerald-50 border-emerald-200 text-emerald-700 hover:bg-emerald-100">
            <Send className="w-4 h-4" /> إنهاء وإرسال للاستقبال
          </MiniBtn>
        </div>
      </Card>
    </div>
  );
}

function OrthoSection() {
  return (
    <Card title="ملف التقويم النشط" icon={<Zap className="w-4 h-4" />}>
      <div className="grid grid-cols-4 gap-3">
        <Info label="رقم الحالة" value="ORTH-2026-014" />
        <Info label="نوع الجهاز" value="Fixed Appliance" />
        <Info label="المرحلة الحالية" value="Space Closure" />
        <Info label="الخطة" value="Extraction case" />
      </div>
    </Card>
  );
}

function HistorySection() {
  return (
    <Card title="التاريخ الطبي المختصر" icon={<History className="w-4 h-4" />}>
      <div className="grid grid-cols-2 gap-3">
        <Info label="أمراض مزمنة" value="لا يوجد" />
        <Info label="حساسية أدوية" value="لا يوجد" />
        <Info label="مشاكل نزف" value="لا" />
        <Info label="صرير الأسنان" value="نعم - بسيط" />
      </div>
    </Card>
  );
}

function PaymentSection(props) {
  var item = props.item;
  return (
    <div className="grid grid-cols-2 gap-4">
      <Card title="تسجيل دفعة" icon={<CreditCard className="w-4 h-4" />}>
        <div className="space-y-3">
          <input className="w-full rounded-lg border border-gray-200 px-3 py-2 text-xs" placeholder="المبلغ" defaultValue="10000" />
          <select className="w-full rounded-lg border border-gray-200 px-3 py-2 text-xs"><option>نقداً</option><option>تحويل</option></select>
          <MiniBtn primary onClick={function () { props.onQuickPay(item); }} className="w-full"><Wallet className="w-4 h-4" /> حفظ الدفعة</MiniBtn>
        </div>
      </Card>
      <Card title="ملخص الحساب" icon={<Wallet className="w-4 h-4" />}>
        <div className="space-y-3 text-sm">
          <div className="flex justify-between"><span>الإجمالي</span><b>{formatMoney(item.totalTreatmentCost)}</b></div>
          <div className="flex justify-between"><span>المدفوع</span><b className="text-green-700">{formatMoney(item.totalPaid)}</b></div>
          <div className="flex justify-between"><span>المتبقي</span><b className="text-red-600">{formatMoney(item.outstandingBalance)}</b></div>
        </div>
      </Card>
    </div>
  );
}

function TimelineSection() {
  return (
    <Card title="سجل سريع" icon={<History className="w-4 h-4" />}>
      <div className="space-y-3">
        {["تم تسجيل الحضور", "تم نداء المريض", "تم حفظ زيارة تقويم", "تم تسجيل دفعة"].map(function (t, i) {
          return <div key={t} className="rounded-lg bg-gray-50 p-3 text-xs"><b>{t}</b><p className="text-gray-500 mt-1">اليوم - 0{9 + i}:15 ص</p></div>;
        })}
      </div>
    </Card>
  );
}

function ScheduleView(props) {
  return <SimpleList title="جدول اليوم" icon={<CalendarDays className="w-4 h-4" />} items={props.items} onSelect={props.onSelect} />;
}

function QueueView(props) {
  var active = props.items.filter(function (x) { return ["Waiting", "Called", "InRoom", "InProgress"].includes(x.appointmentStatus); });
  return <SimpleList title="طابور العيادة" icon={<UserCheck className="w-4 h-4" />} items={active} onSelect={props.onSelect} />;
}

function SimpleList(props) {
  return (
    <div className="flex-1 overflow-y-auto p-4 bg-gray-100">
      <Card title={props.title} icon={props.icon}>
        <div className="space-y-2">
          {props.items.map(function (item) {
            return <button key={item.appointmentId} onClick={function () { props.onSelect(item); }} className="w-full rounded-xl border border-gray-200 bg-white p-3 text-right hover:bg-blue-50"><b className="text-sm text-[#1a3a5c]">{item.patientName}</b><p className="text-xs text-gray-500 mt-1">{item.appointmentTime} · {item.doctorName} · {STATUS_LABELS[item.appointmentStatus]}</p></button>;
          })}
        </div>
      </Card>
    </div>
  );
}

function FinanceView(props) {
  var totalCollected = props.items.reduce(function (sum, p) { return sum + (p.totalPaid || 0); }, 0);
  var totalDue = props.items.reduce(function (sum, p) { return sum + (p.outstandingBalance || 0); }, 0);
  var checkoutItems = props.items.filter(function (p) { return p.appointmentStatus === "Checkout"; });
  var notifications = props.notifications || [];

  return (
    <div className="flex-1 overflow-y-auto p-4 bg-gray-100 space-y-4">
      <div className="grid grid-cols-4 gap-3">
        <Card><p className="text-xs text-gray-500">تحصيلات</p><b className="text-green-700">{formatMoney(totalCollected)}</b></Card>
        <Card><p className="text-xs text-gray-500">متبقي</p><b className="text-red-600">{formatMoney(totalDue)}</b></Card>
        <Card><p className="text-xs text-gray-500">مرضى عند الخروج</p><b>{checkoutItems.length}</b></Card>
        <Card><p className="text-xs text-gray-500">إشعارات من الطبيب</p><b>{notifications.length}</b></Card>
      </div>

      <Card title="مرضى جاهزون للدفع بعد إنهاء الطبيب" icon={<Bell className="w-4 h-4" />}>
        <div className="space-y-2">
          {checkoutItems.length === 0 ? <p className="text-xs text-gray-400 text-center py-4">لا يوجد مرضى جاهزون للدفع حالياً.</p> : null}
          {checkoutItems.map(function (item) {
            return (
              <button key={item.appointmentId} onClick={function () { props.onSelect(item); }} className="w-full rounded-xl border border-emerald-100 bg-emerald-50 p-3 text-right hover:bg-emerald-100">
                <div className="flex items-center justify-between gap-3">
                  <div>
                    <b className="text-sm text-[#1a3a5c]">{item.patientName}</b>
                    <p className="text-xs text-gray-500 mt-1">{item.doctorName} · {item.lastDoctorVisitServices || item.serviceName}</p>
                  </div>
                  <b className="text-xs text-red-600">{formatMoney(item.outstandingBalance)}</b>
                </div>
              </button>
            );
          })}
        </div>
      </Card>
    </div>
  );
}

function ReceptionNotificationsPanel(props) {
  var notifications = props.notifications || [];
  return (
    <Card title="إشعارات الاستقبال من الطبيب" icon={<Bell className="w-4 h-4" />} className="h-full">
      <div className="space-y-2 max-h-[220px] overflow-y-auto pr-1">
        {notifications.length === 0 ? <p className="text-xs text-gray-400 text-center py-4">لا توجد إشعارات جديدة</p> : null}
        {notifications.map(function (note) {
          var cls = note.type === "success" ? "bg-emerald-50 text-emerald-700" : note.type === "warning" ? "bg-amber-50 text-amber-700" : "bg-blue-50 text-blue-700";
          return (
            <div key={note.id} className="rounded-lg border border-gray-100 bg-white p-2 text-xs">
              <div className="flex items-start justify-between gap-2">
                <div>
                  <b className="text-[#1a3a5c]">{note.patientName}</b>
                  <p className="text-gray-500 mt-1 leading-5">{note.message}</p>
                </div>
                <span className={cn("rounded-full px-2 py-0.5 text-[9px] font-bold whitespace-nowrap", cls)}>{note.time}</span>
              </div>
            </div>
          );
        })}
      </div>
    </Card>
  );
}

function MessagesView() {
  return <EmptyView icon={MessageCircle} title="اختر مريضاً من القائمة لعرض الرسائل" />;
}

function DoctorCommandButton(props) {
  var Icon = props.icon;
  return (
    <button
      type="button"
      onClick={props.onClick}
      className={cn(
        "h-9 rounded-md px-3 text-xs font-semibold flex items-center gap-2 border transition",
        props.primary
          ? "bg-[#2563eb] border-[#2563eb] text-white hover:bg-[#1d4ed8]"
          : "bg-white border-gray-200 text-gray-700 hover:bg-gray-50"
      )}
    >
      <Icon className="w-4 h-4" />
      {props.label}
    </button>
  );
}

function DoctorPatientQueueItem(props) {
  var item = props.item;
  var selected = props.selected;
  return (
    <button
      type="button"
      onClick={props.onSelect}
      className={cn(
        "w-full text-right rounded-lg border p-3 transition",
        selected ? "bg-[#eff6ff] border-[#93c5fd] shadow-sm" : "bg-white border-gray-200 hover:bg-gray-50"
      )}
    >
      <div className="flex items-start gap-3">
        <div className="w-9 h-9 rounded-full flex items-center justify-center text-xs font-black text-white flex-shrink-0" style={{ background: item.doctorColor }}>
          {item.patientName.slice(0, 2)}
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-center justify-between gap-2">
            <p className="text-sm font-bold text-[#1f2937] truncate">{item.patientName}</p>
            <span className={cn("text-[10px] px-2 py-0.5 rounded-full font-bold", STATUS_COLORS[item.appointmentStatus])}>{STATUS_LABELS[item.appointmentStatus]}</span>
          </div>
          <p className="text-[11px] text-gray-500 mt-1">{item.appointmentTime} · ملف {item.patientNumber}</p>
          <p className="text-[11px] text-gray-500 mt-1 truncate">{item.serviceName}</p>
        </div>
      </div>
    </button>
  );
}

function DoctorInfoTile(props) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-3">
      <p className="text-[10px] font-semibold text-gray-500">{props.label}</p>
      <p className={cn("mt-1 text-sm font-black", props.danger ? "text-red-600" : "text-[#111827]")}>{props.value}</p>
    </div>
  );
}

function DoctorClinicView(props) {
  const [openScreen, setOpenScreen] = useState(null);

  var doctorPatients = props.items.filter(function (item) {
    return item.doctorName === props.doctorName && ["Called", "InRoom", "InProgress", "Checkout"].includes(item.appointmentStatus);
  });
  var selected = props.selectedItem && props.selectedItem.doctorName === props.doctorName ? props.selectedItem : doctorPatients[0];
  var selectedServices = selected ? props.doctorVisitServices[selected.appointmentId] || [] : [];
  var servicesTotal = selectedServices.reduce(function (sum, service) { return sum + service.price; }, 0);

  function getScreenTitle(key) {
    if (key === "exam") return "الفحص والتشخيص";
    if (key === "procedures") return "الإجراءات المسعّرة المنفذة اليوم";
    if (key === "plan") return "خطة العلاج";
    if (key === "ortho") return "متابعة التقويم";
    if (key === "images") return "الصور والأشعة";
    if (key === "prescription") return "الوصفة والتعليمات";
    if (key === "lab") return "طلب معمل أو إحالة";
    if (key === "followup") return "موعد المتابعة";
    return "شاشة الطبيب";
  }

  function renderOpenScreen() {
    if (!openScreen || !selected) return null;

    return (
      <div className="fixed inset-0 z-50 bg-slate-950/40 p-6 flex items-center justify-center">
        <div className="w-full max-w-5xl max-h-[88vh] overflow-hidden rounded-2xl bg-white shadow-2xl border border-gray-200 flex flex-col">
          <div className="h-14 border-b border-gray-200 px-5 flex items-center justify-between flex-shrink-0">
            <div>
              <p className="text-sm font-black text-[#111827]">{getScreenTitle(openScreen)}</p>
              <p className="text-[10px] text-gray-500">{selected.patientName} · ملف {selected.patientNumber}</p>
            </div>
            <button type="button" onClick={function () { setOpenScreen(null); }} className="rounded-lg border border-gray-200 px-3 py-1.5 text-xs font-bold text-gray-600 hover:bg-gray-50">
              إغلاق
            </button>
          </div>

          <div className="flex-1 overflow-y-auto p-5 bg-gray-50">
            {openScreen === "exam" ? (
              <div className="grid grid-cols-2 gap-4">
                <Card title="الفحص السريري" icon={<Stethoscope className="w-4 h-4" />}>
                  <div className="space-y-3">
                    <textarea className="w-full rounded-xl border border-gray-200 px-3 py-2 text-xs min-h-28" placeholder="Extraoral / Intraoral examination" defaultValue="الفحص السريري العام: لا توجد علامات خطورة واضحة، يتم توثيق الملاحظات حسب الحالة." />
                    <textarea className="w-full rounded-xl border border-gray-200 px-3 py-2 text-xs min-h-24" placeholder="Chief complaint and findings" defaultValue={selected.chiefComplaint} />
                  </div>
                </Card>
                <Card title="التشخيص" icon={<FileText className="w-4 h-4" />}>
                  <div className="space-y-3">
                    <input className="w-full rounded-xl border border-gray-200 px-3 py-2 text-xs" placeholder="التشخيص الأساسي" defaultValue={selected.serviceName} />
                    <textarea className="w-full rounded-xl border border-gray-200 px-3 py-2 text-xs min-h-32" placeholder="ملاحظات التشخيص التفصيلية" />
                  </div>
                </Card>
              </div>
            ) : null}

            {openScreen === "procedures" ? (
              <div className="grid grid-cols-[1fr_320px] gap-4">
                <Card title="اختر الإجراءات من كتالوج الخدمات المسعّرة" icon={<Wallet className="w-4 h-4" />}>
                  <div className="grid grid-cols-2 gap-2">
                    {SERVICE_CATALOG.map(function (service) {
                      var checked = selectedServices.some(function (x) { return x.id === service.id; });
                      return (
                        <button key={service.id} type="button" onClick={function () { props.onToggleService(selected, service); }} className={cn("rounded-xl border p-3 text-right transition", checked ? "bg-[#eff6ff] border-[#2563eb]" : "bg-white border-gray-200 hover:bg-gray-50")}>
                          <div className="flex items-start justify-between gap-2">
                            <div>
                              <p className={cn("text-xs font-black", checked ? "text-[#2563eb]" : "text-[#111827]")}>{service.name}</p>
                              <p className="text-[10px] text-gray-500 mt-1">{service.category}</p>
                            </div>
                            <b className="text-xs text-gray-700 whitespace-nowrap">{formatMoney(service.price)}</b>
                          </div>
                        </button>
                      );
                    })}
                  </div>
                </Card>
                <Card title="ملخص الإجراءات المختارة" icon={<CreditCard className="w-4 h-4" />}>
                  <div className="space-y-2">
                    {selectedServices.length === 0 ? <p className="text-xs text-gray-400 text-center py-8">لم يتم اختيار إجراءات بعد</p> : null}
                    {selectedServices.map(function (service) {
                      return <div key={service.id} className="rounded-lg border border-gray-100 bg-white p-2 text-xs flex justify-between gap-2"><span className="font-bold">{service.name}</span><b>{formatMoney(service.price)}</b></div>;
                    })}
                    <div className="rounded-xl bg-blue-50 border border-blue-100 p-3 flex justify-between text-sm mt-3"><span className="font-bold text-blue-800">الإجمالي</span><b className="text-blue-900">{formatMoney(servicesTotal)}</b></div>
                    <MiniBtn primary onClick={function () { props.onSaveDoctorVisit(selected); }} className="w-full"><CheckCircle2 className="w-4 h-4" /> حفظ الإجراءات</MiniBtn>
                  </div>
                </Card>
              </div>
            ) : null}

            {openScreen === "plan" ? (
              <div className="grid grid-cols-2 gap-4">
                <Card title="خطة العلاج الحالية" icon={<Zap className="w-4 h-4" />}>
                  <textarea className="w-full rounded-xl border border-gray-200 px-3 py-2 text-xs min-h-56" defaultValue="الخطة الحالية: الاستمرار حسب الخطة العلاجية، تقييم الاستجابة، وتعديل الخطة عند الحاجة." />
                </Card>
                <Card title="الأهداف والمتابعة" icon={<CheckCircle2 className="w-4 h-4" />}>
                  <textarea className="w-full rounded-xl border border-gray-200 px-3 py-2 text-xs min-h-56" placeholder="أهداف الزيارة القادمة، نقاط يجب مراجعتها، تعليمات للطبيب أو الاستقبال" />
                </Card>
              </div>
            ) : null}

            {openScreen === "ortho" ? (
              <div className="grid grid-cols-4 gap-3">
                <Info label="مرحلة التقويم" value="Space closure / Finishing" />
                <Info label="السلك العلوي" value="0.019 × 0.025 SS" />
                <Info label="السلك السفلي" value="0.018 NiTi" />
                <Info label="نسبة التقدم" value="65%" />
                <div className="col-span-4"><Card title="ملاحظات التقويم" icon={<Zap className="w-4 h-4" />}><textarea className="w-full rounded-xl border border-gray-200 px-3 py-2 text-xs min-h-36" placeholder="ملاحظات حركة الأسنان، المطاطات، الأسلاك، البراكتات، خطة الشهر القادم" /></Card></div>
              </div>
            ) : null}

            {openScreen === "images" ? (
              <div className="grid grid-cols-3 gap-4">
                {["صور قبل العلاج", "صور اليوم", "أشعة بانوراما", "أشعة سيفالومترية", "صور داخل الفم", "ملفات أخرى"].map(function (title) {
                  return <div key={title} className="rounded-2xl border border-dashed border-gray-300 bg-white p-8 text-center text-xs text-gray-400"><FileText className="w-8 h-8 mx-auto mb-2 opacity-40" />{title}<br /><span className="text-[10px]">رفع / عرض</span></div>;
                })}
              </div>
            ) : null}

            {openScreen === "prescription" ? (
              <div className="grid grid-cols-2 gap-4">
                <Card title="الوصفة" icon={<FileText className="w-4 h-4" />}>
                  <textarea className="w-full rounded-xl border border-gray-200 px-3 py-2 text-xs min-h-56" placeholder="اكتب الوصفة الطبية إن وجدت" />
                </Card>
                <Card title="تعليمات بعد العلاج" icon={<MessageCircle className="w-4 h-4" />}>
                  <textarea className="w-full rounded-xl border border-gray-200 px-3 py-2 text-xs min-h-56" defaultValue="يرجى الالتزام بالتعليمات، التواصل مع المركز عند حدوث ألم شديد أو تورم، والحضور في موعد المتابعة." />
                </Card>
              </div>
            ) : null}

            {openScreen === "lab" ? (
              <div className="grid grid-cols-2 gap-4">
                <Card title="طلب معمل" icon={<FlaskConical className="w-4 h-4" />}>
                  <input className="w-full rounded-xl border border-gray-200 px-3 py-2 text-xs mb-3" placeholder="نوع العمل: Zircon / Emax / Retainer" />
                  <textarea className="w-full rounded-xl border border-gray-200 px-3 py-2 text-xs min-h-40" placeholder="تعليمات المعمل واللون وموعد التسليم" />
                </Card>
                <Card title="إحالة أو طلب داخلي" icon={<Send className="w-4 h-4" />}>
                  <select className="w-full rounded-xl border border-gray-200 px-3 py-2 text-xs mb-3"><option>إحالة لجراحة الفم</option><option>إحالة للعصب</option><option>إحالة للتركيبات</option></select>
                  <textarea className="w-full rounded-xl border border-gray-200 px-3 py-2 text-xs min-h-40" placeholder="سبب الإحالة أو الطلب" />
                </Card>
              </div>
            ) : null}

            {openScreen === "followup" ? (
              <div className="grid grid-cols-2 gap-4">
                <Card title="اقتراح موعد المتابعة" icon={<CalendarDays className="w-4 h-4" />}>
                  <input type="date" className="w-full rounded-xl border border-gray-200 px-3 py-2 text-xs mb-3" />
                  <input type="time" className="w-full rounded-xl border border-gray-200 px-3 py-2 text-xs mb-3" />
                  <textarea className="w-full rounded-xl border border-gray-200 px-3 py-2 text-xs min-h-28" placeholder="سبب موعد المتابعة" />
                </Card>
                <Card title="رسالة المتابعة" icon={<MessageCircle className="w-4 h-4" />}>
                  <textarea className="w-full rounded-xl border border-gray-200 px-3 py-2 text-xs min-h-44" defaultValue={"مرحباً " + selected.patientName + "، تم تحديد موعد متابعة لكم في مركز الدكتور عقلان الكامل. نتمنى لكم دوام الصحة."} />
                </Card>
              </div>
            ) : null}
          </div>

          <div className="h-14 border-t border-gray-200 px-5 flex items-center justify-between flex-shrink-0 bg-white">
            <p className="text-[10px] text-gray-500">كل شاشة تحفظ ضمن زيارة اليوم للمريض المحدد.</p>
            <div className="flex items-center gap-2">
              <button type="button" onClick={function () { props.onSaveDoctorVisit(selected); }} className="rounded-lg border border-gray-200 px-4 py-2 text-xs font-black text-gray-700 hover:bg-gray-50">حفظ</button>
              <button type="button" onClick={function () { props.onFinishDoctorVisit(selected); setOpenScreen(null); }} className="rounded-lg bg-emerald-600 px-4 py-2 text-xs font-black text-white hover:bg-emerald-700">إنهاء وإرسال للاستقبال</button>
            </div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col flex-1 overflow-hidden bg-[#f3f4f6]">
      <div className="h-14 bg-white border-b border-gray-200 flex items-center justify-between px-5 flex-shrink-0">
        <div className="flex items-center gap-3">
          <div className="w-9 h-9 rounded-lg bg-[#2563eb] text-white flex items-center justify-center"><Stethoscope className="w-5 h-5" /></div>
          <div>
            <p className="text-sm font-black text-[#111827]">عيادة الطبيب</p>
            <p className="text-[10px] text-gray-500">الصفحة الرئيسية هادئة، وكل زر يفتح شاشة عمل كاملة</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <DoctorCommandButton icon={RefreshCw} label="تحديث" />
          <DoctorCommandButton icon={FileText} label="حفظ الزيارة" onClick={function () { props.onSaveDoctorVisit(selected); }} />
          <DoctorCommandButton icon={Send} label="إنهاء وإرسال للاستقبال" primary onClick={function () { props.onFinishDoctorVisit(selected); }} />
        </div>
      </div>

      <div className="flex-1 overflow-y-auto p-5">
        <div className="mx-auto max-w-6xl space-y-4">
          <section className="rounded-2xl border border-gray-200 bg-white p-4 shadow-sm">
            <div className="flex items-center justify-between gap-4 mb-4">
              <div>
                <p className="text-sm font-black text-[#111827]">مرضاي اليوم</p>
                <p className="text-[10px] text-gray-500 mt-1">اختيار المريض من شريط واحد، ثم فتح شاشة العمل المطلوبة</p>
              </div>
              <div className="flex items-center gap-2 text-[11px] text-gray-500">
                <span>الطبيب: <b className="text-[#111827]">{props.doctorName}</b></span>
                <span className="w-1 h-1 rounded-full bg-gray-300" />
                <span>عدد المرضى: <b className="text-[#111827]">{doctorPatients.length}</b></span>
              </div>
            </div>

            <div className="flex gap-2 overflow-x-auto pb-1">
              {doctorPatients.length === 0 ? <div className="w-full rounded-xl border border-dashed border-gray-300 p-6 text-center text-xs text-gray-400">لا يوجد مرضى محولين للطبيب حالياً</div> : null}
              {doctorPatients.map(function (item) {
                var active = selected && selected.appointmentId === item.appointmentId;
                return (
                  <button key={item.appointmentId} type="button" onClick={function () { props.setSelectedItem(item); }} className={cn("min-w-[220px] rounded-xl border p-3 text-right transition", active ? "bg-[#eff6ff] border-[#2563eb] shadow-sm" : "bg-white border-gray-200 hover:bg-gray-50")}>
                    <div className="flex items-start gap-3">
                      <div className="w-9 h-9 rounded-full flex items-center justify-center text-xs font-black text-white flex-shrink-0" style={{ background: item.doctorColor }}>{item.patientName.slice(0, 2)}</div>
                      <div className="min-w-0 flex-1">
                        <p className={cn("text-xs font-black truncate", active ? "text-[#2563eb]" : "text-[#111827]")}>{item.patientName}</p>
                        <p className="text-[10px] text-gray-500 mt-1">{item.appointmentTime} · ملف {item.patientNumber}</p>
                        <span className={cn("inline-block mt-2 text-[9px] px-2 py-0.5 rounded-full font-bold", STATUS_COLORS[item.appointmentStatus])}>{STATUS_LABELS[item.appointmentStatus]}</span>
                      </div>
                    </div>
                  </button>
                );
              })}
            </div>
          </section>

          {selected ? (
            <>
              <section className="rounded-2xl border border-gray-200 bg-white overflow-hidden shadow-sm">
                <div className="p-5 border-b border-gray-100 flex items-center justify-between gap-4">
                  <div className="flex items-center gap-3 min-w-0">
                    <div className="w-14 h-14 rounded-2xl flex items-center justify-center text-white font-black flex-shrink-0" style={{ background: selected.doctorColor }}>{selected.patientName.slice(0, 2)}</div>
                    <div className="min-w-0">
                      <h2 className="text-2xl font-black text-[#111827] truncate">{selected.patientName}</h2>
                      <p className="text-xs text-gray-500 mt-1 truncate">ملف {selected.patientNumber} · {selected.serviceName} · {selected.roomName}</p>
                    </div>
                  </div>
                  <span className={cn("text-xs px-3 py-1 rounded-full font-bold flex-shrink-0", STATUS_COLORS[selected.appointmentStatus])}>{STATUS_LABELS[selected.appointmentStatus]}</span>
                </div>
                <div className="grid grid-cols-4 gap-3 p-4 bg-[#fafafa]">
                  <DoctorInfoTile label="الغرفة" value={selected.roomName} />
                  <DoctorInfoTile label="وقت الموعد" value={selected.appointmentTime} />
                  <DoctorInfoTile label="إجراءات مختارة" value={selectedServices.length + " إجراء"} />
                  <DoctorInfoTile label="إجمالي اليوم" value={formatMoney(servicesTotal)} />
                </div>
              </section>

              <section className="rounded-2xl border border-gray-200 bg-white p-5 shadow-sm">
                <div className="mb-4">
                  <p className="text-sm font-black text-[#111827]">ماذا يريد الطبيب أن يعمل؟</p>
                  <p className="text-[10px] text-gray-500 mt-1">كل زر يفتح شاشة كاملة خاصة بهذه المهمة</p>
                </div>
                <div className="grid grid-cols-4 gap-3">
                  <button onClick={function () { setOpenScreen("exam"); }} className="rounded-2xl border border-gray-200 bg-white p-5 text-right hover:bg-blue-50 hover:border-blue-200 transition"><Stethoscope className="w-6 h-6 text-[#2563eb] mb-3" /><p className="font-black text-sm text-[#111827]">الفحص والتشخيص</p><p className="text-[10px] text-gray-500 mt-1">Chief complaint, findings, diagnosis</p></button>
                  <button onClick={function () { setOpenScreen("procedures"); }} className="rounded-2xl border border-gray-200 bg-white p-5 text-right hover:bg-blue-50 hover:border-blue-200 transition"><Wallet className="w-6 h-6 text-[#2563eb] mb-3" /><p className="font-black text-sm text-[#111827]">الإجراءات المسعّرة</p><p className="text-[10px] text-gray-500 mt-1">اختر الخدمات المنفذة اليوم</p></button>
                  <button onClick={function () { setOpenScreen("plan"); }} className="rounded-2xl border border-gray-200 bg-white p-5 text-right hover:bg-blue-50 hover:border-blue-200 transition"><Zap className="w-6 h-6 text-[#2563eb] mb-3" /><p className="font-black text-sm text-[#111827]">خطة العلاج</p><p className="text-[10px] text-gray-500 mt-1">الخطة والأهداف القادمة</p></button>
                  <button onClick={function () { setOpenScreen("ortho"); }} className="rounded-2xl border border-gray-200 bg-white p-5 text-right hover:bg-blue-50 hover:border-blue-200 transition"><Route className="w-6 h-6 text-[#2563eb] mb-3" /><p className="font-black text-sm text-[#111827]">متابعة التقويم</p><p className="text-[10px] text-gray-500 mt-1">أسلاك، مطاطات، مرحلة العلاج</p></button>
                  <button onClick={function () { setOpenScreen("images"); }} className="rounded-2xl border border-gray-200 bg-white p-5 text-right hover:bg-blue-50 hover:border-blue-200 transition"><FileText className="w-6 h-6 text-[#2563eb] mb-3" /><p className="font-black text-sm text-[#111827]">الصور والأشعة</p><p className="text-[10px] text-gray-500 mt-1">رفع أو عرض الملفات</p></button>
                  <button onClick={function () { setOpenScreen("prescription"); }} className="rounded-2xl border border-gray-200 bg-white p-5 text-right hover:bg-blue-50 hover:border-blue-200 transition"><MessageCircle className="w-6 h-6 text-[#2563eb] mb-3" /><p className="font-black text-sm text-[#111827]">الوصفة والتعليمات</p><p className="text-[10px] text-gray-500 mt-1">Prescription & instructions</p></button>
                  <button onClick={function () { setOpenScreen("lab"); }} className="rounded-2xl border border-gray-200 bg-white p-5 text-right hover:bg-blue-50 hover:border-blue-200 transition"><FlaskConical className="w-6 h-6 text-[#2563eb] mb-3" /><p className="font-black text-sm text-[#111827]">طلب معمل / إحالة</p><p className="text-[10px] text-gray-500 mt-1">Lab order or referral</p></button>
                  <button onClick={function () { setOpenScreen("followup"); }} className="rounded-2xl border border-gray-200 bg-white p-5 text-right hover:bg-blue-50 hover:border-blue-200 transition"><CalendarDays className="w-6 h-6 text-[#2563eb] mb-3" /><p className="font-black text-sm text-[#111827]">موعد المتابعة</p><p className="text-[10px] text-gray-500 mt-1">اقتراح موعد ورسالة</p></button>
                </div>
              </section>

              <section className="rounded-2xl border border-gray-200 bg-white p-5 shadow-sm flex items-center justify-between gap-4">
                <div>
                  <p className="text-sm font-black text-[#111827]">إنهاء زيارة الطبيب</p>
                  <p className="text-[10px] text-gray-500 mt-1">بعد اكتمال الفحص والإجراءات، يتم إرسال المريض للاستقبال مع إشعار وقيمة الإجراءات.</p>
                </div>
                <div className="flex items-center gap-2">
                  <button type="button" onClick={function () { props.onSaveDoctorVisit(selected); }} className="rounded-xl border border-gray-200 bg-white px-5 py-3 text-xs font-black text-gray-700 hover:bg-gray-50"><FileText className="w-4 h-4 inline ml-1" /> حفظ الزيارة</button>
                  <button type="button" onClick={function () { props.onFinishDoctorVisit(selected); }} className="rounded-xl bg-emerald-600 px-5 py-3 text-xs font-black text-white hover:bg-emerald-700"><Send className="w-4 h-4 inline ml-1" /> إنهاء وإرسال للاستقبال</button>
                </div>
              </section>
            </>
          ) : <EmptyView icon={Stethoscope} title="لا يوجد مريض محدد للطبيب" />}
        </div>
      </div>

      {renderOpenScreen()}
    </div>
  );
}

function AlertsView(props) {
  return (
    <div className="flex-1 overflow-y-auto p-4 bg-gray-100">
      <Card title="التنبيهات النشطة" icon={<Bell className="w-4 h-4" />}>
        <div className="space-y-3">
          {props.alerts.map(function (a) { return <AlertBadge key={a.id} type={a.type} text={(a.patientName ? a.patientName + " — " : "") + a.message} />; })}
        </div>
      </Card>
    </div>
  );
}

function EmptyView(props) {
  var Icon = props.icon;
  return <div className="flex-1 flex items-center justify-center bg-gray-100 text-gray-400"><div className="text-center"><Icon className="w-12 h-12 mx-auto mb-3 opacity-30" /><p className="text-sm">{props.title}</p></div></div>;
}

export default function DailyOperationsReceptionMockup() {
  const [role, setRole] = useState("reception");
  const [activeModule, setActiveModule] = useState("daily");
  const [topTab, setTopTab] = useState("journey");
  const [patientTab, setPatientTab] = useState("journey");
  const [items, setItems] = useState(patientsSeed);
  const [selectedItem, setSelectedItem] = useState(patientsSeed[0]);
  const [search, setSearch] = useState("");
  const [doctor, setDoctor] = useState("");
  const [clock, setClock] = useState("09:25 ص");
  const [lastCommand, setLastCommand] = useState("جاهز للتشغيل");
  const [activityLogs, setActivityLogs] = useState(activitySeed);
  const [receptionNotifications, setReceptionNotifications] = useState(receptionNotificationsSeed);
  const [doctorVisitServices, setDoctorVisitServices] = useState({});

  const filteredItems = useMemo(function () {
    return items.filter(function (item) {
      var matchSearch = !search || item.patientName.includes(search) || item.patientNumber.includes(search) || item.patientPhone.includes(search);
      var matchDoctor = !doctor || item.doctorName === doctor;
      return matchSearch && matchDoctor;
    });
  }, [items, search, doctor]);

  const stats = useMemo(function () {
    return {
      appointments: items.length,
      waiting: items.filter(function (i) { return i.appointmentStatus === "Waiting"; }).length,
      overdue: items.filter(function (i) { return i.overdueAmount > 0 || i.outstandingBalance > 0; }).length,
      messages: 3,
      alerts: alertsSeed.length,
    };
  }, [items]);

  function updateItem(id, changes) {
    setItems(function (prev) { return prev.map(function (item) { return item.appointmentId === id ? Object.assign({}, item, changes) : item; }); });
    if (selectedItem && selectedItem.appointmentId === id) setSelectedItem(Object.assign({}, selectedItem, changes));
  }

  function addActivity(text, type) {
    var now = clock;
    setActivityLogs(function (prev) {
      return [{ id: "ac" + Date.now(), time: now, text: text, type: type || "info" }].concat(prev).slice(0, 8);
    });
  }

  function addReceptionNotification(patient, message, type) {
    var now = clock;
    setReceptionNotifications(function (prev) {
      return [{ id: "rn" + Date.now(), time: now, patientName: patient.patientName, message: message, type: type || "info" }].concat(prev).slice(0, 8);
    });
  }

  function handleToggleService(patient, service) {
    if (!patient) return;
    var current = doctorVisitServices[patient.appointmentId] || [];
    var exists = current.some(function (x) { return x.id === service.id; });
    var nextServices = exists ? current.filter(function (x) { return x.id !== service.id; }) : current.concat(service);
    setDoctorVisitServices(function (prev) {
      return Object.assign({}, prev, { [patient.appointmentId]: nextServices });
    });
    addActivity((exists ? "تم إلغاء إجراء: " : "تم تحديد إجراء: ") + service.name + " — " + patient.patientName, "info");
  }

  function handleSaveDoctorVisit(patient) {
    if (!patient) return;
    var services = doctorVisitServices[patient.appointmentId] || [];
    if (services.length === 0) {
      addActivity("لم يتم اختيار إجراءات لزيارة " + patient.patientName, "warning");
      return;
    }
    var total = services.reduce(function (sum, service) { return sum + service.price; }, 0);
    updateItem(patient.appointmentId, {
      lastDoctorVisitTotal: total,
      lastDoctorVisitServices: services.map(function (s) { return s.name; }).join("، "),
    });
    addActivity("تم حفظ إجراءات الطبيب بقيمة " + formatMoney(total) + " — " + patient.patientName, "info");
  }

  function handleFinishDoctorVisit(patient) {
    if (!patient) return;
    var services = doctorVisitServices[patient.appointmentId] || [];
    var total = services.reduce(function (sum, service) { return sum + service.price; }, 0);
    updateItem(patient.appointmentId, {
      appointmentStatus: "Checkout",
      roomName: "الخروج",
      lastDoctorVisitTotal: total,
      lastDoctorVisitServices: services.map(function (s) { return s.name; }).join("، "),
      outstandingBalance: patient.outstandingBalance + total,
    });
    setLastCommand("الطبيب أنهى الزيارة وأرسل المريض للاستقبال");
    addActivity("الطبيب أنهى زيارة " + patient.patientName + " وأرسلها للاستقبال", "info");
    addReceptionNotification(patient, "الطبيب أنهى الزيارة. الإجراءات: " + (services.length ? services.map(function (s) { return s.name; }).join("، ") : "لم تحدد") + ". المطلوب تحصيله: " + formatMoney(total), "success");
  }

  function handleMove(item, status) {
    updateItem(item.appointmentId, { appointmentStatus: status, roomName: status === "InRoom" || status === "InProgress" ? "غرفة 1" : item.roomName });
    var text = nextActionText(item.appointmentStatus);
    setLastCommand(text);
    addActivity(text + " — " + item.patientName, "info");
  }

  function handleNoShow(item) {
    updateItem(item.appointmentId, { appointmentStatus: "NoShow" });
    setLastCommand("تم تسجيل غياب المريض");
    addActivity("تم تسجيل غياب " + item.patientName, "danger");
  }

  function handleQuickPay(item) {
    var amount = Math.min(item.outstandingBalance || 10000, 10000);
    updateItem(item.appointmentId, { totalPaid: item.totalPaid + amount, outstandingBalance: Math.max(0, item.outstandingBalance - amount) });
    setLastCommand("تم تسجيل دفعة سريعة");
    addActivity("تم تسجيل دفعة " + formatMoney(amount) + " — " + item.patientName, "info");
  }

  function handleCommand(commandKey) {
    var item = selectedItem || filteredItems[0];
    var command = DAILY_COMMANDS.find(function (c) { return c.key === commandKey; });
    setLastCommand(command ? command.label : commandKey);

    if (commandKey === "walkIn") { addActivity("تم فتح نافذة مريض مشي جديد", "info"); setTopTab("schedule"); return; }
    if (commandKey === "newPatient") { addActivity("تم فتح نموذج مريض جديد", "info"); setTopTab("schedule"); return; }
    if (commandKey === "newAppointment") { addActivity("تم فتح نافذة موعد جديد", "info"); setTopTab("schedule"); return; }
    if (commandKey === "bulkReminders") { setTopTab("messages"); addActivity("تم فتح تذكيرات مواعيد الغد", "info"); return; }
    if (commandKey === "printSchedule") { addActivity("تم تجهيز طباعة جدول اليوم", "info"); return; }
    if (commandKey === "sound") { addActivity("تم تبديل حالة صوت النداء", "info"); return; }
    if (commandKey === "shortcuts") { addActivity("تم عرض اختصارات التشغيل اليومي", "info"); return; }

    if (!item) return;
    if (commandKey === "openFile") { addActivity("تم فتح ملف " + item.patientName, "info"); setTopTab("journey"); return; }
    if (commandKey === "archive") { addActivity("تم فتح تسجيل الأرشيف للمريض " + item.patientName, "info"); setTopTab("journey"); return; }
    if (commandKey === "checkIn") handleMove(item, "Arrived");
    if (commandKey === "sendQueue") handleMove(item, "Waiting");
    if (commandKey === "call") handleMove(item, "Called");
    if (commandKey === "enterRoom") handleMove(item, "InRoom");
    if (commandKey === "startVisit") handleMove(item, "InProgress");
    if (commandKey === "changeRoom") { updateItem(item.appointmentId, { roomName: item.roomName === "غرفة 1" ? "غرفة 2" : "غرفة 1" }); addActivity("تم تغيير غرفة " + item.patientName, "info"); }
    if (commandKey === "cancelQueue") { updateItem(item.appointmentId, { appointmentStatus: "Arrived", roomName: "الاستقبال" }); addActivity("تم إلغاء " + item.patientName + " من الطابور", "warning"); }
    if (commandKey === "finishVisit") handleMove(item, "Checkout");
    if (commandKey === "quickPay") handleQuickPay(item);
    if (commandKey === "draftInvoice") { setTopTab("finance"); addActivity("تم إنشاء فاتورة مسودة لـ " + item.patientName, "info"); }
    if (commandKey === "completeCheckout") handleMove(item, "Completed");
    if (commandKey === "noShow") handleNoShow(item);
    if (commandKey === "receipt") { setTopTab("finance"); addActivity("تم فتح سندات وفواتير " + item.patientName, "info"); }
    if (commandKey === "whatsapp") { setTopTab("messages"); addActivity("تم فتح رسائل واتساب " + item.patientName, "info"); }
    if (commandKey === "nextVisit") { setTopTab("schedule"); addActivity("تم فتح حجز موعد متابعة لـ " + item.patientName, "info"); }
    if (commandKey === "dailyReport") { setTopTab("alerts"); addActivity("تم فتح تقرير وتنبيهات اليوم", "warning"); }
  }

  function renderPatientContent() {
    if (!selectedItem) return <EmptyView icon={User} title="اختر مريضاً من القائمة" />;
    if (patientTab === "journey") return <PatientJourneySection item={selectedItem} onMove={handleMove} onNoShow={handleNoShow} onQuickPay={handleQuickPay} activityLogs={activityLogs} receptionNotifications={receptionNotifications} />;
    if (patientTab === "visit") return <VisitSection item={selectedItem} selectedServices={doctorVisitServices[selectedItem.appointmentId] || []} onToggleService={function (service) { handleToggleService(selectedItem, service); }} onSaveDoctorVisit={handleSaveDoctorVisit} onFinishDoctorVisit={handleFinishDoctorVisit} />;
    if (patientTab === "ortho") return <OrthoSection />;
    if (patientTab === "history") return <HistorySection />;
    if (patientTab === "payment") return <PaymentSection item={selectedItem} onQuickPay={handleQuickPay} />;
    if (patientTab === "timeline") return <TimelineSection />;
    return null;
  }

  function changeRole(nextRole) {
    setRole(nextRole);
    if (nextRole === "doctor") {
      setActiveModule("doctorClinic");
      var firstDoctorPatient = items.find(function (item) { return item.doctorName === "د. عقلان الكامل" && ["Called", "InRoom", "InProgress", "Checkout"].includes(item.appointmentStatus); });
      if (firstDoctorPatient) setSelectedItem(firstDoctorPatient);
    } else {
      setActiveModule("daily");
    }
  }

  function renderDailyOperations() {
    var actualTabs = [
      { key: "appointments", label: "مواعيد اليوم", color: "#3d7ab5", icon: CalendarDays },
      { key: "queue", label: "قائمة الانتظار", color: "#f5922e", icon: Clock },
      { key: "inClinic", label: "داخل العيادة", color: "#9333ea", icon: Stethoscope },
      { key: "completed", label: "مكتمل اليوم", color: "#16a34a", icon: CheckCircle2 },
      { key: "payments", label: "المدفوعات السريعة", color: "#22c55e", icon: CreditCard },
      { key: "overdue", label: "المتأخرات", color: "#ef4444", icon: AlertTriangle },
    ];

    var tabKey = topTab === "journey" ? "appointments" : topTab;
    var readyForCheckout = items.filter(function (i) { return i.appointmentStatus === "Checkout"; });
    var activeRows = filteredItems.filter(function (i) {
      if (tabKey === "appointments") return true;
      if (tabKey === "queue") return ["Waiting", "Called"].includes(i.appointmentStatus);
      if (tabKey === "inClinic") return ["InRoom", "InProgress"].includes(i.appointmentStatus);
      if (tabKey === "completed") return i.appointmentStatus === "Completed";
      if (tabKey === "payments") return i.appointmentStatus === "Checkout" || i.outstandingBalance > 0;
      if (tabKey === "overdue") return i.appointmentStatus === "NoShow" || i.overdueAmount > 0;
      return true;
    });

    function setActualTab(key) {
      if (key === "appointments") setTopTab("journey");
      else setTopTab(key);
    }

    function rowNextAction(item) {
      if (item.appointmentStatus === "Scheduled") return { label: "تسجيل حضور", icon: UserCheck, color: "#16a34a", run: function () { handleMove(item, "Arrived"); } };
      if (item.appointmentStatus === "Arrived") return { label: "إضافة للطابور", icon: Route, color: "#f5922e", run: function () { handleMove(item, "Waiting"); } };
      if (item.appointmentStatus === "Waiting") return { label: "نداء", icon: Bell, color: "#d97706", run: function () { handleMove(item, "Called"); } };
      if (item.appointmentStatus === "Called") return { label: "دخول", icon: DoorOpen, color: "#9333ea", run: function () { handleMove(item, "InRoom"); } };
      if (item.appointmentStatus === "InRoom") return { label: "بدء", icon: Stethoscope, color: "#dc2626", run: function () { handleMove(item, "InProgress"); } };
      if (item.appointmentStatus === "InProgress") return { label: "إنهاء", icon: CheckCircle2, color: "#16a34a", run: function () { handleFinishDoctorVisit(item); } };
      if (item.appointmentStatus === "Checkout") return { label: "خروج", icon: CheckCircle2, color: "#059669", run: function () { handleMove(item, "Completed"); } };
      return null;
    }

    function TinyAction(props) {
      var Icon = props.icon;
      return (
        <button
          type="button"
          title={props.label}
          onClick={props.onClick}
          className="w-8 h-8 rounded-lg flex items-center justify-center transition hover:opacity-80 disabled:opacity-30"
          style={{ background: props.color + "15", color: props.color }}
        >
          <Icon className="w-3.5 h-3.5" />
        </button>
      );
    }

    function StatusBadgeLocal(props) {
      var cls = STATUS_COLORS[props.status] || "bg-gray-100 text-gray-600";
      return <span className={cn("inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-bold", cls)}>{STATUS_LABELS[props.status] || props.status}</span>;
    }

    return (
      <div className="h-full flex flex-col bg-[#f8fafc] overflow-hidden">
        <div className="h-[52px] flex-shrink-0 bg-white flex items-center px-3 gap-2 border-b border-gray-200 shadow-sm">
          <div className="flex items-center gap-2 flex-shrink-0">
            <div className="w-8 h-8 rounded-lg flex items-center justify-center bg-[#1a3a5c]">
              <Activity className="w-4 h-4 text-white" />
            </div>
            <div className="leading-tight">
              <div className="text-sm font-extrabold text-[#1a3a5c]">التشغيل اليومي</div>
              <div className="text-[10px] font-medium text-gray-400">رحلة الاستقبال</div>
            </div>
          </div>

          <div className="w-px h-7 mx-1 bg-gray-200" />

          <div className="flex-1 max-w-md relative">
            <Search className="w-4 h-4 absolute top-1/2 right-3 -translate-y-1/2 text-gray-400" />
            <input
              type="text"
              value={search}
              onChange={function (e) { setSearch(e.target.value); }}
              placeholder="بحث باسم المريض أو الهاتف..."
              className="w-full text-sm rounded-full border-0 pl-16 pr-9 py-1.5 outline-none focus:ring-2 focus:ring-[#3d7ab5]/20 bg-[#f5f7fa] text-[#1a3a5c]"
            />
            <span className="absolute left-3 top-1/2 -translate-y-1/2 text-[10px] font-medium px-1.5 py-0.5 rounded bg-gray-200 text-gray-400">Ctrl+F</span>
          </div>

          <div className="flex items-center gap-1 px-2 py-1 rounded-lg text-xs font-semibold bg-[#f5f7fa] text-[#1a3a5c]">
            <CalendarDays className="w-3.5 h-3.5" />
            <input type="date" defaultValue="2026-05-25" className="bg-transparent text-xs font-semibold outline-none border-0 w-[110px] text-[#1a3a5c]" />
          </div>

          <select value={doctor} onChange={function (e) { setDoctor(e.target.value); }} className="text-xs font-semibold rounded-lg px-2 py-1.5 outline-none border-0 bg-[#f5f7fa] text-[#1a3a5c] max-w-[125px]">
            <option value="">👨‍⚕️ كل الأطباء</option>
            <option>د. عقلان الكامل</option>
            <option>د. عائشة غازي</option>
            <option>د. هشام القدسي</option>
            <option>د. إيمان الكامل</option>
          </select>

          <select className="text-xs font-semibold rounded-lg px-2 py-1.5 outline-none border-0 bg-[#f5f7fa] text-[#1a3a5c] max-w-[115px]">
            <option>📊 الكل</option>
            <option>مجدول</option>
            <option>وصل</option>
            <option>في الانتظار</option>
            <option>داخل الغرفة</option>
            <option>مكتمل</option>
          </select>

          <div className="flex-1" />

          <button onClick={function () { handleCommand("walkIn"); }} className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90 bg-[#f5922e]" title="مريض مشي">
            <UserPlus className="w-3.5 h-3.5" />
            <span>مريض مشي</span>
          </button>

          <button onClick={function () { handleCommand("bulkReminders"); }} className="w-9 h-9 rounded-lg flex items-center justify-center transition hover:bg-[#3d7ab508] border border-[#3d7ab530]" title="إرسال تذكيرات لمواعيد الغد">
            <Send className="w-4 h-4 text-[#3d7ab5]" />
          </button>
          <button onClick={function () { handleCommand("printSchedule"); }} className="w-9 h-9 rounded-lg flex items-center justify-center transition hover:bg-gray-100" title="طباعة جدول اليوم">
            <Printer className="w-4 h-4 text-gray-500" />
          </button>
          <button onClick={function () { setClock(clock === "09:25 ص" ? "09:26 ص" : "09:25 ص"); addActivity("تم تحديث بيانات التشغيل اليومي", "info"); }} className="w-9 h-9 rounded-lg flex items-center justify-center transition hover:bg-gray-100" title="تحديث">
            <RefreshCw className="w-4 h-4 text-gray-500" />
          </button>
          <button onClick={function () { handleCommand("sound"); }} className="w-9 h-9 rounded-lg flex items-center justify-center transition hover:bg-gray-100" title="صوت التنبيه">
            <Bell className="w-4 h-4 text-gray-500" />
          </button>
          <button onClick={function () { handleCommand("shortcuts"); }} className="w-9 h-9 rounded-lg flex items-center justify-center transition hover:bg-gray-100" title="اختصارات لوحة المفاتيح">
            <Settings className="w-4 h-4 text-gray-500" />
          </button>
          <div className="flex items-center gap-1 w-9 h-9 rounded-lg justify-center" title="متصل مباشر">
            <span className="w-2.5 h-2.5 rounded-full bg-green-500" />
          </div>
        </div>

        <div className="flex-1 flex overflow-hidden">
          <div className="flex-1 flex flex-col min-w-0">
            <div className="flex-shrink-0 bg-white px-3 py-2 flex items-center gap-1.5 overflow-x-auto border-b border-gray-100">
              {actualTabs.map(function (tab) {
                var Icon = tab.icon;
                var count = tab.key === "appointments" ? items.length : tab.key === "queue" ? items.filter(function (i) { return ["Waiting", "Called"].includes(i.appointmentStatus); }).length : tab.key === "inClinic" ? items.filter(function (i) { return ["InRoom", "InProgress"].includes(i.appointmentStatus); }).length : tab.key === "completed" ? items.filter(function (i) { return i.appointmentStatus === "Completed"; }).length : tab.key === "payments" ? readyForCheckout.length : items.filter(function (i) { return i.overdueAmount > 0 || i.appointmentStatus === "NoShow"; }).length;
                var isActive = tabKey === tab.key;
                return (
                  <button key={tab.key} onClick={function () { setActualTab(tab.key); }} className="flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-bold whitespace-nowrap transition-all" style={{ background: isActive ? tab.color : "#f5f7fa", color: isActive ? "#fff" : "#64748b", boxShadow: isActive ? "0 1px 3px " + tab.color + "30" : "none" }}>
                    <Icon className="w-3.5 h-3.5" />
                    <span>{tab.label}</span>
                    {count > 0 ? <span className="text-[10px] font-extrabold px-1.5 py-0.5 rounded-full min-w-[18px] text-center" style={{ background: isActive ? "rgba(255,255,255,0.25)" : tab.color + "15", color: isActive ? "#fff" : tab.color }}>{count}</span> : null}
                  </button>
                );
              })}

              {filteredItems[0] ? (
                <div className="flex items-center gap-1.5 mr-auto px-2.5 py-1 rounded-lg text-xs font-bold bg-gradient-to-l from-blue-50 to-purple-50 border border-blue-100 text-[#1a3a5c]">
                  <Activity className="w-3 h-3 text-[#3d7ab5]" />
                  <span>التالي: {filteredItems[0].patientName}</span>
                  <button onClick={function () { handleCommand("call"); }} className="px-2 py-0.5 rounded text-[10px] font-bold text-white bg-[#f5922e]">نداء</button>
                </div>
              ) : null}
            </div>

            {readyForCheckout.length > 0 ? (
              <div className="flex-shrink-0 mx-3 mt-2 mb-1 p-2.5 rounded-xl flex items-center gap-3 bg-gradient-to-l from-orange-50 to-amber-100 border border-orange-200 shadow-sm">
                <div className="w-8 h-8 rounded-lg flex items-center justify-center flex-shrink-0 bg-orange-100">
                  <CreditCard className="w-4 h-4 text-[#f5922e]" />
                </div>
                <div className="flex-1">
                  <div className="text-xs font-extrabold text-[#1a3a5c]">جاهزون للدفع من الطبيب</div>
                  <div className="text-[10px] font-medium text-amber-800">{readyForCheckout.length} مريض بحاجة لتحصيل وخروج</div>
                </div>
                <button onClick={function () { setActualTab("payments"); }} className="px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90 flex items-center gap-1.5 bg-[#f5922e]">
                  <CreditCard className="w-3.5 h-3.5" /> عرض الكل
                </button>
              </div>
            ) : null}

            <div className="flex-1 overflow-auto bg-white">
              <table className="w-full hidden lg:table">
                <thead>
                  <tr className="text-[11px] font-bold border-b-2 text-gray-500 border-gray-100">
                    <th className="text-right py-2.5 px-2">الوقت</th>
                    <th className="text-right py-2.5 px-2">المريض</th>
                    <th className="text-right py-2.5 px-2">الهاتف</th>
                    <th className="text-right py-2.5 px-2">الطبيب</th>
                    <th className="text-right py-2.5 px-2">الخدمة</th>
                    <th className="text-right py-2.5 px-2">الغرفة</th>
                    <th className="text-right py-2.5 px-2">الحالة</th>
                    <th className="text-right py-2.5 px-2">الإجراء التالي</th>
                    <th className="text-right py-2.5 px-2">إجراءات</th>
                  </tr>
                </thead>
                <tbody>
                  {activeRows.map(function (item) {
                    var action = rowNextAction(item);
                    var ActionIcon = action ? action.icon : CheckCircle2;
                    return (
                      <tr key={item.appointmentId} onClick={function () { setSelectedItem(item); }} className="border-b border-gray-100 hover:bg-[#f8fafc] transition-colors cursor-pointer">
                        <td className="py-2.5 px-2 text-sm font-bold text-[#1a3a5c]">{item.appointmentTime}</td>
                        <td className="py-2.5 px-2">
                          <button onClick={function (e) { e.stopPropagation(); setSelectedItem(item); }} className="text-sm font-semibold hover:underline flex items-center gap-1.5 text-[#3d7ab5]">
                            {item.overdueAmount > 0 ? <AlertTriangle className="w-3 h-3 text-red-500" /> : null}
                            {item.patientName}
                          </button>
                          <p className="text-[10px] text-gray-400 mt-0.5">ملف {item.patientNumber}</p>
                        </td>
                        <td className="py-2.5 px-2 text-xs text-gray-500">{item.patientPhone}</td>
                        <td className="py-2.5 px-2 text-xs text-gray-600">{item.doctorName}</td>
                        <td className="py-2.5 px-2 text-xs text-gray-600">{item.serviceName}</td>
                        <td className="py-2.5 px-2 text-xs text-gray-600"><Building2 className="w-3 h-3 inline ml-1" />{item.roomName}</td>
                        <td className="py-2.5 px-2"><StatusBadgeLocal status={item.appointmentStatus} /></td>
                        <td className="py-2.5 px-2"><span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-bold bg-orange-50 text-[#f5922e]">{nextActionText(item.appointmentStatus)}</span></td>
                        <td className="py-2.5 px-2">
                          <div className="flex items-center gap-1 flex-wrap" onClick={function (e) { e.stopPropagation(); }}>
                            <TinyAction icon={UserRound} label="فتح الملف" color="#3d7ab5" onClick={function () { setSelectedItem(item); }} />
                            {action ? <TinyAction icon={ActionIcon} label={action.label} color={action.color} onClick={action.run} /> : null}
                            <TinyAction icon={CreditCard} label="دفعة سريعة" color="#22c55e" onClick={function () { handleQuickPay(item); }} />
                            <TinyAction icon={CalendarCheck} label="حجز متابعة" color="#3d7ab5" onClick={function () { setSelectedItem(item); setActualTab("appointments"); addActivity("تم فتح حجز متابعة لـ " + item.patientName, "info"); }} />
                            <TinyAction icon={MessageCircle} label="واتساب" color="#25D366" onClick={function () { setSelectedItem(item); setActualTab("appointments"); addActivity("تم فتح واتساب لـ " + item.patientName, "info"); }} />
                            {item.appointmentStatus === "Scheduled" ? <TinyAction icon={UserX} label="لم يحضر" color="#ef4444" onClick={function () { handleNoShow(item); }} /> : null}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>

              <div className="lg:hidden space-y-2 p-3">
                {activeRows.map(function (item) {
                  return (
                    <button key={item.appointmentId} onClick={function () { setSelectedItem(item); }} className="w-full rounded-xl border border-gray-200 bg-white p-3 text-right hover:bg-blue-50">
                      <div className="flex items-center justify-between gap-2"><b className="text-sm text-[#1a3a5c]">{item.patientName}</b><StatusBadgeLocal status={item.appointmentStatus} /></div>
                      <p className="text-xs text-gray-500 mt-1">{item.appointmentTime} · {item.doctorName} · {item.serviceName}</p>
                    </button>
                  );
                })}
              </div>
            </div>
          </div>

          {selectedItem ? (
            <div className="hidden lg:flex w-[380px] flex-shrink-0 flex-col bg-white overflow-hidden border-r border-gray-200">
              <div className="px-4 py-3 flex items-start gap-3 border-b border-gray-200">
                <button onClick={function () { setSelectedItem(null); }} className="w-7 h-7 rounded-lg flex items-center justify-center hover:bg-gray-100 transition flex-shrink-0 mt-0.5">
                  <UserX className="w-4 h-4 text-gray-400" />
                </button>
                <div className="flex items-center gap-3 flex-1 min-w-0">
                  <div className="w-10 h-10 rounded-full flex items-center justify-center flex-shrink-0 text-sm font-bold text-white bg-[#1a3a5c]">{selectedItem.patientName.slice(0, 2)}</div>
                  <div className="min-w-0">
                    <div className="text-sm font-extrabold truncate text-[#1a3a5c]">{selectedItem.patientName}</div>
                    <div className="text-[11px] font-medium text-gray-500">{selectedItem.patientPhone}</div>
                    <div className="text-[11px] text-gray-400">{selectedItem.doctorName} — {selectedItem.serviceName}</div>
                  </div>
                </div>
              </div>

              <div className="flex-1 overflow-y-auto p-4 space-y-4">
                {selectedItem.overdueAmount > 0 ? (
                  <div className="px-2.5 py-1.5 rounded-lg text-[11px] font-medium bg-red-50 text-red-700 border border-red-100">
                    <AlertTriangle className="w-3 h-3 inline ml-1" /> تنبيه مالي: يوجد متأخرات على المريض
                  </div>
                ) : null}

                <div className="flex items-center gap-2 flex-wrap">
                  <StatusBadgeLocal status={selectedItem.appointmentStatus} />
                  <span className="inline-flex items-center px-2 py-0.5 rounded text-[10px] font-bold bg-orange-50 text-[#f5922e]">{nextActionText(selectedItem.appointmentStatus)}</span>
                </div>

                <div>
                  <div className="text-[11px] font-bold mb-1.5 text-[#1a3a5c]">💰 الملخص المالي</div>
                  <div className="grid grid-cols-3 gap-1.5">
                    <div className="p-2 rounded-lg text-center bg-orange-50"><div className="text-[9px] font-medium text-[#f5922e]">المستحق</div><div className="text-xs font-bold text-[#1a3a5c]">{formatMoney(selectedItem.outstandingBalance)}</div></div>
                    <div className="p-2 rounded-lg text-center bg-red-50"><div className="text-[9px] font-medium text-red-500">متأخرات</div><div className="text-xs font-bold text-[#1a3a5c]">{formatMoney(selectedItem.overdueAmount)}</div></div>
                    <div className="p-2 rounded-lg text-center bg-green-50"><div className="text-[9px] font-medium text-green-600">المدفوع</div><div className="text-xs font-bold text-[#1a3a5c]">{formatMoney(selectedItem.totalPaid)}</div></div>
                  </div>
                </div>

                <div>
                  <div className="text-[11px] font-bold mb-1.5 text-[#1a3a5c]">📅 زيارات اليوم</div>
                  <div className="space-y-1">
                    <div className="px-2.5 py-1.5 rounded-lg text-[11px] flex items-center justify-between bg-gray-50 border border-gray-100">
                      <span className="font-medium text-[#1a3a5c]">{selectedItem.chiefComplaint}</span>
                      <span className="text-gray-400">اليوم</span>
                    </div>
                  </div>
                </div>

                {selectedItem.appointmentStatus === "Checkout" ? (
                  <div className="rounded-xl bg-orange-50 border border-orange-100 p-3">
                    <p className="text-xs font-black text-[#1a3a5c]">جاهز للدفع من الطبيب</p>
                    <p className="text-[10px] text-amber-800 mt-1">الإجراءات: {selectedItem.lastDoctorVisitServices || selectedItem.serviceName}</p>
                    <div className="grid grid-cols-2 gap-2 mt-3">
                      <button onClick={function () { handleCommand("draftInvoice"); }} className="rounded-lg bg-white border border-orange-200 px-2 py-2 text-[10px] font-black text-[#f5922e]">فاتورة مسودة</button>
                      <button onClick={function () { handleQuickPay(selectedItem); }} className="rounded-lg bg-[#22c55e] px-2 py-2 text-[10px] font-black text-white">دفع سريع</button>
                    </div>
                  </div>
                ) : null}
              </div>

              <div className="flex-shrink-0 p-3 flex items-center gap-1.5 border-t border-gray-200 bg-gray-50">
                <button onClick={function () { handleQuickPay(selectedItem); }} className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-[11px] font-bold bg-green-50 text-green-600 border border-green-100"><CreditCard className="w-3 h-3" /> دفع</button>
                <button onClick={function () { handleCommand("nextVisit"); }} className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-[11px] font-bold bg-blue-50 text-[#3d7ab5] border border-blue-100"><CalendarDays className="w-3 h-3" /> حجز</button>
                <button onClick={function () { handleCommand("whatsapp"); }} className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-[11px] font-bold bg-green-50 text-[#25D366] border border-green-100"><MessageCircle className="w-3 h-3" /> واتساب</button>
                <div className="flex-1" />
                <button onClick={function () { setTopTab("journey"); }} className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-[11px] font-bold bg-[#1a3a5c]/5 text-[#1a3a5c] border border-[#1a3a5c]/10">فتح الملف</button>
              </div>
            </div>
          ) : null}
        </div>

        <div className="h-8 flex-shrink-0 flex items-center px-3 gap-4 text-[11px] font-medium text-white select-none bg-[#1a3a5c]">
          <div className="flex items-center gap-1.5"><span className="w-2 h-2 rounded-full bg-green-400" /><span>مباشر</span></div>
          <div className="w-px h-4 bg-white/20" />
          <span>{items.length} موعد</span>
          <span>{items.filter(function (i) { return i.appointmentStatus === "Waiting"; }).length} انتظار</span>
          <span>{items.filter(function (i) { return ["InRoom", "InProgress"].includes(i.appointmentStatus); }).length} عيادة</span>
          <span>{items.filter(function (i) { return i.appointmentStatus === "Completed"; }).length} مكتمل</span>
          <div className="w-px h-4 bg-white/20" />
          <span>💰 {formatMoney(items.reduce(function (s, i) { return s + i.totalPaid; }, 0))}</span>
          <span>⚠ {formatMoney(items.reduce(function (s, i) { return s + i.overdueAmount; }, 0))} متأخر</span>
          <div className="flex-1" />
          <span>25 مايو 2026</span>
          <span>{clock}</span>
          <div className="w-px h-4 bg-white/20" />
          <span>⌨ ?</span>
        </div>
      </div>
    );
  }

  return (
    <div className="flex h-screen bg-gray-100 overflow-hidden" dir="rtl">
      <SystemSidebar activeModule={activeModule} setActiveModule={setActiveModule} role={role} />
      <div className="flex flex-col flex-1 overflow-hidden">
        <div className="h-10 bg-white border-b border-gray-200 flex items-center justify-between px-4 flex-shrink-0">
          <p className="text-xs font-bold text-gray-500">محاكاة تسجيل الدخول حسب الصلاحية</p>
          <div className="flex items-center gap-2">
            <button onClick={function () { changeRole("reception"); }} className={cn("rounded-lg px-3 py-1.5 text-[10px] font-black", role === "reception" ? "bg-[#3d7ab5] text-white" : "bg-gray-100 text-gray-600")}>حساب استقبال</button>
            <button onClick={function () { changeRole("doctor"); }} className={cn("rounded-lg px-3 py-1.5 text-[10px] font-black", role === "doctor" ? "bg-[#3d7ab5] text-white" : "bg-gray-100 text-gray-600")}>حساب طبيب</button>
          </div>
        </div>
        {role === "doctor" && activeModule === "doctorClinic" ? <DoctorClinicView items={items} selectedItem={selectedItem} setSelectedItem={setSelectedItem} doctorName="د. عقلان الكامل" onMove={handleMove} activityLogs={activityLogs} doctorVisitServices={doctorVisitServices} onToggleService={handleToggleService} onSaveDoctorVisit={handleSaveDoctorVisit} onFinishDoctorVisit={handleFinishDoctorVisit} /> : null}
        {role === "reception" && activeModule === "daily" ? renderDailyOperations() : null}
        {!(role === "doctor" && activeModule === "doctorClinic") && !(role === "reception" && activeModule === "daily") ? <PlaceholderModule moduleKey={activeModule} role={role} /> : null}
      </div>
    </div>
  );
}
