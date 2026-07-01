"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { Loader2 } from "lucide-react";
import api from "@/lib/api";
import type { OrthoSurgicalCaseDetail } from "@/types/orthoSurgical";

export default function OrthoSurgicalLegacyDetailRoute() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    api.get<OrthoSurgicalCaseDetail>(`/api/ortho-surgical-cases/${id}`)
      .then((res) => {
        if (!cancelled) router.replace(`/ortho/${res.data.orthoCaseId}?tab=surgical`);
      })
      .catch(() => {
        if (!cancelled) setFailed(true);
      });
    return () => { cancelled = true; };
  }, [id, router]);

  if (failed) {
    return (
      <div className="py-20 text-center">
        <p className="text-sm text-gray-500">تعذر فتح التخطيط الجراحي من الرابط القديم.</p>
        <Link href="/ortho" className="mt-3 inline-flex text-sm font-medium text-clinic-blue hover:underline">
          العودة إلى حالات التقويم
        </Link>
      </div>
    );
  }

  return (
    <div className="flex items-center justify-center py-24 text-sm text-gray-500">
      <Loader2 className="ml-2 h-4 w-4 animate-spin" />
      جار فتح التخطيط الجراحي داخل حالة التقويم...
    </div>
  );
}
