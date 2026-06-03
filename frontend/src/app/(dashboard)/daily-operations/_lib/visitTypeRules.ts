import type { TodayJourneyItem } from "./constants";

export interface VisitTypeRule {
  code: string;
  arabicName: string;
  aliases: string[];
  requiresPaymentBeforeEntry: boolean;
  requiresPricedService: boolean;
  allowEntryWithoutPayment: boolean;
  isMultiSession: boolean;
  allowsPartialPayment: boolean;
  allowsManagerOverride: boolean;
  defaultDurationMinutes: number;
  visibleInAppointments: boolean;
  visibleInDailyOperations: boolean;
  active: boolean;
}

export const VISIT_TYPE_RULES: VisitTypeRule[] = [
  rule("new_consultation", "كشف جديد / استشارة", ["NewConsultation", "Consultation", "New Patient", "كشف", "استشارة"], true, true, false, false, false, true, 30),
  rule("free_follow_up", "مراجعة مجانية", ["FreeFollowUp", "Free Follow-up", "مراجعة مجانية"], false, false, true, false, false, false, 15),
  rule("regular_follow_up", "متابعة عادية", ["FollowUp", "RegularFollowUp", "متابعة عادية", "مراجعة"], false, false, true, false, false, true, 20),
  rule("ortho_follow_up", "متابعة تقويم", ["OrthoFollowUp", "Orthodontic Follow-up", "متابعة تقويم"], true, false, false, true, true, true, 20),
  rule("new_ortho_appliance", "تركيب تقويم جديد", ["NewOrthoAppliance", "Orthodontic Start", "تركيب تقويم"], true, true, false, true, true, true, 45),
  rule("ortho_repair", "تعديل جهاز تقويم أو إصلاح براكيت/سلك", ["OrthoRepair", "Bracket Repair", "Wire Repair", "إصلاح براكيت", "تعديل تقويم"], false, true, true, true, true, true, 20),
  rule("general_treatment", "علاج عام", ["GeneralTreatment", "Treatment", "علاج عام"], false, true, true, false, true, true, 40),
  rule("cosmetic_filling", "حشوات تجميلية", ["CosmeticFilling", "Filling", "حشوات"], false, true, true, false, true, true, 40),
  rule("root_canal", "علاج عصب", ["RootCanal", "علاج عصب"], false, true, true, true, true, true, 60),
  rule("root_canal_retreatment", "إعادة علاج عصب", ["RootCanalRetreatment", "إعادة علاج عصب"], false, true, true, true, true, true, 60),
  rule("cleaning", "تنظيف وتلميع", ["Cleaning", "Scaling", "تنظيف"], false, true, true, false, true, true, 30),
  rule("whitening", "تبييض", ["Whitening", "تبييض"], true, true, false, false, true, true, 45),
  rule("simple_extraction", "خلع عادي", ["SimpleExtraction", "Extraction", "خلع عادي"], false, true, true, false, true, true, 30),
  rule("surgical_extraction", "خلع جراحي", ["SurgicalExtraction", "خلع جراحي"], false, true, true, false, true, true, 45),
  rule("oral_surgery", "جراحة فموية بسيطة", ["OralSurgery", "جراحة فموية"], false, true, true, false, true, true, 45),
  rule("fixed_prosthodontics", "تركيبات ثابتة", ["FixedProsthodontics", "Crown", "Bridge", "تركيبات ثابتة"], true, true, false, true, true, true, 45),
  rule("removable_prosthodontics", "تركيبات متحركة", ["RemovableProsthodontics", "Denture", "تركيبات متحركة"], true, true, false, true, true, true, 45),
  rule("veneer", "فينير / ابتسامة تجميلية", ["Veneer", "Smile Design", "فينير"], true, true, false, true, true, true, 45),
  rule("implant", "زراعة أسنان", ["Implant", "Dental Implant", "زراعة"], true, true, false, true, true, true, 60),
  rule("implant_follow_up", "متابعة زراعة", ["ImplantFollowUp", "متابعة زراعة"], false, false, true, true, true, true, 20),
  rule("perio", "علاج لثة", ["Perio", "Periodontal", "علاج لثة"], false, true, true, true, true, true, 40),
  rule("pediatric", "أطفال", ["Pediatric", "Child", "أطفال"], false, true, true, false, true, true, 30),
  rule("radiology", "أشعة / تصوير", ["Radiology", "XRay", "أشعة", "تصوير"], false, true, true, false, true, true, 15),
  rule("lab_order", "طلب معمل", ["LabOrder", "طلب معمل"], false, true, true, true, true, true, 20),
  rule("lab_receive", "استلام عمل من المعمل", ["LabReceive", "استلام معمل"], false, false, true, true, false, false, 15),
  rule("prostho_try_in", "تجربة تركيبة", ["TryIn", "تجربة تركيبة"], false, false, true, true, false, false, 20),
  rule("prostho_delivery", "تسليم نهائي للتركيبة", ["FinalDelivery", "تسليم نهائي"], false, true, true, true, true, true, 30),
  rule("emergency", "حالة إسعافية", ["Emergency", "Urgent", "حالة إسعافية", "إسعافية"], false, true, true, false, true, true, 20),
  rule("internal_referral", "تحويل داخلي", ["InternalReferral", "تحويل داخلي"], false, false, true, false, false, true, 20),
  rule("walk_in", "دخول مباشر بدون موعد", ["WalkIn", "دخول مباشر"], false, true, true, false, true, true, 30),
];

