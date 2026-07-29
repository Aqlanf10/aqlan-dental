
import { useState } from "react";
import { Box, Download, Loader2, ShieldCheck } from "lucide-react";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";

interface OrthoSurgicalExportPackagePanelProps {
  orthoSurgicalCaseId: string;
}

export function OrthoSurgicalExportPackagePanel({
  orthoSurgicalCaseId,
}: OrthoSurgicalExportPackagePanelProps) {
  const [busy, setBusy] = useState(false);

  const downloadPackage = async () => {
    setBusy(true);
    try {
      const res = await api.get(
        `/api/ortho-surgical-cases/${orthoSurgicalCaseId}/export-package`
      );
      const json = JSON.stringify(res.data, null, 2);
      const blob = new Blob([json], { type: "application/json;charset=utf-8" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `ortho-surgical-export-${orthoSurgicalCaseId}.json`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
      toast.success("تم تجهيز حزمة التصدير");
    } catch (e) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response
        ?.data?.message;
      toast.error(msg ?? "تعذر تجهيز حزمة التصدير");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="rounded-xl border border-[#dbeafe] bg-white p-5 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-800">
            <Box className="h-4 w-4 text-[#3d7ab5]" />
            حزمة التخطيط الخارجي ثلاثي الأبعاد
          </h2>
          <p className="mt-2 max-w-3xl text-xs leading-6 text-gray-600">
            تصدير قراءة فقط يجمع بيانات الحالة، القياسات، خطة العلاج، سيناريوهات VTO،
            وملفات CBCT/STL/PLY/OBJ المرتبطة بهذه الحالة التقويمية لاستخدامها في
            برامج التخطيط الخارجي مثل 3D Slicer.
          </p>
        </div>
        <button
          type="button"
          onClick={downloadPackage}
          disabled={busy}
          className="inline-flex items-center gap-2 rounded-lg bg-[#163a5f] px-4 py-2 text-sm font-medium text-white transition hover:bg-[#102b47] disabled:opacity-60"
        >
          {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Download className="h-4 w-4" />}
          تنزيل JSON
        </button>
      </div>

      <div className="mt-4 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs leading-6 text-amber-800">
        <div className="flex items-start gap-2">
          <ShieldCheck className="mt-0.5 h-4 w-4 flex-shrink-0" />
          <span>
            هذه الحزمة لا تنشئ segmentation أو splints أو guides ولا تعتمد قراراً جراحياً.
            أي تخطيط خارجي يحتاج مراجعة أخصائي التقويم وجراح الفم والفكين.
          </span>
        </div>
      </div>
    </div>
  );
}
