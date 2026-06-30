# خطة وحدة التخطيط التقويمي الجراحي للفكين (Ortho-Surgical Planning Workspace)

**المرجع المعماري الكامل لميزة «جراحة الفكين / Orthognathic Surgery» داخل Aqlan Dental Pro.**
الحالة: مسوّدة للاعتماد — لم يبدأ التنفيذ بعد · يبني فوق الوحدات الموجودة (`OrthoCase` + `Ceph*` + `SurgeryCase`) — **لا إعادة بناء ولا وحدة منفصلة مكرّرة.**

> **قاعدة ذهبية (من MASTER-PLAN):** البناء فوق وحدة التقويم الموجودة وملحقاتها — لا عزل ولا تكرار.
> هذه الوحدة **جسر مشترك** بين أخصائي التقويم وأخصائي جراحة الفم والفكين، بملف بيانات واحد لا يُكرّر في مكانين.

---

## 0. لماذا «جسر» وليست وحدة جديدة؟ (الواقع في الكود)

النظام يحتوي اليوم على ثلاث وحدات ناضجة، والميزة المطلوبة تقع في **التقاطع** بينها — لا يجوز تكرار أيٍّ منها:

| موجود اليوم | الكيان/الملف | ما يقدّمه ويُعاد استخدامه |
|---|---|---|
| **حالة التقويم** | `OrthoCase` (+ `OrthoDiagnosis`, `OrthoClinicalExam`, `ProblemList`, `TreatmentPlan`, `OrthoVisit`, `RecordsChecklist`, `ClinicalPhoto`) | التشخيص التقويمي، الفحص السريري، قائمة المشاكل، خطة العلاج، الصور، السجلات |
| **السيفالومتري** | `CephAnalysis`, `CephLandmark`, `CephMeasurement`, `CephDiagnosis`, `CephNorm`, `CephAnalysisVersion` + `CephService` + `CephController` | القياسات (SNA/SNB/ANB/Wits…)، التصنيف الهيكلي، الإصدارات والمقارنة، تقرير PDF عربي |
| **الجراحة** | `SurgeryCase` (+ `PreopReport`, `OperativeReport`, `PostopRecord`, `HospitalReferral`) + `SurgeryCaseStatusTransitions` + `SurgeryController` | تنفيذ العملية: ما قبل/أثناء/بعد، الإحالات للمستشفى، انتقالات الحالة المُتحقَّقة |

**الخلاصة:** التخطيط التقويمي الجراحي = تشخيص وسيفالو و VTO (من التقويم) → مراجعة واعتماد جراحي (مفهوم جديد) → تنفيذ جراحي (من `SurgeryCase` الموجود). فالمطلوب كيان **رابط** (`OrthoSurgicalCase`) يجمع المراجع ويدير سير العمل والاعتمادات، لا أن يُعيد تعريف أيٍّ من الثلاثة.

---

## 1. موضع الميزة في خارطة الطريق المعتمدة

في `docs/ortho-module/MASTER-PLAN.md` الميزة مُسجّلة أصلًا ضمن:

> **P12 — جاهزية مستقبلية:** «بنية superimposition و **VTO** بدون حسابات مزيفة».

وفي `CLAUDE.md`:

> «WebCeph هو النموذج المستهدف المعتمد … **مراحل التقويم P3+ مؤجلة حتى اكتمال السيفالو وتقاريره.**»

**النتيجة (بوابة الاعتماد):**
- **2D Surgical VTO يعتمد مباشرة على اكتمال السيفالومتري** (P5 القياسات + P6 محرك التشخيص + تقرير PDF). لا يُبنى VTO فوق سيفالو ناقص.
- لذلك تُقسَّم هذه الخطة إلى:
  - **المسار أ (Workflow & Bridge):** لا يعتمد على VTO، يمكن البدء به فور توفّر `OrthoCase` + `SurgeryCase` (متوفّران) — يدير الإحالة والمراجعة والاعتماد والتنفيذ.
  - **المسار ب (Surgical VTO):** **مُجمّد حتى يكتمل السيفالو** (P5/P6). يُبنى عليه بعد ذلك.

> هذا يحترم قرار المالك: لا قفز فوق السيفالو، ولا «حسابات مزيفة».

---

## 2. القرار المعماري النهائي

