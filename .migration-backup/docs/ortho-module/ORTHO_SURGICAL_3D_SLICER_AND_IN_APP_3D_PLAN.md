# خطة التكامل مع 3D Slicer وبناء Aqlan 3D Viewer داخل التطبيق

**النوع:** إضافة تخطيطية ضمن PR خطة Ortho-Surgical Workspace.  
**النطاق:** لا كود، لا migration، لا تغيير بيانات.  
**الهدف:** الاستفادة من 3D Slicer والأدوات الخارجية بدون نسخ كامل، مع بناء نسخة ويب مصغّرة داخل Aqlan Dental Pro مخصصة لطب الأسنان والتخطيط التقويمي الجراحي.

---

## 1. القرار النهائي

لا ننسخ 3D Slicer كاملًا داخل Aqlan Dental Pro.

بدل ذلك نعتمد مسارين متوازيين:

1. **External 3D Slicer Round‑Trip Workflow**  
   Aqlan يصدّر حزمة الحالة إلى 3D Slicer، والطبيب/الجراح يعمل التخطيط أو segmentation خارجيًا، ثم يرجع النتائج إلى Aqlan كمرفقات وملفات منظمة قابلة للمراجعة والاعتماد.

2. **Aqlan In‑App 3D Viewer Lite**  
   بناء عارض ثلاثي الأبعاد داخل التطبيق، يشبه الوظائف المهمة من 3D Slicer لكنه محدود بما يخدم طب الأسنان والتقويم وجراحة الفكين فقط.

القاعدة: **3D Slicer أداة خارجية قوية، و Aqlan هو السجل الطبي المركزي ومسار الاعتماد والتقرير والمالية.**

---

## 2. لماذا لا ننسخ 3D Slicer كاملًا؟

3D Slicer ضخم ومبني بتقنيات مختلفة عن Aqlan Dental Pro. برنامج Aqlan مبني كتطبيق ويب، لذلك النسخ الكامل سيخلق ديونًا تقنية كبيرة.

المخاطر:

- اختلاف التقنية: 3D Slicer يعتمد Desktop/Qt/C++/Python، بينما Aqlan Web/Next.js/ASP.NET.
- حجم كبير وصعوبة صيانة.
- تعقيد dependencies والرخص.
- صعوبة تشغيل كامل داخل المتصفح.
- احتمال خلق انطباع طبي خاطئ أن النظام معتمد جراحيًا.
- تضييع هدف Aqlan الأساسي: ملف مريض، تقويم، سيفالو، جراحة، موافقات، تقارير، مالية.

لذلك نبني **Dental 3D Planning Lite** داخل Aqlan، ونستفيد من 3D Slicer عبر export/import وامتداد خارجي لاحقًا.

---

## 3. المسار الأول: Export إلى 3D Slicer ثم Import إلى Aqlan

### 3.1 الفكرة

من داخل Ortho‑Surgical Workspace يظهر زر:

**تصدير إلى 3D Slicer**

ينشئ النظام حزمة حالة آمنة تحتوي ما يحتاجه الطبيب/الجراح للعمل الخارجي، ثم بعد الانتهاء يرجع المستخدم إلى Aqlan ويرفع النتائج.

### 3.2 محتوى حزمة التصدير Case Package

الحزمة تكون ZIP منظمة، مثل:

```text
AQLAN-ORTHO-SURGICAL-CASE-{CaseNumber}.zip
  manifest.json
  patient-summary.json
  ceph-measurements.json
  surgical-objectives.json
  records/
    lateral-ceph.png أو dicom
    opg.png
    cbct/ أو رابط آمن إن كان الحجم كبيرًا
    face-photos/
    intraoral-photos/
  models/
    upper.stl
    lower.stl
    occlusion.stl
  reports/
    ortho-summary.pdf
    ceph-report.pdf
```

### 3.3 manifest.json

هذا الملف مهم جدًا لأنه يربط كل شيء عند العودة إلى Aqlan:

```json
{
  "aqlanVersion": "2026.x",
  "exportType": "ortho-surgical-3d-planning",
  "caseId": "OrthoSurgicalCaseId",
  "patientId": "PatientId",
  "orthoCaseId": "OrthoCaseId",
  "cephAnalysisId": "CephAnalysisId",
  "exportedAt": "clinic-local-date-time",
  "exportedBy": "DoctorId/UserId",
  "privacyMode": "identified|deidentified",
  "intendedTool": "3D Slicer",
  "clinicalDisclaimer": "For planning support only; final decision requires doctor and surgeon approval."
}
```

