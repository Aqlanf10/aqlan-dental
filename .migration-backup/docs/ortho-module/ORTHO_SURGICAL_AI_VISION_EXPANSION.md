# توسعة الرؤية الطموحة: الذكاء الاصطناعي والـ Virtual Orthognathic Planning

**النوع:** إضافة تخطيطية مكمّلة لـ `ORTHOGNATHIC-SURGICAL-WORKSPACE-PLAN.md` و `ORTHO_SURGICAL_WORKSPACE_IMPLEMENTATION_PLAN.md`.

**الهدف:** رفع خطة Ortho-Surgical Workspace من مجرد جسر بين التقويم والجراحة إلى منصة متقدمة للحالات التقويمية الجراحية، تشمل الذكاء الاصطناعي، VTO، التقارير، عروض الحالات، 3D future foundation، والتحقق الطبي الآمن.

> هذه الوثيقة لا تنفّذ كودًا ولا تضيف migration. هي مرجع معماري وسريري يجب أن يلتزم به أي Sprint لاحق.

---

## 1. الرؤية النهائية للميزة

الميزة المطلوبة ليست صفحة عادية داخل التقويم ولا شاشة فرعية داخل الجراحة. الرؤية النهائية هي:

**Ortho-Surgical Intelligence Workspace**

أي مساحة عمل مشتركة ذكية بين أخصائي التقويم وأخصائي جراحة الفم والفكين، مرتبطة بملف المريض، السيفالو، الصور، التقارير، الجراحة، المختبر، والمالية.

الهدف أن يستطيع الطبيب أن يفتح حالة مريض واحدة ويرى:

- التشخيص التقويمي والسيفالومتري.
- هل الحالة تقويم فقط، camouflage، أم ortho-surgical.
- جاهزية السجلات.
- جاهزية السيفالو.
- رأي الجراح.
- خطة مشتركة معتمدة من الطرفين.
- VTO تعليمي/تخطيطي.
- تقرير للطبيب.
- تقرير مبسط للمريض.
- عرض حالة Case Presentation.
- تنفيذ العملية عبر SurgeryCase الموجود.
- متابعة post-op orthodontics.

---

## 2. المبدأ المعماري غير القابل للكسر

يجب الحفاظ على القرار الصحيح في الخطة الأصلية:

- لا وحدة مستقلة مكررة.
- لا تكرار لـ OrthoCase.
- لا تكرار لـ SurgeryCase.
- لا تكرار لـ CephAnalysis.
- لا تكرار للصور أو الأشعة أو المستندات.
- كيان رابط واحد هو مركز الحالة المشتركة.

الكيان الرابط `OrthoSurgicalCase` هو **ملف سير العمل والاعتماد**، وليس بديلًا عن التقويم أو الجراحة أو السيفالو.

---

## 3. طبقات النظام الذكي المقترحة

النظام لا يبنى كنموذج AI واحد يفعل كل شيء. يبنى كطبقات:

### 3.1 طبقة البيانات المنظمة

تقرأ من الموجود:

- `Patient`
- `OrthoCase`
- `OrthoDiagnosis`
- `OrthoClinicalExam`
- `TreatmentPlan`
- `CephAnalysis`
- `CephLandmark`
- `CephMeasurement`
- `CephDiagnosis`
- `PhotoAnalysis`
- `ModelAnalysis`
- `Radiograph`
- `Document`
- `SurgeryCase`
- `PreopReport`
- `OperativeReport`
- `PostopRecord`

ولا تنسخها إلا عند الحاجة إلى snapshot موثّق.

### 3.2 طبقة القواعد السريرية Clinical Rules Engine

هذه الطبقة لا تعتمد على AI. تعتمد على قواعد واضحة قابلة للاختبار، مثل:

- ANB/Wits لتصنيف Class II / Class III.
- SNA/SNB لتمييز maxillary vs mandibular component.
- FMA/SN-MP للاتجاه العمودي.
- Incisor inclination لتحديد compensation/decompensation.
- overjet/overbite/open bite/crossbite لتحديد surgical objectives.
- facial asymmetry / chin position / gummy smile كمدخلات جراحية.

هذه الطبقة تُنتج:

- Problem list.
- Treatment need.
- Surgical indication draft.
- Readiness warnings.
- Missing records checklist.

### 3.3 طبقة AI Assist

الذكاء الاصطناعي هنا مساعد فقط:

- يلخص التشخيص.
- يقترح problem list من البيانات المنظمة.
- يقترح أسئلة للجراح.
- يصيغ referral letter.
- يصيغ شرحًا مبسطًا للمريض.
- يقارن Plan A/B/C نصيًا.
- يكتب draft report.

