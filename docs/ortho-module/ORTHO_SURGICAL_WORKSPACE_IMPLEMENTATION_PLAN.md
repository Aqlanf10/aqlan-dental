# تقرير تدقيق وتخطيط: مساحة العمل التقويمية الجراحية المشتركة
# ORTHO_SURGICAL_WORKSPACE_IMPLEMENTATION_PLAN

**النوع: REPORT-ONLY — تدقيق فعلي للكود + قرار تصميم + خطة سبرنتات.** لم يُعدّل أي كود، ولا migration، ولا PR، ولا مساس ببيانات الإنتاج.
**تاريخ التدقيق:** 2026-06-30 · **المصدر:** فحص مباشر لـ `backend/src` و `frontend/src` (مسارات وأسماء كيانات وendpoints وroutes مذكورة كأدلّة).

---

## 0. الخلاصة التنفيذية (القرار النهائي أولًا)

1. **لا تُنشأ وحدة مستقلة مكرّرة.** النظام يملك ثلاث وحدات ناضجة (التقويم، السيفالو، الجراحة) + ملف مريض موحّد. الميزة المطلوبة هي **جسر بيانات واحد** بينها.
2. **القرار:** كيان رابط واحد `OrthoSurgicalCase` (ملف بيانات واحد) + مسار workspace واحد `/ortho-surgical/[id]`، يُفتح من **ثلاثة مداخل**: تبويب في workspace التقويم، فلتر في قسم الجراحة، وتبويب فرعي في ملف المريض (`treatments`). البيانات لا تتكرر — المداخل فقط تتعدد.
3. **مفاجأة التدقيق:** توجد بالفعل صفحة **VTO** في الواجهة (`/ceph/vto`) بمكوّنات `CephVtoCanvas` و `CephSuperimposeCanvas`، **لكنها VTO تقويمي لحركة القواطع فقط (sliders U1/L1) وغير محفوظة في الـ Backend** — لا يوجد أي كيان VTO. فالـ VTO الجراحي (تحريك الفك العلوي/السفلي/الذقن) **يمتدّ على هذه المكوّنات** بدل بنائها من الصفر، لكنه يحتاج **تخزينًا جديدًا**.
4. **بوابة الاعتماد (قرار المالك في CLAUDE.md):** «مراحل التقويم P3+ مؤجلة حتى اكتمال السيفالو». لذا **الـ VTO الجراحي (S4) مجمّد حتى يكتمل السيفالو**؛ نبدأ بطبقة سير العمل/المراجعة/الاعتماد التي لا تعتمد على VTO.
5. **مبدأ تقليل المخاطر:** الكيان الرابط يحمل المراجع (`OrthoCaseId`, `SurgeryCaseId?`, `CephAnalysisId?`) — **لا نبعثر FKs** عبر `Contract`/`Visit`/`Appointment`/`LabOrder` (اقتراح مرفوض في المرحلة الأولى؛ يبقى خيارًا مستقبليًا فقط).

---

## 1. جدول الكيانات (Backend) — أدلّة فعلية + قرار إعادة الاستخدام

> جميع المسارات تحت `backend/src/AqlanDentalPro.Domain/Entities/` ما لم يُذكر غير ذلك.

### 1.أ — التقويم (Orthodontics)

