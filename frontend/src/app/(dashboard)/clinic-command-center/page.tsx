"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import {
  Activity,
  BarChart3,
  CalendarDays,
  ChevronLeft,
  ClipboardList,
  FileText,
  FlaskConical,
  Layers,
  Package,
  RefreshCw,
  Route,
  Users,
  Wallet,
} from "lucide-react";
import api from "@/lib/api";
import { formatYemeniRiyal, localDateString } from "@/lib/utils";
import { useAuthStore } from "@/stores/authStore";
import type { DashboardStats } from "@/types/dashboard";
import type { PatientSegmentsListResponse } from "@/types/patientSegment";
import type { DashboardData } from "../finance-v3/components/types";

type LoadState = "idle" | "loading" | "ready" | "error";

interface DailyOpsReportSummary {
  date?: string;
  patientCounts?: {
    total?: number;
    waiting?: number;
    inRoom?: number;
    readyForCheckout?: number;
    completed?: number;
    noShow?: number;
    emergency?: number;
  };
  financial?: {
    totalCollected?: number;
    newDebts?: number;
    draftInvoices?: number;
    partialPayments?: number;
  };
  labOrders?: {
    sent?: number;
    received?: number;
    delivered?: number;
  };
  tomorrowAppointments?: number;
}

interface CommandTile {
  label: string;
  value: string | number;
  hint: string;
  href: string;
  icon: typeof Activity;
  tone: "blue" | "green" | "orange" | "purple" | "red" | "slate";
}

const toneClass: Record<CommandTile["tone"], string> = {
  blue: "border-blue-100 bg-blue-50 text-blue-700",
  green: "border-emerald-100 bg-emerald-50 text-emerald-700",
  orange: "border-orange-100 bg-orange-50 text-orange-700",
  purple: "border-violet-100 bg-violet-50 text-violet-700",
  red: "border-red-100 bg-red-50 text-red-700",
  slate: "border-slate-100 bg-slate-50 text-slate-700",
};

function metric(value: number | undefined | null) {
  return value ?? "—";
}

function money(value: number | undefined | null) {
  return typeof value === "number" ? formatYemeniRiyal(value) : "—";
}

function CommandMetricCard({ tile }: { tile: CommandTile }) {
  const Icon = tile.icon;
  return (
    <Link
      href={tile.href}
      className="group rounded-2xl border border-slate-100 bg-white p-4 shadow-sm transition hover:-translate-y-0.5 hover:border-slate-200 hover:shadow-md"
    >
      <div className="flex items-start justify-between gap-3">
        <div className={`flex h-10 w-10 items-center justify-center rounded-xl border ${toneClass[tile.tone]}`}>
          <Icon className="h-5 w-5" />
        </div>
        <ChevronLeft className="mt-1 h-4 w-4 text-slate-300 transition group-hover:-translate-x-1 group-hover:text-slate-500" />
      </div>
      <div className="mt-4">
        <p className="text-xs font-semibold text-slate-500">{tile.label}</p>
        <p className="mt-1 text-2xl font-black text-slate-900">{tile.value}</p>
        <p className="mt-1 text-xs leading-5 text-slate-500">{tile.hint}</p>
      </div>
    </Link>
  );
}

function Shortcut({
  href,
  icon: Icon,
  title,
  description,
}: {
  href: string;
  icon: typeof Activity;
  title: string;
  description: string;
}) {
  return (
    <Link
      href={href}
      className="flex items-center gap-3 rounded-xl border border-slate-100 bg-white px-4 py-3 text-start shadow-sm transition hover:border-blue-100 hover:bg-blue-50/40"
    >
      <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-slate-100 text-slate-700">
        <Icon className="h-4 w-4" />
      </div>
      <div className="min-w-0 flex-1">
        <p className="text-sm font-bold text-slate-900">{title}</p>
        <p className="mt-0.5 truncate text-xs text-slate-500">{description}</p>
      </div>
    </Link>
  );
}

