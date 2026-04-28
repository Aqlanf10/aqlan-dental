
// ============================
// Aqlan Dental Pro — Mock Data
// ============================

const MOCK = {
  currentUser: {
    name: 'د. عقلان الكامل',
    role: 'admin',
    specialty: 'أخصائي تقويم الأسنان',
    initials: 'عك'
  },

  stats: {
    totalPatients: 847,
    todayAppointments: 14,
    activeOrtho: 93,
    monthlyRevenue: 2840000,
    pendingPayments: 450000,
    newPatientsMonth: 23,
    completedToday: 6,
    cancelledToday: 1,
  },

  doctors: [
    { id: 1, name: 'د. عقلان الكامل', specialty: 'تقويم أسنان', color: '#3d7ab5', initials: 'عك', patients: 312 },
    { id: 2, name: 'د. عائشة غازي', specialty: 'طب عام', color: '#f5922e', initials: 'عغ', patients: 198 },
    { id: 3, name: 'د. إيمان الكامل', specialty: 'طب عام', color: '#22c55e', initials: 'إك', patients: 176 },
    { id: 4, name: 'د. هشام القدسي', specialty: 'طب عام', color: '#a855f7', initials: 'هق', patients: 94 },
    { id: 5, name: 'د. خلدون البريهي', specialty: 'جراحة وجه وفكين', color: '#ef4444', initials: 'خب', patients: 67 },
  ],

  patients: [
    { id: 'P001', number: 'OR-2024-108', name: 'أحمد محمد الشيباني', age: 22, gender: 'male', phone: '770-123456', doctor: 'د. عقلان الكامل', type: 'ortho', status: 'active', date: '2024-03-15', lastVisit: '2024-10-22' },
    { id: 'P002', number: 'GM-2024-039', name: 'فاطمة علي المقطري', age: 28, gender: 'female', phone: '711-654321', doctor: 'د. عائشة غازي', type: 'general', status: 'active', date: '2024-03-18', lastVisit: '2024-10-18' },
    { id: 'P003', number: 'GM-2024-041', name: 'محمد سالم الحمادي', age: 35, gender: 'male', phone: '770-789012', doctor: 'د. إيمان الكامل', type: 'general', status: 'active', date: '2024-03-20', lastVisit: '2024-10-15' },
    { id: 'P004', number: 'OR-2024-115', name: 'نورة عبدالله القحطاني', age: 17, gender: 'female', phone: '711-345678', doctor: 'د. عقلان الكامل', type: 'ortho', status: 'active', date: '2024-01-10', lastVisit: '2024-10-22' },
    { id: 'P005', number: 'SU-2024-022', name: 'يوسف حسن الزبيدي', age: 45, gender: 'male', phone: '770-901234', doctor: 'د. خلدون البريهي', type: 'surgery', status: 'active', date: '2024-04-05', lastVisit: '2024-10-10' },
    { id: 'P006', number: 'OR-2024-119', name: 'ريم طارق المحاوي', age: 14, gender: 'female', phone: '711-567890', doctor: 'د. عقلان الكامل', type: 'ortho', status: 'active', date: '2024-02-20', lastVisit: '2024-10-19' },
    { id: 'P007', number: 'GM-2024-043', name: 'عبدالرحمن محمود السعدي', age: 52, gender: 'male', phone: '770-234567', doctor: 'د. هشام القدسي', type: 'general', status: 'inactive', date: '2024-05-10', lastVisit: '2024-08-05' },
    { id: 'P008', number: 'OR-2024-122', name: 'لمياء عمر الحداد', age: 19, gender: 'female', phone: '711-876543', doctor: 'د. عقلان الكامل', type: 'ortho', status: 'active', date: '2024-06-01', lastVisit: '2024-10-21' },
    { id: 'P009', number: 'GM-2024-055', name: 'خالد نجيب المروني', age: 31, gender: 'male', phone: '770-543210', doctor: 'د. عائشة غازي', type: 'general', status: 'active', date: '2024-07-14', lastVisit: '2024-10-17' },
    { id: 'P010', number: 'SU-2024-028', name: 'سارة فاروق الشامي', age: 38, gender: 'female', phone: '711-098765', doctor: 'د. خلدون البريهي', type: 'surgery', status: 'active', date: '2024-08-02', lastVisit: '2024-10-12' },
  ],

  todayAppointments: [
    { id: 1, time: '08:30', patient: 'أحمد محمد الشيباني', patientId: 'P001', type: 'تفعيل تقويم', doctor: 'د. عقلان الكامل', doctorColor: '#3d7ab5', status: 'completed', duration: 30 },
    { id: 2, time: '09:00', patient: 'فاطمة علي المقطري', patientId: 'P002', type: 'علاج عصب', doctor: 'د. عائشة غازي', doctorColor: '#f5922e', status: 'in_progress', duration: 60 },
    { id: 3, time: '10:00', patient: 'نورة القحطاني', patientId: 'P004', type: 'مراجعة تقويم', doctor: 'د. عقلان الكامل', doctorColor: '#3d7ab5', status: 'arrived', duration: 30 },
    { id: 4, time: '10:30', patient: 'محمد سالم الحمادي', patientId: 'P003', type: 'تنظيف أسنان', doctor: 'د. إيمان الكامل', doctorColor: '#22c55e', status: 'scheduled', duration: 45 },
    { id: 5, time: '11:00', patient: 'ريم المحاوي', patientId: 'P006', type: 'تفعيل تقويم', doctor: 'د. عقلان الكامل', doctorColor: '#3d7ab5', status: 'scheduled', duration: 30 },
    { id: 6, time: '11:30', patient: 'يوسف الزبيدي', patientId: 'P005', type: 'استشارة جراحية', doctor: 'د. خلدون البريهي', doctorColor: '#ef4444', status: 'scheduled', duration: 45 },
    { id: 7, time: '12:30', patient: 'لمياء عمر الحداد', patientId: 'P008', type: 'تفعيل تقويم', doctor: 'د. عقلان الكامل', doctorColor: '#3d7ab5', status: 'scheduled', duration: 30 },
    { id: 8, time: '14:00', patient: 'خالد نجيب المروني', patientId: 'P009', type: 'حشو ضرس', doctor: 'د. عائشة غازي', doctorColor: '#f5922e', status: 'scheduled', duration: 60 },
  ],

  orthoCase: {
    patientId: 'P001',
    id: 'OR-2024-108',
    patient: { name: 'أحمد محمد الشيباني', age: 22, gender: 'male', phone: '770-123456', number: 'OR-2024-108', address: 'تعز — شارع جمال' },
    doctor: 'د. عقلان الكامل',
    appliance: 'MBT 0.022',
    startDate: '15 مارس 2024',
    expectedDuration: 18,
    currentStage: 'مرحلة المحاذاة والتسوية',
    stagePercentage: 65,
    status: 'active',
    totalFee: 1800000,
    paidAmount: 1050000,

    clinicalExam: {
      extraoral: {
        facialSymmetry: 'متماثل',
        profile: 'Class II — محدب',
        lipsCompetence: false,
        smileLine: 'متوسط',
        verticalProportion: 'طبيعية',
        nasolabialAngle: 'مقبول',
      },
      intraoral: {
        molarRelation: 'Class II كامل',
        canineRelation: 'Class II',
        overjet: 8.5,
        overbite: 4.0,
        crossbite: false,
        openBite: false,
        upperCrowding: 'moderate',
        lowerCrowding: 'mild',
        upperSpacing: 0,
        midlineUpper: 'منحرف 2mm لليمين',
        midlineLower: 'مركزي',
        cocrDiscrepancy: true,
        tmjFindings: 'طبيعي',
        habits: 'لا يوجد',
        perio: 'التهاب لثوي بسيط — تنظيف مطلوب',
      }
    },

    problemList: [
      { id: 1, category: 'skeletal', description: 'Class II هيكلي — ANB = 6°', severity: 'moderate' },
      { id: 2, category: 'skeletal', description: 'نمط عمودي طبيعي — SN-MP = 32°', severity: 'mild' },
      { id: 3, category: 'dental', description: 'إمالة علوية زائدة — U1/SN = 115°', severity: 'moderate' },
      { id: 4, category: 'dental', description: 'Overjet زائد — 8.5 mm', severity: 'moderate' },
      { id: 5, category: 'dental', description: 'Overbite عميق — 4 mm', severity: 'mild' },
      { id: 6, category: 'dental', description: 'ازدحام علوي متوسط — 5 mm', severity: 'moderate' },
      { id: 7, category: 'dental', description: 'ازدحام سفلي خفيف — 2.5 mm', severity: 'mild' },
      { id: 8, category: 'dental', description: 'Midline علوي منحرف 2mm لليمين', severity: 'mild' },
      { id: 9, category: 'soft_tissue', description: 'عدم تقابل الشفاه في الراحة', severity: 'mild' },
      { id: 10, category: 'functional', description: 'CO/CR Discrepancy', severity: 'mild' },
      { id: 11, category: 'space', description: 'ALD علوي: −5mm', severity: 'moderate' },
    ],

    treatmentPlan: {
      applianceType: 'براكيت معدني MBT 0.022',
      bracketSystem: 'Ormco Mini Diamond',
      initialWire: 'NiTi 0.014 علوي وسفلي',
      extractionPlan: 'خلع الضواحك الأولى العلوية (14، 24)',
      anchoragePlan: 'تدعيم حلزوني + Class II Elastics',
      useTADs: false,
      useElastics: true,
      expectedDuration: 18,
      retentionPlan: 'Hawley علوي + Bonded 3-3 سفلي',
      goals: [
        'تصحيح العلاقة الرحوية من Class II إلى Class I',
        'تقليل Overjet من 8.5mm إلى 2–3mm',
        'تقليل Overbite من 4mm إلى 2mm',
        'تصحيح Midline العلوي',
        'تحسين المظهر الشفوي والجمالي',
        'قراءة سيفالومترية نهائية بعد العلاج',
      ],
      isApproved: true,
      approvedDate: '18 مارس 2024',
    },

    stages: [
      { name: 'المحاذاة والتسوية', order: 1, status: 'active', progress: 65, startDate: 'مارس 2024', targetDuration: 4, color: '#3d7ab5' },
      { name: 'إغلاق الفراغات', order: 2, status: 'pending', progress: 0, startDate: null, targetDuration: 6, color: '#f5922e' },
      { name: 'تصحيح Overjet', order: 3, status: 'pending', progress: 0, startDate: null, targetDuration: 4, color: '#a855f7' },
      { name: 'التشطيب والتفصيل', order: 4, status: 'pending', progress: 0, startDate: null, targetDuration: 3, color: '#22c55e' },
      { name: 'الفطام والتثبيت', order: 5, status: 'pending', progress: 0, startDate: null, targetDuration: 1, color: '#f59e0b' },
    ],

    visits: [
      { id: 1, date: '15 مارس 2024', type: 'Bonding', visitNumber: 1, wireUpper: 'NiTi 0.014', wireLower: 'NiTi 0.014', overjet: 8.5, overbite: 4.0, notes: 'تركيب البراكيت كامل علوي وسفلي. المريض متعاون جيداً.', nextDate: '1 مايو 2024' },
      { id: 2, date: '1 مايو 2024', type: 'تفعيل', visitNumber: 2, wireUpper: 'NiTi 0.016', wireLower: 'NiTi 0.016', overjet: 8.0, overbite: 3.8, notes: 'تحسن ملحوظ في المحاذاة. تبديل السلك إلى 0.016.', nextDate: '15 يونيو 2024' },
      { id: 3, date: '15 يونيو 2024', type: 'تفعيل', visitNumber: 3, wireUpper: 'NiTi 0.018', wireLower: 'NiTi 0.016×22', overjet: 7.5, overbite: 3.5, notes: 'استمرار المحاذاة. السلك العلوي 0.018.', nextDate: '1 أغسطس 2024' },
      { id: 4, date: '1 أغسطس 2024', type: 'تفعيل', visitNumber: 4, wireUpper: 'SS 0.019×25', wireLower: 'NiTi 0.018', overjet: 7.0, overbite: 3.2, notes: 'انتقال إلى سلك قوس SS. بدأنا Class II Elastics.', nextDate: '15 سبتمبر 2024' },
      { id: 5, date: '15 سبتمبر 2024', type: 'مراجعة', visitNumber: 5, wireUpper: 'SS 0.019×25', wireLower: 'SS 0.019×25', overjet: 6.5, overbite: 3.0, notes: 'تحسن في الـ Overjet. الـ Elastics فعّالة.', nextDate: '22 أكتوبر 2024' },
      { id: 6, date: '22 أكتوبر 2024', type: 'تفعيل', visitNumber: 6, wireUpper: 'SS 0.019×25', wireLower: 'SS 0.019×25', overjet: 6.0, overbite: 2.8, notes: 'استمرار Class II Elastics. تحسن جيد.', nextDate: '3 ديسمبر 2024' },
    ],

    cephMeasurements: [
      { name: 'SNA', value: 85, norm: 82, sd: 2, unit: '°', category: 'skeletal' },
      { name: 'SNB', value: 79, norm: 80, sd: 2, unit: '°', category: 'skeletal' },
      { name: 'ANB', value: 6, norm: 2, sd: 2, unit: '°', category: 'skeletal' },
      { name: 'SN-MP', value: 32, norm: 32, sd: 5, unit: '°', category: 'skeletal' },
      { name: 'U1/SN', value: 115, norm: 104, sd: 5, unit: '°', category: 'dental' },
      { name: 'IMPA', value: 95, norm: 90, sd: 5, unit: '°', category: 'dental' },
      { name: 'Wits', value: 4.5, norm: 0, sd: 2, unit: 'mm', category: 'skeletal' },
      { name: 'E-Line علوي', value: 2.5, norm: -4, sd: 2, unit: 'mm', category: 'soft' },
      { name: 'E-Line سفلي', value: 1.0, norm: -2, sd: 2, unit: 'mm', category: 'soft' },
      { name: 'Jarabak', value: 63, norm: 65, sd: 3, unit: '%', category: 'skeletal' },
    ],

    cephDiagnosis: {
      skeletalClass: 'Class II هيكلي',
      verticalPattern: 'نمط عمودي متوسط',
      incisors: 'أسنان أمامية علوية مالت للأمام',
      softTissue: 'بروز شفوي معتدل',
      aiRec: 'توصية الذكاء الاصطناعي: ANB = 6° وOverjet = 8.5mm وU1/SN = 115° تشير إلى أن الخلع مناسب. يُنصح بخلع (14، 24) مع تدعيم الإرساء.',
    },

    extractionDecision: {
      decision: 'خلع الضواحك الأولى العلوية (14، 24)',
      proFactors: [
        'ازدحام علوي متوسط — 5mm ALD',
        'Overjet زائد 8.5mm يحتاج تراجعاً أمامياً',
        'Class II هيكلي ANB = 6°',
        'U1/SN = 115° — إمالة زائدة للأمام',
      ],
      conFactors: [
        'نمط الوجه متوسط/طبيعي',
        'ازدحام سفلي خفيف لا يستدعي خلع',
        'العمر 22 — نمو الوجه اكتمل',
      ],
      aiRecommendation: 'الخلع مناسب بناءً على المعطيات السيفالومترية والتحليل السريري.'
    },
  },

  finance: {
    monthly: [
      { month: 'مايو', amount: 2100 },
      { month: 'يونيو', amount: 2450 },
      { month: 'يوليو', amount: 1980 },
      { month: 'أغسطس', amount: 2700 },
      { month: 'سبتمبر', amount: 2300 },
      { month: 'أكتوبر', amount: 2840 },
    ],
    payments: [
      { id: 1, patient: 'أحمد الشيباني', amount: 150000, date: '22 أكتوبر 2024', method: 'نقدي', specialty: 'تقويم', receipt: 'R-10234' },
      { id: 2, patient: 'فاطمة المقطري', amount: 80000, date: '21 أكتوبر 2024', method: 'تحويل', specialty: 'طب عام', receipt: 'R-10233' },
      { id: 3, patient: 'نورة القحطاني', amount: 200000, date: '20 أكتوبر 2024', method: 'نقدي', specialty: 'تقويم', receipt: 'R-10232' },
      { id: 4, patient: 'يوسف الزبيدي', amount: 500000, date: '19 أكتوبر 2024', method: 'نقدي', specialty: 'جراحة', receipt: 'R-10231' },
      { id: 5, patient: 'ريم المحاوي', amount: 120000, date: '18 أكتوبر 2024', method: 'تحويل', specialty: 'تقويم', receipt: 'R-10230' },
    ],
    contracts: [
      { id: 1, patient: 'أحمد الشيباني', specialty: 'تقويم', total: 1800000, paid: 1050000, installments: 12, status: 'active' },
      { id: 2, patient: 'نورة القحطاني', specialty: 'تقويم', total: 1600000, paid: 800000, installments: 10, status: 'active' },
      { id: 3, patient: 'يوسف الزبيدي', specialty: 'جراحة', total: 2500000, paid: 1500000, installments: 6, status: 'active' },
      { id: 4, patient: 'ريم المحاوي', specialty: 'تقويم', total: 1500000, paid: 600000, installments: 10, status: 'active' },
    ]
  },

  notifications: [
    { id: 1, type: 'appointment', text: 'موعد قادم: نورة القحطاني — 10:00', time: 'قبل 30 دق', read: false },
    { id: 2, type: 'payment', text: 'دفعة متأخرة: عبدالرحمن السعدي — 200,000 ر.ي', time: 'منذ ساعة', read: false },
    { id: 3, type: 'lab', text: 'طلب مختبر وصل: ريم المحاوي — Retainer', time: 'منذ ساعتين', read: true },
    { id: 4, type: 'system', text: 'تذكير: تقرير شهري أكتوبر جاهز', time: 'منذ 3 ساعات', read: true },
  ],
};

Object.assign(window, { MOCK });