| الكيان | المسار | أهم الحقول | العلاقات | القرار |
|---|---|---|---|---|
| **OrthoCase** | `OrthoCase.cs` | `CaseNumber`, `PatientId`, `DoctorId?`, `BranchId?`, `Status: OrthoCaseStatus`, `ApplianceType`, `TotalFee?` | → Patient/Doctor/Branch ؛ ← Diagnosis, ClinicalExam, ProblemList, TreatmentPlans, Visits, Stages, **CephAnalyses**, PhotoAnalyses, ModelAnalyses, ExtractionDecision, LabOrders, Photos, RecordsChecklist | **المحور المركزي — يُعاد استخدامه كما هو.** الكيان الرابط يشير إليه عبر `OrthoCaseId`. لا تعديل عليه. |
| **OrthoDiagnosis** | `OrthoDiagnosis.cs` | `OrthoCaseId`, `SkeletalClassification`, `DentalClassification`, `ANB/Wits/FMA/SNA/SNB/IMPA`, `CephSourceAnalysisId?`, `ProfileSourceAnalysisId?`, `ApprovedBy?` | → OrthoCase, CephAnalysis, PhotoAnalysis | **يُقرأ كما هو** (التشخيص الهيكلي ومصدره السيفالو). لا حاجة لتعديل؛ التوصية الجراحية تُخزَّن في الكيان الرابط لا هنا. |
| **OrthoClinicalExam** | `OrthoClinicalExam.cs` | إطباق (مولار/ناب يمين-يسار)، `Overjet/Overbite`, crossbite/openbite, crowding, midline, نمط عمودي… | → OrthoCase, Doctor | **يُقرأ كما هو** (خط الأساس السريري). لا تعديل. |
| **TreatmentPlan** | `TreatmentPlan.cs` | `PlanLabel (A/B/C)`, `IsApproved`, `ExtractionPlan`, `AnchoragePlan`, `TreatmentGoals`, `RisksLimitations` | → OrthoCase, Doctor | **يُقرأ كما هو.** قرار «تقويم فقط / camouflage / جراحة» يُسجَّل في الكيان الرابط لتجنّب تلويث الخطة. |
| OrthoVisit · TreatmentStage · ProblemListItem · ExtractionDecision · RetentionRecord · RecordsChecklist · OrthoClinicalPhoto · OrthoImagePreparation · OrthodonticAiLog | `*.cs` بنفس المجلد | حقول خاصة بكل منها | → OrthoCase (غالبًا) | **تُقرأ/تُعاد كما هي.** `OrthodonticAiLog` يُعاد استخدامه لتدقيق أي مسودة AI لاحقة (S لاحق). |

### 1.ب — الجراحة (Surgery)

| الكيان | المسار | أهم الحقول | العلاقات | القرار |
|---|---|---|---|---|
| **SurgeryCase** | `SurgeryCase.cs` | `CaseNumber`, `PatientId`, `DoctorId?`, `SurgeryType`, `TeethInvolved?`, `Status: SurgeryCaseStatus` | → Patient/Doctor ؛ ← PreopReport, OperativeReport, PostopRecord, HospitalReferrals | **يُعاد استخدامه كاملًا للتنفيذ الجراحي.** لا `OrthoCaseId` فيه — الربط يتم عبر الكيان الرابط (`SurgeryCaseId?`)، فلا حاجة لتعديل `SurgeryCase`. |
| **PreopReport / OperativeReport / PostopRecord / HospitalReferral** | `PreopReport.cs` … | checklist/consent (JSON)، تقنية/نتيجة/مضاعفات، تعليمات/وصفات، إحالات مستشفى | → SurgeryCase | **تُعاد كما هي** لتنفيذ العملية وما بعدها. لا نكرّر operation note في الكيان الرابط. |
| **SurgeryCaseStatus** (enum) | `Domain/Enums/SurgeryCaseStatus.cs` | Scheduled, InProgress, Completed, Cancelled, Postponed | — | يبقى لإدارة **تنفيذ** العملية. حالة **التخطيط** المشترك تُدار بـ enum جديد منفصل (انظر §4). |
| **SurgeryCaseStatusTransitions** (static class) | `Application/Services/SurgeryCaseStatusTransitions.cs` | انتقالات مُتحقَّقة + رسائل عربية + labels | — | **النمط المرجعي** لبناء `OrthoSurgicalStatusTransitions` (تحقّق صارم في الـ Backend). |

### 1.ج — السيفالومتري (Cephalometry)

| الكيان | المسار | أهم الحقول | العلاقات | القرار |
|---|---|---|---|---|
| **CephAnalysis** | `CephAnalysis.cs` | `OrthoCaseId`, `XrayFileUrl?`, `IsAutoTraced`, `AiAssisted`, `IsApproved`, `ApprovedAt?` | → OrthoCase, Landmarks, Measurements, Diagnosis | **يُقرأ كما هو.** الكيان الرابط يشير إليه عبر `CephAnalysisId?`. شرط VTO: `IsApproved == true`. |
| **CephLandmark / CephMeasurement / CephDiagnosis** | `*.cs` | معالم (x/y, AI confidence)، قياسات (قيمة/معياري/انحراف/تصنيف)، تشخيص هيكلي | → CephAnalysis | **تُقرأ كما هي** كمدخلات VTO (SNA/SNB/ANB/Wits/Overjet). لا تُنسخ. |
| **CephAnalysisVersion** | `CephAnalysisVersion.cs` | `Label`, `LandmarksJson`, `MeasurementsJson`, `SnapshotDate` | → CephAnalysis | **نمط اللقطات يُعاد استخدامه** لحفظ سيناريوهات VTO (قبل/بعد جراحي). |
| **CephNorm** | `CephNorm.cs` | norms قابلة للتهيئة حسب العمر/الجنس/المجموعة | — | يُقرأ كما هو؛ أهداف ما بعد الجراحة تبقى قابلة للتهيئة لا hardcoding. |

