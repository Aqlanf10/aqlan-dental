"use client";
// Sprint 11A — thin shell. All per-tab logic lives in
// ./_components/* (extracted from the former 2577-line monolith).
// Behavior unchanged: same UI, same API calls, same state management.

import { useState } from "react";
import {
  Globe, FileSearch, Stethoscope, DoorOpen, CreditCard, Languages,
} from "lucide-react";
import Link from "next/link";
import { cn } from "@/lib/utils";
import { TABS, type Tab } from "./_components/_shared";
import { ClinicTab } from "./_components/ClinicTab";
import { UsersTab } from "./_components/UsersTab";
import { RolesTab } from "./_components/RolesTab";
import { EmailTab } from "./_components/EmailTab";
import { AiTab } from "./_components/AiTab";
import { FinanceTab } from "./_components/FinanceTab";

export default function SettingsPage() {
  const [activeTab, setActiveTab] = useState<Tab>("clinic");

  return (
    <div className="space-y-5 max-w-5xl">
      <div>
        <h1 className="text-2xl font-extrabold text-gray-900">الإعدادات</h1>
        <p className="text-sm text-gray-500 mt-0.5">إدارة إعدادات المركز والمستخدمين</p>
      </div>

      <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
        <div className="flex border-b border-gray-100 overflow-x-auto">
          {TABS.map(({ key, label, icon: Icon }) => (
            <button
              key={key}
              onClick={() => setActiveTab(key)}
              className={cn(
                "flex items-center gap-2 px-5 py-3.5 text-sm font-medium whitespace-nowrap border-b-2 transition",
                activeTab === key
                  ? "border-clinic-blue text-clinic-blue"
                  : "border-transparent text-gray-500 hover:text-gray-900"
              )}
            >
              <Icon className="w-4 h-4" />
              {label}
            </button>
          ))}
        </div>

        <div className="p-5">
          {activeTab === "clinic"  && <ClinicTab />}
          {activeTab === "users"   && <UsersTab />}
          {activeTab === "roles"   && <RolesTab />}
          {activeTab === "finance" && <FinanceTab />}
          {activeTab === "email"   && <EmailTab />}
          {activeTab === "ai"      && <AiTab />}
        </div>
      </div>

      {/* Quick links */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <Link
          href="/settings/website"
          className="flex items-center gap-4 bg-white rounded-xl border border-gray-200 shadow-sm p-4 hover:border-clinic-blue hover:shadow-md transition group"
        >
          <div className="w-10 h-10 rounded-xl bg-blue-50 flex items-center justify-center flex-shrink-0 group-hover:bg-blue-100 transition">
            <Globe className="w-5 h-5 text-blue-600" />
          </div>
          <div>
            <p className="font-semibold text-gray-900">إعدادات الموقع</p>
            <p className="text-sm text-gray-500">تحكم بمحتوى الصفحة الرئيسية والعنوان والتواصل</p>
          </div>
        </Link>
        <Link
          href="/settings/audit"
          className="flex items-center gap-4 bg-white rounded-xl border border-gray-200 shadow-sm p-4 hover:border-clinic-blue hover:shadow-md transition group"
        >
          <div className="w-10 h-10 rounded-xl bg-purple-50 flex items-center justify-center flex-shrink-0 group-hover:bg-purple-100 transition">
            <FileSearch className="w-5 h-5 text-purple-600" />
          </div>
          <div>
            <p className="font-semibold text-gray-900">سجل التدقيق</p>
            <p className="text-sm text-gray-500">عرض كل العمليات المنفذة في النظام</p>
          </div>
        </Link>
        <Link
          href="/settings/services"
          className="flex items-center gap-4 bg-white rounded-xl border border-gray-200 shadow-sm p-4 hover:border-clinic-blue hover:shadow-md transition group"
        >
          <div className="w-10 h-10 rounded-xl bg-emerald-50 flex items-center justify-center flex-shrink-0 group-hover:bg-emerald-100 transition">
            <Stethoscope className="w-5 h-5 text-emerald-600" />
          </div>
          <div>
            <p className="font-semibold text-gray-900">خدمات العيادة</p>
            <p className="text-sm text-gray-500">إدارة كتالوج الخدمات والأسعار</p>
          </div>
        </Link>
        <Link
          href="/settings/rooms"
          className="flex items-center gap-4 bg-white rounded-xl border border-gray-200 shadow-sm p-4 hover:border-clinic-blue hover:shadow-md transition group"
        >
          <div className="w-10 h-10 rounded-xl bg-amber-50 flex items-center justify-center flex-shrink-0 group-hover:bg-amber-100 transition">
            <DoorOpen className="w-5 h-5 text-amber-600" />
          </div>
          <div>
            <p className="font-semibold text-gray-900">غرف العيادة</p>
            <p className="text-sm text-gray-500">إدارة الغرف وتوزيعها</p>
          </div>
        </Link>
        <Link
          href="/settings/payment-methods"
          className="flex items-center gap-4 bg-white rounded-xl border border-gray-200 shadow-sm p-4 hover:border-clinic-blue hover:shadow-md transition group"
        >
          <div className="w-10 h-10 rounded-xl bg-purple-50 flex items-center justify-center flex-shrink-0 group-hover:bg-purple-100 transition">
            <CreditCard className="w-5 h-5 text-purple-600" />
          </div>
          <div>
            <p className="font-semibold text-gray-900">طرق الدفع</p>
            <p className="text-sm text-gray-500">إدارة طرق الدفع المتاحة والرقم المرجعي</p>
          </div>
        </Link>
        <Link
          href="/settings/language"
          className="flex items-center gap-4 bg-white rounded-xl border border-gray-200 shadow-sm p-4 hover:border-clinic-blue hover:shadow-md transition group"
        >
          <div className="w-10 h-10 rounded-xl bg-sky-50 flex items-center justify-center flex-shrink-0 group-hover:bg-sky-100 transition">
            <Languages className="w-5 h-5 text-sky-600" />
          </div>
          <div>
            <p className="font-semibold text-gray-900">لغة الوحدات</p>
            <p className="text-sm text-gray-500">التحكم بلغة كل وحدة (عربي / إنجليزي)</p>
          </div>
        </Link>
      </div>
    </div>
  );
}