### 3.4 Privacy Mode

عند التصدير يجب أن يختار المستخدم:

- **Identified package:** يحتوي اسم المريض ورقم الملف، للاستخدام الداخلي فقط.
- **De‑identified package:** يخفي اسم المريض وأرقام التواصل، للتعليم أو الاستشارة أو البحث.

الافتراضي الأفضل: De‑identified عند أي export خارج المركز.

### 3.5 العمل داخل 3D Slicer

يستخدم الطبيب/الجراح 3D Slicer أو أي برنامج خارجي لعمل:

- عرض CBCT/DICOM.
- segmentation إذا احتاج.
- فصل maxilla/mandible/chin إذا لزم.
- تصدير STL/OBJ/PLY.
- screenshots.
- تقرير PDF.
- ملف JSON اختياري للحركات الجراحية.

Aqlan لا يتحكم في دقة العملية الخارجية، لكنه يحفظ نتائجها ويجعلها قابلة للمراجعة والاعتماد.

### 3.6 Import Results إلى Aqlan

داخل نفس الحالة يظهر زر:

**استيراد نتائج التخطيط الخارجي**

الملفات المقبولة:

- `.stl`, `.obj`, `.ply`
- `.pdf`
- `.png`, `.jpg`
- `.json`
- `.zip`
- DICOM/CBCT عند الحاجة

### 3.7 كيان أو نموذج ExternalPlanningImport

يقترح إضافة كيان لاحقًا:

```text
ExternalPlanningImport
  Id
  OrthoSurgicalCaseId
  SourceTool: 3D_SLICER | DOLPHIN | MATERIALISE | MANUAL | OTHER
  ImportedBy
  ImportedAt
  PackageManifestJson
  MovementPlanJson?
  ReportDocumentId?
  ScreenshotDocumentIds
  MeshFileDocumentIds
  Notes
  ReviewedBy?
  ReviewedAt?
  ApprovedByOrthodontistAt?
  ApprovedBySurgeonAt?
  Status: Imported | UnderReview | Approved | Rejected | Superseded
```

### 3.8 شروط الاعتماد

أي نتيجة خارجية لا تصبح جزءًا من الخطة النهائية إلا إذا:

- راجعها أخصائي التقويم.
- راجعها أخصائي الجراحة.
- تم ربطها بالحالة الصحيحة عبر manifest.
- وُجدت ملاحظة واضحة عن مصدرها الخارجي.
- لا توجد نسخة أحدث superseded.

---

## 4. المسار الثاني: Aqlan In‑App 3D Viewer Lite

### 4.1 الهدف

بناء عارض ثلاثي الأبعاد داخل Aqlan يشبه الوظائف المهمة من 3D Slicer، لكنه مخصص للأسنان وجراحة الفكين فقط.

اسم مقترح:

**Aqlan Dental 3D Viewer Lite**

أو:

**Aqlan 3D Ortho‑Surgical Viewer**

### 4.2 ما يدعمه في البداية

- رفع/import STL/OBJ/PLY.
- ربط الملفات بـ Patient + OrthoSurgicalCase.
- عرض mesh داخل المتصفح.
- rotate / pan / zoom.
- إظهار/إخفاء الطبقات.
- شفافية opacity.
- ألوان للطبقات.
- أسماء الطبقات: maxilla, mandible, upper teeth, lower teeth, chin, splint draft.
- لقطة screenshot للتقرير.
- قياس مسافة بسيطة point-to-point.
- ملاحظات الطبيب والجراح.
- حفظ camera views.

### 4.3 ما لا يدعمه في البداية

- لا DICOM segmentation تلقائي.
- لا osteotomy planning حقيقي.
- لا soft tissue prediction.
- لا تصدير surgical splint/guide.
- لا ادعاء أنه بديل للبرامج الجراحية المعتمدة.

### 4.4 الكيانات المقترحة لاحقًا

```text
ThreeDAsset
  Id
  PatientId
  OrthoSurgicalCaseId?
  OrthoCaseId?
  SurgeryCaseId?
  AssetType: STL | OBJ | PLY | DICOM | SCREENSHOT | JSON_PLAN
  AnatomicalLayer: Maxilla | Mandible | UpperTeeth | LowerTeeth | Chin | Skull | SplintDraft | Other
  FileUrl
  OriginalFileName
  Source: Uploaded | ImportedFrom3DSlicer | ImportedExternal | GeneratedInAqlan
  UploadedBy
  UploadedAt
  IsApproved
  ApprovedBy?
  ApprovedAt?

ThreeDPlanningScenario
  Id
  OrthoSurgicalCaseId
  Label: Plan A | Plan B | Plan C
  Source: Manual | Imported3DSlicer | AI_Draft
  MovementJson
  Notes
  CreatedBy
  CreatedAt
  OrthodontistApprovedAt?
  SurgeonApprovedAt?
  IsLocked

ThreeDAnnotation
  Id
  ThreeDPlanningScenarioId
  Type: Distance | Angle | Landmark | Note | ScreenshotView
  DataJson
  CreatedBy
  CreatedAt
```