### 1.د — المريض والتحاليل

| الكيان | المسار | أهم الحقول | القرار |
|---|---|---|---|
| **Patient** | `Patient.cs` | بيانات المريض، `BranchId?` ؛ ← OrthoCases, SurgeryCases, Photos, Radiographs, Contracts… | **المحور الضمني الموجود** بين التقويم والجراحة. الكيان الرابط يشير إليه عبر `PatientId`. |
| **PhotoAnalysis** | `PhotoAnalysis.cs` | `OrthoCaseId`, `ViewType (profile/frontal)`, `LandmarksJson?` | يُقرأ كما هو (تحليل الأنسجة الرخوة) — مدخل VTO تقريبي للسوفت تيشو. |
| **ModelAnalysis** | `ModelAnalysis.cs` | Bolton/ArchLength/Pont… | يُقرأ كما هو. |

### 1.هـ — الملفات والمستندات والأشعة

| الكيان | المسار | أهم الحقول | القرار |
|---|---|---|---|
| **Radiograph** | `Radiograph.cs` | `PatientId`, `OrthoCaseId?`, `XrayType (OPG/lateral_ceph/PA_ceph/**CBCT**)`, `FileUrl` | **يُعاد استخدامه** — رفع CBCT للمستقبل 3D متاح أصلًا عبر `XrayType=CBCT`. لا كيان جديد. |
| **ClinicalPhoto / OrthoClinicalPhoto** | `*.cs` | صور بفئات وأطوار، `OrthoCaseId?` | **تُعاد كما هي** للسجلات؛ لا جدول صور جديد. وسم الطور موجود (`OrthoTreatmentPhase`). |
| **Document** | `Document.cs` | `PatientId`, `OrthoCaseId?`, `Signed`, `SignedAt?` | **يُعاد استخدامه** للموافقات وتقارير الخطة (PDF) — مرتبط بالمريض. |

### 1.و — العمليات اليومية والمالية/المختبر

| الكيان | المسار | أهم الحقول | القرار |
|---|---|---|---|
| **ClinicQueueItem** | `ClinicQueueItem.cs` | طابور اليوم: `Status`, `Priority`, نداء/غرفة | يُقرأ كما هو؛ شارة «تقويمية جراحية» تُشتق من الكيان الرابط بلا تعديل على الطابور. |
| **Contract** | `Contract.cs` | `PatientId`, `Specialty?`, **`RelatedCaseId?` (Guid بلا FK)**, `TotalAmount`, `Status` | **يُعاد استخدام `RelatedCaseId`** لربط عقد الجراحة بالكيان الرابط — **بلا تغيير سكيمة**. (ملاحظة: الحقل بلا قيد FK بالتصميم؛ لا نضيف قيدًا الآن لتفادي مخاطر الإنتاج.) |
| **LabOrder** | `LabOrder.cs` | `PatientId`, `OrthoCaseId?`, `Status`, `Cost`, `DoctorId? (→ Doctors.Id)` | **لا يرتبط بالجراحة اليوم.** ربط splint/guide مؤجّل (S لاحق) — وعندها يُفضَّل الربط عبر الكيان الرابط لا بإضافة FK جديد. تذكير: `LabOrders.DoctorId → Doctors.Id` (تحويل عبر `Doctors.UserId`). |
| **Visit / Appointment** | `Visit.cs` / `Appointment.cs` | `OrthoCaseId?` موجود؛ لا `SurgeryCaseId?` | **لا نضيف `SurgeryCaseId` الآن** (over-engineering)؛ متابعة ما بعد الجراحة تُسجَّل عبر `SurgeryCase`/الكيان الرابط. خيار مستقبلي فقط. |

> **حكم نهائي على السكيمة:** المطلوب **كيان رابط واحد جديد + كيانات تخطيط صغيرة**، دون تعديل أي كيان قائم في المرحلة الأولى. هذا أقلّ مخاطرة من اقتراح «أضف FKs ووسوم جراحية على 8 كيانات».

