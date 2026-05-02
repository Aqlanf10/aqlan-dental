
// ============================
// Advanced Cephalometric Analysis Module
// مستوى Dolphin / CephX / BCeph
// ============================

const ANALYSES = {
  steiner: {
    name: 'Steiner',
    fullName: 'تحليل Steiner',
    year: '1953',
    desc: 'المرجع الأساسي — SNA/SNB/ANB والقواطع',
    measurements: [
      { id: 'SNA', label: 'SNA', norm: 82, sd: 2, unit: '°', category: 'skeletal', desc: 'موضع الفك العلوي بالنسبة للقاعدة القحفية' },
      { id: 'SNB', label: 'SNB', norm: 80, sd: 2, unit: '°', category: 'skeletal', desc: 'موضع الفك السفلي بالنسبة للقاعدة القحفية' },
      { id: 'ANB', label: 'ANB', norm: 2, sd: 2, unit: '°', category: 'skeletal', desc: 'العلاقة الهيكلية بين الفكين' },
      { id: 'SND', label: 'SND', norm: 76, sd: 3, unit: '°', category: 'skeletal', desc: 'موضع D-point بالنسبة للقاعدة القحفية' },
      { id: 'OccPlane_SN', label: 'SN-OccPlane', norm: 14, sd: 3, unit: '°', category: 'dental', desc: 'ميل المستوى الإطباقي' },
      { id: 'GoGnSN', label: 'GoGn-SN', norm: 32, sd: 5, unit: '°', category: 'skeletal', desc: 'زاوية المستوى السفلي = نمط النمو العمودي' },
      { id: 'U1_NA_deg', label: 'U1-NA °', norm: 22, sd: 5, unit: '°', category: 'dental', desc: 'ميل القاطعة العلوية' },
      { id: 'U1_NA_mm', label: 'U1-NA mm', norm: 4, sd: 2, unit: 'mm', category: 'dental', desc: 'بروز القاطعة العلوية' },
      { id: 'L1_NB_deg', label: 'L1-NB °', norm: 25, sd: 5, unit: '°', category: 'dental', desc: 'ميل القاطعة السفلية' },
      { id: 'L1_NB_mm', label: 'L1-NB mm', norm: 4, sd: 2, unit: 'mm', category: 'dental', desc: 'بروز القاطعة السفلية' },
      { id: 'Interincisal', label: 'IIA — زاوية القواطع', norm: 130, sd: 10, unit: '°', category: 'dental', desc: 'الزاوية بين القاطعتين العلوية والسفلية' },
      { id: 'Pog_NB', label: 'Pog-NB', norm: 4, sd: 2, unit: 'mm', category: 'skeletal', desc: 'بروز الذقن — Pogonion' },
      { id: 'L1_APog', label: 'L1-APog', norm: 2, sd: 2, unit: 'mm', category: 'dental', desc: 'موضع القاطعة السفلية' },
    ]
  },
  ricketts: {
    name: 'Ricketts',
    fullName: 'تحليل Ricketts',
    year: '1960',
    desc: 'VTO والتحليل الوجهي المتكامل',
    measurements: [
      { id: 'FacialAngle', label: 'Facial Angle', norm: 87, sd: 3, unit: '°', category: 'skeletal', desc: 'NPog/FH — اتجاه الوجه' },
      { id: 'ConvexityAngle', label: 'Convexity — A-NPog', norm: 2, sd: 2, unit: 'mm', category: 'skeletal', desc: 'تحدب الوجه' },
      { id: 'ABPlane', label: 'AB-NPog', norm: -4, sd: 3, unit: '°', category: 'skeletal', desc: 'العلاقة بين القواعد السنية والفك' },
      { id: 'MandPlane', label: 'Mand Plane — FH', norm: 26, sd: 4, unit: '°', category: 'skeletal', desc: 'ميل الفك السفلي' },
      { id: 'LFH', label: 'LFH — ANS-Me', norm: 57, sd: 3, unit: '°', category: 'skeletal', desc: 'الارتفاع الوجهي السفلي' },
      { id: 'Jarabak', label: 'Jarabak Ratio', norm: 65, sd: 3, unit: '%', category: 'skeletal', desc: 'نسبة الارتفاع الأمامي/الخلفي' },
      { id: 'U1_AP', label: 'U1 to AP plane', norm: 3.5, sd: 2, unit: 'mm', category: 'dental', desc: 'موضع القاطعة العلوية' },
      { id: 'U1_FH', label: 'U1-FH', norm: 111, sd: 5, unit: '°', category: 'dental', desc: 'ميل القاطعة العلوية لـ FH' },
      { id: 'L1_APog', label: 'L1 to APog', norm: 1, sd: 2, unit: 'mm', category: 'dental', desc: 'موضع القاطعة السفلية' },
      { id: 'Interincisal_R', label: 'Interincisal Angle', norm: 130, sd: 10, unit: '°', category: 'dental', desc: 'الزاوية بين القاطعتين' },
      { id: 'UMolar_PtV', label: 'UM to PtV', norm: 16, sd: 3, unit: 'mm', category: 'dental', desc: 'موضع الرحى العلوية' },
      { id: 'E_Line_Upper', label: 'Upper Lip — E-Line', norm: -2, sd: 2, unit: 'mm', category: 'soft', desc: 'موضع الشفة العلوية من خط E' },
      { id: 'E_Line_Lower', label: 'Lower Lip — E-Line', norm: -2, sd: 2, unit: 'mm', category: 'soft', desc: 'موضع الشفة السفلية من خط E' },
    ]
  },
  mcnamara: {
    name: 'McNamara',
    fullName: 'تحليل McNamara',
    year: '1984',
    desc: 'قياسات خطية لعلاقة الفكين بالقاعدة',
    measurements: [
      { id: 'MaxLen', label: 'Maxillary Length — Co-A', norm: 91, sd: 5, unit: 'mm', category: 'skeletal', desc: 'طول الفك العلوي الفعّال' },
      { id: 'MandLen', label: 'Mandibular Length — Co-Gn', norm: 120, sd: 6, unit: 'mm', category: 'skeletal', desc: 'طول الفك السفلي الفعّال' },
      { id: 'MaxMandDiff', label: 'Mx-Md Difference', norm: 29, sd: 4, unit: 'mm', category: 'skeletal', desc: 'الفرق بين طولي الفكين' },
      { id: 'A_Nasion_Perp', label: 'A to Nasion Perp', norm: 1, sd: 2, unit: 'mm', category: 'skeletal', desc: 'موضع نقطة A من خط Nasion العمودي' },
      { id: 'Pog_Nasion_Perp', label: 'Pog to Nasion Perp', norm: -1, sd: 4, unit: 'mm', category: 'skeletal', desc: 'موضع Pogonion من خط Nasion العمودي' },
      { id: 'UFH', label: 'Upper Facial Height — N-ANS', norm: 53, sd: 4, unit: 'mm', category: 'skeletal', desc: 'الارتفاع الوجهي العلوي' },
      { id: 'LFH_mm', label: 'Lower Facial Height — ANS-Me', norm: 65, sd: 4, unit: 'mm', category: 'skeletal', desc: 'الارتفاع الوجهي السفلي' },
      { id: 'U1_Nasion_Perp', label: 'U1 to NaPerp', norm: 4, sd: 2, unit: 'mm', category: 'dental', desc: 'بروز القاطعة العلوية' },
      { id: 'L1_APog_Mc', label: 'L1 to APog', norm: 2, sd: 2, unit: 'mm', category: 'dental', desc: 'بروز القاطعة السفلية' },
      { id: 'OverbiteAmt', label: 'Overbite', norm: 2, sd: 1, unit: 'mm', category: 'dental', desc: 'العمق التطابقي العمودي' },
      { id: 'OverjetAmt', label: 'Overjet', norm: 2, sd: 1, unit: 'mm', category: 'dental', desc: 'الفجوة الأمامية الأفقية' },
      { id: 'NasoPharynx', label: 'Nasopharynx Width', norm: 22, sd: 4, unit: 'mm', category: 'airway', desc: 'عرض البلعوم الأنفي — مجرى الهواء العلوي' },
      { id: 'OroPharynx', label: 'Oropharynx Width', norm: 15, sd: 4, unit: 'mm', category: 'airway', desc: 'عرض البلعوم الفموي — مجرى الهواء السفلي' },
    ]
  },
  jarabak: {
    name: 'Jarabak',
    fullName: 'تحليل Jarabak — Björk',
    year: '1972',
    desc: 'نمط النمو وتوقعه',
    measurements: [
      { id: 'Saddle_N_S_Ar', label: 'Saddle Angle — N-S-Ar', norm: 123, sd: 5, unit: '°', category: 'skeletal', desc: 'زاوية السرج — النمط العمودي' },
      { id: 'Articular_S_Ar_Go', label: 'Articular Angle — S-Ar-Go', norm: 143, sd: 6, unit: '°', category: 'skeletal', desc: 'الزاوية المفصلية' },
      { id: 'Gonial_Ar_Go_Me', label: 'Gonial Angle — Ar-Go-Me', norm: 130, sd: 7, unit: '°', category: 'skeletal', desc: 'زاوية المفصل السفلي الكاملة' },
      { id: 'UpperGonial', label: 'Upper Gonial — Ar-Go-Na', norm: 52, sd: 4, unit: '°', category: 'skeletal', desc: 'الجزء العلوي من زاوية المفصل' },
      { id: 'LowerGonial', label: 'Lower Gonial — Na-Go-Me', norm: 70, sd: 6, unit: '°', category: 'skeletal', desc: 'الجزء السفلي من زاوية المفصل' },
      { id: 'SumAngles', label: 'Sum of Angles', norm: 396, sd: 6, unit: '°', category: 'skeletal', desc: 'مجموع الزوايا الثلاث — النمط الوجهي' },
      { id: 'PFH', label: 'PFH — S-Go', norm: 73, sd: 5, unit: 'mm', category: 'skeletal', desc: 'الارتفاع الوجهي الخلفي' },
      { id: 'AFH', label: 'AFH — N-Me', norm: 123, sd: 6, unit: 'mm', category: 'skeletal', desc: 'الارتفاع الوجهي الأمامي الكامل' },
      { id: 'JaraRatio', label: 'Jarabak Ratio — PFH/AFH', norm: 62, sd: 3, unit: '%', category: 'skeletal', desc: '>65% نمو أمامي ← <62% نمو خلفي' },
      { id: 'RamusHeight', label: 'Ramus Height — Ar-Go', norm: 44, sd: 4, unit: 'mm', category: 'skeletal', desc: 'ارتفاع الفرع الصاعد للفك السفلي' },
      { id: 'MandBody', label: 'Mandibular Body — Go-Me', norm: 71, sd: 5, unit: 'mm', category: 'skeletal', desc: 'طول جسم الفك السفلي' },
    ]
  },
  tweed: {
    name: 'Tweed',
    fullName: 'تحليل Tweed',
    year: '1954',
    desc: 'المثلث التشخيصي للفك',
    measurements: [
      { id: 'FMPA', label: 'FMA — FH-MP', norm: 25, sd: 5, unit: '°', category: 'skeletal', desc: 'زاوية مستوى الفك السفلي لـ FH — النمط العمودي' },
      { id: 'IMPA', label: 'IMPA — L1-MP', norm: 90, sd: 5, unit: '°', category: 'dental', desc: 'ميل القاطعة السفلية على مستوى الفك السفلي' },
      { id: 'FMIA', label: 'FMIA — L1-FH', norm: 68, sd: 5, unit: '°', category: 'dental', desc: 'ميل القاطعة السفلية على FH' },
      { id: 'TweedSum', label: 'Tweed Triangle Sum', norm: 180, sd: 0, unit: '°', category: 'check', desc: 'يجب أن يساوي 180° دائماً (للتحقق)' },
    ]
  },
  holdaway: {
    name: 'Holdaway',
    fullName: 'تحليل Holdaway للأنسجة الرخوة',
    year: '1983',
    desc: 'الجمال الوجهي وتوازن الشفاه',
    measurements: [
      { id: 'HAngle', label: 'H-Angle', norm: 7, sd: 4, unit: '°', category: 'soft', desc: 'زاوية H للملف الشفوي' },
      { id: 'HLine_Upper', label: 'Upper Lip — H-Line', norm: 0, sd: 1, unit: 'mm', category: 'soft', desc: 'علاقة الشفة العلوية بخط H' },
      { id: 'SkelConvex', label: 'Skeletal Profile Convexity', norm: 0, sd: 2, unit: 'mm', category: 'soft', desc: 'تحدب الملف الهيكلي' },
      { id: 'NasoLabial', label: 'Nasolabial Angle', norm: 100, sd: 10, unit: '°', category: 'soft', desc: 'الزاوية الأنفية الشفوية — يؤثر على جمال الوجه' },
      { id: 'UpperLipThick', label: 'Upper Lip Thickness', norm: 15, sd: 2, unit: 'mm', category: 'soft', desc: 'سماكة الشفة العلوية' },
      { id: 'UpperLipStrain', label: 'Upper Lip Strain', norm: 0, sd: 1, unit: 'mm', category: 'soft', desc: 'توتر الشفة العلوية — 0=مريح، + = توتر' },
      { id: 'MentoLabial', label: 'Mentolabial Sulcus', norm: 4, sd: 2, unit: 'mm', category: 'soft', desc: 'عمق الأخدود الذقني الشفوي' },
      { id: 'SoftTissueFacial', label: 'Soft Tissue Facial Angle', norm: 90, sd: 4, unit: '°', category: 'soft', desc: 'الزاوية الوجهية للأنسجة الرخوة' },
      { id: 'Chin_H', label: 'Chin to H-Line', norm: 0, sd: 2, unit: 'mm', category: 'soft', desc: 'موضع الذقن من خط H' },
    ]
  },
};

