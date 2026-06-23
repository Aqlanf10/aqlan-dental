import { Skeleton } from "@/components/ui/skeleton";

export default function CephLoading() {
  return (
    <div className="space-y-6" dir="rtl">
      <Skeleton className="h-7 w-48" />
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <div className="lg:col-span-2 rounded-xl border border-gray-100 bg-white p-5">
          <Skeleton className="mb-4 h-5 w-1/4" />
          <Skeleton className="h-80 w-full" />
        </div>
        <div className="space-y-3 rounded-xl border border-gray-100 bg-white p-5">
          <Skeleton className="h-5 w-1/2" />
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
        </div>
      </div>
    </div>
  );
}