---

## 2. هل يوجد جسر أو VTO قائم؟ (إجابة قاطعة بالأدلّة)

- **جسر ortho↔surgery مباشر:** ❌ غير موجود. الربط الوحيد ضمني عبر `Patient` (كلاهما `PatientId`). `Visit.OrthoCaseId` يربط التقويم فقط، لا الجراحة.
- **جسر ceph→ortho:** ✅ موجود عبر `OrthoDiagnosis.CephSourceAnalysisId` (اتجاه واحد).
- **كيان VTO / superimposition / surgical-plan:** ❌ غير موجود في الـ Backend إطلاقًا.
- **VTO في الواجهة:** ✅ موجود لكنه **تقويمي فقط وغير محفوظ** — `frontend/src/app/(dashboard)/ceph/vto/page.tsx` + `components/ceph/CephVtoCanvas.tsx` + `CephSuperimposeCanvas.tsx` (sliders U1/L1 ±8mm، حساب overjet تقريبي على الـclient). **النتيجة:** الـ VTO الجراحي يمتدّ على هذه المكوّنات لكنه يحتاج كيان تخزين جديد (`OrthoSurgicalVto`).

---

## 3. جدول الـ APIs الحالية (أدلّة)

> جميع المسارات تحت `backend/src/AqlanDentalPro.API/Controllers/`.

| المجال | Controller | البادئة | السياسة | أبرز endpoints |
|---|---|---|---|---|
| حالات التقويم | `OrthoCasesController.cs` | `api/ortho-cases` | `OrthoAccess` + per-patient | overview, visits, stages, clinical-exam, problem-list, treatment-plans (A/B/C + approve), extraction-decision, checklist, **diagnosis (+approve)**, retention, photos, case-summary/PDF, case-presentation/pptx |
| السيفالو | `CephController.cs` | `api/ceph` | `OrthoAccess` | create, `{id}/landmarks`, `{id}/versions`, `{id}/compare`, `{id}/ai/auto-trace`, `{id}/ai/draft-diagnosis`, **`{id}/approve`**, `{id}/report/pdf`, `{id}/diagnosis` |
| معايير السيفالو | `CephNormsController.cs` | `api/ceph-norms` | StaffOnly/AdminOnly | list, best (age/sex)، CRUD، reset-defaults |
| تحليل النماذج | `OrthoModelAnalysesController.cs` | `api/ortho-cases/{id}/model-analyses` | `OrthoAccess` | preview, create, approve, report/pdf |
| تحليل الصور | `PhotoAnalysisController.cs` | `api/photo-analysis` | `OrthoAccess` | list, create, report/pdf |
| مسودة AI للتقويم | `OrthoCaseAiController.cs` | `api/ortho-cases/{id}/ai` | `OrthoAccess` | clinical-draft (مع 403/429/502 صريحة) |
| **الجراحة** | `SurgeryController.cs` | `api/surgery-cases` | **`SurgeryAccess`** + per-patient | CRUD, **`{id}/status` (انتقالات مُتحقَّقة CLIN-03)**, preop, operative (+approve), postop, referrals |
| المريض | `PatientsController.cs` | `api/patients` | StaffOnly | summary (يتضمن ortho cases)، timeline، medical/dental history |
| الخطة الموحّدة | `TreatmentPlanController.cs` | `api/patients/{id}/treatment-plan` | StaffOnly/DoctorAccess | خطوات **عابرة للتخصصات** (ortho+surgery)، `EstimatedCost` مرجعي لا يؤثّر على المالية |
| رحلة المريض | `PatientJourneyController.cs` | `api/patient-journey` | حسب الدور | today، `{patientId}/daily-summary` (يتضمن ortho case)، intake→queue→visit→checkout |
| الصور/الأشعة/المستندات | `ClinicalPhotosController.cs` (+`RadiographsController` بنفس الملف)، `DocumentsController.cs`، `UploadsController.cs` | `api/clinical-photos`، `api/radiographs`، `api/documents`، `api/uploads` | StaffOnly + per-patient | رفع/قائمة/حذف، **CBCT عبر `api/radiographs`**، رفع ملفات حتى 10MB |

