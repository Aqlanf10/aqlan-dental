# تسليم عمل — إكمال A9–A11 من التخطيط التقويمي الجراحي

**النوع:** وثيقة تسليم (handoff) لوكيل آخر يواصل تنفيذ خارطة `docs/ortho-module/ORTHO_SURGICAL_AI_VISION_EXPANSION.md`.
**الحالة عند كتابة هذه الوثيقة:** A1–A8 مُنفَّذة ومدموجة بالكامل في `main`. هذه الوثيقة تسلّم **A9 (مشروطة) و A10 و A11**.

---

## 1. ما اكتمل بالفعل (A1–A8) — لا تُعِد بناءه

كل سبرنت أدناه فرع مستقل → CI أخضر (5 فحوص) → دمج squash إلى `main`. راجع الكود مباشرة (المسارات أدناه) قبل أي عمل جديد — **لا تخمّن أسماء الحقول**.

| سبرنت | المحتوى | الملفات الأساسية |
|---|---|---|
| **A1** | الجسر الخلفي: `OrthoSurgicalCase`/`SurgeonReview`/`JointPlan` + `OrthoSurgicalStatus` (15 حالة) + `OrthoSurgicalStatusTransitions` + `OrthoSurgicalCasesController` (CRUD، إرسال للجراح، مراجعة الجراح، اعتماد مزدوج، فتح حالة جراحية) | `backend/src/AqlanDentalPro.Domain/Entities/OrthoSurgicalCase.cs`, `SurgeonReview.cs`, `JointPlan.cs`, `Domain/Enums/OrthoSurgicalStatus.cs`, `Application/Services/OrthoSurgicalStatusTransitions.cs`, `API/Controllers/OrthoSurgicalCasesController.cs` |
| **A2** | هيكل الواجهة + 3 مداخل (Sidebar، تبويب ملف المريض، قائمة قابلة للتصفية) | `frontend/src/app/(dashboard)/ortho-surgical/page.tsx` و `[id]/page.tsx`، `components/patient/tabs/OrthoSurgicalTab.tsx`، `types/orthoSurgical.ts` |
| **A3** | بوابات جاهزية (قراءة بحتة فوق `RecordsChecklist`/`CephAnalysis`/`OrthoDiagnosis`) | `GET .../readiness` في `OrthoSurgicalCasesController.cs` |
| **A4** | تعليقات تراكمية + سجل تدقيق لكل حالة (قراءة من `AuditLogs` الموجود) | `Domain/Entities/OrthoSurgicalComment.cs`، `GET/POST .../comments`، `GET .../audit-trail` |
| **A5** | محرر الخطة المشتركة (`PUT .../joint-plan`) + **إصلاح خلل حقيقي**: القفل بعد الاعتماد المزدوج كان يتجاهل صمتًا إن لم يوجد صف `JointPlan` مسبقًا — أُصلح بإنشاء الصف تلقائيًا عند اكتمال الاعتماد | نفس الكنترولر — راجع دالة `ApplyApproval` و`UpsertJointPlan` |
| **A6** | لمحة تنفيذ الجراحة (`GET .../surgery-summary` — قراءة بحتة فوق `SurgeryCase`/`PreopReport`/`OperativeReport`/`PostopRecord`) | نفس الكنترولر |
| **A7** | تقريرا PDF (طبيب مشترك + شرح مريض مبسّط)، QuestPDF عربي RTL، هوية المركز من Settings | `API/Services/OrthoSurgicalReportPdfGenerator.cs` |
| **A8** | مساعد AI نصي (4 مسودات: تلخيص/إحالة/شرح مريض/مسودة خطة) — **مسودة فقط، لا حفظ تلقائي** | `Infrastructure/Services/OrthoSurgicalDraftService.cs` |

**الاختبارات:** 2179 اختبار وحدة تمر محليًا حتى نهاية A8. شغّل `dotnet test tests/AqlanDentalPro.UnitTests/AqlanDentalPro.UnitTests.csproj -c Release` وتحقق أن العدد لا ينخفض قبل أي دمج.

---

## 2. منهجية العمل الإلزامية (نفّذها لكل سبرنت جديد، بلا استثناء)

