# MS-TASK-006 — تدقيق القيم التشغيلية المثبتة في الكود (2026-07-11)

تدقيق بقراءة كود لكل قيمة مالية/تشغيلية/هوية مثبتة كان يجب أن تأتي من جدول `Settings`.
القاعدة: «كل القيم المالية والقواعد التشغيلية قابلة للتهيئة — لا hardcoding».

## الخلاصة

البنية سليمة إجمالًا: 11 مفتاح `finance.*` بقيَم افتراضية (`FinanceSettingsKeys` + `FinanceSettingsReader`)،
وهوية العيادة عبر `FinanceClinicIdentity` (`clinic.*`) في كل مستندات المالية، ومفاتيح `website.*` للموقع العام
مع hook ‏`useClinicBranding` في الواجهة. النمط المعتمد: قراءة من الإعدادات مع fallback ثابت.

## ما أُصلح في هذه الجولة (البنود العالية — commit ضمن فرع SEQ-12)

| # | الموقع | كان | صار |
|---|--------|-----|-----|
| 1 | `CommissionService.AutoFillFromServiceAsync` (موقعان) | نسبة عمولة طبيب احتياطية `40m` مثبتة — **قيمة مالية** تُكتب على بنود الفواتير | يقرأ `finance.commission.default_doctor_percentage` (المفتاح كان موجودًا وغير مستخدم هنا) |
| 2 | `AppointmentReminderJob` + `AppointmentsController` | اسم المركز مثبت في عنوان بريد التذكير | `FinanceClinicIdentity.ResolveAsync(db).Name` |
| 3 | `EmailService` (قالبا الاستعادة والتذكير — 9 مواقع) | الاسم والعنوان مثبتان في ترويسة/تذييل البريد | معاملا `clinicName`/`clinicLocation` اختياريان + fallback لثوابت `FinanceClinicIdentity` الرسمية |
| 4 | `PatientPortalService.GetClinicInfo` | اسم/هاتف/عنوان مثبتة — **الهاتف كان placeholder خاطئًا** `+967123456789` والعنوان «اليمن» فقط | يقرأ مفاتيح `website.*` (نفس مصدر الموقع العام) |
| 5 | `PatientPortalService.SendOtpViaWhatsAppAsync` | اسم المركز مثبت في نص SMS رمز التحقق | من `website.clinicName` |
| 6 | `PatientPortalDto.ClinicName` | افتراضي مثبت في الـDTO | يُملأ دائمًا من الخدمة |
| 7 | `prescriptions/[id]/page.tsx` → `PrescriptionPrint` | **الوصفة المطبوعة** كانت تعتمد افتراضيات المكوّن المثبتة (الصفحة لا تمرر شيئًا) | تمرر `useClinicBranding()` (الاسم/العنوان/الهاتف) |
| 8 | `components/ceph/AnalysisReport.tsx` | ترويسة **تقرير السيفالو المطبوع** مثبتة | `useClinicBranding().clinicName` |
| 9 | `BookingRequestsController` | اسم المركز مثبت في رسالة واتساب حالة الحجز | `FinanceClinicIdentity.ResolveAsync(db).Name` |

## المتبقي (موثَّق، لم يُصلح بعد — بالأولوية)

### متوسط
- **`ClinicTimeProvider` المنطقة الزمنية `"Asia/Aden"` مثبتة** (+`AppointmentReminderJob`، `BookingRequestService`) —
  تعليق الكلاس نفسه يدّعي القراءة من `Settings:ClinicTimezone` لكن لا كود يقرأها. تحتاج مفتاح `clinic.timezone`
  فعليًا. حساسة (تحدد حدود «اليوم» للمالية والتقارير) — تُنفذ بحذر وباختبارات.
- قالب SMS الاستدعاء/الغياب الافتراضي في `appointments/recall/page.tsx:30` — يستحق مفتاح قالب رسالة.
- رسائل واتساب التواصل في `booking-requests/page.tsx` (موقعان) و`BookingRequestsView.tsx` — تحتاج `useClinicBranding`.
- شاشة الانتظار العامة `clinic-display/page.tsx` (موقعان) واسم المركز في `PublicNavbar` وصفحة الحجز العامة
  `home/book/page.tsx` (هاتف/عنوان في 5 مواقع) — كلها عامة للجمهور وتحتاج `useClinicBranding`/`website.*`.
- هوية الطبيب في مصفوفة «الفريق» بموقع الويب `home/page.tsx:42`.

### منخفض (chrome واجهة — لا يستعجل)
- اسم المركز في `Sidebar`/`Topbar`/ترويسة المواعيد/صفحتي تسجيل الدخول (بما فيها قائمة هواتف كاملة في
  `(auth)/login/page.tsx`)/`<title>` وmeta الـSEO — و`LabOrderPdfGenerator.cs:204` (اسم النظام في تذييل PDF).
- ملاحظة: مدة الموعد الافتراضية `30` دقيقة متناثرة كـfallback إنشاء (المدة الفعلية عمود قابل للتهيئة في
  `DoctorSchedule.SlotDurationMinutes`) — مفتاح `appointment.default_duration_minutes` اختياري مستقبلًا.

## ما فُحص ووُجد سليمًا (لا انتهاك)

أسعار الصرف (تُقرأ من `finance.exchange_rate.*` وترمي خطأ إن غابت — لا سعر مثبت)، سقف الخصم، عتبة اعتماد
المصروفات، منع سالب الخزينة، افتراضيات الخدمة الجديدة، كل PDFs المالية (عبر `FinanceClinicIdentity`)،
مُهَل التذكير (`appointment.reminder_hours`)، نواة حساب العمولات، `ContractService`. الثوابت السريرية
(معايير السيفالو — لها جدولها؛ معايير قرار الخلع) خارج النطاق عمدًا.