**استنتاج APIs:** لا يوجد أي endpoint عابر يجمع ortho+surgery، ولا VTO/surgical-plan. مفاتيح الصلاحيات نمط `OrthoAccess`/`SurgeryAccess` — الكيان الرابط يحتاج سياسة/مفاتيح خاصة به (انظر §6).

---

## 4. الواجهة الحالية (Routes) + نموذج الحالة المقترح

> جميع المسارات تحت `frontend/src/app/(dashboard)/`.

| المجال | URL | الملف | ملاحظة |
|---|---|---|---|
| قائمة التقويم | `/ortho` | `ortho/page.tsx` | قائمة الحالات |
| **workspace التقويم** | `/ortho/[id]` | `ortho/[id]/page.tsx` | **18 تبويبًا** (overview…ceph…diagnosis…plan…lab…reports) — مكان مثالي لإضافة تبويب «جراحة الفكين» |
| قائمة الجراحة | `/surgery` | `surgery/page.tsx` | فلتر بالحالة |
| **workspace الجراحة** | `/surgery/[id]` | `surgery/[id]/page.tsx` | 5 تبويبات (info/preop/operative/postop/referrals) |
| السيفالو | `/ceph/[id]` | `ceph/[id]/page.tsx` | canvas المعالم + القياسات |
| **VTO (قائم، تقويمي)** | `/ceph/vto?analysisId=` | `ceph/vto/page.tsx` | `CephVtoCanvas`/`CephSuperimposeCanvas` — يمتدّ للـ VTO الجراحي |
| **ملف المريض** | `/patients/[id]` | `patients/[id]/page.tsx` | تبويب `treatments` فيه **sub-tabs: general / orthodontics / surgery** (`OrthodonticsTab.tsx`, `SurgeryTab.tsx`) — **نقطة الربط المثالية** |
| التنقل | — | `components/layout/Sidebar.tsx` ، `lib/routePermissions.ts` | `/ortho`→Admin/Orthodontist ، `/surgery`→Admin/OralSurgeon |

**نموذج الحالة المقترح `OrthoSurgicalStatus`** (enum جديد، منفصل عن `SurgeryCaseStatus`):
`DraftByOrthodontist → RecordsIncomplete → CephReady → VtoDraft → SentToSurgeon → SurgeonReviewPending → SurgeonRequestedChanges → JointPlanApproved → ReadyForSurgery → SurgeryScheduled → SurgeryDone → PostOpOrthodontics → Completed` + طرفية: `NotSurgicalCandidate`, `Cancelled`. يُبنى `OrthoSurgicalStatusTransitions` على نمط `SurgeryCaseStatusTransitions`. الواجهة تعرض دائمًا **«من المسؤول الآن؟»**.

---

## 5. التكرار/التعارض المحتمل — وأين الخطر

| الخطر | الواقع | القرار/التخفيف |
|---|---|---|
| **تكرار التنفيذ الجراحي** | operation note موجود في `OperativeReport` | لا نكرّره؛ الكيان الرابط يربط `SurgeryCaseId` ويُعيد استخدام شاشات الجراحة. |
| **تكرار التشخيص/القياسات** | `OrthoDiagnosis` + `CephMeasurement` مصدر الحقيقة | الكيان الرابط **يقرأ** لا يخزّن نسخًا. |
| **تكرار الصور/السجلات** | `OrthoClinicalPhoto`/`ClinicalPhoto`/`Radiograph`/`Document` | يُعاد استخدامها بـ `OrthoCaseId`/`PatientId`؛ لا جدول جديد. |
| **VTO مزدوج** | `/ceph/vto` تقويمي غير محفوظ | الـ VTO الجراحي **يمتدّ** المكوّنات نفسها مع تخزين جديد؛ تمييز واضح «تقويمي» مقابل «جراحي». |
| **تشتيت FKs** | اقتراح إضافة `SurgeryCaseId` لـ Contract/Visit/Appointment/LabOrder | **مرفوض في المرحلة الأولى**؛ المراجع تتركّز في الكيان الرابط. خيار مستقبلي عند الحاجة فقط. |
| **اعتماد أحادي للخطة** | لا ضمان حاليًا | **اعتماد مزدوج إلزامي في الـ Backend** (`OrthodontistApprovedAt` + `SurgeonApprovedAt`). |
| **بوابة السيفالو** | قرار المالك: P3+ مؤجّل حتى اكتمال السيفالو | **VTO الجراحي مجمّد** حتى تكتمل مراحل السيفالو وتقاريره. |

---