1. **زامن مع main** قبل كل سبرنت: `git checkout main && git pull origin main` ثم فرع جديد `feat/ortho-surgical-aN-<وصف-قصير>`.
2. **اقرأ الكود الفعلي قبل الكتابة** — افحص الكيانات/الحقول/الـ endpoints ذات الصلة بـ `Read`/`Grep` قبل افتراض أي اسم حقل. لا تخمّن.
3. **راجع مستندات الخطة أولًا** وحدّد الفقرة بالضبط التي يغطيها السبرنت:
   - `docs/ortho-module/ORTHOGNATHIC-SURGICAL-WORKSPACE-PLAN.md` (القرار المعماري الأصلي)
   - `docs/ortho-module/ORTHO_SURGICAL_WORKSPACE_IMPLEMENTATION_PLAN.md` (الجداول والسبرنتات S0-S8/الخريطة الأصلية)
   - `docs/ortho-module/ORTHO_SURGICAL_AI_VISION_EXPANSION.md` (الأقسام 1-14، خصوصًا §5 مستويات VTO، §7 بوابات الجاهزية، §9 قواعد الأمان، §12 خطة A0-A12)
   - `docs/ortho-module/ORTHO_SURGICAL_3D_SLICER_AND_IN_APP_3D_PLAN.md` (لـ A10 فقط)
4. **Backend:**
   - كيانات جديدة فقط **إضافية** (لا تعديل `OrthoCase`/`CephAnalysis`/`SurgeryCase`/`Patient`/`Doctor` — العلاقات دائمًا عبر `WithMany()` بلا inverse navigation على الكيانات الموجودة، إلا كيانات وحدة Ortho-Surgical نفسها مثل `OrthoSurgicalCase.Comments`).
   - **لا ملف migration يدوي.** القواعد الفارغة تُبنى من خط أساس EF تلقائيًا؛ قواعد الإنتاج عبر hotfix إقلاعي idempotent في `backend/src/AqlanDentalPro.API/Configuration/StartupDatabaseMaintenance.cs` (`CREATE TABLE/INDEX/CONSTRAINT IF NOT EXISTS` — انسخ نمط `EnsureOrthoSurgicalSchemaAsync`/`EnsureOrthoSurgicalCommentsSchemaAsync` بالضبط).
   - أضِف الـ endpoints إلى `OrthoSurgicalCasesController.cs` الموجود ما لم يكن هناك سبب معماري قوي لكنترولر منفصل. أعِد استخدام `CanAsync("view"/"edit"/"approve")` و`DenyIfDoctorCannotAccess` الموجودَين.
   - سجّل أي خدمة جديدة في `backend/src/AqlanDentalPro.API/Configuration/ServiceRegistrationConfiguration.cs` (`services.AddScoped<...>()`).
   - صلاحيات: أعِد استخدام مورد `ortho_surgical` الموجود في `RolePermissions` (لا تُنشئ مفتاحًا جديدًا إلا لضرورة واضحة)، وسياسة `OrthoSurgicalAccess` الموجودة في `AuthorizationPolicyConfiguration.cs`.
5. **اختبارات Backend إلزامية قبل أي push:**
   - أضِف مجلد/ملف اختبار جديد تحت `backend/tests/AqlanDentalPro.UnitTests/OrthoSurgical/` يتبع نمط الملفات الموجودة هناك (EF InMemory + Moq، `CreateDb()`، `SeedCase`/`SeedBareCase`، إعادة استخدام `Build(...)` مع `IPatientAccessService` mock بـ `IsDoctor=false` لتجاوز فحص المريض عند اختبار منطق سير العمل مباشرة).
   - شغّل: `export PATH="$PATH:/root/.dotnet" && dotnet build src/AqlanDentalPro.API/AqlanDentalPro.API.csproj -c Release` من `backend/` — تأكد **0 أخطاء**.
   - شغّل الاختبارات الجديدة أولًا بفلتر، ثم المجموعة الكاملة: `dotnet test tests/AqlanDentalPro.UnitTests/AqlanDentalPro.UnitTests.csproj -c Release` — تأكد **Failed: 0** وأن العدد الكلي ازداد بعدد اختباراتك الجديدة فقط.