// Patient measurements for Ahmed (pre-treatment)
const PATIENT_VALS = {
  SNA: 85, SNB: 79, ANB: 6, SND: 74, OccPlane_SN: 16, GoGnSN: 32,
  U1_NA_deg: 28, U1_NA_mm: 6, L1_NB_deg: 28, L1_NB_mm: 5,
  Interincisal: 118, Pog_NB: 3, L1_APog: 3,
  FacialAngle: 84, ConvexityAngle: 5, ABPlane: -6, MandPlane: 28,
  LFH: 58, Jarabak: 63, U1_AP: 6, U1_FH: 115, L1_APog: 3,
  Interincisal_R: 118, UMolar_PtV: 18, E_Line_Upper: 2.5, E_Line_Lower: 1,
  MaxLen: 89, MandLen: 116, MaxMandDiff: 27, A_Nasion_Perp: 3,
  Pog_Nasion_Perp: -4, UFH: 52, LFH_mm: 66, U1_Nasion_Perp: 7,
  L1_APog_Mc: 4, OverbiteAmt: 4, OverjetAmt: 8.5,
  NasoPharynx: 20, OroPharynx: 12,
  Saddle_N_S_Ar: 124, Articular_S_Ar_Go: 146, Gonial_Ar_Go_Me: 128,
  UpperGonial: 54, LowerGonial: 70, SumAngles: 398, PFH: 71, AFH: 126,
  JaraRatio: 56, RamusHeight: 42, MandBody: 69,
  FMPA: 28, IMPA: 95, FMIA: 57, TweedSum: 180,
  HAngle: 12, HLine_Upper: 3, SkelConvex: 4, NasoLabial: 96,
  UpperLipThick: 16, UpperLipStrain: 2, MentoLabial: 3,
  SoftTissueFacial: 86, Chin_H: -2,
};