## 6. القرار التصميمي النهائي (مُلخّص قابل للتنفيذ)

- **كيان رابط واحد:** `OrthoSurgicalCase { Id, CaseNumber, PatientId, OrthoCaseId, CephAnalysisId?, SurgeryCaseId?, OrthodontistId, SurgeonId?, Status: OrthoSurgicalStatus, DiagnosisSummary, OrthodontistApprovedAt?, SurgeonApprovedAt?, BranchId }` (يرث `BaseEntity`).
- **كيانات تخطيط صغيرة:** `SurgeonReview`, `JointPlan`, و(لاحقًا) `OrthoSurgicalVto`.
- **مسار واحد للبيانات، ثلاثة مداخل:** `/ortho-surgical/[id]` ← (1) تبويب «جراحة الفكين» في `/ortho/[id]`، (2) فلتر «حالات تقويمية جراحية» في `/surgery`، (3) sub-tab جديد `ortho-surgical` في `patients/[id]` بجانب general/orthodontics/surgery.
- **الصلاحيات (RolePermissions، INSERT-ONLY، لا hardcoding):** `OrthoSurgical.view/create/edit_ortho/vto/surgeon_review/approve_ortho/approve_surgeon/create_surgery` + سياسة `OrthoSurgicalAccess` تجمع `Orthodontist`+`OralSurgeon`+`Admin`.
- **التنفيذ الجراحي والمالية:** يُعاد استخدام `SurgeryCase` (+تقاريره) و`Contract.RelatedCaseId` بلا تغيير سكيمة.

---

## 7. خطة السبرنتات (S0–S8) — لكلٍّ: الملفات · migration؟ · المخاطر · الاختبارات · الممنوع

> القاعدة: كل سبرنت = PR مستقل، CI أخضر (5 فحوص)، اختبارات + smoke قبل الدمج. الهجرات **additive فقط** على نمط `StartupDatabaseMaintenance` (`ADD TABLE/COLUMN IF NOT EXISTS`).

### Sprint 0 — تدقيق فقط ✅ (هذا المستند)
- **الملفات:** `docs/ortho-module/ORTHO_SURGICAL_WORKSPACE_IMPLEMENTATION_PLAN.md` فقط.
- **migration:** لا. **المخاطر:** لا. **الاختبارات:** لا (مستند).
- **الممنوع:** أي كود/سكيمة/PR.

### Sprint 1 — أساس الـ Backend (الكيان الرابط + الحالة + APIs)
- **الملفات المتوقعة:**
  - `Domain/Entities/OrthoSurgicalCase.cs` (+ `SurgeonReview.cs`, `JointPlan.cs`).
  - `Domain/Enums/OrthoSurgicalStatus.cs`.
  - `Application/Services/OrthoSurgicalStatusTransitions.cs` (نمط `SurgeryCaseStatusTransitions.cs`).
  - `Infrastructure/Data/Configurations/OrthoSurgicalCaseConfiguration.cs` (+ المرافِق).
  - `Infrastructure/Data/Migrations/<ts>_AddOrthoSurgicalCase.cs` (additive).
  - hotfix إقلاعي في `API/Configuration/StartupDatabaseMaintenance.cs` (`Ensure*` على نمط C-08).
  - `API/Controllers/OrthoSurgicalCasesController.cs` (CRUD + send-to-surgeon + approvals + create-surgery-case).
  - بذور صلاحيات INSERT-ONLY + سياسة `OrthoSurgicalAccess`.
- **migration؟** نعم — **إضافة جداول جديدة فقط**، لا تعديل/حذف أعمدة قائمة.
- **المخاطر:** وصول الأعمدة للإنتاج (تُعالَج بـ hotfix الإقلاعي)؛ ربط `Doctors.Id` لا `Users.Id`.
- **الاختبارات:** UnitTests للانتقالات (مسموح/ممنوع + رسائل عربية)، وللاعتماد المزدوج (لا يُقفل قبل اعتماد الطرفين)، وper-patient access.
- **الممنوع:** تعديل `OrthoCase`/`SurgeryCase`/`Contract`/`Visit`؛ إضافة سمة `[Migration]` للهجرات التاريخية؛ أي hardcoding مالي/هوية.