كل مخرجاته يجب أن تكون:

- Draft.
- محفوظة في audit log.
- قابلة للمراجعة.
- لا تعتمد إلا بعد موافقة الطبيب.

### 3.4 طبقة Simulation / VTO

هذه الطبقة تنقسم إلى:

- 2D Orthodontic VTO: موجود جزئيًا ويجب الحفاظ عليه.
- 2D Surgical VTO: لاحق بعد اكتمال السيفالو.
- 3D viewer foundation: لاحق، عرض فقط.
- 3D surgical planning: مؤجل جدًا.
- splint/guide export: ممنوع الآن ومؤجل حتى تحقق سريري وقانوني.

---

## 4. نموذج AI المقترح على مراحل

### مرحلة A — AI بدون تدريب خاص

تستخدم نماذج عامة عبر API مع بيانات منظمة ومحدودة:

- لا ترسل صورًا أو ملفات حساسة إلا بموافقة وإعدادات واضحة.
- لا ترسل كل ملف المريض؛ فقط DTO سريري مختصر.
- لا ترسل أرقام هواتف أو بيانات شخصية عند عدم الحاجة.
- احفظ prompt/output/model/version في `OrthodonticAiLog` أو كيان AI log مشترك.

المخرجات:

- Case summary.
- Surgical referral draft.
- Patient explanation.
- Joint plan draft.
- Risk/limitations wording.

### مرحلة B — AI للـ Landmark Draft

بعد اكتمال السيفالو الحالي:

- AI يقترح landmarks فقط.
- الطبيب يراجع ويعتمد.
- لا تُستخدم النقاط في التقرير أو VTO إلا بعد اعتماد الطبيب.
- كل نقطة AI لها confidence ومصدر.

### مرحلة C — AI Diagnosis Support

- مقارنة القياسات بالـ norms.
- تفسير القياسات.
- اقتراح skeletal pattern.
- اقتراح هل الحالة surgical candidate أو لا كمؤشر أولي.

المخرجات يجب أن تكون بصيغة:

> "اقتراح مبدئي يحتاج مراجعة الطبيب".

### مرحلة D — Local Dataset & Future Custom Model

بعد استخدام النظام في المركز، يمكن بناء dataset داخلي:

- Ceph images + approved landmarks.
- Measurements.
- Diagnosis.
- Treatment decision.
- Surgical movement plan.
- Pre/post photos.
- Outcomes.

لا يبدأ تدريب نموذج خاص إلا بعد:

- موافقات خصوصية.
- إزالة تعريف المريض de-identification.
- عدد كافٍ من الحالات.
- labeling موحد.
- clinical validation.

---

## 5. Virtual Orthognathic Planning — المستويات المطلوبة

### المستوى 1 — Workflow + Joint Planning

هذا يبدأ أولًا:

- إنشاء OrthoSurgicalCase.
- ربط OrthoCase/Ceph/Surgery.
- إرسال للجراح.
- مراجعة الجراح.
- خطة مشتركة.
- اعتماد مزدوج.
- تقرير وموافقة.

### المستوى 2 — 2D Surgical VTO

بعد اكتمال السيفالو:

- Maxilla advancement / setback.
- Maxillary impaction / down-grafting.
- Mandibular advancement / setback.
- Genioplasty advancement / setback.
- Clockwise / counter-clockwise rotation.
- Bimaxillary scenario.
- Before/after overlay.
- جدول الحركة بالـ mm والدرجات.
- قياسات قبل/بعد.
- soft tissue approximation مع disclaimer.

### المستوى 3 — Case Presentation Generator

النظام يولد عرض حالة:

- PowerPoint أو PDF.
- شعار واسم المركز.
- اسم الطبيب والمؤهل من Settings.
- بيانات المريض حسب الخصوصية.
- قبل/بعد.
- التشخيص.
- القياسات.
- plan A/B/C.
- صور السجلات.
- موافقة/اعتماد.

هذا مهم لطموح المركز والتدريس وشرح الحالة للمريض والجراح.

### المستوى 4 — 3D Viewer Foundation

لاحقًا:

- رفع CBCT/DICOM عبر Radiograph الحالي إذا أمكن.
- رفع STL/PLY/OBJ كملفات مرفقة.
- عرض 3D فقط.
- measurement viewer فقط.
- لا segmentation تلقائي في البداية.
- لا تصدير splints/guides.

### المستوى 5 — 3D Surgical Planning

مؤجل جدًا:

- CBCT segmentation.
- Maxilla/mandible/chin segmentation.
- osteotomy planning.
- occlusion setup.
- registration بين CBCT و STL.
- soft tissue prediction.

