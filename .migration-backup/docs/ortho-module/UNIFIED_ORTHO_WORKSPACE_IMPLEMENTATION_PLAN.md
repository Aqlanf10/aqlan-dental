# Unified Orthodontics Workspace — Implementation Plan (Sprint 0 Audit & Map)

**الحالة:** Sprint 0 — تدقيق وخريطة تنفيذ. **وثيقة فقط، لا كود.**
**الفرع:** `feature/unified-orthodontics-workspace`
**التاريخ:** 2026-06-15 · **آخر هجرة في main:** `20260630000000_AddPhotoOrthoDiagnosisSyncMetadata`

> **القاعدة الذهبية:** البناء فوق ما هو موجود. **لا إعادة بناء.** أغلب الوحدة منفّذ بالفعل
> (13 تبويبًا + باك إند كامل). هذه الوثيقة تحدّد بدقة *ما هو موجود* لئلا نكرّره، و*ما المتبقّي فعلًا*.

---

## 1) الخلاصة التنفيذية — الواقع الحالي

مساحة عمل التقويم `/ortho/{caseId}` **موجودة وعاملة** بنظام تبويبات (`?tab=`) في
`frontend/src/app/(dashboard)/ortho/[id]/page.tsx` (~3247 سطر) مع 13 تبويبًا:
`overview, records, compare, exam, problems, ceph, diagnosis, plan, stages, visits, extraction, retention, finance`.

الباك إند `OrthoCasesController` يوفّر CRUD كاملًا لكل هذه الأقسام، ونقطة **`GET /api/ortho-cases/{id}/overview`**
تجمّع بالفعل: قائمة الجاهزية، عدّادات السيفالو/الصور، حالة مزامنة التشخيص، وملخّص العقد المالي (Total/Paid/Remaining).

**النتيجة:** خطة المالك الكبيرة (28 قسمًا/10 سبرنتات) أغلبها **مُنفّذ**. العمل الجديد الحقيقي محصور في:
1. **مولّد عرض الحالة PowerPoint (PPTX)** — **غير موجود إطلاقًا** (لا حزمة OpenXML، لا أي كود).
2. **أداة تحضير الصور** (قص/تكبير/تدوير لنسخة جاهزة للتقرير دون المساس بالأصل) — غير موجودة.
3. **دمج تبويبات** Cast Analysis و Lab Orders في مساحة العمل (الباك إند جاهز، الواجهة على مسار منفصل).
4. **ربط العمليات اليومية/المواعيد بـ OrthoCaseId** + موعد بعد 21 يومًا — غير موجود.
5. **تقرير PDF للحالة الموحّد** + **تقرير PDF لتحليل النماذج (Cast)** — غير موجود (تقريرا السيفالو والصور موجودان).
6. (تنظيف) ترحيل معايرة السيفالو من Notes JSON إلى حقول صريحة.

---

## 2) مصفوفة التبويبات الـ14 (موجود / جزئي / ناقص)

