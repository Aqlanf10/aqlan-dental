# Aqlan Dental Pro — Comprehensive Development Roadmap

## 1. Project Direction

Aqlan Dental Pro is the main active web system for Dr. Aqlan Al-Kamel Dental Center.

The project must be completed on top of the existing GitHub codebase. Do not rebuild it from scratch.

## 2. System Identity

- System name: Aqlan Dental Pro
- Clinic: مركز د. عقلان الكامل لتقويم وزراعة وتجميل الأسنان
- Language: Arabic RTL
- Currency: Yemeni Rial YER
- Main users: Admin, Orthodontist, General Dentist, Oral Surgeon, Reception, Accountant, Assistant, Branch Manager, Patient Portal User later

## 3. Current Technical Stack

### Frontend

- Next.js 14
- TypeScript
- Tailwind CSS
- Zustand
- TanStack React Query
- React Hook Form
- Zod
- Axios
- Lucide Icons
- Recharts

### Backend

- ASP.NET Core Web API .NET 8
- Entity Framework Core
- PostgreSQL
- JWT Authentication
- Refresh Tokens
- Role-based Authorization
- Swagger/OpenAPI
- Serilog
- FluentValidation

### Deployment

- Frontend: Vercel
- Backend: Railway
- Database: PostgreSQL on Railway

## 4. Core Development Rules

1. Continue development on the existing project.
2. Do not rebuild from scratch.
3. Do not change the current architecture unless approved.
4. No dummy data in production.
5. Every page and button must connect to a real API.
6. Every database schema change must use EF Core migrations.
7. All UI must support Arabic RTL.
8. All money values use Yemeni Rial.
9. Use soft delete for medical and financial data.
10. Log sensitive operations in Audit Log.

## 5. Current Project Status

The project already includes foundations for:

- Authentication and authorization
- Patients
- Appointments
- Messages
- Finance basics
- Orthodontic cases
- Cephalometric module
- General dentistry basics
- Surgery basics
- Prescriptions
- Referrals
- Reports basics
- Inventory basics
- Lab orders basics
- WhatsApp basics
- Patient portal basics
- Settings
- Notifications
- Audit logs

## 6. Sprint 1 — Stabilization and Deployment

### Goal

Make sure the current code in GitHub is the same code running in production.

### Tasks

1. Verify Vercel deploys frontend from `main`.
2. Verify Railway deploys backend from `main`.
3. Verify PostgreSQL connection string in Railway.
4. Verify `NEXT_PUBLIC_API_URL` in Vercel.
5. Verify `AllowedOrigins` contains Vercel URL.
6. Run backend build.
7. Run frontend build.
8. Apply EF Core migrations.
9. Check that seed data does not reset production data.
10. Verify all environment variables.

## 7. Sprint 2 — Patients Core

### Existing features to preserve

- Patient list
- Search
- Gender filter
- Doctor filter
- Active/archived/all filter
- Add patient
- Edit patient
- Archive patient
- Restore patient
- Row actions
- Right-click context menu
- CSV export
- Open patient file
- Create appointment from patient
- Add payment from patient
- Open orthodontic case
- Internal message
- WhatsApp link
- Print summary
- Duplicate check
- PhoneNormalizer

### Required tests

1. Add new patient.
2. Try duplicate number with different formats:
   - 770245745
   - 0770245745
   - +967770245745
   - 00967770245745
   - Arabic digits
3. Edit patient to another patient’s phone number.
4. Archive patient.
5. Restore patient.
6. Filter active patients.
7. Filter archived patients.
8. Use right-click menu.
9. Use row actions menu.
10. Search by name, patient number, and phone.

## 8. Sprint 3 — Unified Patient File

### Goal

Make the patient file the central place for all clinical, financial, and administrative data.

### Required tabs

1. Overview
2. Basic information
3. Medical history
4. Dental history
5. Appointments
6. Visits / treatment sessions
7. Finance
8. Contracts
9. Payments
10. Invoices
11. Receipts
12. Account statement
13. Messages
14. Orthodontics
15. General dentistry
16. Surgery
17. Photos
18. Radiographs
19. Prescriptions
20. Referrals
21. Documents and consents
22. Lab orders
23. Timeline

## 9. Sprint 4 — Appointments and Visits

### Existing features

- Today appointments
- Date range appointments
- Create appointment
- Edit appointment
- Update appointment status
- Conflict detection inside create/update

### Required additions

1. Cancel/delete appointment.
2. Add standalone conflict-check endpoint.
3. Daily calendar.
4. Weekly calendar.
5. Monthly calendar.
6. Patient appointment history.
7. Doctor appointment history.
8. Appointment status workflow:
   - Scheduled
   - Confirmed
   - Arrived
   - In Progress
   - Completed
   - Cancelled
   - No Show
