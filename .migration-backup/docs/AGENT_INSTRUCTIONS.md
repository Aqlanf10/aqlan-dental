# Agent Instructions — Aqlan Dental Pro

هذه التعليمات إلزامية لأي Agent أو مبرمج يعمل على مشروع Aqlan Dental Pro.

## القرار الأساسي

هذا هو المشروع الحالي المعتمد في GitHub. المطلوب هو استكماله وتثبيته، وليس إعادة بنائه من الصفر.

## التقنية الحالية

- Frontend: Next.js 15 + TypeScript + Tailwind CSS
- Backend: ASP.NET Core Web API .NET 8
- Database: PostgreSQL
- Deployment: Vercel + Railway
- UI: Arabic RTL
- Currency: Yemeni Rial

## ممنوعات مهمة

1. ممنوع إعادة بناء المشروع من الصفر.
2. ممنوع تغيير البنية التقنية الحالية بدون موافقة صريحة.
3. ممنوع حذف أو تعطيل وحدات تعمل بالفعل.
4. ممنوع استخدام Dummy Data في الإنتاج.
5. ممنوع إضافة زر أو رابط لا يعمل.
6. ممنوع تعديل قاعدة البيانات يدويًا بدون EF Core Migration.
7. ممنوع Hard Delete للمرضى أو البيانات الطبية أو المالية.
8. ممنوع عرض AI simulation كأنه AI حقيقي.

## قواعد التطوير

1. اعمل دائمًا على branch جديد لكل مهمة.
2. لا تدفع مباشرة إلى main إلا بعد مراجعة.
3. استخدم EF Core Migrations لكل تغيير في schema.
4. حافظ على Arabic RTL في كل الواجهات.
5. كل صفحة يجب أن تتصل بـ API حقيقي.
6. كل عملية حساسة يجب أن تسجل في Audit Log.
7. كل خطأ يجب أن يظهر للمستخدم برسالة عربية واضحة.
8. حافظ على الصلاحيات حسب الدور.
9. احترم عزل الفروع Branch-based data isolation.
10. اكتب كود واضح وقابل للصيانة.

## أولوية العمل

### Sprint 1 — Stabilization

- Verify Vercel/Railway deployment from main.
- Verify EF Core migrations are applied to Railway PostgreSQL.
- Test patients module.
- Test PhoneNormalizer.
- Test duplicate prevention.
- Test archive/restore.
- Test active/archived/all filter.
- Test row actions and right-click context menu.

### Sprint 2 — Unified Patient File

- Complete patient overview.
- Add tabs for appointments, visits, finance, messages, ortho, general dentistry, surgery, photos, radiographs, documents, and timeline.

### Sprint 3 — Messaging

- Link conversations to PatientId.
- Allow internal staff-to-patient-file conversations even if the patient has no portal account.
- Add unread count, notifications, and auto-refresh.

### Sprint 5b — Message Attachments & Notifications

- Allow file attachments in conversations.
- Add in-app notification badge for unread messages.
- Mark-as-read on message open.

### Sprint 5c — Public Website & Appointment Booking Requests

- Develop professional public homepage for the clinic.
- Add public booking request form (not direct booking — requests must be reviewed by staff).
- Add "طلبات الحجز" page for reception/admin to manage booking requests.
- Request statuses: جديد → تم التواصل → تم تأكيد الموعد / مرفوض / تم تحويله إلى مريض.
- Security: public users must not access internal calendar, patient data, or create portal accounts automatically.
- See ROADMAP.md for full specification.

### Sprint 4 — Appointments and Visits

- Complete appointment workflow.
- Add cancel/delete appointment.
- Add standalone conflict-check endpoint.
- Add treatment visits/sessions module.

### Sprint 5 — Finance

- Complete contracts and payments.
- Add receipts.
- Add account statements.
- Add invoices and invoice items.
- Add payment vouchers and expenses.

### Sprint 6 — Orthodontics

- Complete clinical exam, problem list, treatment plan, extraction decision, visits, stages, debonding, and retention.

### Sprint 7 — Lab, Inventory, Suppliers

- Complete labs, lab orders, work types, shades, suppliers, purchases, and inventory movement.

### Sprint 8 — Reports and Printing

- Add PDF reports with clinic identity.
- Add receipt, invoice, account statement, patient report, and financial reports.

### Sprint 9 — Cephalometric

- Improve manual landmarking, calibration, measurements, diagnosis, and PDF report.
- Keep simulated AI clearly marked as demo.

### Sprint 10 — HR

- Add employees, attendance, salaries, leaves, employee documents, and doctor statistics.

## Testing checklist before every PR

- Backend builds successfully.
- Frontend builds successfully.
- No TypeScript errors.
- API endpoints tested.
- UI buttons tested.
- Permissions tested.
- Arabic RTL checked.
- Production environment variables not broken.
- Existing features not regressed.
