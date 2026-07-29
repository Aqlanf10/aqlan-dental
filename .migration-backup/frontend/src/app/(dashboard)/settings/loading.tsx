import { Skeleton } from "@/components/ui/skeleton";

export default function SettingsLoading() {
  return (
    <div className="space-y-6" dir="rtl">
      <Skeleton className="h-7 w-40" />
      <div className="flex gap-3">
        <Skeleton className="h-9 w-28" />
        <Skeleton className="h-9 w-28" />
        <Skeleton className="h-9 w-28" />
      </div>
      <div className="space-y-4 rounded-xl border border-gray-100 bg-white p-6">
        <Skeleton className="h-5 w-1/3" />
        <Skeleton className="h-11 w-full" />
        <Skeleton className="h-11 w-full" />
        <Skeleton className="h-11 w-2/3" />
        <Skeleton className="h-10 w-32" />
      </div>
    </div>
  );
}
