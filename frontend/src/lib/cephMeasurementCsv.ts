import type { CephMeasurement, MeasurementGroup } from "@/types/ceph";

const UTF8_BOM = "\ufeff";

const GROUP_LABELS: Record<MeasurementGroup, string> = {
  steiner: "ستاينر",
  tweed: "تويد",
  mcnamara: "ماكنامارا",
  ricketts: "ريكتس",
  downs: "داونز",
  jarabak: "جاراباك",
  wits: "وتس",
  pa: "أمامي PA",
};

const SEVERITY_LABELS: Record<CephMeasurement["severity"], string> = {
  normal: "طبيعي",
  mild: "انحراف خفيف",
  severe: "انحراف واضح",
  unclassified: "وصفي بلا معيار عام",
};

const DIRECTION_LABELS: Record<CephMeasurement["direction"], string> = {
  above: "أعلى من المعيار",
  below: "أقل من المعيار",
  within: "ضمن المعيار",
};

export interface CephMeasurementCsvInput {
  analysisId: string;
  patientName: string;
  analysisDate: string;
  caseNumber?: string | null;
  measurements: CephMeasurement[];
}

function protectSpreadsheetText(value: string): string {
  const trimmedStart = value.trimStart();
  return /^[=+\-@]/.test(trimmedStart) ? `'${value}` : value;
}

function csvCell(value: string | number | null | undefined): string {
  const raw = typeof value === "number"
    ? String(value)
    : protectSpreadsheetText(value ?? "");
  return `"${raw.replace(/"/g, '""')}"`;
}

function row(values: Array<string | number | null | undefined>): string {
  return values.map(csvCell).join(",");
}

export function buildCephMeasurementCsv(input: CephMeasurementCsvInput): string {
  const metadata = [
    row(["معرّف التحليل", input.analysisId]),
    row(["المريض", input.patientName]),
    row(["رقم الحالة", input.caseNumber ?? ""]),
    row(["تاريخ التحليل", input.analysisDate]),
  ];

  const header = row([
    "مجموعة التحليل",
    "القياس بالعربية",
    "رمز القياس",
    "القيمة",
    "الوحدة",
    "المعيار",
    "الانحراف المعياري",
    "الفرق عن المعيار",
    "الحالة",
    "الاتجاه",
    "التفسير",
  ]);

  const measurementRows = input.measurements.map((measurement) => row([
    GROUP_LABELS[measurement.analysisGroup],
    measurement.nameAr,
    measurement.name,
    measurement.value,
    measurement.unit,
    measurement.normal,
    measurement.stdDev,
    measurement.deviation,
    SEVERITY_LABELS[measurement.severity],
    DIRECTION_LABELS[measurement.direction],
    measurement.apiInterpretation ?? measurement.interpretationAr ?? "",
  ]));

  return UTF8_BOM + [...metadata, "", header, ...measurementRows].join("\r\n");
}

export function downloadCephMeasurementCsv(input: CephMeasurementCsvInput): void {
  const blob = new Blob([buildCephMeasurementCsv(input)], {
    type: "text/csv;charset=utf-8;",
  });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = `ceph-measurements-${input.analysisId}-${input.analysisDate}.csv`;
  link.click();
  URL.revokeObjectURL(url);
}