**ملف واحد مشترك** اسمه `OrthoSurgicalCase` (عربيًا: «حالة تقويمية جراحية» / «تخطيط جراحة الفكين»)، **يظهر من مكانين بنفس البيانات**:
- من داخل مساحة عمل التقويم (تبويب «جراحة الفكين»).
- من داخل قسم الجراحة (فلتر «حالات تقويمية جراحية»).

يربط: `Patient` ← `OrthoCase` ← `CephAnalysis` ← (لاحقًا) `SurgeryCase`. البيانات مصدرها الوحدات الأصلية؛ هذا الكيان يحمل **حالة سير العمل والاعتمادات والمراجعة الجراحية فقط**.

---

## 3. الأدوار والمسؤوليات

| الدور | يفعل | لا يفعل |
|---|---|---|
| **أخصائي التقويم** (`Orthodontist`) | ينشئ الحالة من التقويم، يحرّر التشخيص/السيفالو/VTO وخطة التقويم، يرسل للجراح، يعتمد **جزء التقويم** | لا يعتمد الخطة الجراحية وحده، لا يكتب operation note النهائي |
| **جراح الفم والفكين** (`OralSurgeon`) | يراجع VTO والتشخيص، يحدّد نوع العملية، يطلب تعديلات، يعتمد **الجزء الجراحي**، ينفّذ عبر `SurgeryCase`، يتابع post-op | لا يغيّر تحليل التقويم الأساسي (يطلب تعديلًا فقط) |
| **Admin** | صلاحية كاملة | — |
| **Reception** | يشاهد الحالة والمواعيد، يطبع التعليمات | لا يعدّل الخطة الطبية |
| **Accountant** | يرى العقد/الفاتورة المرتبطة فقط | لا يرى التفاصيل السريرية الحساسة |

> **مفتاح أمان:** لا تصبح الخطة نهائية إلا باعتماد **الطرفين** (`OrthodontistApprovedAt` و `SurgeonApprovedAt`). هذا يُفرض في الـ Backend لا الواجهة فقط.

---

## 4. نموذج الحالة (Status) — سير العمل المشترك

يُضاف enum جديد `OrthoSurgicalStatus` (لإدارة التخطيط)، منفصل عن `SurgeryCaseStatus` الموجود (الذي يبقى لإدارة **تنفيذ** العملية كما هو):

```
DraftByOrthodontist      مسودة لدى التقويم
RecordsIncomplete        السجلات ناقصة
CephReady                السيفالو معتمد وجاهز
VtoDraft                 مسودة VTO (المسار ب)
SentToSurgeon            بانتظار مراجعة الجراح
SurgeonReviewPending     قيد مراجعة الجراح
SurgeonRequestedChanges  الجراح طلب تعديلًا
JointPlanApproved        الخطة المشتركة معتمدة من الطرفين
ReadyForSurgery          جاهزة للجدولة الجراحية
SurgeryScheduled         تم فتح SurgeryCase وجدولتها
SurgeryDone              تمت العملية
PostOpOrthodontics       تقويم ما بعد الجراحة
Completed                مكتملة
NotSurgicalCandidate     غير مرشّحة للجراحة (terminal)
Cancelled                ملغاة (terminal)
```

يُبنى جدول انتقالات `OrthoSurgicalStatusTransitions` **على نفس نمط** `SurgeryCaseStatusTransitions` الموجود (تحقّق صارم + رسائل عربية + labels مصدر واحد). الانتقال إلى `SurgeryScheduled` هو ما يُنشئ `SurgeryCase` المرتبطة.

> **أهم نقطة في الواجهة:** الحالة تعرض دائمًا **«من المسؤول الآن؟»** (بانتظار التقويم / بانتظار الجراح / بانتظار تعديل / بانتظار موافقة المريض / جاهزة للعملية) حتى لا تضيع بين التخصصين.

---

## 5. الكيانات (Backend) — إضافية فقط، Additive Migrations

> كل migration **additive** (لا حذف، لا تعديل أعمدة قائمة)، وتتبع نمط hotfix الإقلاعي الموثّق في `StartupDatabaseMaintenance` (`ADD COLUMN/TABLE IF NOT EXISTS`) لتصل للإنتاج بأمان.

