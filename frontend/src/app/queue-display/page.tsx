"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";

/**
 * @deprecated Use /clinic-display instead. This page redirects there.
 * Sprint 7B: Consolidated duplicate display pages.
 */
export default function QueueDisplayPage() {
  const router = useRouter();

  useEffect(() => {
    router.replace("/clinic-display");
  }, [router]);

  return (
    <div
      dir="rtl"
      className="min-h-screen bg-[#0F172A] text-white flex items-center justify-center"
    >
      <p className="text-xl text-gray-400">جاري التحويل إلى شاشة العرض…</p>
    </div>
  );
}
