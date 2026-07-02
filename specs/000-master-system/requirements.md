# 000 Master System Requirements

هذه المتطلبات هي المصدر الأعلى للحقيقة للنظام. كل متطلب يحمل معرف `MS-REQ`.

## المتطلبات

- `MS-REQ-001` هوية العيادة: يجب أن يعرض النظام هوية مركز الدكتور عقلان الكامل من إعدادات النظام، خصوصا `clinic.name`, `clinic.lead_doctor`, `clinic.lead_doctor_title`, `clinic.lead_doctor_credentials`. الدليل: `CLAUDE.md`, `FinanceClinicIdentity.cs`. القبول: WHEN يتم إنشاء تقرير أو PDF THEN SHALL يقرأ الهوية من Settings أو fallback موثق.
- `MS-REQ-002` العربية وRTL: يجب أن تكون الواجهة عربية RTL. الدليل: `frontend/src/app/layout.tsx` يستخدم `dir="rtl"` و `lang="ar"`. القبول: WHEN تظهر شاشة مستخدم THEN SHALL تكون RTL وبنص عربي.
- `MS-REQ-003` المستخدمون والأدوار: يجب أن تعتمد الصلاحيات على أدوار `Admin`, `Orthodontist`, `GeneralDentist`, `OralSurgeon`, `Reception`, `Accountant`, `Assistant`, `BranchManager`, `Patient`. الدليل: `UserRole.cs`, `AuthorizationPolicyConfiguration.cs`.
- `MS-REQ-004` المرضى: يجب حماية بيانات المرضى عبر سياسات الخادم وفلتر وصول المرضى. الدليل: `PatientAccessFilter.cs`, `PatientAccessService`, `PatientsController.cs`.
- `MS-REQ-005` العمليات اليومية: يجب أن تكون شاشة `daily-operations` هي مساحة الاستقبال اليومية المركزية للحضور، الطابور، الغرف، الدفع السريع، المختبر، والتقرير اليومي. الدليل: `frontend/src/app/(dashboard)/daily-operations/`, `DailyOperationsController.cs`, `ClinicQueueController.cs`.
- `MS-REQ-006` المواعيد: يجب إدارة المواعيد من مسارات `/appointments` و `/schedule` و `/appointments/recall` مع فحص التعارضات. الدليل: `AppointmentsController.cs`, `AppointmentService.cs`, اختبارات Appointments.
- `MS-REQ-007` قائمة الانتظار: يجب أن تكون قائمة الانتظار مرتبطة بالعمليات اليومية ولا تنشئ شاشة استقبال موازية. الدليل: تعليقات `Sidebar.tsx` تشير إلى إزالة روابط موازية للطابور ورحلة المريض.
- `MS-REQ-008` عيادة الطبيب: يجب أن يستخدم الطبيب مساحة `/doctor-clinic` ومسارات المريض القائمة، دون إنشاء عيادة ثانية. الدليل: `frontend/src/app/(dashboard)/doctor-clinic/`.
- `MS-REQ-009` التقويم: يجب أن تكون حالات التقويم في `/ortho` ومملوكة لـ `OrthoCasesController`, `OrthoService`, `OrthoCaseQueryService`. الدليل: مسارات ortho ومكونات `frontend/src/components/ortho/`.
- `MS-REQ-010` السيفالومتري: يجب أن يكون `/ceph` مساحة السيفالومتري، وأن تبقى نتائج AI مسودات حتى مراجعة الطبيب. الدليل: `CephController.cs`, `CephService`, `CephAiDraftService`, `CephAiDraftResultDto`. Needs runtime verification.
- `MS-REQ-011` الجراحة: يجب أن تبقى الجراحة في `/surgery` و orthognathic في `/ortho-surgical` بسياسات `SurgeryAccess` و `OrthoSurgicalAccess`. الدليل: `SurgeryController.cs`, `OrthoSurgicalCasesController.cs`.
- `MS-REQ-012` المالية: يجب أن تكون المالية في `/finance-v3` و `api/finance-v3`. يجب منع الحسابات الخاطئة والتجاوزات غير المصرح بها. الدليل: `FinanceV3Controller*.cs`, `FinanceService.cs`, `TreasuryResolutionService.cs`, finance tests.
- `MS-REQ-013` المختبر: يجب أن تكون طلبات المختبر ومستحقاته وتقاريره في مسارات lab الحالية. الدليل: `LabOrdersController.cs`, `LabPayablesController.cs`, `LabReportsController.cs`, `frontend/src/app/(dashboard)/lab/`.
- `MS-REQ-014` المخزون: يجب أن يكون المخزون والموردون والمشتريات في مسارات inventory الحالية. الدليل: `InventoryController.cs`, `PurchaseOrdersController.cs`, `frontend/src/app/(dashboard)/inventory/`.
- `MS-REQ-015` التقارير والطباعة: يجب أن تستخدم التقارير وملفات PDF الخطوط العربية والهوية من الإعدادات. الدليل: `PdfService`, `CephReportPdfGenerator`, `LabOrderPdfGenerator`, `backend/Fonts/`.
- `MS-REQ-016` الإعدادات: يجب أن تكون قواعد العمل القابلة للضبط في Settings لا في كود ثابت. الدليل: `SettingsController.cs`, `FinanceSettingsKeys.cs`, صفحات settings.
- `MS-REQ-017` dashboard/navigation: يجب ألا يوجد dashboard/control panel ثان أو sidebar entry مكرر. الدليل: `Sidebar.tsx`, `routePermissions.ts`.
- `MS-REQ-018` سلامة الإنتاج: يجب احترام Railway/Vercel/CI وأسرار الإنتاج. الدليل: `Program.cs`, `.github/workflows/ci.yml`, `.github/workflows/encoding-guard.yml`.
- `MS-REQ-019` كل PR يجب أن يربط Spec ID ويحدث المواصفات عند تغيير السلوك.
- `MS-REQ-020` إذا لم يمكن التحقق الثابت من سلوك ما، يجب كتابة `Needs runtime verification`.