```
OrthoSurgicalCase
  Id, CaseNumber, PatientId,
  OrthoCaseId (FK → OrthoCase),
  CephAnalysisId? (FK → CephAnalysis),
  SurgeryCaseId? (FK → SurgeryCase, يُملأ عند SurgeryScheduled),
  OrthodontistId (Doctors.Id), SurgeonId? (Doctors.Id),
  Status (OrthoSurgicalStatus, HasConversion<string>),
  DiagnosisSummary,
  OrthodontistApprovedAt?, SurgeonApprovedAt?,
  BranchId, CreatedAt, UpdatedAt   // BaseEntity

OrthoSurgicalVto            (المسار ب — مجمّد حتى اكتمال السيفالو)
  Id, OrthoSurgicalCaseId, CephAnalysisId,
  MaxillaMoveMm, MandibleMoveMm, ChinMoveMm, RotationDegree,
  PredictedSNA?, PredictedSNB?, PredictedANB?, PredictedWits?, PredictedOverjet?,
  Notes, CreatedBy, IsApprovedByOrthodontist

SurgeonReview
  Id, OrthoSurgicalCaseId, SurgeonId,
  Decision (Approved | RequestChanges | NotCandidate | NeedsImaging),
  ProposedProcedure, RequiredRecords, Risks, Notes, ReviewedAt

JointPlan
  Id, OrthoSurgicalCaseId,
  OrthodonticObjectives, SurgicalObjectives, ProcedureType, Timing,
  PreSurgicalRequirements, PostSurgicalPlan, Risks, PatientExplanation,
  OrthodontistApprovedAt?, SurgeonApprovedAt?, FinalStatus, LockedAt?
```

- **التنفيذ الجراحي يُعاد استخدامه بالكامل من `SurgeryCase`** (`PreopReport`/`OperativeReport`/`PostopRecord`/`HospitalReferral`) — لا نكرّر operation note.
- **السجلات والصور** تُعاد من `OrthoClinicalPhoto`/`ClinicalPhoto` الموجودة — لا جدول صور جديد.
- **القياسات** تُقرأ من `CephMeasurement` — لا تُخزَّن منسوخة.
- **`SurgeonId`/`OrthodontistId` يشيران إلى `Doctors.Id`** (تذكير: ربط المختبر `LabOrders.DoctorId → Doctors.Id` نفس النمط — التحويل من المستخدم عبر `Doctors.UserId`).

---

## 6. الصلاحيات (RolePermissions — لا hardcoding)

مفاتيح جديدة على نمط `Orthodontics.*` و `Surgery.*` الموجودة، تُزرع **INSERT-ONLY** (لا تطمس تخصيصات المالك):

```
OrthoSurgical.view            عرض الحالة
OrthoSurgical.create          إنشاء/تحويل من التقويم
OrthoSurgical.edit_ortho      تحرير جزء التقويم (Orthodontist/Admin)
OrthoSurgical.vto             تحرير/اعتماد VTO (Orthodontist/Admin)
OrthoSurgical.surgeon_review  مراجعة وقرار الجراح (OralSurgeon/Admin)
OrthoSurgical.approve_ortho   اعتماد التقويم
OrthoSurgical.approve_surgeon اعتماد الجراحة
OrthoSurgical.create_surgery  فتح SurgeryCase من الخطة
```

كل تغيير `Status` و كل اعتماد يُسجَّل تدقيقيًا (نمط audit الموجود). الفحص في الـ Backend إلزامي (لا اعتماد على إخفاء الواجهة).

---

## 7. الـ API المقترحة

```
GET    /api/ortho-surgical-cases                       قائمة (فلاتر: status, surgeonId, pending-review)
GET    /api/ortho-surgical-cases/{id}                  تفاصيل + المراجع
POST   /api/ortho-surgical-cases                       إنشاء (من OrthoCaseId)
PUT    /api/ortho-surgical-cases/{id}
POST   /api/ortho-surgical-cases/{id}/send-to-surgeon
POST   /api/ortho-surgical-cases/{id}/surgeon-review   قرار الجراح
POST   /api/ortho-surgical-cases/{id}/request-changes
POST   /api/ortho-surgical-cases/{id}/approve-orthodontist
POST   /api/ortho-surgical-cases/{id}/approve-surgeon
POST   /api/ortho-surgical-cases/{id}/create-surgery-case   → ينشئ SurgeryCase ويربطها
GET    /api/ortho-surgical-cases/{id}/vto                    (المسار ب)
POST   /api/ortho-surgical-cases/{id}/vto                    (المسار ب)
GET    /api/ortho-surgical-cases/{id}/report                 PDF للطبيب
GET    /api/ortho-surgical-cases/{id}/patient-explanation    PDF مبسّط للمريض
GET    /api/ortho-surgical-cases/{id}/consent               نموذج موافقة
```

