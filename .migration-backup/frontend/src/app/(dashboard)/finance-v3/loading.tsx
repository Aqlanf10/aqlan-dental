import { CardSkeleton, Skeleton } from "@/components/ui/skeleton";

export default function FinanceLoading() {
  return (
    <div className="space-y-6" dir="rtl">
      <Skeleton className="h-7 w-48" />
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <CardSkeleton />
        <CardSkeleton />
        <CardSkeleton />
        <CardSkeleton />
      </div>
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <div className="rounded-xl border border-gray-100 bg-white p-5">
          <Skeleton className="mb-4 h-5 w-1/3" />
          <Skeleton className="h-64 w-full" />
        </div>
        <div className="rounded-xl border border-gray-100 bg-white p-5">
          <Skeleton className="mb-4 h-5 w-1/3" />
          <Skeleton className="h-64 w-full" />
        </div>
      </div>
    </div>
  );
}
