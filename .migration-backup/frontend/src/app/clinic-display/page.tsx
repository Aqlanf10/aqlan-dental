"use client";

import dynamic from "next/dynamic";
import { useLayoutEffect, useState } from "react";
import api from "@/lib/api";

const ClinicDisplayContent = dynamic(
  () => import("@/components/clinic-display/ClinicDisplayContent"),
  { ssr: false }
);

/**
 * CORE-PAT-020 security boundary for the waiting-room display.
 *
 * The legacy display component fetches the Railway URL directly. Keep its large,
 * battle-tested UI intact, but intercept only the queue-data request and route it
 * through the staff axios client so the JWT, refresh flow, and Arabic error contract
 * are applied. The child is mounted only after the scoped interceptor is installed.
 */
export default function ClinicDisplayPage() {
  const [ready, setReady] = useState(false);

  useLayoutEffect(() => {
    const originalFetch = window.fetch.bind(window);

    window.fetch = async (input: RequestInfo | URL, init?: RequestInit) => {
      const url =
        typeof input === "string"
          ? input
          : input instanceof URL
            ? input.toString()
            : input.url;

      if (url.endsWith("/api/clinic-queue/display")) {
        const response = await api.get("/api/clinic-queue/display", {
          headers: { "Cache-Control": "no-store" },
        });

        return new Response(JSON.stringify(response.data), {
          status: response.status,
          statusText: response.statusText,
          headers: { "Content-Type": "application/json; charset=utf-8" },
        });
      }

      return originalFetch(input, init);
    };

    setReady(true);
    return () => {
      window.fetch = originalFetch;
    };
  }, []);

  if (!ready) {
    return (
      <div className="min-h-screen bg-[#0F172A] text-white flex items-center justify-center" dir="rtl">
        <p className="text-xl text-gray-400">جاري تأمين شاشة الانتظار…</p>
      </div>
    );
  }

  return <ClinicDisplayContent />;
}
