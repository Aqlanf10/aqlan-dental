import Link from "next/link";
import { ShieldCheck } from "lucide-react";

export default function CephAnalysisLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: { id: string };
}) {
  return (
    <>
      {children}
      <Link
        href={`/ceph/${params.id}/quality`}
        className="fixed bottom-4 end-4 z-40 inline-flex items-center gap-2 rounded-full border border-blue-200 bg-blue-50 px-4 py-2 text-xs font-bold text-blue-800 shadow-lg hover:bg-blue-100"
      >
        <ShieldCheck className="h-4 w-4" />
        فحص جودة السيفالو
      </Link>
    </>
  );
}