| # | التبويب | الحالة | الدليل |
|---|---|---|---|
| 1 | Overview | ✅ موجود | `OverviewPanel` + `GET /overview` يجمّع الجاهزية/المالية |
| 2 | Records & Photos | ✅ موجود | `RecordsPanel` + `OrthoClinicalPhoto` (Category/Subtype/TreatmentPhase/IsSelectedForReport) + `RecordsChecklist` |
| 3 | Clinical Exam | ✅ موجود | `OrthoClinicalExam` (Phase-3 منظّم ~100 حقل) + `PUT /clinical-exam` |
| 4 | **Cast Analysis** | 🟡 جزئي | `ModelAnalysis` + `OrthoModelAnalysesController` + حاسبة كاملة + صفحة `/ortho/[id]/model-analysis` — **لكن ليست تبويبًا في مساحة العمل** |
| 5 | Cephalometric | ✅ موجود | تبويب `ceph` + `CephController` + مزامنة `CephSourceAnalysisId` |
| 6 | **Facial Photo Analysis** | 🟡 جزئي | `PhotoAnalysis` (profile/frontal) + مزامنة للتشخيص — **صفحتا `/ceph/photo[/frontal]` منفصلتان، لا تبويب مخصّص** |
| 7 | Problem List | ✅ موجود | `ProblemListItem` + `GET/POST/DELETE /problem-list` |
| 8 | Diagnosis | ✅ موجود | `OrthoDiagnosis` + مزامنة سيفالو/صور + أعلام «قديم» + اعتماد |
| 9 | Treatment Plan | ✅ موجود | `TreatmentPlan` متعدّد (A/B/C) + اعتماد واحد فقط |
| 10 | Visits | ✅ موجود | `OrthoVisit` + `OrthoVisitTimeline` |
| 11 | Stages & Appliances | ✅ موجود | `TreatmentStage` + `OrthoStagesTimeline` + StagePercentage |
| 12 | **Lab Orders** | 🟡 جزئي | `LabOrder.OrthoCaseId` موجود — **لا تبويب ولا فلتر list-by-case** |
| 13 | **Finance** | 🟡 جزئي | الملخّص في Overview + `FinancePanel` يربط Finance V3 — لا نقطة ملخّص by-case مخصّصة (Overview يكفي مبدئيًا) |
| 14 | **Reports / Case Presentation** | 🔴 ناقص | **لا تقرير حالة موحّد ولا مولّد PPTX** (تقريرا السيفالو/الصور الفرديان فقط) |

---

## 3) جرد الأنظمة الفرعية (مرجع — موجود بالفعل)

### كيانات التقويم (`backend/src/AqlanDentalPro.Domain/Entities/`)
`OrthoCase` (+ ملاحات: Patient/Doctor/Branch/ClinicalExam/ProblemList/TreatmentPlans/Visits/Stages/RetentionRecord/CephAnalyses/PhotoAnalyses/ModelAnalyses/ExtractionDecision/LabOrders/OrthoClinicalPhotos/Diagnosis/RecordsChecklist) ·
`OrthoClinicalExam` · `OrthoDiagnosis` (CephSourceAnalysisId/ProfileSourceAnalysisId/FrontalSourceAnalysisId/PhotoAnalysisSummary/Approved) ·
`OrthoClinicalPhoto` · `RecordsChecklist` (14 بند) · `OrthoVisit` (NextAppointmentDate/Type) · `TreatmentPlan` (PlanLabel A/B/C) ·
`TreatmentStage` · `ExtractionDecision` · `RetentionRecord` + `RetentionVisit` · `ProblemListItem` ·
`CephAnalysis`/`CephLandmark`/`CephMeasurement`/`CephDiagnosis` · `PhotoAnalysis` · `ModelAnalysis` · `LabOrder`.

### تحليل النماذج (Cast) — مُنفّذ بالكامل (PR #364)
`DentalModelAnalysisCalculator` يحسب تلقائيًا: **Bolton (أمامي/كلي)، Arch Space/ALD، Pont، Ashley Howe، Moyers، Tanaka-Johnston، Huckaba**.
نقاط: `GET/POST .../model-analyses` (+ `preview`, `latest`, `PUT`, `PATCH approve`). واجهة: `frontend/src/app/(dashboard)/ortho/[id]/model-analysis/page.tsx`. **الناقص: تقرير PDF + تبويب داخل المساحة.**

### السيفالو — شامل
7 مجموعات (Steiner/Tweed/McNamara/Ricketts/Downs/Jarabak/Wits) ~40 قياسًا · تتبّع AI (Gemini) · تراكب · VTO ·
تقرير PDF (`CephReportPdfGenerator`). **المعايرة مخزّنة داخل `CephAnalysis.Notes` JSON (`CephNotesData`: PixelsPerMm/ImageWidth/ImageHeight)** — مرشّحة للترحيل لحقول صريحة.

### تحليل الصور — مُنفّذ + مدمج بالتشخيص
`PhotoAnalysisService` يزامن إلى `OrthoDiagnosis` (`ProfileSourceAnalysisId`/`FrontalSourceAnalysisId`/`PhotoAnalysisSummary`) · تقرير PDF (`PhotoAnalysisReportPdfGenerator`).

