"use client";
// Sprint 11A — thin shell. All per-tab logic lives in
// ./_components/* (extracted from the former 2577-line monolith).
// Behavior unchanged: same UI, same API calls, same state management.
//
// YOLO-S3 (Settings Hub): reorganized the 6 flat tabs into 8 logical
// category groups (clinic / permissions / services / finance / lab /
// communication / templates / ortho-ceph). The QuickLinksGrid below each
// tab now surfaces related /settings/* sub-routes inline, replacing the
// old hard-coded "Quick links" block at the bottom. No existing per-tab
// component was deleted or modified — they are grouped differently here.

import { Suspense, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { cn } from "@/lib/utils";
import { TABS, type Tab } from "./_components/_shared";
import { ClinicTab } from "./_components/ClinicTab";
import { UsersTab } from "./_components/UsersTab";
import { RolesTab } from "./_components/RolesTab";
import { EmailTab } from "./_components/EmailTab";
import { AiTab } from "./_components/AiTab";
import { FinanceTab } from "./_components/FinanceTab";
import { QuickLinksGrid } from "./_components/QuickLinksGrid";

// SEQ-03: deep-linkable tabs — /settings?tab=permissions opens the users/roles
// section directly (the /settings/users, /settings/permissions and /users
// legacy paths redirect here instead of 404ing). Same useSearchParams+Suspense
// pattern as finance-v3/page.tsx.
const VALID_TABS = new Set<string>(TABS.map((t) => t.key));

function SettingsPageInner() {
  const searchParams = useSearchParams();
  const tabFromUrl = searchParams.get("tab");
  const initialTab: Tab =
    tabFromUrl && VALID_TABS.has(tabFromUrl) ? (tabFromUrl as Tab) : "clinic";
  const [activeTab, setActiveTab] = useState<Tab>(initialTab);

  useEffect(() => {
    if (tabFromUrl && VALID_TABS.has(tabFromUrl)) setActiveTab(tabFromUrl as Tab);
  }, [tabFromUrl]);

  // Resolve the current tab's quick links once per render. Tabs without
  // links (none currently, but defensive) just skip the grid.
  const currentTab = TABS.find((t) => t.key === activeTab);
  const quickLinks = currentTab?.links ?? [];

  return (
    <div className="space-y-5 max-w-5xl">
      <div>
        <h1 className="text-2xl font-extrabold text-gray-900">الإعدادات</h1>
        <p className="text-sm text-gray-500 mt-0.5">
          مركز التحكم الموحد لإعدادات المركز والصلاحيات والمالية والمختبر
        </p>
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

        <div className="p-5 space-y-6">
          {/* 1. بيانات المركز — existing clinic form + website/rooms links */}
          {activeTab === "clinic" && (
            <>
              <ClinicTab />
              <QuickLinksGrid links={quickLinks} title="روابط ذات صلة" />
            </>
          )}

          {/* 2. الصلاحيات — users + roles merged into one section, plus audit/backup */}
          {activeTab === "permissions" && (
            <>
              <section className="space-y-4">
                <UsersTab />
              </section>
              <section className="space-y-4 border-t border-gray-100 pt-6">
                <RolesTab />
              </section>
              <QuickLinksGrid links={quickLinks} title="أدوات إدارية" />
            </>
          )}

          {/* 3. الخدمات — no inline form, just links to services + packages */}
          {activeTab === "services" && (
            <QuickLinksGrid links={quickLinks} title="كتالوج الخدمات والباقات" />
          )}

          {/* 4. المالية — existing finance form + payment-methods link */}
          {activeTab === "finance" && (
            <>
              <FinanceTab />
              <QuickLinksGrid links={quickLinks} title="روابط ذات صلة" />
            </>
          )}

          {/* 5. المختبر — no inline form, links to labs/work-types/pricing */}
          {activeTab === "lab" && (
            <QuickLinksGrid links={quickLinks} title="إعدادات أعمال المختبر" />
          )}

          {/* 6. التواصل — existing email form + language link */}
          {activeTab === "communication" && (
            <>
              <EmailTab />
              <QuickLinksGrid links={quickLinks} title="روابط ذات صلة" />
            </>
          )}

          {/* 7. القوالب — placeholder cards for future template features */}
          {activeTab === "templates" && (
            <QuickLinksGrid links={quickLinks} title="قوالب جاهزة (قيد التطوير)" />
          )}

          {/* 8. التقويم والسيفالو — AI tab moved here + ceph norms placeholder */}
          {activeTab === "ortho-ceph" && (
            <>
              <AiTab />
              <QuickLinksGrid links={quickLinks} title="أدوات التقويم والسيفالو" />
            </>
          )}
        </div>
      </div>
    </div>
  );
}

export default function SettingsPage() {
  return (
    <Suspense
      fallback={
        <div className="min-h-screen flex items-center justify-center text-sm text-slate-500">
          جاري التحميل...
        </div>
      }
    >
      <SettingsPageInner />
    </Suspense>
  );
}
