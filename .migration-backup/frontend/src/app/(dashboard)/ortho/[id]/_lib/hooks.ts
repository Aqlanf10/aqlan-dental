"use client";

import { useEffect, useState } from "react";
import type { Tab } from "./types";

/**
 * useActiveTab — owns the active-tab state and keeps the `?tab=` query param
 * in sync with the URL (replaceState, no navigation). Extracted verbatim from
 * the original page.tsx so the behavior is unchanged.
 *
 * FE-20: structural extraction only — no logic changes.
 */
export function useActiveTab(tabs: { key: Tab }[]): {
  activeTab: Tab;
  setActiveTab: (tab: Tab) => void;
} {
  const [activeTab, setActiveTab] = useState<Tab>("overview");

  useEffect(() => {
    const requestedTab = new URLSearchParams(
      window.location.search
    ).get("tab") as Tab | null;
    if (requestedTab && tabs.some((tab) => tab.key === requestedTab)) {
      setActiveTab(requestedTab);
    }
  }, [tabs]);

  const setActiveTabWithUrl = (tab: Tab) => {
    setActiveTab(tab);
    const url = new URL(window.location.href);
    if (tab === "overview") url.searchParams.delete("tab");
    else url.searchParams.set("tab", tab);
    window.history.replaceState(null, "", `${url.pathname}${url.search}`);
  };

  return { activeTab, setActiveTab: setActiveTabWithUrl };
}