### مولّدات PDF (QuestPDF) — `backend/src/AqlanDentalPro.API/Services/`
`CephReportPdfGenerator` · `PhotoAnalysisReportPdfGenerator` · `LabOrderPdfGenerator` · `PdfService` (تسجيل خط Noto Naskh).
الهوية من Settings: `clinic.name`/`clinic.lead_doctor`/`clinic.lead_doctor_title`/`clinic.lead_doctor_credentials`/`clinic.phones`/`clinic.location` + شعار `Fonts/logo.png` مع fallback.
**الناقص: مولّد PDF لتحليل النماذج، ومولّد تقرير الحالة الموحّد.**

### PPTX — **صفر قدرة**
لا `DocumentFormat.OpenXml` ولا أي حزمة/كود PPTX في المشروع. **يُبنى من الصفر (Sprint 8).**

---

## 4) حالة التكاملات

| التكامل | الحالة | الدليل / المتبقّي |
|---|---|---|
| **Finance ↔ Case** | 🟡 | `Contract.RelatedCaseId` موجود؛ Overview يعرض Total/Paid/Remaining. الناقص (اختياري): نقطة `GET /api/contracts/by-ortho-case/{id}` صريحة |
| **Lab ↔ Case** | 🟡 | `LabOrder.OrthoCaseId` موجود. الناقص: فلتر `GET /api/lab-orders?orthoCaseId=` + تبويب |
| **DailyOps ↔ Case** | 🔴 | لا تمييز لمرضى التقويم. الناقص: شارة/فلتر + ربط الزيارة بالحالة |
| **Appointments ↔ Case** | 🔴 | **لا `Appointment.OrthoCaseId`**. الناقص: حقل + منطق «موعد بعد 21 يومًا» من زيارة التقويم |
| **OrthoVisit ↔ Appointment/Visit** | 🔴 | لا ربط؛ `OrthoVisit.NextAppointmentDate` تُخزَّن فقط ولا تُنشئ موعدًا |

**فخّ موثّق:** `LabOrder.DoctorId` و`CephAnalysis.DoctorId` يشيران إلى `Doctors.Id` لا `Users.Id` (حوّل عبر `Doctors.UserId`).

---

## 5) العمل الجديد الحقيقي (ما يجب بناؤه)

1. **تبويب Cast Analysis داخل المساحة** — استدعاء صفحة model-analysis كتبويب (الباك إند جاهز).
2. **تبويب Facial Photo Analysis** — ربط `/ceph/photo[/frontal]?orthoCaseId=` كتبويب + عرض المحفوظات.
3. **تبويب Lab Orders** — فلتر by-case + عرض قراءة فقط (لا تكرار وحدة المختبر).
4. **تقرير PDF لتحليل النماذج** (`ModelAnalysisReportPdfGenerator` على نمط الموجود).
5. **تقرير الحالة الموحّد PDF** (يجمع الأقسام).
6. **🎯 مولّد عرض الحالة PPTX** (`DocumentFormat.OpenXml`) — أكبر بند جديد (انظر §7).
7. **أداة تحضير الصور** + كيان `OrthoImagePreparation` (الأصل لا يُمسّ، تُنشأ نسخة جاهزة).
8. **ربط DailyOps/Appointments** — `Appointment.OrthoCaseId` + موعد 21 يومًا + شارة العمليات.
9. **ترويسة حالة دائمة مُثراة** (عمر/جنس/مرحلة/الموعد القادم/نسبة الجاهزية + أزرار).
10. (تنظيف) ترحيل معايرة السيفالو إلى حقول صريحة مع fallback.

---

## 6) الهجرات المقترحة (النمط الصحيح إلزامي)

الترقيم التالي بعد `20260630000000` → **ابدأ من `20260701000000`** (يونيو فيه 30 يومًا فقط، فلا `20260631`؛
أو اترك `dotnet ef` يولّد timestamp صحيحًا ثم وحّده مع النمط). كل هجرة: سمة `[Migration]` + ملف Designer + تحديث snapshot
(مولّدة بـ `dotnet ef migrations add` ثم رقّمها بعد آخر هجرة) **+ hotfix إقلاع `CREATE TABLE/COLUMN IF NOT EXISTS`** في `StartupDatabaseMaintenance`
(مثل `EnsurePhotoAnalysisSchemaAsync`). الهجرة Up تستخدم SQL idempotent (`IF NOT EXISTS`) كما في `AddCephNorms`.

