const statusLabels: Record<string, string> = {
  Scheduled: "مجدول",
  Confirmed: "مؤكد",
  Arrived: "وصل",
  Waiting: "في الانتظار",
  Called: "تم النداء",
  InProgress: "قيد العلاج",
  Completed: "مكتمل",
  Cancelled: "ملغي",
  NoShow: "لم يحضر"
};

export function appointmentStatusLabel(value: string): string {
  return statusLabels[value] ?? value;
}

export function formatYemeniRial(value: number): string {
  return new Intl.NumberFormat("ar-YE", {
    maximumFractionDigits: 0
  }).format(value) + " ر.ي";
}

export function fullPatientName(patient: {
  firstName: string;
  middleName?: string | null;
  lastName: string;
}): string {
  return [patient.firstName, patient.middleName, patient.lastName]
    .filter(Boolean)
    .join(" ");
}

export function isoDateLocal(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}