export function resolveVisitTypeRule(appointmentType?: string | null): VisitTypeRule {
  if (!appointmentType) return VISIT_TYPE_RULES[2];
  const normalized = normalize(appointmentType);
  return VISIT_TYPE_RULES.find((ruleItem) =>
    normalize(ruleItem.code) === normalized ||
    normalize(ruleItem.arabicName) === normalized ||
    ruleItem.aliases.some((alias) => normalize(alias) === normalized || normalized.includes(normalize(alias)))
  ) ?? VISIT_TYPE_RULES[2];
}

export function getFinancialGate(item: TodayJourneyItem): {
  label: string;
  reason: string;
  tone: "danger" | "success" | "warning" | "neutral";
} {
  if (item.paymentBeforeEntryRequired || item.financialEntryStatus === "WaitingForPayment") {
    return {
      label: "منتظر الدفع",
      reason: item.financialEntryReason ?? "توجد دفعة مطلوبة قبل دخول الطبيب",
      tone: "danger",
    };
  }

  const ruleItem = resolveVisitTypeRule(item.appointmentType);
  if (ruleItem.code === "emergency") {
    return {
      label: "حالة إسعافية",
      reason: "يسمح بالدخول حسب أولوية الحالة مع توثيق السبب",
      tone: "warning",
    };
  }

  if (item.consultationFeeRequired && item.consultationFeePaid) {
    return {
      label: "مدفوع قبل الدخول",
      reason: "تم تحصيل رسوم الزيارة المطلوبة",
      tone: "success",
    };
  }

  return {
    label: ruleItem.allowEntryWithoutPayment ? "الدخول مسموح" : "جاهز للتشغيل",
    reason: ruleItem.allowEntryWithoutPayment ? "لا توجد دفعة إلزامية قبل الدخول لهذه الزيارة" : "لا توجد موانع مالية حالية",
    tone: "neutral",
  };
}

function rule(
  code: string,
  arabicName: string,
  aliases: string[],
  requiresPaymentBeforeEntry: boolean,
  requiresPricedService: boolean,
  allowEntryWithoutPayment: boolean,
  isMultiSession: boolean,
  allowsPartialPayment: boolean,
  allowsManagerOverride: boolean,
  defaultDurationMinutes: number
): VisitTypeRule {
  return {
    code,
    arabicName,
    aliases,
    requiresPaymentBeforeEntry,
    requiresPricedService,
    allowEntryWithoutPayment,
    isMultiSession,
    allowsPartialPayment,
    allowsManagerOverride,
    defaultDurationMinutes,
    visibleInAppointments: true,
    visibleInDailyOperations: true,
    active: true,
  };
}

function normalize(value: string): string {
  return value.trim().toLowerCase().replace(/\s+/g, "");
}