**كيانات/أعمدة جديدة مقترحة:**
- `OrthoImagePreparation` (Id, OrthoClinicalPhotoId/OrthoPhotoId, OriginalImageUrl, PreparedImageUrl, Category, Phase, AspectRatio, CropX/Y, Zoom, Rotation, Brightness, Contrast, Flip, IsApproved, ApprovedBy/At).
- `OrthoCasePresentation` (Id, OrthoCaseId, TemplateName, Status, GeneratedFileUrl, GeneratedAt, CreatedByUserId, Notes).
- `OrthoCasePresentationSlide` (Id, PresentationId, SlideOrder, SlideType, Title, IsEnabled, CustomTextJson, ImageSlotJson, DataSourceJson).
- `Appointment.OrthoCaseId` (nullable) + فهرس.
- (اختياري) `CephAnalysis`: PixelsPerMm/ImageWidth/ImageHeight/CalibrationDistanceMm صريحة (مع الإبقاء على parsing الـNotes كـ fallback).
- **Cast: لا حاجة لكيان جديد** — `ModelAnalysis` موجود ويكفي.

---

## 7) عرض الحالة PPTX — المرجع والنهج

> **خصوصية:** ملف «Case presentation Allysa .pptx» الحقيقي يحتوي بيانات وصور مريض — **لا يُرفع للمستودع إطلاقًا**.
> يُستخدم مرجعًا بنيويًا فقط. الأثر الدائم في الريبو هو **slide map النصي** أدناه؛ وعند بناء مولّد PPTX (Sprint 5)
> يُنشأ **قالب dummy نظيف** (بلا صور/بيانات شخصية) داخل المستودع.

### تسلسل الشرائح المرجعي (slide map — من العرض المرفق كمرجع بنيوي فقط — 43 شريحة)
1. عنوان (مؤتمر سريري/MSc) · 2. معلومات المريض + المقابلة/الشكوى الرئيسية · 3. صور خارج الفم (شبكة) ·
4–8. الفحص السريري: البروفايل، الخط المتوسط، **قاعدة الأخماس (Rule of Fifths)**، **الأثلاث الرأسية** ·
9. صور داخل الفم · 10–11. تقييم داخل الفم + الإطباق · 12. **بانوراما** · 13. **سيفالو** ·
14. **جدول التحليل (ABO/Ceph)** · 15. **تحليل النماذج (Cast)** · 16. **Bolton** ·
17. **التشخيص** · 18. **أهداف العلاج** · 19–20. **خطة العلاج (قائمة المشاكل + الاستراتيجيات)** ·
21–24. **الميكانيكا (خلع، إغلاق المسافات، تراجع أمامي، إنهاء)** ·
25–40. **زيارات الضبط** (strap-up + 15 ضبطًا — كل شريحة ~5 صور تقدّم) · 41–42. **إزالة الأجهزة + الاحتفاظ** · 43. شكر.

> هذا التسلسل يطابق القسم 21 في طلب المالك. كل بيانات هذه الشرائح متوفّرة في الكيانات الموجودة
> (المريض، الفحص، الصور بالفئة/الطور، السيفالو/القياسات، النماذج/Bolton، التشخيص، الخطة، الزيارات، الاحتفاظ).

### النهج التقني
- **حزمة:** `DocumentFormat.OpenXml` (مفتوحة المصدر، MIT) لتوليد `.pptx`.
- **القوالب:** تعريف قابل لإعادة الاستخدام (نوع الشريحة، عنوان، حشوات نص/صورة/جدول، ربط البيانات، إلزامي/اختياري) — يسمح بقوالب مستقبلية.
- **الصور:** استخدام **النسخ المُحضّرة فقط** (من أداة تحضير الصور)، فتحات ثابتة، **crop-to-fit عبر `a:srcRect`/`a:stretch><a:fillRect`** — **بلا تشويه/تمدّد**؛ الصور الناقصة → placeholder أو تُتخطّى حسب اختيار الطبيب.
- **التوليد:** `POST /api/ortho-cases/{id}/case-presentation` (فحص جاهزية → توليد → ملف قابل للتنزيل) مع فحص صلاحية الوصول للمريض.