لا يبدأ إلا بعد اكتمال 2D ووجود حالات كافية وتعاون جراح وجه وفكين.

---

## 6. واجهة المستخدم المطلوبة حسب طموح النظام

داخل `/ortho-surgical/[id]` يجب أن تكون الواجهة على شكل workspace احترافي:

### 6.1 Header ثابت

يعرض:

- اسم المريض ورقم الملف.
- الحالة الحالية.
- من المسؤول الآن.
- Orthodontist.
- Surgeon.
- Ceph readiness.
- Records completeness.
- Joint approval state.
- أزرار: إرسال للجراح، طلب تعديل، اعتماد، تقرير، عرض حالة.

### 6.2 Timeline رأسي

يعرض مراحل الحالة:

1. Records.
2. Ceph.
3. Orthodontic preparation.
4. Surgeon review.
5. Joint plan.
6. Consent.
7. Surgery.
8. Post-op orthodontics.
9. Completion.

### 6.3 Tabs

- Overview.
- Records.
- Diagnosis.
- Cephalometric.
- Orthodontic Preparation.
- Surgical Objectives.
- VTO.
- Surgeon Review.
- Joint Plan.
- Surgery Execution.
- Post-op Outcome.
- Reports & Consent.
- AI Assistant.
- Audit Log.

### 6.4 لغة واضحة للمستخدم

كل شاشة يجب أن تعرض:

- ما المطلوب الآن؟
- من المسؤول؟
- ماذا ينقص؟
- هل يمكن الانتقال للمرحلة التالية؟
- لماذا الزر معطل؟

---

## 7. متطلبات الجاهزية Readiness Gates

لا تسمح الواجهة ولا الـ Backend بالانتقال الخاطئ.

### RecordsReady

يتطلب:

- Facial photos.
- Intraoral photos.
- OPG.
- Lateral Ceph.
- PA Ceph عند asymmetry.
- Model/cast أو intraoral scan عند الحاجة.
- CBCT عند طلب الجراح.

### CephReady

يتطلب:

- Calibration محفوظة.
- Landmarks محفوظة.
- Measurements محفوظة.
- No unsaved edits.
- Analysis approved by doctor.

### SurgeonReviewReady

يتطلب:

- Ortho diagnosis.
- Ceph approved.
- Records minimum complete.
- Orthodontic objective draft.

### JointPlanReady

يتطلب:

- Surgeon review completed.
- Procedure proposed.
- Orthodontic preparation plan.
- Risk/limitations.
- Patient explanation draft.

### ReadyForSurgery

يتطلب:

- Orthodontist approval.
- Surgeon approval.
- Consent signed.
- SurgeryCase created or scheduled.
- Pre-op checklist started.

---

## 8. التقارير المطلوبة

### 8.1 تقرير الطبيب المشترك

يشمل:

- Patient summary.
- Orthodontic diagnosis.
- Ceph measurements.
- Records status.
- Surgical objectives.
- VTO scenario إذا موجود.
- Surgeon review.
- Joint plan.
- Approvals.
- Risks/limitations.

### 8.2 تقرير المريض المبسط

بلغة عربية سهلة:

- ما المشكلة؟
- لماذا قد نحتاج جراحة؟
- ما مراحل العلاج؟
- ماذا سيعمل التقويم؟
- ماذا سيعمل الجراح؟
- ما حدود المحاكاة؟
- ما المخاطر العامة؟
- ما المتوقع بعد العملية؟

### 8.3 Referral Letter للجراح

- من أخصائي التقويم إلى الجراح.
- summary مختصر.
- objective.
- مرفقات مطلوبة.
- سؤال واضح للجراح.

### 8.4 Case Presentation

- PDF/PPT.
- مناسب للتدريس والشرح.
- يعتمد على الصور والسيفالو والخطة.
- لا يخرج بيانات حساسة إلا حسب خيار privacy.

---

## 9. قواعد الأمان الطبي والقانوني

يجب تضمين هذه العبارات في VTO والتقارير:

> هذه المحاكاة تعليمية/تخطيطية تقريبية ولا تُعد قرارًا جراحيًا نهائيًا.

> القرار النهائي يعتمد على فحص الطبيب، مراجعة أخصائي جراحة الفم والفكين، الأشعة، القياسات، السجلات، وموافقة المريض.

> أي اقتراح صادر عن AI هو مسودة تحتاج مراجعة واعتماد الطبيب.

ممنوع:

- AI يعتمد خطة.
- AI يحدد جراحة كقرار نهائي.
- تصدير surgical splint/guide بدون تحقق.
- تشغيل VTO بدون ceph approved.
- استخدام صور أو بيانات المريض في تدريب نموذج دون موافقة.

---

