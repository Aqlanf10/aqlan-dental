import type { LandmarkGroup } from "@/types/ceph";

export interface PilotLandmarkDefinition {
  key: string;
  nameAr: string;
  instructionAr: string;
  group: LandmarkGroup;
  core: boolean;
}

export const PILOT_LANDMARKS: readonly PilotLandmarkDefinition[] = [
  { key: "S", nameAr: "سيلا", instructionAr: "المركز الهندسي لحدود السرج التركي الشعاعية.", group: "cranial", core: true },
  { key: "N", nameAr: "نازيون", instructionAr: "أكثر نقطة أمامية في الدرز الجبهي الأنفي.", group: "cranial", core: true },
  { key: "Or", nameAr: "أوربيتال", instructionAr: "أدنى نقطة على الحافة السفلية للحجاج الأقرب للمستقبل.", group: "cranial", core: true },
  { key: "Po", nameAr: "بوريون", instructionAr: "أعلى نقطة في الصماخ السمعي الخارجي التشريحي.", group: "cranial", core: true },
  { key: "ANS", nameAr: "الشوكة الأنفية الأمامية", instructionAr: "أبعد طرف أمامي للشوكة الأنفية العظمية.", group: "maxilla", core: true },
  { key: "PNS", nameAr: "الشوكة الأنفية الخلفية", instructionAr: "النهاية الخلفية للحنك الصلب على المحيط المعتمد.", group: "maxilla", core: true },
  { key: "A", nameAr: "النقطة A", instructionAr: "أعمق تقعر أمامي للفك العلوي بين ANS والعرف السنخي.", group: "maxilla", core: true },
  { key: "B", nameAr: "النقطة B", instructionAr: "أعمق تقعر أمامي للفك السفلي بين السنخ والذقن.", group: "mandible", core: true },
  { key: "Pog", nameAr: "بوغونيون عظمي", instructionAr: "أكثر نقطة أمامية على القشرة الخارجية للذقن العظمي.", group: "mandible", core: true },
  { key: "Gn", nameAr: "غناثيون", instructionAr: "النقطة المنشأة بين Pog وMe وفق منصف المستويين.", group: "mandible", core: true },
  { key: "Me", nameAr: "منتون", instructionAr: "أدنى نقطة في الارتفاق العظمي للفك السفلي.", group: "mandible", core: true },
  { key: "Go", nameAr: "غونيون", instructionAr: "تقاطع منصف مماسي الحافة الخلفية للرامس والسفلية للجسم.", group: "mandible", core: true },
  { key: "Co", nameAr: "كونديليون", instructionAr: "أكثر نقطة خلفية علوية على رأس اللقمة حسب قاعدة الجانب المعتمدة.", group: "mandible", core: true },
  { key: "Ar", nameAr: "أرتيكولاري", instructionAr: "تقاطع محيط الرامس الخلفي مع محيط قاعدة القحف السفلي.", group: "mandible", core: true },
  { key: "D", nameAr: "النقطة D", instructionAr: "نقطة على المحيط الشفوي للارتفاق بين B والذقن العظمي.", group: "mandible", core: true },
  { key: "Pm", nameAr: "بروز الذقن", instructionAr: "موضع تحول محيط الارتفاق الأمامي من مقعر إلى محدب فوق Pog.", group: "mandible", core: true },
  { key: "U1T", nameAr: "حافة القاطع العلوي", instructionAr: "طرف الحافة القاطعة للثنية العلوية المختارة.", group: "dental", core: true },
  { key: "U1A", nameAr: "ذروة القاطع العلوي", instructionAr: "ذروة جذر السن نفسه المستخدم في U1T.", group: "dental", core: true },
  { key: "L1T", nameAr: "حافة القاطع السفلي", instructionAr: "طرف الحافة القاطعة للثنية السفلية المختارة.", group: "dental", core: true },
  { key: "L1A", nameAr: "ذروة القاطع السفلي", instructionAr: "ذروة جذر السن نفسه المستخدم في L1T.", group: "dental", core: true },
  { key: "LS", nameAr: "الشفة العلوية", instructionAr: "أكثر نقطة أمامية على حد الشفة العلوية المرئي.", group: "soft", core: true },
  { key: "LI", nameAr: "الشفة السفلية", instructionAr: "أكثر نقطة أمامية على حد الشفة السفلية المرئي.", group: "soft", core: true },
  { key: "Pn", nameAr: "برونازال", instructionAr: "أكثر نقطة أمامية في طرف الأنف الرخو.", group: "soft", core: true },
  { key: "Cm", nameAr: "الكولوميلا", instructionAr: "نقطة المماس الأمامية السفلية للكولوميلا في البناء الأنفي الشفوي.", group: "soft", core: true },
  { key: "SPog", nameAr: "بوغونيون رخو", instructionAr: "أكثر نقطة أمامية في جلد الذقن فوق Pog العظمي.", group: "soft", core: false },
  { key: "U6", nameAr: "الرحى الأولى العلوية", instructionAr: "النقطة الإطباقية الأنسية للرحى الأولى العلوية المختارة.", group: "dental", core: false },
  { key: "L6", nameAr: "الرحى الأولى السفلية", instructionAr: "النقطة الإطباقية الأنسية للرحى الأولى السفلية المقابلة.", group: "dental", core: false },
] as const;

export const PILOT_CORE_LANDMARKS = PILOT_LANDMARKS.filter(item => item.core);
export const PILOT_LANDMARK_BY_KEY = new Map(PILOT_LANDMARKS.map(item => [item.key, item]));