---

## 8) مناطق الخطر

- **الهجرات:** 31 هجرة قديمة بلا سمة `[Migration]`؛ لا تُصلَح. كل جديدة بالنمط + hotfix idempotent (انظر §6).
- **DoctorId FK:** `Doctors.Id` لا `Users.Id`.
- **التواريخ المحلية:** `localDateString()` (اليمن UTC+3) — لا `toISOString().slice`.
- **المالية مصدر الحقيقة Finance V3** — لا تكرار منطق؛ ملخّص قراءة فقط.
- **المختبر مصدر الحقيقة** — ربط بـ OrthoCaseId فقط، لا تكرار.
- **حجم الصفحة:** `ortho/[id]/page.tsx` ضخم (~3247 سطر) — أيّ تبويب جديد يُفضَّل مكوّنًا منفصلًا لتقليل الخطر.
- **AI مسودة فقط** بعبارة المراجعة الإلزامية؛ لا تنبؤ مزيف.
- **PPTX:** التوليد قد يكون ثقيلًا (108 صورة) — توليد غير متزامن/محدود الحجم، وتنزيل من تخزين دائم (`UPLOADS_PATH`).

---

## 9) ترتيب السبرنتات (مُعدّل حسب الواقع — لا تكرار الموجود)

| السبرنت | النطاق | ملاحظة الواقع |
|---|---|---|
| **S0** | هذا التدقيق + الوثيقة | ✅ (هذه الوثيقة) |
| **S1** | ترويسة حالة مُثراة + إدراج تبويبَي Cast/Photo + تبويب Lab (قراءة) + تبويب Reports placeholder | أغلب التبويبات موجودة؛ العمل = دمج + ترويسة |
| **S2** | تقرير PDF لتحليل النماذج + تقرير الحالة الموحّد PDF | يبني على مولّدات PDF الموجودة |
| **S3** | أداة تحضير الصور + كيان `OrthoImagePreparation` (+ هجرة) | جديد |
| **S4** | ربط DailyOps/Appointments بـ OrthoCaseId + موعد 21 يومًا + شارة | جديد (هجرة `Appointment.OrthoCaseId`) |
| **S5** | **مولّد PPTX** + قوالب + كيانات العرض (+ هجرة) — أكبر بند | جديد كليًا |
| **S6** | (اختياري) ترحيل معايرة السيفالو لحقول صريحة + تلميع + اختبار انحدار شامل | تنظيف |

> كل سبرنت = PR مستقل، أخضر CI (backend build/test + frontend tsc/lint/vitest/build)، مع استجابة لمراجعة Codex، **بلا دمج تلقائي إلا بإذن المالك**، ولقطات للواجهة.

---

## 10) ما لا يُبنى (موجود — يُعاد استخدامه فقط)

تبويبات Overview/Records/Exam/Problems/Ceph/Diagnosis/Plan/Visits/Stages/Extraction/Retention · حاسبة النماذج السبع (Bolton…)
· محرّك السيفالو السبعة · تحليل الصور + مزامنته للتشخيص · مولّدات PDF للسيفالو/الصور · Finance V3 · وحدة المختبر ·
نقطة `GET /overview` التجميعية · نظام صلاحيات الوصول للمريض.

---

**خلاصة Sprint 0:** الوحدة أنضج بكثير مما تفترضه الخطة. العمل المتبقّي الحقيقي = **دمج تبويبات + تقارير PDF ناقصة +
أداة تحضير الصور + ربط المواعيد/العمليات + مولّد PPTX**. لا إعادة بناء. التالي: Sprint 1 على هذا الفرع بعد موافقة المالك على هذه الخريطة.
