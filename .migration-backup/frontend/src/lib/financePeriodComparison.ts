import { localDateString } from "@/lib/utils";

export interface FinancePeriodRange {
  fromDate: string;
  toDate: string;
}

export interface FinancePeriodComparisonRanges {
  current: FinancePeriodRange;
  previous: FinancePeriodRange;
}

export function buildMonthToDateComparisonRanges(today: Date = new Date()): FinancePeriodComparisonRanges {
  const currentStart = new Date(today.getFullYear(), today.getMonth(), 1);
  const previousStart = new Date(today.getFullYear(), today.getMonth() - 1, 1);
  const previousMonthLastDay = new Date(today.getFullYear(), today.getMonth(), 0).getDate();
  const previousEndDay = Math.min(today.getDate(), previousMonthLastDay);
  const previousEnd = new Date(previousStart.getFullYear(), previousStart.getMonth(), previousEndDay);

  return {
    current: {
      fromDate: localDateString(currentStart),
      toDate: localDateString(today),
    },
    previous: {
      fromDate: localDateString(previousStart),
      toDate: localDateString(previousEnd),
    },
  };
}

export function calculatePercentageChange(current: number, previous: number): number | null {
  if (previous === 0) {
    return current === 0 ? 0 : null;
  }

  return ((current - previous) / Math.abs(previous)) * 100;
}
