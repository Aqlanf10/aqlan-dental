# تدقيق وحدة المواعيد/الجدولة — 2026-07-26 (CORE-APPT)

تدقيق بقراءة الكود لوحدة المواعيد: الخلفية، الواجهة، التذكيرات وعدم الحضور
وإعادة الاستدعاء، والترابط مع الطابور والمرضى. طُلب بتوجيه مالك مباشر بعد
إغلاق وحدة المرضى (`docs/audits/PATIENTS_MODULE_AUDIT_2026-07-24.md`)، واختار
المالك «المواعيد/الجدولة» — يخاطب أولوية «تراكم المواعيد» في `CLAUDE.md`.

خط الأساس وقت التدقيق: `main` عند `6756481`.

الترقيم `CORE-APPT-xxx` يُستخدم في التعليقات داخل الكود وفي رسائل الـcommits.

---

## 1) نتائج الخلفية — الحجز وكشف التعارض

| # | الخطورة | الموقع | العطل |
|---|---------|--------|-------|
| `CORE-APPT-001` | **P1 أمني** | `AppointmentsController` | `[ServiceFilter(typeof(PatientAccessFilter))]` على مستوى الكلاس يحمي فعليًا endpoint واحدًا فقط (`GetByPatient`، لأنه الوحيد الذي يحمل `patientId` صريحًا في الـroute). `GetToday`، `GetByRange` (بلا `patientId` في الاستعلام)، `GetById`، `Create`، `Update`، `UpdateStatus`، `BatchUpdateStatus`، `Delete`، `SendReminder`، `StartVisit` — كلها بلا حماية فعلية. طبيب مقيّد الوصول (Orthodontist/GeneralDentist/OralSurgeon) يقرأ اسم المريض ورقمه وملاحظاته وهاتف مرافقه عبر أي من هذه المسارات، بل ويمكنه إنشاء/تعديل/إلغاء موعد أو إطلاق تذكير لمريض خارج نطاق وصوله. نفس فئة الثغرة التي أُغلقت في CORE-PAT-006، لكنها بقيت مفتوحة في كل مسارات الوحدة عدا واحد. |
| `CORE-APPT-002` | **P2 تكامل بيانات** | `AppointmentService.UpdateAsync` | `CreateAsync` يستخدم `TryCreateWithConflictGuardAsync` (معاملة + `pg_advisory_xact_lock` لكل طبيب) لإغلاق فجوة التحقق-ثم-الحفظ في الحجز. `UpdateAsync` (إعادة الجدولة) يتحقق من `HasConflictAsync` ثم يحفظ بلا معاملة ولا قفل — طلبا إعادة جدولة متزامنان لموعدين مختلفين على نفس الطبيب/الوقت يمكن أن ينجحا معًا فيُحجز الطبيب مرتين. |
| `CORE-APPT-003` | **P2 تكامل بيانات** | `AppointmentsController.Create`/`Update` | فحص تعارض الغرفة (`ClinicRoomId`) استعلام `AnyAsync` منفصل خارج أي قفل/معاملة، في كلا المسارين. طلبان متزامنان لحجز نفس الغرفة بطبيبين مختلفين (فلا يُسري عليهما القفل الخاص بالطبيب) يمكن أن ينجحا معًا — حجز مزدوج حقيقي للغرفة. |
| `CORE-APPT-004` | **P2 تكامل بيانات** | `BookingRequestService.ConvertToAppointmentAsync` | مسار إنشاء موعد ثانٍ مستقل بالكامل عن `AppointmentService.CreateAsync`/`TryCreateWithConflictGuardAsync` — بلا معاملة ولا قفل advisory (نفس الفجوة التي أُغلقت في مسار الإنشاء المباشر)، **وبلا أي فحص تعارض غرفة إطلاقًا** رغم تخزين `ClinicRoomId`. نفس فئة عطل CORE-PAT-010 (تحويل حجز يتجاوز مسار الإنشاء القانوني) لكن في وحدة المواعيد. |
| `CORE-APPT-005` | **P2 اتساق** | `AppointmentsController.BatchUpdateStatus` مقابل `AppointmentService.UpdateStatusAsync` | الفردي يسمح صراحة بإعادة الجدولة من `NoShow`/`Cancelled` إلى `Scheduled` (استثناء خارج جدول `AppointmentStatusTransitions`)، بينما الدفعي يتحقق من `IsValidTransition` مباشرة بلا هذا الاستثناء — فيُرفض نفس التحويل ضمن رسالة «تعارض في الحالة» الخاطئة عند تنفيذه بالجملة. |
| `CORE-APPT-006` | **P3 ازدواجية بمخاطرة كامنة** | `BookingRequestService.GetClinicNow()` | ينفّذ منطق منطقة زمنية خاصًا به (`"Asia/Aden"`/`"Arab Standard Time"` مُبَيَّتة) مستقلًا عن `ClinicTimeProvider.CurrentTimeZone` المُهيَّأ من الإعدادات. يتفقان حاليًا، لكن أي تغيير مستقبلي لـ`clinic.timezone` من الإعدادات يجعل هذا الملف يختلف عن بقية النظام في تحديد «اليوم». |