// After treatment predicted values
const POST_VALS = {
  SNA: 85, SNB: 79, ANB: 6, SND: 74, OccPlane_SN: 14, GoGnSN: 32,
  U1_NA_deg: 22, U1_NA_mm: 4, L1_NB_deg: 25, L1_NB_mm: 4,
  Interincisal: 130, Pog_NB: 4, L1_APog: 2,
  FacialAngle: 84, ConvexityAngle: 3, ABPlane: -4, MandPlane: 26,
  LFH: 57, Jarabak: 65, U1_AP: 3.5, U1_FH: 109, L1_APog: 1,
  Interincisal_R: 130, UMolar_PtV: 16, E_Line_Upper: -1, E_Line_Lower: -1,
  E_Line_Upper: -1, E_Line_Lower: -1,
  MaxLen: 89, MandLen: 116, MaxMandDiff: 27, A_Nasion_Perp: 3,
  Pog_Nasion_Perp: -1, UFH: 52, LFH_mm: 65, U1_Nasion_Perp: 4,
  L1_APog_Mc: 2, OverbiteAmt: 2, OverjetAmt: 2.5,
  NasoPharynx: 20, OroPharynx: 12,
  Saddle_N_S_Ar: 124, Articular_S_Ar_Go: 146, Gonial_Ar_Go_Me: 128,
  UpperGonial: 54, LowerGonial: 70, SumAngles: 398, PFH: 71, AFH: 126,
  JaraRatio: 57, RamusHeight: 42, MandBody: 69,
  FMPA: 28, IMPA: 90, FMIA: 62, TweedSum: 180,
  HAngle: 7, HLine_Upper: 0, SkelConvex: 1, NasoLabial: 102,
  UpperLipThick: 15, UpperLipStrain: 0, MentoLabial: 4,
  SoftTissueFacial: 90, Chin_H: 1,
};