كل 4xx/5xx يحمل `message` عربي. انتقالات الحالة تُتحقَّق عبر `OrthoSurgicalStatusTransitions` وتُرجع رسالة عربية عند المنع.

---

## 8. الواجهة (Frontend) — داخل مساحة العمل الموحّدة، لا صفحة عشوائية

- **داخل مساحة عمل التقويم الموحّدة** (انظر `UNIFIED_ORTHO_WORKSPACE_IMPLEMENTATION_PLAN.md`): تبويب جديد **«جراحة الفكين»** يفتح الـ workspace المشترك.
- **داخل قسم الجراحة:** فلتر/تبويب **«حالات تقويمية جراحية»** (بانتظار المراجعة / تحتاج تعديل / معتمدة / جاهزة / تمت).
- **Routes:** `/ortho/{orthoCaseId}/surgical` و قائمة `/surgery?type=ortho-surgical` — تُعاد استخدام مكوّنات الـ workspace الموجودة.

**تبويبات الحالة المشتركة:** الملخص («من المسؤول الآن؟») · السجلات (مرتبطة لا منسوخة) · التشخيص التقويمي · تحليل السيفالو · تحضير التقويم (readiness) · Surgical VTO (المسار ب) · مراجعة الجراح · الخطة المشتركة + الاعتمادات · تنفيذ الجراحة (`SurgeryCase`) · المتابعة والنتيجة · المستندات/الموافقات.

- التواريخ عبر `localDateString()` (لا `toISOString().slice(0,10)`).
- RTL سليم، هوية المركز في كل PDF (من مفاتيح `clinic.*` في Settings — لا hardcoding).

---

## 9. التقارير والموافقات (QuestPDF عربي)

- **تقرير الطبيب:** البيانات · السجلات · التشخيص · السيفالو · VTO · خطة التقويم · خطة الجراحة · الاعتمادات.
- **تقرير المريض (مبسّط):** المشكلة · لماذا قد تلزم جراحة · الخطة · قبل/بعد توضيحي · المخاطر العامة · المراحل · التوقيع.
- **الموافقات:** مشاركة الخطة بين التخصصين · جراحة الفكين · استخدام الصور للتخطيط · إقرار أن **VTO محاكاة تقريبية لا قرار جراحي نهائي**.
- كل تقرير يحمل اسم المركز الكامل + «د. عقلان الكامل — أخصائي تقويم الأسنان» + المؤهل (من Settings) كما في قرار المالك.

---

## 10. الذكاء الاصطناعي (لاحقًا فقط — على نمط P10 الموجود)

يُعاد استخدام بنية `OrthodonticAiLog` ومسار Claude API الآمن. مسموح لاحقًا: تلخيص التشخيص، اقتراح problem list، صياغة رسالة إحالة للجراح، شرح مبسّط للمريض، صياغة تقرير مشترك — **من البيانات المنظمة فقط**، مع العبارة الإلزامية «مسودة AI تتطلب مراجعة الأخصائي»، ومفتاح تشغيل من الإعدادات.
**ممنوع تمامًا:** أن يقرّر AI الجراحة، أو يعتمد خطة، أو يغيّر قياسات بلا مراجعة طبيب.

---

## 11. خطة التنفيذ على Sprintات (كل سبرنت = PR مستقل قابل للاختبار)

> الترتيب يبدأ بالمسار أ (لا يحتاج VTO)، ويُؤجّل المسار ب حتى اكتمال السيفالو (P5/P6).