6. **Frontend:**
   - مكوّنات جديدة تحت `frontend/src/app/(dashboard)/ortho-surgical/[id]/_components/` (النمط الموجود: مكوّن مستقل self-fetching، يُمرَّر له `orthoSurgicalCaseId` فقط).
   - أنواع جديدة في `frontend/src/types/orthoSurgical.ts` (لاحظ: خصائص الاستجابة من Backend بصيغة PascalCase في C# تُسلسَل تلقائيًا camelCase في JSON — طابق ذلك في الـ TypeScript types).
   - عند إضافة تنزيل PDF استخدم `downloadPdfFromApi` الموجود في `frontend/src/lib/pdfDownload.ts` — لا تُعِد تطبيقه.
   - شغّل من `frontend/`: `npx tsc --noEmit` ثم `npm run lint` ثم `npm run build` (قد يستغرق حتى دقيقتين — استخدم مهلة كافية). كلها يجب أن تنجح بلا أخطاء جديدة (تجاهل التحذيرات الموجودة مسبقًا مثل `'Doctor' is defined but never used`).
7. **تحقّق من نطاق diff قبل commit:** `git status --short` — يجب ألا يظهر أي ملف خارج نطاق السبرنت. إن ظهر ملف غير متوقّع، افحصه قبل الإضافة.
8. **Commit + Push + PR:** رسالة commit تشرح "لماذا" لا "ماذا" فقط (اتبع أسلوب رسائل A1-A8 كمرجع عبر `git log --oneline | grep ortho-surgical`). افتح PR عبر أداة GitHub مع نص عربي بنفس بنية أوصاف A1-A8 (الهدف، ما تم، الاختبارات، النطاق/الأمان، التحقق المحلي).
9. **انتظر CI الأخضر** (5 فحوص: Backend Build&Test، Frontend Lint/Type/Build، E2E Playwright، Vercel Preview، Arabic Mojibake Guard) ثم ادمج بـ squash. إن فشل فحص وبدا **عابرًا (flaky)** وغير متعلّق بتغييرك (مثال: فجوة زمنية غريبة بين آخر اختبار ناجح وملخص الفشل، أو عدد فاشل=1 من آلاف)، أعد المحاولة بدفع commit فارغ لإعادة تشغيل CI بدل محاولة "إصلاح" شيء غير مرتبط بتغييرك.

---

## 3. القواعد الصارمة (من CLAUDE.md — لا استثناء)

- **لا تعديل الهجرات التاريخية** ولا إضافة سمة `[Migration]` لها — القواعد الفارغة تُبنى من خط أساس EF، والإنتاج عبر hotfix idempotent فقط.
- **لا hardcoding** لهوية المركز — كل نص/PDF يقرأ `clinic.name`/`clinic.lead_doctor`/`clinic.lead_doctor_title`/`clinic.lead_doctor_credentials` من `Settings` (راجع `CephReportPdfGenerator.ResolveClinicIdentityAsync` كمرجع جاهز — أعِد استخدامه، لا تُعِد تطبيقه).
- **رسائل الأخطاء عربية** بحقل `message` في كل استجابة 4xx/5xx. **لا تسريب تفاصيل استثناءات** في استجابات HTTP (اطبعها بـ `logger.LogError` فقط).
- **لا حذف حالة لها سجلات** — أرشفة (`IsActive=false`) فقط. كل تغيير حالة/اعتماد يُسجَّل عبر `IAuditService.LogAsync`.
- **لا تُضعِف الصلاحيات أو الأمان لتمرير اختبار.** إن بدا اختبار يتطلب إضعاف فحص وصول، أعد صياغة الاختبار لا الكود.
- **لا تلمس منطق المالية** (`FinanceV3*`, `TreasuryResolutionService`, إلخ) إلا إن طلب المستخدم ذلك صراحة.
- **التواريخ:** لا تستخدم `toISOString().slice(0,10)` في الواجهة أبدًا (انزياح يوم بتوقيت اليمن UTC+3) — استخدم `localDateString()` من `frontend/src/lib/utils.ts`.
- **أرقام الأسنان:** نظام FDI فقط عند التعامل مع بيانات سنية.
- **VTO الجراحي تحديدًا (P12/A9):** ممنوع أي "حسابات مزيفة" — أي تحويل حركة عظمية↔نسيج رخو يجب أن يكون إما (أ) نِسَبًا سريرية منشورة قابلة للتهيئة من `Settings`، أو (ب) موثّقة بمصدرها. لا تخترع معاملًا رقميًا.
- **الذكاء الاصطناعي:** ممنوع أن يعتمد AI خطة أو يقرّر جراحة أو يُنشئ حالة جراحية تلقائيًا. كل مخرج AI مسودة فقط، تتطلب نسخًا يدويًا من الطبيب، وتُسجَّل في `OrthodonticAiLogs`.

---

## 4. الخطوة صفر الإلزامية قبل A9 — تدقيق جاهزية السيفالو الفعلية

**لا تبدأ A9 مباشرة.** جدول `docs/ortho-module/MASTER-PLAN.md` يُظهر P1-P12 كلها `⬜` (غير محدَّثة)، لكن الفحص الفعلي للكود خلال هذه الجلسة وجد أن أجزاء كبيرة من P5/P6/P10/P11 **منفَّذة فعليًا** (`CephController` بمعالم/قياسات/إصدارات/اعتماد/PDF، `CephNormsController` بمعايير قابلة للتهيئة، `OrthoCaseAiController`/`CephController.draft-diagnosis` بمساعد AI آمن، `CasePresentationPanel` بعرض PPTX). **الجدول لا يعكس الواقع — لا تثق به، ولا تثق بانطباعي، دقّق بنفسك:**

1. افحص `backend/src/AqlanDentalPro.API/Controllers/CephController.cs` و`CephNormsController.cs` وقارن الوظائف الفعلية بمتطلبات **P5** (رفع+معايرة، معالم يدوية، Steiner/Tweed/Downs/Wits/McNamara، معايير قابلة للتهيئة، تفسير، إصدارات ومقارنة، PDF عربي/إنجليزي) — حدّد ما المكتمل وما الناقص فعليًا.
2. افحص `OrthoDiagnosis`/`CephDiagnosis` وقارن بمتطلبات **P6** (هل يوجد rule-engine حقيقي يصنّف I/II/III تلقائيًا من القياسات، أم إدخال يدوي فقط؟).
3. **معيار القرار:** A9 (VTO الجراحي 2D) يتطلب **حد أدنى**: تحليل سيفالو **معتمد** (`CephAnalysis.IsApproved=true`) بقياسات SNA/SNB/ANB/Wits محفوظة فعليًا لحالات حقيقية، وتشخيص هيكلي مسجَّل. إن كان هذا متوفرًا تقنيًا وقابلًا للاستخدام الآن (حتى لو الجدول التوثيقي يقول غير ذلك) — **A9 غير محظورة تقنيًا، تابع للقسم 5**.
4. إن وجدت فجوة حقيقية (مثلًا: لا معايير قابلة للتهيئة، أو لا اعتماد سريري فعلي) — **لا تبدأ A9**. اكتب تقريرًا مختصرًا (REPORT-ONLY، بلا كود) في `docs/ortho-module/` يوثّق الفجوة، وانتقل مباشرة إلى **A10** (لا يعتمد على السيفالو إطلاقًا)، ثم أبلغ في ملخصك النهائي أن A9 ما زالت مجمّدة ولماذا بالتحديد.

---

## 5. A9 — Surgical VTO 2D (مشروطة بنتيجة القسم 4)

المرجع الكامل: `ORTHO_SURGICAL_AI_VISION_EXPANSION.md` §5 (المستوى 2) و§12 (Sprint A9-A10).

### النطاق
- كيان جديد **إضافي** `OrthoSurgicalVto` (على نمط `OrthoSurgicalComment`): `OrthoSurgicalCaseId`, `CephAnalysisId`, `MaxillaMoveMm`, `MandibleMoveMm`, `ChinMoveMm`, `RotationDegree`, `PredictedSNA/SNB/ANB/Wits/Overjet` (كلها `decimal?`)، `Notes`، `CreatedBy`، `IsApprovedByOrthodontist`.
- Endpoint: `POST/GET .../vto` — يحفظ سيناريو حركة ويحسب **تأثيره على القياسات فقط** (SNA/SNB/ANB/Wits تتغيّر حسابيًا حسب حركة الفك — علاقات هندسية بسيطة موثّقة، ليست "حسابات مزيفة" لأنها تحويلات هندسية مباشرة لا تنبؤات سريرية).
- **شرط تشغيل صارم في الـ Backend:** ارفض الطلب (400 برسالة عربية) إن كان `CephAnalysis.IsApproved != true`.
- الواجهة: تمديد `frontend/src/components/ceph/CephVtoCanvas.tsx`/`CephSuperimposeCanvas.tsx` الموجودَين (VTO تقويمي حاليًا لحركة القواطع فقط) — لا تبنِ canvas من الصفر.
- **نِسَب تحويل السوفت تيشو (إن أُضيفت):** يجب أن تكون قيمًا قابلة للتهيئة من `Settings` (مثال: `ortho_surgical.soft_tissue_ratio_lower_lip`)، موثّقة المصدر في تعليق الكود، لا مُخترَعة.
- **عبارة الإخلاء الإلزامية** في كل عرض/PDF لنتيجة VTO: "هذه محاكاة تخطيطية تقريبية ولا تُعد قرارًا جراحيًا نهائيًا."

### ممنوع في A9
- أي "تنبؤ" سوفت تيشو غير موثّق المصدر.
- عرض 3D أو استخدام CBCT (ذلك A10).
- اعتماد تلقائي للسيناريو — يتطلب `IsApprovedByOrthodontist` صريحًا.

---

## 6. A10 — أساس عارض 3D (لا يعتمد على السيفالو — ابدأ به إن كانت A9 محظورة)

المرجع الكامل: `ORTHO_SURGICAL_3D_SLICER_AND_IN_APP_3D_PLAN.md` (كامل) و`ORTHO_SURGICAL_AI_VISION_EXPANSION.md` §5 (المستوى 4) و§12 (Sprint A11 في تلك الوثيقة).

### النطاق (المسار الأول فقط — Export/Import، وليس العارض المصغّر الكامل)
- **رفع CBCT:** يستخدم `api/radiographs` **الموجود بالفعل** (`Radiograph.XrayType = "CBCT"`) — **لا كيان جديد لهذا الجزء**، فقط تأكد أن حقل CBCT ظاهر ومسموح من واجهة السجلات المرتبطة بحالة Ortho-Surgical.
- **رفع STL/PLY/OBJ:** كملفات مرفقة عبر `Document` الموجود (`DocumentType` جديد مثل `"3d_model"`) — لا كيان جديد، فقط قيمة `DocumentType` إضافية واستخدام `api/documents` الموجود.
- **حزمة تصدير الحالة (Export Package):** endpoint جديد `GET .../export-package` يبني ملف JSON منظّم (بيانات المريض المجرَّدة، قياسات السيفالو، أهداف الخطة المشتركة) — **قراءة بحتة**، لا كيان جديد. لا تبنِ ZIP كامل بالصور الآن إن كان معقّدًا؛ ابدأ بـ JSON manifest فقط وثبّت ذلك بوضوح في تقريرك إن قصّرت النطاق.
- **عارض 3D داخل التطبيق (In-App Viewer Lite):** إن اتسع الوقت، عارض `three.js`/`@react-three/fiber` **للعرض فقط** (لا segmentation، لا قياس تفاعلي، لا تعديل) لملفات STL المرفوعة. إن كانت هذه الإضافة الأثقل تقنيًا (مكتبة جديدة في `package.json`)، نفّذها في **سبرنت منفصل A10b** بعد التأكد من قبول A10a (Export/Import + رفع الملفات).

### ممنوع في A10
- **لا** segmentation تلقائي.
- **لا** تصدير splints/guides جراحية (مسؤولية طبية/قانونية عالية — يتطلب تحقق سريري ومراجعة جراح، مؤجّل بلا استثناء).
- **لا** نسخ 3D Slicer أو محاولة تضمينه — القرار المعماري في الوثيقة صريح: عارض مصغّر مخصّص + round-trip خارجي فقط.

---

## 7. A11 — حوكمة البيانات (أولوية منخفضة جدًا — آخر ما يُنفَّذ)

المرجع: `ORTHO_SURGICAL_AI_VISION_EXPANSION.md` §4 (Phase D) و§12 (Sprint A12 في تلك الوثيقة).

### النطاق (عند الوصول إليه فقط)
- هذا سبرنت **سياسة وتوثيق أساسًا**، وليس ميزة تفاعلية. لا تبدأه قبل استنفاد A9/A10 أو إن طلب المالك صراحة.
- إن نُفِّذ: توثيق REPORT-ONLY لبروتوكول de-identification + موافقات + معايير التصنيف (labeling) قبل أي بناء نموذج مخصّص مستقبلي. **لا نموذج AI مخصّص يُبنى في هذا السبرنت أو أي سبرنت لاحق بلا إذن صريح من المالك.**

---

## 8. التقرير النهائي المطلوب عند انتهاء العمل (أو التوقف)

عند إكمال ما تيسّر من A9/A10/A11 (أو التوقف عند حاجز)، اكتب ملخصًا (بالعربية، موجّهًا لوكيل آخر سيراجع كل شيء) يتضمّن:
- قائمة أرقام PRs المدموجة بالترتيب مع عنوان كل واحد.
- عدد اختبارات الوحدة النهائي (`dotnet test`) ومقارنته بـ 2179 (نقطة البداية).
- **لكل سبرنت نُفِّذ:** ماذا بُني بالضبط، وماذا استُبعد عمدًا من النطاق (إن وُجد) ولماذا.
- **إن جُمِّدت A9:** سبب التجميد بالضبط بناءً على تدقيق القسم 4 (لا افتراضات).
- أي خلل أو قصور اكتُشف في كود A1-A8 أثناء العمل (على نمط اكتشاف خلل القفل الصامت في A5) مع رقم الـ PR الذي أصلحه إن أصلحته، أو وصفه إن تركته للمراجعة.
- توصية بأولوية العمل التالية.

**لا تدمج أي شيء لا تفهمه بالكامل. عند الشك في قرار تصميمي (مثل: هل نبني عارض 3D الآن أم نكتفي بالتصدير؟)، وثّق القرار الذي اتخذته وسببه بدل التوقف — الوكيل المراجع سيقيّم القرار لاحقًا.**