## 2) نتائج التذكيرات وعدم الحضور وإعادة الاستدعاء

| # | الخطورة | الموقع | العطل |
|---|---------|--------|-------|
| `CORE-APPT-007` | **P2 توقيت** | `WhatsAppService.SendPendingRemindersAsync` | يحسب «غدًا» بـ`DateOnly.FromDateTime(DateTime.Today.AddDays(1))` — ساعة الخادم UTC لا يوم العيادة. في نافذة 21:00–00:00 UTC (00:00–03:00 بتوقيت اليمن) يُرسل تذكيرات ليوم خاطئ. |
| `CORE-APPT-008` | **P2 توقيت + منطق** | `SmsService.SendPendingRemindersAsync` | نفس عطل UTC، إضافة إلى: `targetHour` محسوب ولا يُستخدم إطلاقًا (كود ميت)، والاستعلام يُصفّي بالتاريخ فقط بلا أي فلتر بالساعة — تفعيل هذا المسار لنافذة «قبل ساعتين» يرسل «موعدك بعد ساعتين» لكل المواعيد المجدولة في ذلك اليوم بصرف النظر عن وقتها الفعلي. |
| `CORE-APPT-009` | **P2 عزل قنوات** | `AppointmentReminderJob.SendRemindersAsync` | مسار البريد الإلكتروني وSMS داخل نفس كتلة `try` الخارجية دون عزل داخلي للبريد؛ استثناء في البريد (مثلًا فشل `GetPatientEmailAsync` أو `FinanceClinicIdentity.ResolveAsync`) يقفز إلى `catch` الخارجي **ويُسقط إرسال SMS لنفس الموعد في نفس الجولة** — يخالف تعليق الكود نفسه القائل بعزل القنوات. |
| `CORE-APPT-010` | **P2 ارتداد/تكرار** | `AppointmentReminderJob.SendRemindersAsync` | `SaveChangesAsync()` واحد بعد كل الحلقة المتداخلة (كل نوافذ الساعات × كل المواعيد المطابقة). انقطاع العملية (إعادة نشر Railway) بعد إرسال فعلي لبعض التذكيرات وقبل هذا الحفظ الوحيد يفقد كل علامات «أُرسل» لتلك الجولة، فتُعاد نفس الرسائل في الجولة التالية. |
| `CORE-APPT-011` | **P2 تكرار** | `SmsService` مقابل `AppointmentReminderJob` | علمان مستقلان وغير متزامنين لـ«تم الإرسال»: الوظيفة التلقائية تكتب `SmsReminderWindowsSent` (لكل نافذة ساعة)، بينما `SmsService.SendAppointmentReminderAsync` (المسار اليدوي/الاحتياطي) يكتب `ConfirmationSent` (منطقي واحد فقط) ولا يمس `SmsReminderWindowsSent`. تفعيل المسار اليدوي لنافذة 24 ساعة ثم وصول الوظيفة التلقائية لنفس النافذة لاحقًا يرسل نفس رسالة التذكير مرتين لنفس المريض. |
| `CORE-APPT-012` | **P2 بيانات/توصيل** | `WhatsAppService.NormalizePhone` و`SmsService.NormalizePhone` | كلاهما يعمل على `Patient.Phone`/`WhatsApp` الخام (غير المُطبَّع) لا على `NormalizedPhone`/`NormalizedWhatsApp`. (أ) أرقام هندية-عربية (٠-٩) يقبلها `char.IsDigit` لكن لا تُحوَّل لـASCII كما تفعل `PhoneNormalizer.Normalize` القانونية — فتفشل كل مطابقات `StartsWith` اللاحقة ويُرسَل رقم غير صالح بصمت. (ب) بادئة الاتصال الدولي `00` غير معالجة في أي منهما (بينما `PhoneNormalizer.Normalize` تُزيلها صراحة) — رقم مثل `00967770245745` يخرج من `SmsService` مشوَّهًا (`+9670967770245745`) ومن `WhatsAppService` بلا أي تعديل. |