### Sprint 2 — هيكل الواجهة (Shell + 3 مداخل)
- **الملفات:** `app/(dashboard)/ortho-surgical/[id]/page.tsx` (+ `_components/`)؛ تبويب «جراحة الفكين» داخل `ortho/[id]/page.tsx`؛ فلتر في `surgery/page.tsx`؛ sub-tab + `components/patient/tabs/OrthoSurgicalTab.tsx` في `patients/[id]`؛ تحديث `Sidebar.tsx` + `lib/routePermissions.ts` (`/ortho-surgical` → Admin/Orthodontist/OralSurgeon).
- **migration؟** لا.
- **المخاطر:** تكرار مداخل دون تكرار بيانات (يُعالَج بقراءة من الكيان الرابط فقط)؛ RTL.
- **الاختبارات:** `vitest` للمكوّن + `tsc/lint/build`؛ smoke لكل مدخل.
- **الممنوع:** أي منطق كتابة هنا؛ إنشاء صفحة منفصلة خارج الـ workspace الموحّد.

### Sprint 3 — التشخيص والسجلات (قراءة من الموجود + جاهزية)
- **الملفات:** تبويبات «الملخص/السجلات/التشخيص/السيفالو/تحضير التقويم» داخل `ortho-surgical/[id]` تقرأ من `ortho-cases`/`ceph`/`radiographs`/`documents`؛ مؤشّر readiness.
- **migration؟** لا (قراءة فقط) — إلا إذا لزم حقل `ReadinessSnapshot` (يُؤجَّل).
- **المخاطر:** عرض بيانات حسّاسة حسب الدور (Accountant لا يرى السريري).
- **الاختبارات:** عقود الـ DTOs + per-patient access + إخفاء الحقول حسب الدور.
- **الممنوع:** نسخ التشخيص/القياسات إلى الكيان الرابط.

### Sprint 4 — Surgical VTO 2D ⏸ (مجمّد حتى اكتمال السيفالو)
- **الملفات:** `Domain/Entities/OrthoSurgicalVto.cs` + migration additive + endpoints `…/vto`؛ امتداد `components/ceph/CephVtoCanvas.tsx`/`CephSuperimposeCanvas.tsx` لحركات maxilla/mandible/chin + جدول تأثير على SNA/SNB/ANB/Wits/Overjet + disclaimer إلزامي.
- **migration؟** نعم (جدول VTO جديد).
- **المخاطر:** «حسابات مزيفة» — ممنوع؛ يعتمد على معايرة `pixelsPerMm` وسيفالو **معتمد** (`IsApproved`).
- **الاختبارات:** اختبارات حسابية للتنبؤ + رفض التشغيل قبل اعتماد السيفالو.
- **الممنوع:** أي قرار جراحي تلقائي؛ تشغيل VTO على سيفالو غير معتمد؛ بدء هذا السبرنت قبل اكتمال مراحل السيفالو (CLAUDE.md).

### Sprint 5 — مراجعة الجراح وسير العمل
- **الملفات:** `SurgeonReview` APIs + شاشة مراجعة الجراح + إرسال/طلب تعديل + انتقالات الحالة + audit.
- **migration؟** لا (الجدول من S1) أو حقول صغيرة additive.
- **المخاطر:** انتقالات حالة غير صالحة (يمنعها `OrthoSurgicalStatusTransitions`).
- **الاختبارات:** مصفوفة الانتقالات + صلاحية `surgeon_review` للجراح/Admin فقط.
- **الممنوع:** السماح للتقويم باعتماد الجزء الجراحي.

### Sprint 6 — الخطة المشتركة والاعتماد المزدوج
- **الملفات:** `JointPlan` APIs + شاشة الخطة + اعتماد التقويم + اعتماد الجراحة + **قفل الخطة بعد الاعتماد المزدوج** (`LockedAt`).
- **migration؟** حقول additive عند اللزوم.
- **المخاطر:** قفل قبل اكتمال الاعتمادين.
- **الاختبارات:** لا قفل قبل اعتماد الطرفين؛ لا تعديل بعد القفل إلا بمسار تصحيح.
- **الممنوع:** تجاوز الاعتماد المزدوج في الواجهة دون فحص Backend.