9. Manual WhatsApp reminder.
10. Later: automatic WhatsApp reminders.

### Treatment Visits / Sessions

Add a clear visit/session module:

- PatientId
- AppointmentId optional
- DoctorId
- Specialty
- VisitDate
- ChiefComplaint
- Diagnosis
- TreatmentDone
- ToothNumber
- ServiceId
- Cost
- Notes
- NextVisitDate
- CreatedBy

## 10. Sprint 5 — Messaging

### Problem to solve

Patient-linked conversations must not require a patient portal account.

### Required design

Conversation types:

1. StaffToStaff
2. StaffToPatientInternal
3. PatientPortalConversation later

Add or support:

- PatientId nullable
- BranchId
- ConversationType
- CreatedBy

### Required behavior

1. From patient file, open “محادثة حول المريض”.
2. Conversation works even if the patient has no portal account.
3. Messages appear in patient file.
4. Messages appear in timeline.
5. Unread count works.
6. Notifications work.
7. Auto-refresh or realtime update.
8. Clear Arabic error messages.

## 11. Sprint 6 — Finance Basics

### Existing features

- Contracts
- Payments
- Finance summary
- Overdue

### Required additions

1. Edit contracts.
2. Cancel/archive contracts.
3. Payment receipt generation.
4. Receipt PDF with clinic identity.
5. Account statement.
6. Daily financial report.
7. Monthly financial report.
8. Doctor revenue report.
9. Specialty revenue report.
10. Overdue report.

## 12. Sprint 7 — Desktop Finance Features

Merge useful finance features from older desktop systems.

### Invoices

Invoice:

- InvoiceNumber
- PatientId
- DoctorId
- Specialty
- InvoiceDate
- TotalAmount
- Discount
- NetAmount
- Status
- Notes

InvoiceItem:

- InvoiceId
- ServiceId
- Description
- ToothNumber
- Quantity
- UnitPrice
- Total

### Receipts

- ReceiptNumber
- PaymentId
- PatientId
- Amount
- PrintedAt
- PrintedBy
- Reprint history
- PDF printing

### Account Statements

- Charges
- Payments
- Discounts
- Balance
- Date range
- PDF export

### Payment Vouchers / Expenses

- VoucherNumber
- PayeeType: Supplier / Lab / Employee / Other
- PayeeName
- Amount
- Date
- Reason
- ApprovedBy
- PaidBy
- Notes

## 13. Sprint 8 — Service Catalog

Add a service and price catalog:

- ServiceName
- Specialty
- DefaultPrice
- IsActive
- Description

Examples:

- Examination
- Filling
- Root canal treatment
- Extraction
- Scaling
- Fixed orthodontics
- Orthodontic installment
- Implant
- Surgery
- Zircon crown
- Emax
- Veneer

Service catalog must link to:

- Invoice items
- Treatment sessions
- Payments
- Doctor revenue
- Reports

## 14. Sprint 9 — Orthodontic Module

### Existing features

- Ortho cases
- Visits
- Stages
- Clinical exam
- Problem list
- Treatment plan
- Extraction decision

### Required additions

1. Diagnosis summary.
2. Treatment objectives.
3. Mechanics plan.
4. Anchorage plan.
5. Appliance plan.
6. Visit timeline.
7. Stage progress.
8. Debonding summary.
9. Retention records.
10. Retention visits.
11. Link photos.
12. Link radiographs.
13. Link ceph analyses.
14. Link contracts and payments.

## 15. Sprint 10 — Cephalometric Module

### Existing features

- Ceph analyses
- Landmarks
- Measurements
- Diagnosis
- Simulated AI landmarks

### Required improvements

1. Manual landmark canvas.
2. Calibration tool.
3. Save landmarks.
4. Accurate measurements.
5. Arabic diagnosis.
6. Measurement table.
7. Charts.
8. PDF report.
9. Superimposition later.
10. VTO later.
11. Real AI Auto Trace later.

Important: simulated AI is demo only and must be clearly labeled as simulation.

## 16. Sprint 11 — General Dentistry

Required features:

1. Interactive FDI dental chart.
2. Tooth conditions.
3. General treatments.
4. Treatment sessions.
5. Restorative treatment.
6. Endodontic treatment.
7. Prosthodontic treatment.
8. Periodontal assessment.
9. Prescriptions.
10. Treatment history.

Tooth conditions:

- Healthy
- Caries
- Filled
- RCT
- Crown
- Missing
- Extracted
- Impacted
- Implant
- Prosthesis

## 17. Sprint 12 — Surgery

Required features:

1. Surgery case.
2. Surgery type.
3. Teeth involved.
4. Pre-op report.
5. Consent.
6. Required tests.
7. Operative report.
8. Post-op instructions.
9. Follow-up schedule.
10. Hospital referral.
11. Prescription.
12. Patient timeline integration.

## 18. Sprint 13 — Lab Module

Required features:

1. Labs.
2. Lab orders.
3. Lab work types.
4. Shade management.
5. Sent date.
6. Expected date.
7. Received date.
8. Status.
9. Cost.
10. Doctor.
11. Patient.
12. Linked tooth/teeth.
13. Image upload.
14. Lab payment voucher.

Work type examples:

- Zircon
- Emax
- Veneer
- Crown
- Bridge
- Implant crown
- Night guard
- Retainer
- Space maintainer
- Orthodontic appliance

Shade examples:

- A1, A2, A3, A3.5
- B1, B2
- C1, C2

## 19. Sprint 14 — Inventory, Suppliers, Purchases

### Suppliers

- Name
- Phone
- Address
- Category
- Balance
- Notes

### Purchases

- SupplierId
- Date
- InvoiceNumber
- TotalAmount
- PaidAmount
- Balance

### Purchase Items

- PurchaseId
- InventoryItemId
- Quantity
- UnitCost
- Total

### Inventory

- ItemName
- Category
- Quantity
- MinQuantity
- Unit
- Cost
- BranchId

### Alerts

- Low stock
- Expired items
- Inventory report
- Item movement

## 20. Sprint 15 — HR and Employees

Required features:

1. Employees.
2. Attendance.
3. Salaries.
4. Advances.
5. Leaves.
6. Employee documents.
7. Employee reports.
8. Doctor statistics.

Employee:

- Name
- Phone
- JobTitle
- Salary
- BranchId
- IsActive

Attendance:

- EmployeeId
- Date
- CheckIn
- CheckOut
- Status

Salary:

- EmployeeId
- Month
- BaseSalary
- Deductions
- Advances
- NetSalary
- PaidAt

## 21. Sprint 16 — Reports and Printing

Required reports:

1. Patient report.
2. Treatment plan report.
3. Orthodontic report.
4. Cephalometric report.
5. Daily financial report.
6. Monthly financial report.
7. Overdue report.
8. Doctor report.
9. Specialty report.
10. Inventory report.
11. Purchase report.
12. Lab report.
13. Employee report.
14. Patient account statement.

PDF must include:

- Clinic logo
- Clinic name
- Address
- Contact numbers
- Doctor name
- Date
- Signature/stamp area
- Arabic RTL layout
- Yemeni Rial currency

## 22. Sprint 17 — Settings and Permissions

Resources needing permissions:

- Patients
- Appointments
- Visits
- Finance
- Invoices
- Receipts
- Ortho
- Ceph
- General dentistry
- Surgery
- Lab
- Inventory
- Reports
- Users
- Settings
- Audit

Actions:

- View
- Create
- Edit
- Delete
- Export
- Print
- Approve

Settings:

1. Clinic identity.
2. Branches.
3. Doctors.
4. Services and prices.
5. Invoice numbering.
6. Receipt numbering.
7. WhatsApp settings.
8. Backup settings.
9. Report settings.

## 23. Sprint 18 — Backup and Security

### Backup

1. Backup status page.
2. Manual export.
3. Database backup reminder.
4. Storage backup for photos/radiographs.
5. Restore policy documentation.

### Security

1. JWT + Refresh Token.
2. HttpOnly Cookie.
3. Strong password policy.
4. Role-based permissions.
5. Audit log.
6. Soft delete.
7. Branch isolation.
8. Rate limiting later.
9. 2FA later.

## 24. Sprint 19 — WhatsApp

### Stage 1

WhatsApp Web link.

### Stage 2

Message templates:

- Appointment reminder
- Appointment confirmation
- Due payment
- Post-surgery instructions
- Orthodontic instructions

### Stage 3

WhatsApp Business API later.

## 25. Sprint 20 — AI

AI comes after the core system is stable.

Potential AI features:

1. Case summary.
2. Treatment suggestion.
3. Problem list suggestion.
4. Extraction decision support.
5. Ceph auto tracing.
6. VTO.
7. Smart alerts.

AI rule: AI provides suggestions only. The final decision belongs to the doctor.

## 26. First Practical Step

Do not start new modules immediately.

First:

1. Verify deployment.
2. Verify migrations.
3. Test patients module.
4. Test messaging around patient file.
5. Test appointments.
6. Fix broken buttons and links.

Only after the foundation is stable, continue with finance, ortho, reports, lab, inventory, and HR.