## 3) نتائج الواجهة الأمامية

| # | الخطورة | الموقع | العطل |
|---|---------|--------|-------|
| `CORE-APPT-013` | **P2 عطل وظيفي 100%** | `components/appointments/UpcomingWidget.tsx` | `/api/appointments/upcoming` (الخلفية) لا يُرجع `patientId` إطلاقًا (إسقاط مجهول بحقول محدودة)، لكن `types/appointment.ts` (`UpcomingAppointment`) يعلنه كأنه موجود، والمكوّن يستخدمه في `window.location.href = /patients/${a.patientId}`. أي نقرة على أي صف في ودجت «المواعيد القادمة» (مستخدم في صفحتين) تنتقل إلى `/patients/undefined` وتفشل بصمت (لا GUID صالح). |
| `CORE-APPT-014` | **P2 عرض مضلِّل** | `DaySchedule.tsx`، `WeekCalendar.tsx`، `MonthCalendar.tsx` | جلب البيانات بـ`useEffect`/`api.get(...).then(setAppointments)` بلا `AbortController` ولا حارس تسلسل — بعكس فحص وجود الزيارة في نفس الملف الذي يستخدم علم `active` بنجاح. تنقّل سريع بين التواريخ يمكن أن يجعل استجابة قديمة تصل بعد الأحدث وتُظهر جدول يوم/أسبوع/شهر مختلف عن التاريخ المعروض في الترويسة. |
| `CORE-APPT-015` | **P2 أخطاء مضلِّلة** | `daily-operations/_modules/BookingRequestsView.tsx` | لا يستورد `extractErrorMessage` إطلاقًا؛ كل معالجات الأخطاء الثلاثة (`fetchItems`، `handleConfirm`، `handleReject`) تعرض toast عامًا ثابتًا. الخلفية (`BookingRequestService.ConvertToAppointmentAsync`) ترسل فعليًا رسائل عربية دقيقة («يوجد موعد آخر في نفس الوقت لهذا الطبيب»، «تم تحويل هذا الطلب بالفعل»…) لكنها تُستبدل بـ«فشل تأكيد الطلب أو إنشاء الموعد» العامة — موظف الاستقبال لا يعرف أن السبب تعارض حجز فعلي. |
| `CORE-APPT-016` | **P3** | `AppointmentForm.tsx` (محمّلات الخدمات/الغرف/الباقات)، `DaySchedule.tsx`/`[id]/page.tsx` (فحص بريد المريض) | `.catch(() => {})`/`.catch(() => setHasEmail(false))` تُخفي فشل الشبكة فتُخفي المنتقيات الاختيارية أو تُعطّل زر تذكير البريد بصمت دون تمييز عن «لا بريد فعلاً». |
| `CORE-APPT-017` | **P3 توثيق غير دقيق** | `hooks/useAppointments.ts` (`useCheckConflict`) | تعليق الكود يدّعي أنه «نشط ومتاح للتحقق أثناء تعبئة النموذج»، لكن الاستخدام الفعلي الوحيد استيراده هو اختبار الوحدة نفسه — لا تحذير حي عند اختيار طبيب/وقت متعارض قبل الإرسال؛ التعارض يُكتشف فقط بعد الإرسال عبر 409 (والذي تُعالج رسالته بشكل صحيح). فجوة بين ادّعاء التعليق والسلوك الفعلي، لا عطل في المسار المشحون.

