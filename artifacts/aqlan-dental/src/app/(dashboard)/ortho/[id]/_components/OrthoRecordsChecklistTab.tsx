
import { useMemo } from "react";
import { CheckCircle2 } from "lucide-react";
import { cn } from "@/lib/utils";
import { useRecordsChecklist, useSaveChecklist } from "@/hooks/useOrtho";
import type { RecordsChecklist } from "@/types/ortho";
import { RECORDS_CHECKLIST_ITEMS } from "@/types/ortho";
import { QueryErrorState } from "./_shared";

/**
 * Records checklist tab — the "قائمة السجلات المطلوبة" portion of the
 * original `RecordsPanel`. Split out (FE-20) from the photos portion; the two
 * are rendered together by the page shell for the `records` tab.
 */
export function OrthoRecordsChecklistTab({ caseId }: { caseId: string }) {
  const {
    data: checklist,
    isError,
    refetch: refetchChecklist,
  } = useRecordsChecklist(caseId);
  const saveChecklist = useSaveChecklist(caseId);

  // Group checklist items
  const grouped = useMemo(() => {
    const map = new Map<string, typeof RECORDS_CHECKLIST_ITEMS>();
    for (const item of RECORDS_CHECKLIST_ITEMS) {
      const list = map.get(item.group) ?? [];
      list.push(item);
      map.set(item.group, list);
    }
    return map;
  }, []);

  const completedCount = useMemo(() => {
    if (!checklist) return 0;
    return RECORDS_CHECKLIST_ITEMS.filter(
      (item) => checklist[item.key]
    ).length;
  }, [checklist]);

  const totalCount = RECORDS_CHECKLIST_ITEMS.length;
  const percent =
    totalCount > 0 ? Math.round((completedCount / totalCount) * 100) : 0;

  const toggleItem = (key: keyof RecordsChecklist) => {
    if (!checklist) return;
    const newValue = !checklist[key];
    saveChecklist.mutate(
      { [key]: newValue },
      {
        onSuccess: () => {
          refetchChecklist();
        },
      }
    );
  };

  // ORTHO-REQ-006: on a failed fetch the checklist would render all-unchecked
  // (0%) AND silently read-only (toggleItem early-returns on !checklist).
  if (isError) {
    return (
      <QueryErrorState
        text="تعذر تحميل قائمة السجلات — تحقق من الاتصال وحاول مجددًا"
        onRetry={() => refetchChecklist()}
      />
    );
  }

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-5">
      <div className="mb-4 flex items-center justify-between">
        <div>
          <h2 className="font-semibold text-gray-900">
            قائمة السجلات المطلوبة
          </h2>
          <p className="text-sm text-gray-500">
            {completedCount} من {totalCount} عنصر مكتمل ({percent}%)
          </p>
        </div>
        <div className="flex items-center gap-3">
          <div className="h-2 w-24 overflow-hidden rounded-full bg-gray-100">
            <div
              className="h-full rounded-full bg-clinic-blue transition-all"
              style={{ width: `${percent}%` }}
            />
          </div>
          <span className="text-sm font-semibold text-clinic-blue">
            {percent}%
          </span>
        </div>
      </div>

      <div className="space-y-5">
        {Array.from(grouped.entries()).map(([group, items]) => (
          <div key={group}>
            <h3 className="mb-2 text-xs font-semibold uppercase text-gray-400">
              {group}
            </h3>
            <div className="grid gap-2 md:grid-cols-2 lg:grid-cols-3">
              {items.map((item) => {
                const checked = checklist?.[item.key] ?? false;
                return (
                  <button
                    key={item.key}
                    type="button"
                    onClick={() => toggleItem(item.key)}
                    className={cn(
                      "flex items-center gap-3 rounded-lg border px-3 py-2.5 text-start transition",
                      checked
                        ? "border-green-200 bg-green-50"
                        : "border-gray-200 bg-white hover:border-clinic-blue/40"
                    )}
                  >
                    <div
                      className={cn(
                        "flex h-5 w-5 flex-shrink-0 items-center justify-center rounded border transition",
                        checked
                          ? "border-green-500 bg-green-500"
                          : "border-gray-300 bg-white"
                      )}
                    >
                      {checked && (
                        <CheckCircle2 className="h-3.5 w-3.5 text-white" />
                      )}
                    </div>
                    <span
                      className={cn(
                        "text-sm",
                        checked
                          ? "font-medium text-green-800"
                          : "text-gray-700"
                      )}
                    >
                      {item.label}
                    </span>
                  </button>
                );
              })}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
