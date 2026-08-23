import type { Bundle } from "./types";

/**
 * Translation bundles.
 *
 * `ar` is the source of truth: its values are the exact strings the components show today, so
 * an untranslated key falls back to what is already on screen rather than to a blank or a key
 * name. `en` is allowed to be incomplete — that is the whole point of the fallback.
 *
 * Keys are namespaced by surface (`nav.*`, `common.*`, `lab.*`) so a translator can be given
 * one area at a time, and so a missing key points at the screen it belongs to.
 *
 * Clinical and financial terms are translated deliberately, not mechanically. Where the English
 * is a term of art the Arabic does not map onto one-to-one, the note says so rather than
 * guessing — a wrong term on a prescription is worse than an untranslated one.
 */

export const ar: Bundle = {
  // ── Navigation ────────────────────────────────────────────────────────────
  "nav.dashboard": "لوحة التحكم",
  "nav.dailyOperations": "العمليات اليومية",
  "nav.patients": "المرضى",
  "nav.appointments": "المواعيد",
  "nav.schedule": "الجدول",
  "nav.doctorClinic": "عيادة الطبيب",
  "nav.ortho": "التقويم",
  "nav.general": "طب الأسنان العام",
  "nav.surgery": "جراحة الفم",
  "nav.lab": "المعامل",
  "nav.inventory": "المخزون",
  "nav.finance": "المالية",
  "nav.reports": "التقارير",
  "nav.employees": "الموظفون",
  "nav.settings": "الإعدادات",
  "nav.messages": "الرسائل",
  "nav.ceph": "السيفالومتري",
  "nav.prescriptions": "الوصفات الطبية",
  "nav.referrals": "الإحالات",
  "nav.whatsapp": "واتساب",
  "nav.sms": "رسائل SMS",
  "nav.patientSegments": "مجموعات المرضى",
  "nav.bookingRequests": "طلبات الحجز",
  "nav.radiologyOrders": "طلبات الأشعة",
  "nav.doctors": "الأطباء",
  "nav.branches": "الفروع",
  "nav.hr": "الموارد البشرية",

  // ── Shared actions ────────────────────────────────────────────────────────
  "common.save": "حفظ",
  "common.cancel": "إلغاء",
  "common.delete": "حذف",
  "common.edit": "تعديل",
  "common.add": "إضافة",
  "common.search": "بحث",
  "common.close": "إغلاق",
  "common.confirm": "تأكيد",
  "common.back": "رجوع",
  "common.next": "التالي",
  "common.print": "طباعة",
  "common.export": "تصدير",
  "common.loading": "جارٍ التحميل…",
  "common.noResults": "لا توجد نتائج",
  "common.required": "مطلوب",
  "common.optional": "اختياري",
  "common.yes": "نعم",
  "common.no": "لا",
  "common.total": "الإجمالي",
  "common.date": "التاريخ",
  "common.status": "الحالة",
  "common.notes": "ملاحظات",
  "common.actions": "إجراءات",

  // ── Language switching ────────────────────────────────────────────────────
  "language.label": "اللغة",
  "language.arabic": "العربية",
  "language.english": "English",
  "language.switchTo": "التبديل إلى الإنجليزية",

  // ── Lab module ────────────────────────────────────────────────────────────
  "lab.orders": "أوامر المختبر",
  "lab.orderNumber": "رقم الطلب",
  "lab.applianceType": "نوع العمل",
  "lab.shade": "الظل (اللون)",
  "lab.tooth": "رقم الأسنان",
  "lab.materials": "المواد",
  "lab.sendToLab": "واتساب",
  "lab.expectedDate": "تاريخ الاستلام المتوقع",

  // ── Top bar ───────────────────────────────────────────────────────────────
  "topbar.messages": "الرسائل",
  "topbar.logout": "تسجيل الخروج",
  "topbar.password.current": "كلمة المرور الحالية",
  "topbar.password.new": "كلمة المرور الجديدة (8 أحرف+)",
  "topbar.password.confirm": "تأكيد كلمة المرور الجديدة",
  "topbar.password.mismatch": "كلمة المرور الجديدة غير متطابقة",
  "topbar.password.tooShort": "يجب أن تكون 8 أحرف على الأقل",
  "topbar.save": "حفظ",
  "topbar.saving": "جارٍ الحفظ...",
  "common.genericError": "حدث خطأ",
  "topbar.avatarInitialFallback": "م",
  "topbar.changePassword": "تغيير كلمة المرور",
};