## 4) ما فُحص ووُجد سليمًا (لا حاجة لإصلاح)

- توقيت اليوم في المسار الأساسي: `AppointmentsController` (`GetDailyStats`، `GetRecallCandidates`، `GetUpcoming`، `Delete`) و`AppointmentRepository.GetTodayAsync` و`AppointmentReminderJob` كلها تستخدم `ClinicTimeProvider` بشكل صحيح.
- `DaySchedule`/`WeekCalendar`/`MonthCalendar`/`AppointmentsTab`/`recall/page.tsx` تعرض حالة خطأ حقيقية مع إعادة محاولة، ولا تُظهر حالة فارغة كاذبة عند فشل التحميل — نمط CORE-PAT-020 مطبَّق بالكامل هنا.
- لا وجود لبَتر `toISOString().slice(0,10)` في كامل ملفات المواعيد/التقويم — كلها تستخدم `localDateString()` أو حسابات محلية مكافئة.
- استجابات `/api/appointments/*` مصفوفات عارية (`IEnumerable<AppointmentDto>`) والواجهة تتعامل معها بصحة تامة — لا عطل تغليف استجابة.
- `AppointmentForm.tsx` نموذج مثالي لعرض رسالة التعارض 409 الحقيقية مع رابط لعرض جدول اليوم.
- `DoctorRoomResolver` يُستخدم فقط في `ClinicQueueController` لعرض اسم الغرفة الافتراضي في شاشة النداء — مفهوم مختلف تمامًا عن `ClinicRoomId` الخاص بحجز الموعد؛ لا خلل هنا، فُحص ليتأكد لا ليُفترض.
- `LabOrders.DoctorId` غير المرتبط بـ`Users.Id`: لا استخدام له إطلاقًا داخل وحدة المواعيد.
- عدم الحضور التلقائي: لا وظيفة خلفية تُحوّل موعدًا فات وقته تلقائيًا لحالة NoShow (فجوة وظيفية موثّقة للعلم، وليست عطلاً في كود موجود).
- إعادة الاستدعاء (`PatientSegmentsController` — «مرضى لم يحضروا») قائمة قراءة فقط صحيحة الحساب، بلا أي ربط إرسال/تنبيه تلقائي — يطابق وصف تدقيق المرضى تمامًا، لا انحراف.

## 5) قرار مالك/تصميم مطلوب (ليس عطلًا)

- غياب قناة واتساب التلقائية من `AppointmentReminderJob` (البريد وSMS فقط تلقائيًا؛ واتساب يدوي بالكامل) — تعليق الكود يدّعي تتبعًا منفصلاً لثلاث قنوات لكن التنفيذ الفعلي قناتان فقط. قد يكون قرار تكلفة/اعتماد قالب متعمَّد، لا عطلاً، لكنه يستحق قرار مالك صريح نظرًا لأهمية واتساب كقناة أساسية في اليمن.
- تزامن `Appointment.Status` مع `ClinicQueueItem.Status` عند وضع علامة NoShow من شاشة المواعيد مباشرة (بخلاف شاشة الطابور التي تُزامن الاثنين) — امتداد لقرار مالك سابق موثّق في تدقيق المرضى (تعدد أماكن تخزين حالة المريض «الآن»)، لا يُفتح من جديد هنا.

---

**الخطوة التالية:** إصلاح `CORE-APPT-001` (فجوة الأمان P1) أولًا، ثم البنود P2 بالترتيب حسب الخطر والتكلفة، كل بند في PR منفصل صغير مع اختبارات ارتداد، مطابقة لنمط عمل CORE-PAT.