### Sprint 7 — ربط التنفيذ الجراحي
- **الملفات:** زر/endpoint `create-surgery-case` ينشئ `SurgeryCase` ويملأ `SurgeryCaseId`؛ يعيد استخدام شاشات preop/operative/postop/referrals؛ ربط العقد عبر `Contract.RelatedCaseId`.
- **migration؟** لا.
- **المخاطر:** ازدواج إنشاء `SurgeryCase` (idempotency + advisory lock كما في `SurgeryController`).
- **الاختبارات:** الانتقال إلى `SurgeryScheduled` ينشئ حالة واحدة فقط ويربطها.
- **الممنوع:** تعديل منطق المالية؛ صرف خزينة خارج `TreasuryResolutionService`.

### Sprint 8 — التقارير والموافقات (QuestPDF عربي)
- **الملفات:** مولّدات PDF (تقرير طبيب + تقرير مريض مبسّط + موافقات) تحت `API/Services/`؛ تُخزَّن عبر `Document` (`Signed`).
- **migration؟** لا.
- **المخاطر:** هوية المركز — تُقرأ من `clinic.*` في Settings لا hardcoding؛ سلامة العربية (Mojibake Guard).
- **الاختبارات:** بناء PDF + وجود ترويسة الهوية + عبارة «VTO محاكاة تقريبية».
- **الممنوع:** hardcoding الاسم/المؤهل؛ كشف تفاصيل استثناءات؛ إدراج بيانات مريض حسّاسة في عيّنات.

> **سبرنتات لاحقة (مؤجّلة):** ربط مختبر splint/guide · أساس 3D (رفع CBCT عبر `api/radiographs` موجود + عارض فقط) · مساعد AI على نمط P10 (`OrthodonticAiLog`) — كلها doctor-reviewed، بلا تصدير splints، بلا قرار AI.

---

## 8. القواعد المُلزِمة (من CLAUDE.md و MASTER-PLAN) — لا تُخالَف

- البناء فوق الموجود — لا إعادة بناء، لا وحدة مكرّرة، لا حذف ميزات/هجرات/`CashFlowTransaction`.
- migrations **additive فقط**؛ لا سمة `[Migration]` للهجرات التاريخية؛ القواعد الفارغة من خط أساس EF؛ وصول الأعمدة عبر hotfix إقلاعي idempotent.
- صلاحيات عبر RolePermissions (INSERT-ONLY)؛ لا hardcoding مالي ولا هوية مركز.
- رسائل الأخطاء عربية بحقل `message`؛ لا تسريب استثناءات في HTTP.
- التواريخ عبر `localDateString()` (لا `toISOString().slice(0,10)` — اليمن UTC+3)؛ أرقام FDI؛ RTL سليم.
- `Doctors.Id` لا `Users.Id` (التحويل عبر `Doctors.UserId`)؛ صرف الخزينة عبر `TreasuryResolutionService` فقط؛ الدفع/الاعتماد يتطلب وردية كاشير مفتوحة.
- لا حذف حالة لها سجلات — أرشفة فقط؛ كل تغيير Status مسجّل تدقيقيًا؛ اختبارات + smoke + CI أخضر قبل أي PR.

---

## 9. مصفوفة القرار السريعة (Reuse / Extend / New)

| العنصر | القرار |
|---|---|
| OrthoCase, OrthoDiagnosis, OrthoClinicalExam, TreatmentPlan | **REUSE** (قراءة) |
| CephAnalysis/Landmark/Measurement/Diagnosis/Version/Norm | **REUSE** (قراءة؛ النسخ ممنوع) |
| SurgeryCase + Preop/Operative/Postop/Referral | **REUSE** (تنفيذ) |
| Radiograph (CBCT), ClinicalPhoto/OrthoClinicalPhoto, Document | **REUSE** (سجلات/موافقات) |
| Contract.RelatedCaseId | **REUSE** (ربط مالي بلا تغيير سكيمة) |
| `CephVtoCanvas`/`CephSuperimposeCanvas` | **EXTEND** (للـ VTO الجراحي) |
| Visit/Appointment/LabOrder (SurgeryCaseId) | **لا تغيير الآن** (خيار مستقبلي) |
| OrthoSurgicalCase, SurgeonReview, JointPlan, OrthoSurgicalVto, OrthoSurgicalStatus(+Transitions) | **NEW** (إضافي فقط) |
| OrthoSurgicalCasesController + صلاحيات `OrthoSurgical.*` + سياسة `OrthoSurgicalAccess` | **NEW** |
| `/ortho-surgical/[id]` + 3 مداخل (ortho tab / surgery filter / patient sub-tab) | **NEW** (مسار)، **REUSE** (مكوّنات) |
