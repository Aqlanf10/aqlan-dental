# MS-TASK-006 — تدقيق القيم التشغيلية المثبتة في الكود (2026-07-11)

تدقيق بقراءة كود لكل قيمة مالية/تشغيلية/هوية مثبتة كان يجب أن تأتي من جدول `Settings`.
القاعدة: «كل القيم المالية والقواعد التشغيلية قابلة للتهيئة — لا hardcoding».

## الخلاصة

البنية سليمة إجمالًا: 11 مفتاح `finance.*` بقيَم افتراضية (`FinanceSettingsKeys` + `FinanceSettingsReader`)،
وهوية العيادة عبر `FinanceClinicIdentity` (`clinic.*`) في كل مستندات المالية، ومفاتيح `website.*` للموقع العام
مع hook ‏`useClinicBranding` في الواجهة. النمط المعتمد: قراءة من الإعدادات مع fallback ثابت.

## ما أُصلح في الجولة الأولى (البنود العالية — SEQ-12)

| # | الموقع | كان | صار |
|---|--------|-----|-----|
| 1 | `CommissionService.AutoFillFromServiceAsync` (موقعان) | نسبة عمولة طبيب احتياطية `40m` مثبتة — **قيمة مالية** تُكتب على بنود الفواتير | يقرأ `finance.commission.default_doctor_percentage` |
| 2 | `AppointmentReminderJob` + `AppointmentsController` | اسم المركز مثبت في عنوان بريد التذكير | `FinanceClinicIdentity.ResolveAsync(db).Name` |
| 3 | `EmailService` (قالبا الاستعادة والتذكير — 9 مواقع) | الاسم والعنوان مثبتان في ترويسة/تذييل البريد | معاملا `clinicName`/`clinicLocation` اختياريان + fallback رسمي |
| 4 | `PatientPortalService.GetClinicInfo` | اسم/هاتف/عنوان مثبتة — الهاتف كان placeholder خاطئًا | يقرأ مفاتيح `website.*` |
| 5 | `PatientPortalService.SendOtpViaWhatsAppAsync` | اسم المركز مثبت في نص رمز التحقق | من `website.clinicName` |
| 6 | `PatientPortalDto.ClinicName` | افتراضي مثبت في الـDTO | يُملأ دائمًا من الخدمة |
| 7 | `prescriptions/[id]/page.tsx` → `PrescriptionPrint` | الوصفة المطبوعة تعتمد افتراضيات مثبتة | تمرر `useClinicBranding()` |
| 8 | `components/ceph/AnalysisReport.tsx` | ترويسة تقرير السيفالو مثبتة | `useClinicBranding().clinicName` |
| 9 | `BookingRequestsController` | اسم المركز مثبت في رسالة واتساب حالة الحجز | `FinanceClinicIdentity.ResolveAsync(db).Name` |

## ما أُصلح في الجولة الثانية

- ✅ قالب SMS الاستدعاء/الغياب في `appointments/recall/page.tsx` — الاسم من `useClinicBranding`.
- ✅ رسائل واتساب التواصل في `booking-requests/page.tsx` و`BookingRequestsView.tsx`.
- ✅ شاشة الانتظار العامة `clinic-display/page.tsx` (الترويسة + التذييل).
- ✅ صفحة الحجز العامة `home/book/page.tsx` — الهاتف والعنوان من الإعدادات.

## ما أُصلح في الجولة الثالثة — SEQ-13 / PR #648

- ✅ أصبح `ClinicTimeProvider` يقرأ المنطقة الزمنية مرة واحدة عند الإقلاع بترتيب:
  `Settings[clinic.timezone]` → `Clinic:Timezone` → `Settings:ClinicTimezone` → `Asia/Aden`.
- ✅ القيم المفقودة أو غير الصحيحة لا توقف الإنتاج؛ تسجل تحذيرًا وتعود إلى `Asia/Aden`.
- ✅ التخزين مؤقت وآمن على مستوى العملية؛ لا قراءة قاعدة بيانات في كل استدعاء `ClinicToday`/`ClinicNow`.
- ✅ `AppointmentReminderJob` أزيل منه التثبيت الخاص لـ`Asia/Aden` وأصبح يستخدم نفس المنطقة الزمنية المهيأة.
- ✅ أضيفت اختبارات للقيمة الافتراضية، منطقة بديلة، fallback غير صالح، نطاق UTC، والعزل في التشغيل المتوازي.

## المتبقي (موثَّق — بالأولوية)

### متوسط
- اسم المركز في `PublicNavbar` — مقسوم عمدًا لسطرين تصميميين؛ يحتاج قرار تصميم قبل ربطه بمفتاح واحد.
- هوية الطبيب في مصفوفة «الفريق» بموقع الويب `home/page.tsx`.

### منخفض (chrome واجهة — لا يستعجل)
- اسم المركز في `Sidebar`/`Topbar`/ترويسة المواعيد/صفحتي تسجيل الدخول/`<title>` وmeta SEO.
- `LabOrderPdfGenerator.cs` (اسم النظام في تذييل PDF).
- مدة الموعد الافتراضية `30` دقيقة متناثرة كـfallback إنشاء؛ مفتاح
  `appointment.default_duration_minutes` اختياري مستقبلًا.

## ما فُحص ووُجد سليمًا (لا انتهاك)

أسعار الصرف، سقف الخصم، عتبة اعتماد المصروفات، منع سالب الخزينة، افتراضيات الخدمة الجديدة،
كل PDFs المالية، مُهَل التذكير، نواة حساب العمولات، و`ContractService`. الثوابت السريرية
(معايير السيفالو ومعايير قرار الخلع) خارج النطاق عمدًا.
