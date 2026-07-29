import { Skeleton, TableSkeleton } from "@/components/ui/skeleton";

export default function LabLoading() {
  return (
    <div className="space-y-6" dir="rtl">
      <div className="flex items-center justify-between">
        <Skeleton className="h-7 w-40" />
        <Skeleton className="h-9 w-36" />
      </div>
      <div className="flex gap-3">
        <Skeleton className="h-9 w-24" />
        <Skeleton className="h-9 w-24" />
        <Skeleton className="h-9 w-24" />
      </div>
      <div className="rounded-xl border border-gray-100 bg-white p-5">
        <TableSkeleton rows={7} cols={5} />
      </div>
    </div>
  );
}