function AdvancedCephPage() {
  const [activeAnalysis, setActiveAnalysis] = React.useState('steiner');
  const [showPost, setShowPost] = React.useState(false);
  const [tab, setTab] = React.useState('measurements');
  const [landmarks, setLandmarks] = React.useState({});
  const [placingLandmark, setPlacingLandmark] = React.useState(null);
  const [svgTab, setSvgTab] = React.useState('tracing');

  const analysis = ANALYSES[activeAnalysis];

  // Calculate deviation
  function getDeviation(id, val) {
    const measurements = Object.values(ANALYSES).flatMap(a => a.measurements);
    const m = measurements.find(m => m.id === id);
    if (!m) return null;
    const diff = val - m.norm;
    const absDiff = Math.abs(diff);
    const ratio = absDiff / m.sd;
    return {
      diff: diff.toFixed(1),
      severity: ratio <= 1 ? 'normal' : ratio <= 2 ? 'mild' : ratio <= 3 ? 'moderate' : 'severe',
      color: ratio <= 1 ? '#22c55e' : ratio <= 2 ? '#f59e0b' : ratio <= 3 ? '#f5922e' : '#ef4444',
      label: ratio <= 1 ? 'ضمن الطبيعي' : ratio <= 2 ? 'خفيف' : ratio <= 3 ? 'متوسط' : 'شديد',
    };
  }

  // Clinical interpretation
  function getClinicalInterp(id, val) {
    const interps = {
      ANB: val > 4 ? `Class II هيكلي — ${val}° (يحتاج تدخل)` : val < 0 ? `Class III هيكلي — ${val}°` : `Class I هيكلي — ${val}°`,
      GoGnSN: val > 37 ? 'نمط عمودي مفتوح — High Angle' : val < 27 ? 'نمط عمودي مغلق — Low Angle' : `نمط عمودي متوسط — ${val}°`,
      JaraRatio: val > 65 ? 'نمط نمو أمامي — تشخيص جيد' : val < 62 ? 'نمط نمو خلفي — تعقيد أعلى' : 'نمط نمو متوسط',
      NasoLabial: val > 110 ? 'زاوية مفتوحة — تراجع الشفة العلوية' : val < 90 ? 'زاوية حادة — بروز شفوي' : 'طبيعي',
      U1_NA_deg: val > 27 ? `قاطعة علوية مالت للأمام — ${val}° (تحتاج إمالة للخلف)` : val < 17 ? 'قاطعة علوية مالت للخلف' : 'طبيعي',
      IMPA: val > 95 ? `قاطعة سفلية مالت للأمام — ${val}° (خطر التعويض)` : val < 85 ? 'قاطعة سفلية مالت للخلف' : 'طبيعي',
      E_Line_Upper: val > 0 ? `شفة علوية بارزة +${val}mm من خط E` : `شفة علوية مدرجة ${val}mm من خط E`,
      HAngle: val > 11 ? `ملف شفوي بارز — H=${val}°` : `ملف شفوي مقبول — H=${val}°`,
    };
    return interps[id] || null;
  }

  const catColors = {
    skeletal: '#3d7ab5', dental: '#f5922e', soft: '#22c55e', airway: '#a855f7', check: '#64748b',
  };
  const catLabels = {
    skeletal: 'هيكلي', dental: 'سني', soft: 'أنسجة رخوة', airway: 'مجرى الهواء', check: 'تحقق',
  };

  // ── Tracing SVG ──
  function TracingSVG() {
    const LANDMARKS_DEF = [
      { id: 'S', label: 'S — Sella', x: 200, y: 48 },
      { id: 'N', label: 'N — Nasion', x: 150, y: 68 },
      { id: 'Ar', label: 'Ar — Articulare', x: 240, y: 85 },
      { id: 'Or', label: 'Or — Orbitale', x: 100, y: 85 },
      { id: 'Po', label: 'Po — Porion', x: 230, y: 75 },
      { id: 'ANS', label: 'ANS — الشوكة الأنفية الأمامية', x: 120, y: 150 },
      { id: 'PNS', label: 'PNS — الشوكة الأنفية الخلفية', x: 200, y: 145 },
      { id: 'A', label: 'A — نقطة A', x: 118, y: 162 },
      { id: 'B', label: 'B — نقطة B', x: 122, y: 195 },
      { id: 'Pog', label: 'Pog — Pogonion', x: 115, y: 210 },
      { id: 'Me', label: 'Me — Menton', x: 118, y: 222 },
      { id: 'Gn', label: 'Gn — Gnathion', x: 116, y: 218 },
      { id: 'Go', label: 'Go — Gonion', x: 210, y: 190 },
      { id: 'U1_tip', label: 'U1 — طرف القاطعة العلوية', x: 130, y: 165 },
      { id: 'L1_tip', label: 'L1 — طرف القاطعة السفلية', x: 128, y: 185 },
      { id: 'UMolar', label: 'UM — الرحى العلوية', x: 155, y: 162 },
      { id: 'LMolar', label: 'LM — الرحى السفلية', x: 160, y: 188 },
    ];

    const colors = { S: '#f5922e', N: '#f5922e', Ar: '#3d7ab5', Or: '#64748b', Po: '#64748b', ANS: '#22c55e', PNS: '#22c55e', A: '#3d7ab5', B: '#3d7ab5', Pog: '#a855f7', Me: '#a855f7', Gn: '#a855f7', Go: '#3d7ab5', U1_tip: '#ef4444', L1_tip: '#ef4444', UMolar: '#f59e0b', LMolar: '#f59e0b' };

    return (
      <div style={{ position: 'relative' }}>
        <svg width="340" height="280" viewBox="0 0 340 280" style={{ background: '#0d1520', borderRadius: 12, display: 'block' }}>
          {/* Background grid */}
          {[50,100,150,200,250,300].map(x => <line key={`v${x}`} x1={x} y1={0} x2={x} y2={280} stroke="rgba(61,122,181,0.08)" strokeWidth={1}/>)}
          {[50,100,150,200,250].map(y => <line key={`h${y}`} x1={0} y1={y} x2={340} y2={y} stroke="rgba(61,122,181,0.08)" strokeWidth={1}/>)}

          {/* Skull outline */}
          <path d="M150,20 C100,20 70,55 68,95 C66,130 75,158 88,172 C94,180 100,188 104,200 L142,200 C144,188 148,180 152,172 C165,158 175,130 173,95 C171,55 200,20 150,20Z" fill="none" stroke="rgba(61,122,181,0.2)" strokeWidth="1.5"/>
          {/* Mandible */}
          <path d="M104,200 Q104,222 118,225 Q135,228 145,223 L145,200" fill="none" stroke="rgba(61,122,181,0.2)" strokeWidth="1.5"/>
          <path d="M142,200 Q180,195 210,190 Q240,185 242,172 Q244,155 240,140" fill="none" stroke="rgba(61,122,181,0.18)" strokeWidth="1.5"/>

          {/* Reference lines */}
          {/* SN line */}
          <line x1="130" y1="52" x2="220" y2="52" stroke="rgba(245,146,46,0.5)" strokeWidth="1.5" strokeDasharray="6,3"/>
          <text x="222" y="56" fontSize="8" fill="rgba(245,146,46,0.7)" fontFamily="monospace">SN</text>
          {/* FH line */}
          <line x1="90" y1="82" x2="240" y2="82" stroke="rgba(100,116,139,0.4)" strokeWidth="1" strokeDasharray="4,3"/>
          <text x="242" y="86" fontSize="8" fill="rgba(100,116,139,0.6)" fontFamily="monospace">FH</text>
          {/* MP line */}
          <line x1="100" y1="225" x2="215" y2="190" stroke="rgba(61,122,181,0.3)" strokeWidth="1" strokeDasharray="4,3"/>
          <text x="216" y="190" fontSize="7" fill="rgba(61,122,181,0.5)" fontFamily="monospace">MP</text>
          {/* NA line */}
          <line x1="150" y1="68" x2="118" y2="195" stroke="rgba(168,85,247,0.3)" strokeWidth="1" strokeDasharray="3,3"/>
          {/* NB line */}
          <line x1="150" y1="68" x2="122" y2="210" stroke="rgba(168,85,247,0.25)" strokeWidth="1" strokeDasharray="3,3"/>

          {/* Landmarks */}
          {LANDMARKS_DEF.map(lm => {
            const placed = landmarks[lm.id] || { x: lm.x, y: lm.y };
            return (
              <g key={lm.id} style={{ cursor: 'pointer' }} onClick={() => setPlacingLandmark(lm.id)}>
                <circle cx={placed.x} cy={placed.y} r={placingLandmark === lm.id ? 6 : 4.5}
                  fill={colors[lm.id] || '#3d7ab5'}
                  stroke={placingLandmark === lm.id ? '#fff' : 'rgba(255,255,255,0.3)'}
                  strokeWidth={placingLandmark === lm.id ? 2 : 1}
                  opacity={0.9}
                />
                <text x={placed.x + 7} y={placed.y + 4} fontSize="8.5" fill={colors[lm.id] || '#3d7ab5'}
                  fontFamily="monospace" fontWeight="bold" opacity={0.9}
                >{lm.id}</text>
              </g>
            );
          })}

          {/* Overjet indicator */}
          <line x1="118" y1="173" x2="126" y2="173" stroke="#f5922e" strokeWidth="2"/>
          <text x="128" y="177" fontSize="7" fill="#f5922e" fontFamily="monospace">OJ:{PATIENT_VALS.OverjetAmt}mm</text>

          {/* Overbite indicator */}
          <line x1="108" y1="165" x2="108" y2="177" stroke="#ef4444" strokeWidth="2"/>
          <text x="96" y="174" fontSize="7" fill="#ef4444" fontFamily="monospace">OB</text>

          {/* E-line */}
          <line x1="118" y1="152" x2="115" y2="215" stroke="#22c55e" strokeWidth="1" strokeDasharray="3,2" opacity="0.6"/>
          <text x="100" y="148" fontSize="7" fill="#22c55e" opacity="0.7" fontFamily="monospace">E-line</text>
        </svg>

        {/* Landmark guide */}
        <div style={{ marginTop: 10 }}>
          <div style={{ fontSize: 11, fontWeight: 700, color: '#94a3b8', marginBottom: 6 }}>النقاط المرجعية:</div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
            {Object.entries(colors).map(([id, color]) => (
              <div key={id} style={{ display: 'flex', gap: 4, alignItems: 'center', fontSize: 10, color: '#94a3b8' }}>
                <div style={{ width: 8, height: 8, borderRadius: 99, background: color }} />
                <span style={{ color, fontFamily: 'monospace', fontWeight: 700 }}>{id}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    );
  }

  // ── Radar Chart ──
  function RadarChart({ analysisKey }) {
    const an = ANALYSES[analysisKey];
    const items = an.measurements.filter(m => m.unit === '°').slice(0, 8);
    if (items.length < 3) return null;
    const cx = 130, cy = 130, r = 100;
    const n = items.length;

    const getScore = (m) => {
      const val = PATIENT_VALS[m.id] || m.norm;
      const dev = Math.abs(val - m.norm) / (m.sd || 1);
      return Math.max(0.1, Math.min(1, 1 - dev * 0.22));
    };

    const pts = (scale) => items.map((_, i) => {
      const angle = (i / n) * 2 * Math.PI - Math.PI / 2;
      return [cx + Math.cos(angle) * r * scale, cy + Math.sin(angle) * r * scale];
    });

    const normPts = pts(1);
    const dataPts = items.map((m, i) => {
      const angle = (i / n) * 2 * Math.PI - Math.PI / 2;
      return [cx + Math.cos(angle) * r * getScore(m), cy + Math.sin(angle) * r * getScore(m)];
    });

    const labelPts = items.map((m, i) => {
      const angle = (i / n) * 2 * Math.PI - Math.PI / 2;
      return { x: cx + Math.cos(angle) * (r + 22), y: cy + Math.sin(angle) * (r + 22), label: m.id.split('_')[0] };
    });

    return (
      <svg width="260" height="260" viewBox="0 0 260 260">
        {/* Grid polygons */}
        {[0.25,0.5,0.75,1].map(scale => (
          <polygon key={scale} points={pts(scale).map(p=>p.join(',')).join(' ')}
            fill="none" stroke="#e8f0f9" strokeWidth={1}/>
        ))}
        {/* Axis lines */}
        {items.map((_, i) => {
          const angle = (i / n) * 2 * Math.PI - Math.PI / 2;
          return <line key={i} x1={cx} y1={cy} x2={cx+Math.cos(angle)*r} y2={cy+Math.sin(angle)*r} stroke="#e8f0f9" strokeWidth={1}/>;
        })}
        {/* Norm polygon */}
        <polygon points={normPts.map(p=>p.join(',')).join(' ')} fill="rgba(61,122,181,0.05)" stroke="rgba(61,122,181,0.25)" strokeWidth={1.5} strokeDasharray="4,3"/>
        {/* Data polygon */}
        <polygon points={dataPts.map(p=>p.join(',')).join(' ')} fill="rgba(61,122,181,0.15)" stroke="#3d7ab5" strokeWidth={2}/>
        {dataPts.map((p,i) => <circle key={i} cx={p[0]} cy={p[1]} r={3.5} fill="#3d7ab5"/>)}
        {/* Labels */}
        {labelPts.map((p,i) => (
          <text key={i} x={p.x} y={p.y} textAnchor="middle" dominantBaseline="middle"
            fontSize={9} fill="#64748b" fontFamily="monospace">{p.label}</text>
        ))}
        {/* Center label */}
        <text x={cx} y={cy} textAnchor="middle" dominantBaseline="middle" fontSize={9} fill="#94a3b8" fontFamily="Tajawal">Polygon</text>
      </svg>
    );
  }

  // ── Superimposition ──
  function SuperimpositionView() {
    return (
      <div>
        <div style={{ display: 'flex', gap: 10, marginBottom: 14, alignItems: 'center' }}>
          <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <div style={{ width: 24, height: 3, background: '#3d7ab5', borderRadius: 99 }} />
            <span style={{ fontSize: 12, color: '#3d7ab5', fontWeight: 700 }}>T0 — قبل العلاج (أزرق)</span>
          </div>
          <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <div style={{ width: 24, height: 3, background: '#22c55e', borderRadius: 99 }} />
            <span style={{ fontSize: 12, color: '#22c55e', fontWeight: 700 }}>T1 — بعد العلاج (أخضر)</span>
          </div>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
          {/* Superimposition on SN at S */}
          <Card>
            <div style={{ fontWeight: 700, fontSize: 13, marginBottom: 8, color: '#0d2137' }}>SN Superimposition — Cranial Base</div>
            <svg width="200" height="180" viewBox="0 0 200 180" style={{ background: '#f7fafd', borderRadius: 8, display: 'block' }}>
              {/* T0 — Blue */}
              <path d="M100,40 L60,140 Q100,160 140,140 Z" fill="none" stroke="#3d7ab5" strokeWidth="2" opacity="0.7"/>
              <line x1="60" y1="30" x2="140" y2="30" stroke="#3d7ab5" strokeWidth="1.5" opacity="0.5"/>
              <circle cx="85" cy="90" r="3" fill="#3d7ab5"/>
              <circle cx="88" cy="115" r="3" fill="#3d7ab5"/>
              {/* T1 — Green */}
              <path d="M100,40 L63,138 Q100,157 137,138 Z" fill="none" stroke="#22c55e" strokeWidth="2" opacity="0.7"/>
              <circle cx="84" cy="88" r="3" fill="#22c55e"/>
              <circle cx="86" cy="112" r="3" fill="#22c55e"/>
              {/* SN line */}
              <line x1="40" y1="30" x2="160" y2="30" stroke="rgba(245,146,46,0.6)" strokeWidth="1" strokeDasharray="4,3"/>
              <text x="162" y="34" fontSize="8" fill="#f5922e" fontFamily="monospace">SN</text>
              <text x="10" y="170" fontSize="8" fill="#64748b" fontFamily="Tajawal">إجمالية — SN</text>
            </svg>
          </Card>

          {/* Superimposition on ANS-PNS */}
          <Card>
            <div style={{ fontWeight: 700, fontSize: 13, marginBottom: 8, color: '#0d2137' }}>Maxillary Superimposition — ANS-PNS</div>
            <svg width="200" height="180" viewBox="0 0 200 180" style={{ background: '#f7fafd', borderRadius: 8, display: 'block' }}>
              {/* Palate line */}
              <line x1="50" y1="90" x2="160" y2="90" stroke="rgba(245,146,46,0.5)" strokeWidth="1" strokeDasharray="4,3"/>
              {/* T0 — Upper teeth blue */}
              <path d="M75,90 L65,130 L90,130 L80,90" fill="rgba(61,122,181,0.15)" stroke="#3d7ab5" strokeWidth="1.5" opacity="0.8"/>
              <path d="M110,90 L100,130 L125,130 L115,90" fill="rgba(61,122,181,0.1)" stroke="#3d7ab5" strokeWidth="1.5" opacity="0.6"/>
              {/* T1 — Upper teeth green (retracted) */}
              <path d="M72,90 L62,128 L87,128 L77,90" fill="rgba(34,197,94,0.15)" stroke="#22c55e" strokeWidth="1.5" opacity="0.8"/>
              <path d="M107,90 L97,128 L122,128 L112,90" fill="rgba(34,197,94,0.1)" stroke="#22c55e" strokeWidth="1.5" opacity="0.6"/>
              <text x="48" y="84" fontSize="8" fill="#f5922e" fontFamily="monospace">ANS</text>
              <text x="148" y="84" fontSize="8" fill="#f5922e" fontFamily="monospace">PNS</text>
              <text x="10" y="170" fontSize="8" fill="#64748b" fontFamily="Tajawal">علوية — تراجع القواطع</text>
            </svg>
          </Card>

          {/* Mandibular superimposition */}
          <Card>
            <div style={{ fontWeight: 700, fontSize: 13, marginBottom: 8, color: '#0d2137' }}>Mandibular Superimposition — MP</div>
            <svg width="200" height="180" viewBox="0 0 200 180" style={{ background: '#f7fafd', borderRadius: 8, display: 'block' }}>
              <line x1="40" y1="140" x2="170" y2="120" stroke="rgba(245,146,46,0.5)" strokeWidth="1" strokeDasharray="4,3"/>
              {/* T0 lower teeth blue */}
              <path d="M82,120 L72,80 L97,80 L87,120" fill="rgba(61,122,181,0.15)" stroke="#3d7ab5" strokeWidth="1.5" opacity="0.8"/>
              {/* T1 lower teeth green (slightly uprighted) */}
              <path d="M81,120 L74,80 L99,80 L86,120" fill="rgba(34,197,94,0.15)" stroke="#22c55e" strokeWidth="1.5" opacity="0.8"/>
              <text x="10" y="170" fontSize="8" fill="#64748b" fontFamily="Tajawal">سفلية — MP at Me</text>
            </svg>
          </Card>

          {/* Soft tissue change */}
          <Card>
            <div style={{ fontWeight: 700, fontSize: 13, marginBottom: 8, color: '#0d2137' }}>Soft Tissue Change</div>
            <svg width="200" height="180" viewBox="0 0 200 180" style={{ background: '#f7fafd', borderRadius: 8, display: 'block' }}>
              {/* T0 profile blue */}
              <path d="M120,20 Q80,50 70,80 Q65,100 72,115 Q80,130 78,150 Q76,165 80,175" fill="none" stroke="#3d7ab5" strokeWidth="2.5" opacity="0.6"/>
              {/* T1 profile green */}
              <path d="M120,20 Q80,50 70,80 Q65,100 68,115 Q74,130 73,150 Q71,165 75,175" fill="none" stroke="#22c55e" strokeWidth="2.5" opacity="0.7"/>
              {/* E-line */}
              <line x1="69" y1="82" x2="75" y2="175" stroke="rgba(168,85,247,0.5)" strokeWidth="1" strokeDasharray="3,2"/>
              <text x="54" y="78" fontSize="8" fill="#a855f7" fontFamily="monospace">E-line</text>
              <text x="10" y="178" fontSize="8" fill="#64748b" fontFamily="Tajawal">أنسجة رخوة — ملف جانبي</text>
            </svg>
          </Card>
        </div>
      </div>
    );
  }

  // ── Measurements Table ──
  function MeasurementsTable() {
    const vals = showPost ? POST_VALS : PATIENT_VALS;
    const grouped = {};
    analysis.measurements.forEach(m => {
      if (!grouped[m.category]) grouped[m.category] = [];
      grouped[m.category].push(m);
    });

    return (
      <div>
        {Object.entries(grouped).map(([cat, items]) => (
          <div key={cat} style={{ marginBottom: 14 }}>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 8 }}>
              <div style={{ width: 10, height: 10, borderRadius: 99, background: catColors[cat] }} />
              <span style={{ fontWeight: 700, fontSize: 13, color: catColors[cat] }}>{catLabels[cat]}</span>
            </div>
            <Card padding={0}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
                <thead>
                  <tr style={{ background: '#f7fafd', borderBottom: '1px solid #e8f0f9' }}>
                    {['القياس', 'القيمة', 'الطبيعي', 'الانحراف', 'التصنيف', 'التفسير السريري'].map(h => (
                      <th key={h} style={{ padding: '8px 12px', textAlign: 'right', fontWeight: 700, color: '#64748b', fontSize: 11 }}>{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {items.map((m, i) => {
                    const val = vals[m.id] ?? m.norm;
                    const dev = getDeviation(m.id, val);
                    const interp = getClinicalInterp(m.id, val);
                    return (
                      <tr key={m.id} style={{
                        borderBottom: i < items.length - 1 ? '1px solid #f8fafc' : 'none',
                        background: dev && dev.severity !== 'normal' ? dev.color + '06' : 'transparent',
                      }}>
                        <td style={{ padding: '9px 12px' }}>
                          <div style={{ fontWeight: 700, color: '#0d2137', fontFamily: 'monospace', fontSize: 12 }}>{m.label}</div>
                          <div style={{ fontSize: 10, color: '#94a3b8', marginTop: 1 }}>{m.desc}</div>
                        </td>
                        <td style={{ padding: '9px 12px' }}>
                          <span style={{ fontWeight: 800, fontSize: 15, color: dev?.color || '#0d2137', fontFamily: 'monospace' }}>
                            {val}{m.unit}
                          </span>
                        </td>
                        <td style={{ padding: '9px 12px', color: '#94a3b8', fontSize: 12, fontFamily: 'monospace' }}>
                          {m.norm}±{m.sd}
                        </td>
                        <td style={{ padding: '9px 12px', fontFamily: 'monospace', fontSize: 12 }}>
                          <span style={{ color: dev?.color, fontWeight: 700 }}>
                            {dev && (+dev.diff > 0 ? '+' : '')}{dev?.diff}{m.unit}
                          </span>
                        </td>
                        <td style={{ padding: '9px 12px' }}>
                          {dev && <Badge color={dev.color}>{dev.label}</Badge>}
                        </td>
                        <td style={{ padding: '9px 12px', fontSize: 12, color: '#64748b', maxWidth: 200 }}>
                          {interp || '—'}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </Card>
          </div>
        ))}
      </div>
    );
  }

  // ── Clinical Summary ──
  function ClinicalSummary() {
    const findings = [
      { cat: 'هيكلي — الإطار', color: '#ef4444', items: [
        `Class II هيكلي — ANB = ${PATIENT_VALS.ANB}° (طبيعي: 2°±2)`,
        `SNA = ${PATIENT_VALS.SNA}° — نقطة A أمامية للقاعدة القحفية`,
        `SNB = ${PATIENT_VALS.SNB}° — ضمن الطبيعي`,
        `Wits = +4.5mm — تأكيد Class II الهيكلية`,
      ]},
      { cat: 'عمودي — نمط النمو', color: '#3d7ab5', items: [
        `GoGn-SN = ${PATIENT_VALS.GoGnSN}° — نمط عمودي متوسط (Normodivergent)`,
        `Jarabak Ratio = ${PATIENT_VALS.JaraRatio}% — نمط نمو مدمج`,
        `Gonial Angle = ${PATIENT_VALS.Gonial_Ar_Go_Me}° — متوسط`,
      ]},
      { cat: 'سني — ميلان القواطع', color: '#f5922e', items: [
        `U1-SN = ${PATIENT_VALS.U1_FH - 5}° — إمالة أمامية زائدة (طبيعي: 104°)`,
        `U1-NA = ${PATIENT_VALS.U1_NA_deg}° / +${PATIENT_VALS.U1_NA_mm}mm — بروز القاطعة العلوية`,
        `IMPA = ${PATIENT_VALS.IMPA}° — القاطعة السفلية أمامية قليلاً`,
        `IIA = ${PATIENT_VALS.Interincisal}° — الزاوية البينية منخفضة`,
      ]},
      { cat: 'Overjet & Overbite', color: '#a855f7', items: [
        `Overjet = ${PATIENT_VALS.OverjetAmt}mm — زائد جداً (طبيعي: 2±1)`,
        `Overbite = ${PATIENT_VALS.OverbiteAmt}mm — عميق (طبيعي: 2±1)`,
      ]},
      { cat: 'أنسجة رخوة', color: '#22c55e', items: [
        `H-Angle = ${PATIENT_VALS.HAngle}° — بروز شفوي`,
        `E-line علوي = +${PATIENT_VALS.E_Line_Upper}mm — شفة أمام خط E`,
        `النسب الوجهية: الثلث السفلي طبيعي`,
      ]},
    ];

    const treatment = [
      'خلع الضواحك الأولى العلوية (14، 24) للتراجع الأمامي',
      'استخدام Class II Elastics لتصحيح العلاقة الرحوية',
      'تدعيم الإرساء (Anchorage) بالحلزوني (TPA) أو TADs',
      'براكيت MBT 0.022 — Torque مناسب لتصحيح الميلان',
      'الهدف: Overjet → 2mm، ANB لا يتغير (هيكلي ثابت)',
      'Overbite → 2mm بزيادة ارتفاع الاطباق الخلفي',
      'Soft tissue prediction: تحسن ملف الشفاه بعد التراجع',
    ];

    return (
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
        <div>
          <div style={{ fontWeight: 800, fontSize: 15, color: '#0d2137', marginBottom: 14 }}>📋 ملخص التشخيص</div>
          {findings.map(f => (
            <Card key={f.cat} style={{ marginBottom: 12 }}>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 10 }}>
                <div style={{ width: 10, height: 10, borderRadius: 99, background: f.color }} />
                <span style={{ fontWeight: 700, fontSize: 13, color: f.color }}>{f.cat}</span>
              </div>
              {f.items.map((item, i) => (
                <div key={i} style={{ display: 'flex', gap: 8, padding: '5px 0', borderBottom: i < f.items.length - 1 ? '1px solid #f8fafc' : 'none', fontSize: 12, color: '#64748b', lineHeight: 1.5 }}>
                  <span style={{ color: f.color, fontWeight: 700, flexShrink: 0 }}>•</span>
                  <span>{item}</span>
                </div>
              ))}
            </Card>
          ))}
        </div>

        <div>
          <div style={{ fontWeight: 800, fontSize: 15, color: '#0d2137', marginBottom: 14 }}>🎯 خطة العلاج المقترحة</div>
          <Card style={{ marginBottom: 14 }}>
            <div style={{ fontWeight: 700, fontSize: 13, color: '#3d7ab5', marginBottom: 10 }}>خطوات العلاج:</div>
            {treatment.map((t, i) => (
              <div key={i} style={{ display: 'flex', gap: 10, padding: '7px 0', borderBottom: i < treatment.length - 1 ? '1px solid #f8fafc' : 'none', fontSize: 12 }}>
                <div style={{ width: 22, height: 22, borderRadius: 99, background: '#3d7ab5', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 10, fontWeight: 700, color: '#fff', flexShrink: 0 }}>{i + 1}</div>
                <span style={{ color: '#64748b', lineHeight: 1.6 }}>{t}</span>
              </div>
            ))}
          </Card>

          <Card style={{ background: '#f0f5fb', border: '1px solid #dce8f5' }}>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 10 }}>
              <Icon name="ai" size={16} stroke="#3d7ab5" />
              <span style={{ fontWeight: 700, fontSize: 13, color: '#3d7ab5' }}>توصية الذكاء الاصطناعي</span>
            </div>
            <p style={{ fontSize: 12, color: '#64748b', lineHeight: 1.9 }}>
              بناءً على: ANB=6°، Overjet=8.5mm، U1-SN=115°، IMPA=95°، وJarabak=63%:
              <br/><strong style={{ color: '#0d2137' }}>الخلع مُوصى به</strong> — خلع (14،24) سيمنح مساحة كافية للتراجع الأمامي وتصحيح Overjet وتحسين الملف الشفوي. العلاج بدون خلع سيضطلع بتعويض سني غير مقبول.
            </p>
            <div style={{ marginTop: 10, display: 'flex', gap: 6 }}>
              <Btn variant="primary" size="sm">قبول التوصية</Btn>
              <Btn variant="ghost" size="sm">تخصيص</Btn>
            </div>
          </Card>
        </div>
      </div>
    );
  }

  return (
    <div>
      {/* Analysis selector */}
      <div style={{ display: 'flex', gap: 6, marginBottom: 16, flexWrap: 'wrap', alignItems: 'center' }}>
        <div style={{ display: 'flex', gap: 4, background: '#f0f5fb', padding: 4, borderRadius: 10, flexWrap: 'wrap' }}>
          {Object.entries(ANALYSES).map(([key, an]) => (
            <button key={key} onClick={() => setActiveAnalysis(key)} style={{
              padding: '5px 14px', borderRadius: 7, border: 'none',
              background: activeAnalysis === key ? '#fff' : 'transparent',
              color: activeAnalysis === key ? '#0d2137' : '#64748b',
              fontFamily: 'Tajawal', fontWeight: activeAnalysis === key ? 700 : 500,
              fontSize: 12, cursor: 'pointer',
              boxShadow: activeAnalysis === key ? '0 1px 4px rgba(0,0,0,0.1)' : 'none',
            }}>{an.name}</button>
          ))}
        </div>
        <div style={{ marginRight: 'auto', display: 'flex', gap: 8, alignItems: 'center' }}>
          <button onClick={() => setShowPost(!showPost)} style={{
            display: 'flex', gap: 6, alignItems: 'center', padding: '6px 14px', borderRadius: 8,
            border: `1.5px solid ${showPost ? '#22c55e' : '#dce8f5'}`,
            background: showPost ? '#22c55e18' : '#fff',
            color: showPost ? '#22c55e' : '#64748b',
            fontFamily: 'Tajawal', fontWeight: 600, fontSize: 12, cursor: 'pointer',
          }}>
            {showPost ? '✅ T1 — بعد العلاج' : '⬜ T0 — قبل العلاج'}
          </button>
          <Btn variant="outline" size="sm" icon="report">طباعة تقرير PDF</Btn>
        </div>
      </div>

      {/* Analysis info bar */}
      <div style={{ background: '#f7fafd', borderRadius: 10, padding: '10px 16px', marginBottom: 16, display: 'flex', gap: 16, alignItems: 'center' }}>
        <div style={{ fontWeight: 800, fontSize: 15, color: '#0d2137' }}>{analysis.fullName}</div>
        <div style={{ fontSize: 12, color: '#64748b' }}>السنة: {analysis.year}</div>
        <div style={{ fontSize: 12, color: '#64748b' }}>{analysis.desc}</div>
        <Badge color="#3d7ab5">{analysis.measurements.length} قياس</Badge>
        <div style={{ marginRight: 'auto', fontSize: 12, color: showPost ? '#22c55e' : '#3d7ab5', fontWeight: 700 }}>
          {showPost ? 'T1 — القيم المتوقعة بعد العلاج' : 'T0 — القيم الحالية قبل العلاج'}
        </div>
      </div>

      {/* Main tabs */}
      <Tabs tabs={[
        {id:'measurements', label:'القياسات الكاملة'},
        {id:'tracing', label:'الرسم والنقاط'},
        {id:'superimposition', label:'المقارنة (Superimposition)'},
        {id:'summary', label:'التشخيص والخطة'},
      ]} active={tab} onChange={setTab} />

      {tab === 'measurements' && (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 260px', gap: 16 }}>
          <MeasurementsTable />
          <div>
            <Card style={{ marginBottom: 14 }}>
              <div style={{ fontWeight: 700, fontSize: 13, marginBottom: 10 }}>Polygon Chart</div>
              <RadarChart analysisKey={activeAnalysis} />
            </Card>
            <Card>
              <div style={{ fontWeight: 700, fontSize: 13, marginBottom: 10 }}>ملخص الانحرافات</div>
              {['normal','mild','moderate','severe'].map(sev => {
                const sevColors = {normal:'#22c55e',mild:'#f59e0b',moderate:'#f5922e',severe:'#ef4444'};
                const sevLabels = {normal:'طبيعي',mild:'خفيف',moderate:'متوسط',severe:'شديد'};
                const count = analysis.measurements.filter(m => {
                  const val = PATIENT_VALS[m.id] || m.norm;
                  const dev = getDeviation(m.id, val);
                  return dev?.severity === sev;
                }).length;
                return (
                  <div key={sev} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '6px 0', borderBottom: '1px solid #f8fafc', fontSize: 13 }}>
                    <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                      <div style={{ width: 10, height: 10, borderRadius: 99, background: sevColors[sev] }} />
                      <span style={{ color: '#64748b' }}>{sevLabels[sev]}</span>
                    </div>
                    <span style={{ fontWeight: 800, color: sevColors[sev] }}>{count}</span>
                  </div>
                );
              })}
            </Card>
          </div>
        </div>
      )}

      {tab === 'tracing' && (
        <div style={{ display: 'grid', gridTemplateColumns: '380px 1fr', gap: 16 }}>
          <div>
            <Card>
              <div style={{ fontWeight: 700, fontSize: 13, marginBottom: 12 }}>رسم السيفالو — Tracing</div>
              <TracingSVG />
              <div style={{ marginTop: 12, padding: 10, background: '#f0f5fb', borderRadius: 8, fontSize: 12, color: '#64748b' }}>
                💡 انقر على أي نقطة في الرسم لتحديدها وتعديل موضعها
              </div>
            </Card>
          </div>
          <div>
            <Card>
              <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 14 }}>نقاط السيفالو المرجعية</div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                {[
                  ['S', 'Sella', 'مركز السرج التركي', '#f5922e'],
                  ['N', 'Nasion', 'نقطة التقاء العظم الجبهي والأنف', '#f5922e'],
                  ['Ar', 'Articulare', 'التقاء قاعدة القحف والفرع الصاعد', '#3d7ab5'],
                  ['A', 'Point A — Subspinale', 'أعمق نقطة في الفك العلوي', '#3d7ab5'],
                  ['B', 'Point B — Supramentale', 'أعمق نقطة في الفك السفلي', '#3d7ab5'],
                  ['Pog', 'Pogonion', 'أبرز نقطة في الذقن الهيكلي', '#a855f7'],
                  ['Me', 'Menton', 'أسفل نقطة في الذقن', '#a855f7'],
                  ['Go', 'Gonion', 'نقطة زاوية الفك السفلي', '#3d7ab5'],
                  ['ANS', 'Anterior Nasal Spine', 'الشوكة الأنفية الأمامية', '#22c55e'],
                  ['PNS', 'Posterior Nasal Spine', 'الشوكة الأنفية الخلفية', '#22c55e'],
                  ['Or', 'Orbitale', 'أسفل حافة الحجاج', '#64748b'],
                  ['Po', 'Porion', 'أعلى الصماخ', '#64748b'],
                ].map(([id, en, ar, color]) => (
                  <div key={id} style={{ padding: '8px 10px', background: '#f7fafd', borderRadius: 8, border: `1px solid ${color}20` }}>
                    <div style={{ display: 'flex', gap: 6, alignItems: 'center', marginBottom: 3 }}>
                      <div style={{ width: 8, height: 8, borderRadius: 99, background: color }} />
                      <span style={{ fontWeight: 800, fontSize: 12, color, fontFamily: 'monospace' }}>{id}</span>
                    </div>
                    <div style={{ fontSize: 10, fontWeight: 600, color: '#0d2137' }}>{en}</div>
                    <div style={{ fontSize: 10, color: '#94a3b8' }}>{ar}</div>
                  </div>
                ))}
              </div>
            </Card>
          </div>
        </div>
      )}

      {tab === 'superimposition' && <SuperimpositionView />}
      {tab === 'summary' && <ClinicalSummary />}
    </div>
  );
}

Object.assign(window, { AdvancedCephPage });