---

## 5. 3D Surgical Planning Lite داخل Aqlan

بعد نجاح viewer، نضيف planning خفيف:

### 5.1 Movement Controls

الطبيب يختار layer ثم يدخل:

- Translation X/Y/Z بالـ mm.
- Rotation pitch/yaw/roll بالدرجات.
- Notes.

أمثلة:

- Maxilla advancement +3 mm.
- Maxillary impaction 2 mm.
- Mandible setback 4 mm.
- Chin advancement 5 mm.

### 5.2 Scenarios

حفظ سيناريوهات:

- Plan A.
- Plan B.
- Plan C.
- Imported 3D Slicer Plan.
- Surgeon modified plan.

كل سيناريو له:

- before/after view.
- movement table.
- screenshots.
- notes.
- approvals.

### 5.3 حدود التخطيط الخفيف

هذا ليس osteotomy planning حقيقي. هو visual planning وتعليمي/توضيحي.

أي surgical movement داخل Aqlan يجب أن يحمل disclaimer واضح:

> هذا التخطيط ثلاثي الأبعاد داخل Aqlan هو محاكاة تخطيطية/تعليمية ولا يغني عن التخطيط الجراحي المتخصص ومراجعة جراح الفم والفكين.

---

## 6. Aqlan 3D Slicer Extension — خيار مستقبلي ذكي

بدل نسخ 3D Slicer، يمكن لاحقًا بناء Extension باسم:

**Aqlan Dental 3D Slicer Extension**

وظيفته:

- يستورد case package من Aqlan.
- يقرأ manifest.json.
- ينظم الملفات في Slicer.
- يساعد المستخدم على تصنيف الطبقات.
- يصدّر results package متوافقًا مع Aqlan.
- يكتب movement-plan.json.
- يصدّر screenshots/report.

### 6.1 Results Package من Extension

```text
AQLAN-3D-SLICER-RESULT-{CaseNumber}.zip
  source-manifest.json
  result-manifest.json
  movement-plan.json
  meshes/
    maxilla-segmented.stl
    mandible-segmented.stl
    chin.stl
  screenshots/
    before.png
    after-plan-a.png
    occlusion.png
  reports/
    slicer-planning-report.pdf
```

### 6.2 result-manifest.json

```json
{
  "sourceCaseId": "OrthoSurgicalCaseId",
  "sourceTool": "3D Slicer + Aqlan Extension",
  "plannedBy": "doctor/surgeon name or id",
  "exportedAt": "date-time",
  "movementPlanFile": "movement-plan.json",
  "meshFiles": [],
  "screenshots": [],
  "disclaimer": "External planning result; requires Aqlan doctor and surgeon review."
}
```

---

## 7. File Size & Storage Strategy

CBCT/DICOM وSTL قد تكون ملفات كبيرة. لذلك:

- لا نخزن ملفات ضخمة داخل قاعدة البيانات.
- نخزنها في storage الموجود أو external object storage.
- نخزن metadata في DB فقط.
- نضيف progress upload لاحقًا.
- نضيف checksum لكل ملف مهم.
- نضيف virus/file-type validation.
- نحدد حد حجم أولي لكل Sprint.

### أنواع الملفات المسموحة مبدئيًا

- STL
- OBJ
- PLY
- ZIP
- PDF
- PNG/JPG
- JSON
- DICOM لاحقًا بحدود واضحة

---

## 8. صلاحيات 3D وExternal Planning

مفاتيح صلاحيات مقترحة:

```text
OrthoSurgical3D.view
OrthoSurgical3D.upload
OrthoSurgical3D.export_external
OrthoSurgical3D.import_external
OrthoSurgical3D.review_external
OrthoSurgical3D.approve_ortho
OrthoSurgical3D.approve_surgeon
OrthoSurgical3D.delete_or_archive
```

القواعد:

- Reception لا يصدّر ولا يستورد تخطيط 3D.
- Accountant لا يرى ملفات 3D الطبية.
- Orthodontist يستطيع export/import/review/approve ortho part.
- OralSurgeon يستطيع review/approve surgical part.
- Admin يشرف ولا يتجاوز الاعتماد الطبي إلا وفق الصلاحيات.

---

## 9. التكامل مع التقارير

التقارير يجب أن تستطيع إدراج:

- screenshots من in-app viewer.
- screenshots المستوردة من 3D Slicer.
- movement table.
- source tool.
- reviewed/approved by.
- disclaimer.

لا يجوز أن يظهر تقرير للمريض وكأن النتيجة نهائية إذا لم تكن الخطة معتمدة من الطرفين.

---

## 10. ترتيب التنفيذ كسبرنتات

### Sprint 3D‑0 — Plan فقط

- إضافة هذه الوثيقة.
- لا كود.

### Sprint 3D‑1 — External Export Package

- endpoint لإنشاء case package.
- manifest.json.
- patient privacy mode.
- تنزيل ZIP.
- لا import بعد.

### Sprint 3D‑2 — External Import Results

- رفع ZIP/PDF/STL/OBJ/PLY/JSON.
- قراءة manifest.
- ربط النتائج بالحالة.
- status Imported/UnderReview.
- لا approval تلقائي.

### Sprint 3D‑3 — Review & Approval

- شاشة review للنتائج الخارجية.
- approve/reject/supersede.
- audit log.
- إدراج في JointPlan عند الاعتماد فقط.

### Sprint 3D‑4 — In‑App STL/OBJ/PLY Viewer Lite

- viewer داخل `/ortho-surgical/[id]`.
- rotate/pan/zoom.
- layers.
- screenshots.
- notes.

### Sprint 3D‑5 — Measurements & Annotations

- قياس مسافة.
- landmarks/notes.
- save camera views.
- screenshot report.

### Sprint 3D‑6 — 3D Planning Lite

- movement controls.
- scenarios A/B/C.
- movement table.
- compare before/after.

### Sprint 3D‑7 — 3D Slicer Extension Spec

- تصميم extension فقط.
- format للتبادل.
- لا تنفيذ extension إلا بعد اعتماد.

### Sprint 3D‑8 — DICOM/CBCT Viewer Foundation

- DICOM upload/view basic.
- slices only أو volume preview حسب القدرة.
- لا segmentation.

### Sprint 3D‑9 — Advanced Segmentation Research

- research only.
- لا production clinical decision.
- يحتاج موافقة منفصلة.

---

## 11. ما نضيفه إلى الخطة العامة

يجب أن تصبح الخطة العامة للـ Ortho‑Surgical Workspace فيها مسارين إضافيين:

1. **External Planning Round‑Trip**  
   تصدير من Aqlan إلى 3D Slicer/أي برنامج خارجي، ثم استيراد النتائج إلى Aqlan للمراجعة والاعتماد.

2. **In‑App 3D Viewer Lite**  
   نسخة ويب مصغّرة داخل التطبيق تعرض ملفات 3D وتدعم planning خفيف وسيناريوهات، بدون ادعاء أنها بديل كامل لـ 3D Slicer.

---

## 12. Definition of Done

لا يُقبل أي Sprint في هذا المسار إلا إذا:

- لا يكرر ملفات المريض.
- لا يخزن ملفات ضخمة في DB.
- يحفظ metadata وsource tool.
- يدعم audit.
- يحترم الصلاحيات.
- لا يعمل approval تلقائي.
- يضع disclaimer في export/import/report.
- لا يكسر Ortho/Ceph/Surgery.
- CI وEncoding Guard أخضر.
- لا mojibake.

---

## 13. الخلاصة

نعم، نستطيع الاستفادة من 3D Slicer بقوة، ونستطيع أيضًا بناء نسخة مشابهة داخل Aqlan، لكن بالطريقة الصحيحة:

- **3D Slicer**: أداة خارجية متقدمة للتخطيط، نستفيد منها عبر export/import وربما extension لاحقًا.
- **Aqlan 3D Viewer Lite**: عارض وتخطيط ويب داخل التطبيق، مخصص للأسنان والتقويم وجراحة الفكين.
- **Aqlan Dental Pro**: يبقى المصدر المركزي للحالة، الاعتمادات، التقارير، الموافقات، اليومية، المالية، والمختبر.

بهذا نختصر سنوات تطوير، ونحصل على قوة 3D Slicer، ونبني داخل Aqlan ما نحتاجه فقط بدون ديون تقنية ضخمة.