## 10. التكامل مع المالية والمختبر

### Finance

- لا تغيير في Finance V3 في المراحل الأولى.
- يمكن استخدام `Contract.RelatedCaseId` لربط عقد بالحالة.
- لا يتم إنشاء دفعات أو قيود مالية تلقائيًا من الخطة الطبية.
- أي خدمة جراحية يتم تسعيرها من Service Catalog لاحقًا.
- لا صرف خزينة إلا عبر الخدمات المالية الحالية.

### Lab

لاحقًا فقط:

- Surgical splint order.
- Model surgery order.
- Retainer بعد الجراحة.
- Surgical guide مستقبلًا.

لا يبدأ splint/guide export في النظام الآن.

---

## 11. التكامل مع Daily Operations

عند زيارة المريض يجب أن تظهر شارة:

**حالة تقويمية جراحية**

وفي شاشة الطبيب:

- آخر status.
- من المسؤول الآن.
- هل يوجد مراجعة جراح معلقة؟
- هل يوجد موافقة مريض ناقصة؟
- زر فتح Ortho-Surgical Workspace.

في الاستقبال:

- جدولة مواعيد التقويم والجراحة.
- طباعة تعليمات أو موافقة.
- لا تعديل للخطة الطبية.

---

## 12. خطة التنفيذ الموسعة حسب الطموح

### Sprint A0 — قبول الخطة الموسعة

- إضافة هذه الوثيقة.
- تثبيت أن الميزة ستكون Intelligence Workspace.
- لا كود.

### Sprint A1 — Backend Bridge

- `OrthoSurgicalCase`.
- `SurgeonReview`.
- `JointPlan`.
- Status + transitions.
- Access policy.
- CRUD minimal.

### Sprint A2 — Workspace UI

- `/ortho-surgical/[id]`.
- Header + status + responsible party.
- مداخل من التقويم والجراحة وملف المريض.

### Sprint A3 — Records/Diagnosis Readiness

- قراءة السجلات.
- قراءة ceph/diagnosis.
- readiness gates.
- missing items.

### Sprint A4 — Surgeon Collaboration

- إرسال للجراح.
- review.
- طلب تعديل.
- تعليقات.
- audit.

### Sprint A5 — Joint Plan + Double Approval

- خطة مشتركة.
- اعتماد مزدوج.
- lock بعد الاعتماد.

### Sprint A6 — Surgery Execution Link

- create SurgeryCase.
- link to existing surgery module.
- show preop/operative/postop.

### Sprint A7 — Reports/Consent/Presentation

- PDF doctor report.
- PDF patient explanation.
- consent forms.
- case presentation shell.

### Sprint A8 — AI Text Assistant

- summary.
- referral draft.
- patient explanation.
- joint plan draft.
- doctor-reviewed.

### Sprint A9 — 2D Surgical VTO

- after Ceph ready.
- manual movement controls.
- before/after.
- measurements delta.
- disclaimer.

### Sprint A10 — VTO AI Suggestions

- AI suggests possible movement scenarios.
- doctor edits.
- no automatic approval.

### Sprint A11 — 3D Viewer Foundation

- CBCT/STL upload/view only.
- no splint export.

### Sprint A12 — Dataset & Model Governance

- de-identification.
- labeling protocol.
- consent.
- export dataset only for owner-approved research.

---

## 13. Definition of Done لكل Sprint

أي Sprint لا يُقبل إلا إذا:

- لا يكسر Ortho/Ceph/Surgery.
- لا يغير Finance بدون طلب صريح.
- CI أخضر.
- Encoding Guard أخضر.
- لا mojibake.
- لا hardcoding لهوية المركز.
- صلاحيات Backend قبل الواجهة.
- رسائل عربية.
- audit للحالة والاعتمادات.
- smoke test للمداخل الثلاثة.
- توثيق ما تم وما أُجّل.

---

## 14. القرار النهائي

الخطة الحالية جيدة كأساس، لكن طموح المركز يحتاج أن يكون المنتج النهائي:

**Ortho-Surgical Intelligence Workspace**

وليس فقط:

**Ortho-Surgical Workflow Bridge**

لذلك يعتمد التنفيذ على مرحلتين:

1. **الأساس الآمن:** Workflow + bridge + approvals + reports.
2. **الذكاء والمحاكاة:** AI text assistant + 2D VTO + presentation + 3D foundation لاحقًا.

هذا يحقق طموح مركز د. عقلان الكامل بأن يكون النظام ليس فقط برنامج عيادة، بل منصة تخصصية متقدمة للتقويم والسيفالو وجراحة الفكين، مع أمان طبي ومسار اعتماد واضح بين التقويم والجراحة.