| سبرنت | النطاق | يعتمد على | الحالة |
|---|---|---|---|
| **S0 — تدقيق وتصميم** | فحص `OrthoCase`/`Ceph*`/`SurgeryCase` وتثبيت نقاط الربط ومنع التكرار (هذا المستند) — تقرير فقط | — | ✅ هذا المستند |
| **S1 — أساس Backend** | `OrthoSurgicalCase` + `OrthoSurgicalStatus` + `OrthoSurgicalStatusTransitions` + APIs CRUD + migration additive + اختبارات build/test | S0 | ⬜ التالي |
| **S2 — هيكل الواجهة** | تبويب «جراحة الفكين» في workspace التقويم + فلتر في الجراحة + شاشة الملخص («من المسؤول الآن؟») + صلاحيات حسب الدور | S1 | ⬜ |
| **S3 — السجلات والتشخيص** | ربط السجلات/الصور والتشخيص التقويمي والسيفالو (قراءة من الكيانات الموجودة) + مؤشر الجاهزية | S2 | ⬜ |
| **S4 — مراجعة الجراح وسير العمل** | `SurgeonReview` + إرسال للجراح + قرار/طلب تعديل + انتقالات الحالة + audit | S3 | ⬜ |
| **S5 — الخطة المشتركة والاعتمادات** | `JointPlan` + اعتماد التقويم + اعتماد الجراحة + قفل الخطة بعد الاعتماد المزدوج | S4 | ⬜ |
| **S6 — ربط التنفيذ الجراحي** | زر «فتح SurgeryCase» يربط `SurgeryCaseId` ويعيد استخدام pre-op/op-note/post-op الموجودة | S5 | ⬜ |
| **S7 — التقارير والموافقات** | PDF تقرير الطبيب + تقرير المريض + الموافقات (هوية المركز من Settings) | S5 | ⬜ |
| **S8 — Surgical VTO 2D** | **مجمّد حتى اكتمال السيفالو P5/P6** — `OrthoSurgicalVto` + حركات maxilla/mandible/chin + تأثير القياسات + disclaimer | اكتمال Ceph | ⬜ مؤجّل |
| **S9 — مالية ومختبر** | ربط الخدمات الجراحية بعقد/فاتورة Finance V3 (لا منطق مكرر) + طلبات splint/guide في المختبر لاحقًا | S6 | ⬜ |
| **S10 — أساس 3D مستقبلي** | رفع CBCT/DICOM + STL + عارض 3D فقط (بدون تصدير splints) | لاحقًا | ⬜ مؤجّل |
| **S11 — مساعد AI** | تلخيص/إحالة/شرح مريض على نمط P10 — doctor-reviewed | لاحقًا | ⬜ مؤجّل |

---

## 12. ما لا نفعله الآن (حدود صريحة)

- ❌ 3D surgical planning كامل، أو تصدير surgical splints/guides (مسؤولية طبية/قانونية عالية — تحتاج جراحًا ومراجعة).
- ❌ AI يقرّر/يعتمد الخطة.
- ❌ أي تغيير في نموذج المالية الأساسي.
- ❌ وحدة منفصلة مكرّرة أو نسخ تصميم نظام آخر.
- ❌ حسابات VTO قبل اكتمال السيفالو (لا «حسابات مزيفة»).

**البداية = المسار أ (Workflow + Approvals + ربط SurgeryCase)، ثم المسار ب (VTO 2D) بعد اكتمال السيفالو.**

---

## 13. معايير اعتبار الحالة «جاهزة للجراحة»

لا تنتقل إلى `ReadyForSurgery` إلا إذا: السجلات مكتملة · السيفالو معتمد · (VTO محفوظ عند تفعيل المسار ب) · خطة التقويم مكتملة · مراجعة الجراح تمّت · اعتماد التقويم · اعتماد الجراحة · موافقة المريض موقّعة. ثم `create-surgery-case` ينشئ `SurgeryCase` ويربطها.

---

## 14. القواعد الثابتة المُلزِمة (من CLAUDE.md و MASTER-PLAN)

- البناء فوق الموجود — لا إعادة بناء، لا حذف ميزات/هجرات/`CashFlowTransaction`.
- migrations additive فقط؛ لا إضافة سمة `[Migration]` للهجرات التاريخية؛ القواعد الفارغة من خط أساس EF.
- مفاتيح الصلاحيات عبر RolePermissions (INSERT-ONLY)، لا hardcoding مالي ولا هوية مركز مضمّنة.
- رسائل الأخطاء عربية بحقل `message`؛ لا تسريب تفاصيل استثناءات في HTTP.
- التواريخ عبر `localDateString()`؛ أرقام FDI؛ RTL سليم؛ PDF بهوية المركز من Settings.
- لا حذف حالة لها سجلات — أرشفة فقط؛ كل تغيير Status مسجّل تدقيقيًا.
- اختبارات + smoke حي قبل أي PR؛ CI أخضر (5 فحوص) قبل الدمج.
