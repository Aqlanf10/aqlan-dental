# خطة تنفيذ المهام المتبقية — Aqlan Dental Pro

## المهام الست الكبرى المتبقية من تقرير التدقيق

كل مهمة مصممة كـ prompt جاهز لتنفيذه بواسطة agent آخر. انسخ الـ prompt كاملاً وألصقه في جلسة الـ agent.

---

## المهمة 1: استخراج PatientJourneyService (CLIN-22)

### Prompt:

```
أنت مهندس full-stack تعمل على مستودع Aqlan Dental Pro (Next.js + ASP.NET Core). المرجع: backend/src/AqlanDentalPro.API/Controllers/PatientJourneyController.cs — ملف بطول 2242 سطر ينفذ: تجميع الرحلة، الملخص اليومي، الاستقبال، الإرسال للطابور، بدء الزيارة، التسليم، الدفع، إنشاء فاتورة مسودة، التحقق من الإغلاق المالي، تشكيل الاستجابة حسب الدور، وتوليد رقم الفاتورة عبر استدعاء ثابت لـ InvoicesController.GenerateInvoiceNumberAsync.

المطلوب:
1. أنشئ ملف Application/Services/PatientJourneyService.cs يحتوي على منطق القراءة والتجميع (GetTodayAsync, GetDailySummaryAsync).
2. أنشئ ملف Application/Services/CheckoutService.cs يحتوي على منطق الكتابة (IntakeAsync, SendToQueueAsync, StartVisitAsync, HandoffToReceptionAsync, CheckoutAsync, CreateDraftInvoiceAsync, ValidateFinancialClosureAsync, MarkLeftWithoutCompletionAsync).
3. انقل توليد رقم الفاتورة من InvoicesController.GenerateInvoiceNumberAsync إلى IInvoiceService.GenerateInvoiceNumberAsync — حقن الخدمة بدلاً من الاستدعاء الثابت.
4. انقل تشكيل الاستجابة حسب الدور (role-based field filtering، الأسطر ~700-770) إلى PatientDataShaper مشترك مع PatientsController.
5. PatientJourneyController يجب أن يصبح ~300 سطر: حقن الخدمتين + تفويض الاستدعاءات + إرجاع النتائج.
6. لا تغيير أي منطق عمل — فقط نقل الكود من المتحكم إلى الخدمات.
7. شغّل dotnet build + dotnet test للتأكد من عدم كسر شيء.
8. لا تنشئ PR — اترك التغييرات على برانش feat/extract-patient-journey-service.
```

---

## المهمة 2: توحيد OrthoVisit مع Visit (CLIN-05)

### Prompt:

```
أنت مهندس backend تعمل على مستودع Aqlan Dental Pro (ASP.NET Core + EF Core + PostgreSQL). المرجع: النظام لديه مفهومين متوازيين للزيارة: Visit (لطب الأسنان العام، مرتبط بـ Appointment + ClinicQueueItem، يظهر في PatientJourney) و OrthoVisit (مرتبط فقط بـ OrthoCase، له حقول wire/elastic/stage، لا يظهر في العمليات اليومية).

المطلوب:
1. أضف Guid? VisitId (nullable FK) إلى كيان OrthoVisit يربطه بـ Visit الأب.
2. أنشئ migration: AddOrthoVisitVisitLink — أضف العمود فقط (nullable، لا cascade delete).
3. عدّل OrthoCasesController.AddVisit: عند إنشاء OrthoVisit، أنشئ أيضاً Visit مرتبط بنفس المريض والموعد (إذا وجد OrthoCaseId على الموعد)، واضبط OrthoVisit.VisitId = visit.Id.
4. عدّل PatientJourneyController.GetToday / LoadOrthoJourneySummariesAsync: حمّل أحدث OrthoVisit للحالة المرتبطة بالموعد، واعرض WireUpper/WireLower/CurrentStage في بند الرحلة.
5. DetermineNextAction: إذا كان المريض في InProgress ولديه OrthoVisit نشط، اعرض "HandoffToReception" كالعادة — لا تغير المنطق.
6. لا تحذف OrthoVisit — أبقِه كجدول منفصل للحقول التقويمية الخاصة، لكن اربطه بـ Visit.
7. شغّل dotnet build + dotnet test.
8. لا تنشئ PR — اترك التغييرات على برانش feat/unify-ortho-visit.
```

---

## المهمة 3: تقسيم صفحة ortho/[id] (3469 سطر) (FE-20)

### Prompt:

```
أنت مهندس frontend تعمل على مستودع Aqlan Dental Pro (Next.js 15 + React 19 + TypeScript). المرجع: frontend/src/app/(dashboard)/ortho/[id]/page.tsx — ملف بطول 3469 سطر يحتوي على: نظرة عامة، فحص سريري، قائمة مشاكل، خطط علاج (A/B/C)، قرار قلع، قائمة فحص السجلات، تشخيص، احتجاز، صور، تحليلات نموذج، مسودة AI.

المطلوب:
1. أنشئ مجلد frontend/src/app/(dashboard)/ortho/[id]/_components/ — انقل كل قسم إلى مكوّن منفصل:
   - OrthoOverviewTab.tsx (النظرة العامة + بيانات المريض + المالية)
   - OrthoClinicalExamTab.tsx (الفحص السريري المهيكل)
   - OrthoProblemListTab.tsx (قائمة المشاكل)
   - OrthoTreatmentPlansTab.tsx (خطط العلاج A/B/C + الاعتماد)
   - OrthoExtractionTab.tsx (قرار القلع)
   - OrthoRecordsChecklistTab.tsx (قائمة فحص السجلات)
   - OrthoDiagnosisTab.tsx (التشخيص + المزامنة مع ceph/photo)
   - OrthoRetentionTab.tsx (الاحتجاز + زيارات المتابعة)
   - OrthoPhotosTab.tsx (الصور السريرية + تحضير الصور)
   - OrthoModelAnalysisTab.tsx (تحليلات النموذج)
   - OrthoAiDraftPanel.tsx (مسودة AI السريرية)
2. page.tsx يجب أن يصبح ~200 سطر: تحميل البيانات + إدارة التبويبات + تمرير props لكل مكوّن.
3. انقل الـ hooks المساعدة (fetch, mutate, etc.) إلى _lib/hooks.ts.
4. انقل الـ types المحلية إلى _lib/types.ts.
5. لا تغيير أي منطق أو styling — فقط تقسيم المكوّنات.
6. شغّل npx tsc --noEmit + npx next lint + npx next build للتأكد.
7. لا تنشئ PR — اترك التغييرات على برانش refactor/split-ortho-page.
```

---

## المهمة 4: دمج 3 شاشات تفاصيل المريض (FE-06)

### Prompt:

```
أنت مهندس frontend تعمل على مستودع Aqlan Dental Pro (Next.js 15 + React 19). المرجع: ثلاث شاشات متوازية تعرض بيانات المريض بشكل متداخل:
- /patients/[id] — الملف الكنسي للمريض (22 تبويب فرعي)
- /patient-journey/[patientId] — لوحة رحلة اليوم (زيارة، دفع، تقويم، جدول زمني)
- /doctor-clinic — لوحة الطبيب (فحص، علاج، وصفة، مختبر، تسليم)

المطلوب:
1. اجعل /patients/[id] الشاشة الكنسية الوحيدة لتفاصيل المريض.
2. /patient-journey/[patientId] يجب أن يُعيد توجيه (redirect) إلى /patients/[id]?focus=journey.
3. في /patients/[id]، أضف تبويب "رحلة اليوم" يعرض نسخة مدمجة من لوحة الرحلة (البطاقات المشتركة من patient-journey/_components/Cards.tsx).
4. /doctor-clinic يبقى كقائمة أطباء منفصلة، لكن عند اختيار مريض يفتح /patients/[id]?focus=doctor-clinic في لوحة جانبية (side panel) بدلاً من صفحة مستقلة.
5. انقل البطاقات المشتركة (PatientHeaderCard, TodaysAppointmentCard, FinanceSummaryCard, RecentVisitsCard, TimelineCard) من patient-journey/_components/Cards.tsx إلى components/patient/cards/ واستوردها في كلا المكانين.
6. لا تحذف /patient-journey/[patientId] — استبدل محتواه بـ redirect.
7. شغّل npx tsc --noEmit + npx next lint + npx next build.
8. لا تنشئ PR — اترك التغييرات على برانش refactor/consolidate-patient-screens.
```

---

## المهمة 5: إخراج StartupDatabaseMaintenance تدريجياً (C-08/DB-01)

### Prompt:

```
أنت مهندس database تعمل على مستودع Aqlan Dental Pro (ASP.NET Core + EF Core + PostgreSQL). المرجع: backend/src/AqlanDentalPro.API/Configuration/StartupDatabaseMaintenance.cs — ملف بطول 3963 سطر يحتوي على 99 CREATE TABLE IF NOT EXISTS + 366 ExecuteSqlRaw + 54 try/catch. يعمل عند كل إقلاع كـ "شبكة أمان" لأن هجرات EF Core قد تفشل على Railway.

المطلوب (تنفيذ تدريجي — لا تحذف الملف كاملاً دفعة واحدة):
1. جرد كل كتلة hotfix: لكل CREATE TABLE/INDEX IF NOT EXISTS أو ExecuteSqlRaw، حدد:
   - هل الجدول/العمود موجود في ModelSnapshot؟ (إذا نعم، الهجرة المسؤولة يجب أن تكون أنشأته)
   - هل الهجرة المسؤولة موجودة في __EFMigrationsHistory على Railway؟
2. للكتل التي لها هجرة EF Core مقابلة (مؤكدة بوجود الجدول/العمود في ModelSnapshot):
   - علّق الكتلة بـ // PHASED-OUT: covered by migration XXXXX
   - لا تحذفها — فقط علّقها حتى يتم التأكد من عدم ظهور تحذيرات في سجلات Railway لمدة أسبوعين
3. للكتل التي ليس لها هجرة مقابلة (DDL غير مُدار بـ EF):
   - أنشئ هجرة EF Core جديدة لكل منها (AddColumn, CreateTable, CreateIndex)
   - علّق الكتلة اليدوية بـ // REPLACED BY MIGRATION: XXXXX
4. أضف فحص صحة (health check) عند البدء: قارن المخطط الفعلي بـ ModelSnapshot. إذا كان هناك انحراف، سجّل تحذيراً بدلاً من المحاولة الصامتة للإصلاح.
5. شغّل dotnet build + dotnet test + dotnet ef migrations list للتأكد.
6. لا تنشئ PR — اترك التغييرات على برانش refactor/phase-out-startup-maintenance.
```

---

## المهمة 6: اختبارات تكامل PostgreSQL (TEST-18)

### Prompt:

```
أنت مهندس QA تعمل على مستودع Aqlan Dental Pro (ASP.NET Core + xUnit + EF Core). المرجع: backend/tests/AqlanDentalPro.UnitTests/ — 102 ملف اختبار، كلها تستخدم EF Core InMemory. لا توجد اختبارات تكامل حقيقية (WebApplicationFactory + PostgreSQL).

المطلوب:
1. أنشئ مشروع اختبارات جديد: backend/tests/AqlanDentalPro.IntegrationTests/
   - dotnet new xunit -n AqlanDentalPro.IntegrationTests
   - أضف مراجع إلى AqlanDentalPro.API و AqlanDentalPro.Infrastructure
   - أضف حزم: Microsoft.AspNetCore.Mvc.Testing, Testcontainers.PostgreSql, FluentAssertions
2. أنشئ WebApplicationFactory<Program> مخصصة:
   - تستخدم Testcontainers لإنشاء PostgreSQL حقيقي
   - تطبق الهجرات (db.Database.MigrateAsync)
   - تُعيد تعيين قاعدة البيانات بين الاختبارات (أو تستخدم معاملة لكل اختبار)
3. اكتب اختبارات تكامل للتدفقات الحرجة:
   a. Appointment double-booking race: طلبا POST متزامنان لنفس الطبيب/الوقت → واحد ينجح، الآخر 409
   b. Cashier session close race: إغلاقان متزامنان → واحد ينجح، الآخر 400
   c. Treasury concurrent decrement: خصمان متزامنان من نفس الخزينة → لا يصبح الرصيد سالباً
   d. Invoice update atomicity: تحديث بنود الفاتورة → المجموع يتطابق مع البنود
   e. Vault transfer source-balance: تحويل أكبر من رصيد المصدر → 400
   f. Login + JWT: تسجيل دخول صحيح → JWT valid + claims صحيحة
   g. Patient access: طبيب غير معيّن → 403 على بيانات المريض
   h. Surgery status transitions: Completed → Scheduled → 400
4. أضف المشروع إلى الحل (dotnet sln add).
5. شغّل dotnet test على مشروع IntegrationTests للتأكد.
6. لا تنشئ PR — اترك التغييرات على برانش feat/integration-tests.
```

---

## ملاحظات للمُنفّذ

- كل مهمة مستقلة ويمكن تنفيذها بالتوازي.
- استنسخ المستودع أولاً: `git clone https://github.com/Aqlanf10/aqlan-dental.git`
- أنشئ البرانش المحدد في كل prompt.
- بعد الانتهاء، ادفع البرانش وأنشئ PR على main.
- CI يجب أن يكون أخضر قبل الدمج.
- لا تعدّل ملفات خارج نطاق المهمة المحددة.
- اقرأ تقرير التدقيق PROJECT_AUDIT_REPORT.md للسياق الكامل إن لزم.