export default function ClinicCommandCenterPage() {
  const { user } = useAuthStore();
  const [state, setState] = useState<LoadState>("idle");
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [dailyReport, setDailyReport] = useState<DailyOpsReportSummary | null>(null);
  const [finance, setFinance] = useState<DashboardData | null>(null);
  const [segments, setSegments] = useState<PatientSegmentsListResponse | null>(null);
  const [updatedAt, setUpdatedAt] = useState<Date | null>(null);

  const canViewFinance = user?.role === "Admin" || user?.role === "Accountant";
  const today = localDateString();

  const load = async () => {
    setState("loading");
    const [statsRes, reportRes, financeRes, segmentsRes] = await Promise.allSettled([
      api.get<DashboardStats>("/api/dashboard/stats"),
      api.get<DailyOpsReportSummary>("/api/daily-operations/report", { params: { date: today } }),
      canViewFinance ? api.get<DashboardData>("/api/finance-v3/dashboard") : Promise.resolve({ data: null }),
      user?.role === "Admin"
        ? api.get<PatientSegmentsListResponse>("/api/patient-segments")
        : Promise.resolve({ data: null }),
    ]);

    if (statsRes.status === "fulfilled") setStats(statsRes.value.data);
    if (reportRes.status === "fulfilled") setDailyReport(reportRes.value.data);
    if (financeRes.status === "fulfilled") setFinance(financeRes.value.data as DashboardData | null);
    if (segmentsRes.status === "fulfilled") setSegments(segmentsRes.value.data as PatientSegmentsListResponse | null);

    const hasCriticalFailure = statsRes.status === "rejected" && reportRes.status === "rejected";
    setState(hasCriticalFailure ? "error" : "ready");
    setUpdatedAt(new Date());
  };

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canViewFinance, user?.role]);

  const segmentCount = useMemo(() => {
    const builtIn = segments?.builtIn?.length ?? 0;
    const custom = segments?.custom?.length ?? 0;
    return builtIn + custom;
  }, [segments]);

  const tiles: CommandTile[] = [
    {
      label: "مواعيد اليوم",
      value: metric(stats?.appointmentsToday),
      hint: "انتقال مباشر إلى شاشة التشغيل اليومي",
      href: "/daily-operations?tab=appointments",
      icon: CalendarDays,
      tone: "blue",
    },
    {
      label: "داخل الانتظار",
      value: metric(stats?.queueWaitingCount ?? dailyReport?.patientCounts?.waiting),
      hint: "قائمة الانتظار والنداء والغرف",
      href: "/daily-operations?tab=queue",
      icon: Route,
      tone: "orange",
    },
    {
      label: "جاهز للحساب",
      value: metric(dailyReport?.patientCounts?.readyForCheckout),
      hint: "مرضى بانتظار الفاتورة أو التحصيل",
      href: "/daily-operations?tab=checkout",
      icon: Wallet,
      tone: "green",
    },
    {
      label: "تحصيل اليوم",
      value: money(dailyReport?.financial?.totalCollected),
      hint: "من تقرير التشغيل اليومي",
      href: "/daily-operations?tab=report",
      icon: BarChart3,
      tone: "green",
    },
    {
      label: "طلبات المعمل",
      value: metric(stats?.pendingLabOrders ?? dailyReport?.labOrders?.sent),
      hint: "متابعة الطلبات المرسلة والمستلمة",
      href: "/lab",
      icon: FlaskConical,
      tone: "purple",
    },
    {
      label: "ديون وفواتير",
      value: money(finance?.totalOutstanding ?? dailyReport?.financial?.newDebts),
      hint: "فتح المركز المالي وحسابات المرضى",
      href: "/finance-v3?tab=patients",
      icon: FileText,
      tone: "red",
    },
    {
      label: "مرضى جدد",
      value: metric(stats?.newPatientsToday),
      hint: "إضافة أو متابعة المرضى الجدد",
      href: "/patients",
      icon: Users,
      tone: "blue",
    },
    {
      label: "مجموعات المرضى",
      value: user?.role === "Admin" ? metric(segmentCount) : "—",
      hint: "شرائح للمتابعة والتسويق بدون إرسال تلقائي",
      href: "/patient-segments",
      icon: Layers,
      tone: "slate",
    },
  ];

  return (
    <div className="min-h-full bg-slate-50 px-4 py-5 text-start sm:px-6 lg:px-8" dir="rtl">
      <div className="mx-auto flex max-w-7xl flex-col gap-5">
        <section className="rounded-3xl border border-slate-100 bg-white p-5 shadow-sm">
          <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
            <div>
              <div className="flex items-center gap-2 text-xs font-bold text-blue-700">
                <Route className="h-4 w-4" />
                مركز قيادة العيادة
              </div>
              <h1 className="mt-2 text-2xl font-black text-slate-950">ملخص تنفيذي سريع للتشغيل والمالية</h1>
              <p className="mt-2 max-w-3xl text-sm leading-7 text-slate-500">
                صفحة مستوحاة من شاشة الإدارة في البرنامج المرجعي: تجمع المؤشرات، التقارير، السندات،
                المخزون، ومجموعات المرضى في نقطة واحدة بدون تغيير أي عملية مالية أو سريرية.
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              {updatedAt ? (
                <span className="rounded-full bg-slate-100 px-3 py-2 text-xs font-semibold text-slate-600">
                  آخر تحديث: {updatedAt.toLocaleTimeString("ar-YE", { hour: "2-digit", minute: "2-digit" })}
                </span>
              ) : null}
              <button
                type="button"
                onClick={() => void load()}
                disabled={state === "loading"}
                className="inline-flex items-center gap-2 rounded-xl bg-blue-700 px-4 py-2 text-sm font-bold text-white shadow-sm transition hover:bg-blue-800 disabled:cursor-not-allowed disabled:opacity-60"
              >
                <RefreshCw className={`h-4 w-4 ${state === "loading" ? "animate-spin" : ""}`} />
                تحديث
              </button>
            </div>
          </div>

          {state === "error" ? (
            <div className="mt-4 rounded-2xl border border-red-100 bg-red-50 px-4 py-3 text-sm font-semibold text-red-700">
              تعذر تحميل مؤشرات التشغيل. جرب التحديث أو افحص الاتصال بالخادم.
            </div>
          ) : null}
        </section>

        <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          {tiles.map((tile) => (
            <CommandMetricCard key={tile.label} tile={tile} />
          ))}
        </section>

        <section className="grid gap-5 xl:grid-cols-[1.4fr_0.9fr]">
          <div className="rounded-3xl border border-slate-100 bg-white p-5 shadow-sm">
            <div className="flex items-center justify-between gap-3">
              <div>
                <h2 className="text-lg font-black text-slate-950">مسارات العمل السريعة</h2>
                <p className="mt-1 text-xs text-slate-500">بدل التنقل الطويل: افتح الشاشة الصحيحة مباشرة حسب المهمة.</p>
              </div>
              <ClipboardList className="h-6 w-6 text-slate-300" />
            </div>
            <div className="mt-4 grid gap-3 md:grid-cols-2">
              <Shortcut href="/daily-operations" icon={ClipboardList} title="التشغيل اليومي" description="وصول، انتظار، نداء، غرفة، تحصيل وخروج" />
              <Shortcut href="/appointments/new" icon={CalendarDays} title="موعد جديد" description="إضافة موعد سريع أو متابعة مريض" />
              <Shortcut href="/finance-v3?tab=collections" icon={Wallet} title="التحصيلات والسندات" description="سند قبض، إيصال PDF، وربط مالي" />
              <Shortcut href="/finance-v3?tab=invoices" icon={FileText} title="الفواتير" description="مراجعة الفواتير والدفع الجزئي والرصيد" />
              <Shortcut href="/reports" icon={BarChart3} title="التقارير" description="تقارير المركز، الأطباء، المواعيد والمالية" />
              <Shortcut href="/inventory" icon={Package} title="المخزون" description="رصيد المواد، التحركات، والصرف للخدمات" />
              <Shortcut href="/patient-segments" icon={Layers} title="مجموعات المرضى" description="شرائح للمتابعة والتواصل اليدوي الآمن" />
              <Shortcut href="/settings/templates" icon={FileText} title="قوالب المستندات" description="نماذج موافقة وتعليمات قابلة للطباعة" />
            </div>
          </div>

          <aside className="rounded-3xl border border-slate-100 bg-white p-5 shadow-sm">
            <div className="flex items-center gap-2">
              <Activity className="h-5 w-5 text-blue-700" />
              <h2 className="text-lg font-black text-slate-950">مستوحى من الفيديو</h2>
            </div>
            <div className="mt-4 space-y-3">
              {[
                "مؤشرات تشغيلية في أول الشاشة بدل البحث داخل كل وحدة.",
                "روابط مباشرة للتقارير والسندات والمخزون مثل البرنامج المرجعي.",
                "عرض مجموعات المرضى كمدخل للمتابعة بدون إرسال رسائل تلقائية.",
                "إبقاء التحصيل الحقيقي في التشغيل اليومي والمركز المالي فقط.",
              ].map((item) => (
                <div key={item} className="rounded-2xl border border-slate-100 bg-slate-50 px-4 py-3 text-sm leading-6 text-slate-600">
                  {item}
                </div>
              ))}
            </div>
          </aside>
        </section>
      </div>
    </div>
  );
}