export const en: Bundle = {
  // ── Navigation ────────────────────────────────────────────────────────────
  "nav.dashboard": "Dashboard",
  "nav.dailyOperations": "Daily Operations",
  "nav.patients": "Patients",
  "nav.appointments": "Appointments",
  "nav.schedule": "Schedule",
  "nav.doctorClinic": "Doctor Clinic",
  "nav.ortho": "Orthodontics",
  "nav.general": "General Dentistry",
  "nav.surgery": "Oral Surgery",
  "nav.lab": "Laboratory",
  "nav.inventory": "Inventory",
  "nav.finance": "Finance",
  "nav.reports": "Reports",
  "nav.employees": "Employees",
  "nav.settings": "Settings",
  "nav.messages": "Messages",
  "nav.prescriptions": "Prescriptions",
  "nav.referrals": "Referrals",
  "nav.whatsapp": "WhatsApp",
  "nav.sms": "SMS",
  "nav.patientSegments": "Patient Segments",
  "nav.bookingRequests": "Booking Requests",
  "nav.radiologyOrders": "Radiology Orders",
  "nav.doctors": "Doctors",
  "nav.branches": "Branches",
  "nav.hr": "Human Resources",
  // nav.ceph is intentionally absent: cephalometry is a specialist term the owner uses in
  // Arabic, and leaving it untranslated keeps a live example of the Arabic fallback in the
  // running app rather than only in a test.

  // ── Shared actions ────────────────────────────────────────────────────────
  "common.save": "Save",
  "common.cancel": "Cancel",
  "common.delete": "Delete",
  "common.edit": "Edit",
  "common.add": "Add",
  "common.search": "Search",
  "common.close": "Close",
  "common.confirm": "Confirm",
  "common.back": "Back",
  "common.next": "Next",
  "common.print": "Print",
  "common.export": "Export",
  "common.loading": "Loading…",
  "common.noResults": "No results",
  "common.required": "Required",
  "common.optional": "Optional",
  "common.yes": "Yes",
  "common.no": "No",
  "common.total": "Total",
  "common.date": "Date",
  "common.status": "Status",
  "common.notes": "Notes",
  "common.actions": "Actions",

  // ── Language switching ────────────────────────────────────────────────────
  "language.label": "Language",
  "language.arabic": "العربية",
  "language.english": "English",
  "language.switchTo": "Switch to Arabic",

  // ── Lab module ────────────────────────────────────────────────────────────
  "lab.orders": "Lab Orders",
  "lab.orderNumber": "Order number",
  "lab.applianceType": "Work type",
  // "الظل" is the dental shade (tooth colour), not a shadow.
  "lab.shade": "Shade",
  "lab.tooth": "Tooth number",
  "lab.materials": "Materials",
  "lab.sendToLab": "WhatsApp",
  "lab.expectedDate": "Expected return date",

  // ── Top bar ───────────────────────────────────────────────────────────────
  "topbar.messages": "Messages",
  "topbar.logout": "Sign out",
  "topbar.password.current": "Current password",
  "topbar.password.new": "New password (8+ characters)",
  "topbar.password.confirm": "Confirm new password",
  "topbar.password.mismatch": "The new passwords do not match",
  "topbar.password.tooShort": "Must be at least 8 characters",
  "topbar.save": "Save",
  "topbar.saving": "Saving…",
  "common.genericError": "Something went wrong",
  "topbar.avatarInitialFallback": "U",
  "topbar.changePassword": "Change password",
};

export const BUNDLES: Record<string, Bundle> = { ar, en };
