خطة Aqlan Dental Pro — وثيقة Claude Code الكاملة



🏥 هوية المشروع

اسم النظام: Aqlan Dental Pro

المركز: مركز د. عقلان الكامل لطب وتقويم الأسنان

الموقع: اليمن — تعز — شارع التحرير الأعلى

الهواتف: 04-253028 · 770-245745 · 711-752823

الفريق الطبي:



د. عقلان الكامل — أخصائي تقويم الأسنان (Admin)

د. عائشة غازي — طب أسنان عام

د. إيمان الكامل — طب أسنان عام

د. هشام القدسي — طب أسنان عام

د. خلدون البريهي — أخصائي جراحة وجه وفكين





1. نظرة عامة على النظام

اسم المشروع:   Aqlan Dental Pro v1.0

نوع النظام:    نظام ويب شامل لإدارة مركز طب وتقويم الأسنان

الجمهور:       أطباء أسنان + موظفو استقبال + محاسبون

اللغة:         عربية RTL بالكامل

العملة:        ريال يمني (YER)

المتصفحات:    Chrome, Firefox, Safari, Edge (آخر إصدارين)

الأجهزة:      Desktop أولوية + Tablet

الفلسفة الأساسية

النظام ليس مجرد برنامج عيادة عام — بل منصة متخصصة تجمع:



Clinic Management → مواعيد، مرضى، مالية

Orthodontic Records → ملف تقويمي شامل كـ Dolphin Imaging

Cephalometric Analysis → تحليل سيفالومتري كـ WebCeph

Treatment Planning → خطة علاج + VTO + قرار خلع

Multi-specialty → طب عام + تقويم + جراحة وجه وفكين

AI Advisory → ذكاء اصطناعي للمساعدة (مقترحات فقط، لا قرارات)





2. التقنيات المطلوبة

Frontend

Framework:     Next.js 14 (App Router)

Language:      TypeScript

Styling:       Tailwind CSS + CSS Variables

UI Library:    Shadcn/ui (مخصصة للـ RTL)

State:         Zustand + React Query

Forms:         React Hook Form + Zod

Canvas:        Fabric.js (للرسم السيفالومتري)

Charts:        Recharts + D3.js

PDF:           React-PDF + @react-pdf/renderer

Upload:        UploadThing أو AWS S3 مباشر

RTL:           direction: rtl + Tajawal Google Font

Icons:         Lucide React

Backend

Framework:     .NET 8 Web API (Clean Architecture)

Language:      C# 12

ORM:           Entity Framework Core 8

Auth:          JWT + Refresh Tokens + 2FA (TOTP)

Validation:    FluentValidation

Mapping:       AutoMapper

Logging:       Serilog

Documentation: Swagger/OpenAPI

Database

Primary:       PostgreSQL 16

Migrations:    EF Core Migrations

Caching:       Redis (sessions + frequent queries)

Search:        PostgreSQL Full-Text Search (بحث المرضى)

Audit:         Temporal Tables أو Audit Log جدول مستقل

Storage & Services

File Storage:  Cloudflare R2 أو AWS S3

CDN:           Cloudflare

Email:         SendGrid أو Resend

SMS/WhatsApp:  WhatsApp Business API (Twilio)

AI:            Claude API (Anthropic) — Advisory فقط

Background:    Hangfire أو .NET Worker Service

Deployment

Frontend:      Vercel أو Cloudflare Pages

Backend:       Docker + VPS (DigitalOcean أو Hetzner)

Database:      Managed PostgreSQL (Supabase أو Neon)

Monitoring:    Sentry + Uptime Robot



3. هيكل قاعدة البيانات الكاملة

3.1 جداول المرضى

sql-- المريض الأساسي

CREATE TABLE patients (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  patient_number    VARCHAR(20) UNIQUE NOT NULL, -- GM-2024-039

  first_name        VARCHAR(100) NOT NULL,

  middle_name       VARCHAR(100),

  last_name         VARCHAR(100) NOT NULL,

  date_of_birth     DATE,

  gender            VARCHAR(10) CHECK (gender IN ('male','female')),

  phone             VARCHAR(20),

  whatsapp          VARCHAR(20),

  address           TEXT,

  occupation        VARCHAR(100),

  referral_source   VARCHAR(200),

  primary_doctor_id UUID REFERENCES doctors(id),

  branch_id         UUID REFERENCES branches(id),

  created_at        TIMESTAMPTZ DEFAULT NOW(),

  updated_at        TIMESTAMPTZ DEFAULT NOW(),

  is_active         BOOLEAN DEFAULT true

);



-- التاريخ الطبي

CREATE TABLE medical_histories (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  patient_id        UUID REFERENCES patients(id) ON DELETE CASCADE,

  chronic_diseases  TEXT,

  current_medications TEXT,

  drug_allergies    TEXT,

  bleeding_disorders BOOLEAN DEFAULT false,

  is_pregnant       VARCHAR(20), -- 'yes','no','na'

  tmj_problems      BOOLEAN DEFAULT false,

  previous_surgeries TEXT,

  notes             TEXT,

  updated_at        TIMESTAMPTZ DEFAULT NOW()

);



-- البيانات السنية

CREATE TABLE dental_histories (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  patient_id        UUID REFERENCES patients(id) ON DELETE CASCADE,

  chief_complaint   TEXT,

  previous_treatments TEXT,

  mouth_breathing   BOOLEAN DEFAULT false,

  bruxism           BOOLEAN DEFAULT false,

  thumb_sucking     BOOLEAN DEFAULT false,

  tongue_thrusting  BOOLEAN DEFAULT false,

  notes             TEXT,

  updated_at        TIMESTAMPTZ DEFAULT NOW()

);

3.2 جداول التقويم

sql-- الحالة التقويمية

CREATE TABLE ortho_cases (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  case_number       VARCHAR(20) UNIQUE NOT NULL, -- OR-2024-108

  patient_id        UUID REFERENCES patients(id),

  doctor_id         UUID REFERENCES doctors(id),

  branch_id         UUID REFERENCES branches(id),

  appliance_type    VARCHAR(100), -- 'MBT 0.022', 'Invisalign', etc.

  start_date        DATE,

  expected_duration INTEGER, -- months

  current_stage     VARCHAR(100),

  stage_percentage  INTEGER DEFAULT 0,

  status            VARCHAR(50) DEFAULT 'active',

  extraction_decision VARCHAR(50), -- 'no_extraction','4_premolars','2_upper', etc.

  retention_plan    TEXT,

  total_fee         DECIMAL(12,2),

  created_at        TIMESTAMPTZ DEFAULT NOW()

);



-- الفحص السريري التقويمي

CREATE TABLE ortho_clinical_exams (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  ortho_case_id     UUID REFERENCES ortho_cases(id),

  exam_date         DATE DEFAULT CURRENT_DATE,

  -- Extraoral

  facial_symmetry   VARCHAR(50),

  profile           VARCHAR(50), -- 'Class I', 'Convex', 'Concave'

  lips_competence   BOOLEAN,

  smile_line        VARCHAR(50),

  vertical_proportion VARCHAR(50),

  -- Intraoral

  molar_relation    VARCHAR(50), -- 'Class I', 'Class II', 'Class III'

  canine_relation   VARCHAR(50),

  overjet           DECIMAL(5,2), -- mm

  overbite          DECIMAL(5,2), -- mm

  crossbite         BOOLEAN,

  open_bite         BOOLEAN,

  upper_crowding    VARCHAR(50), -- 'none','mild','moderate','severe'

  lower_crowding    VARCHAR(50),

  upper_spacing     DECIMAL(5,2),

  midline_upper     VARCHAR(50),

  midline_lower     VARCHAR(50),

  -- Functional

  co_cr_discrepancy BOOLEAN,

  tmj_findings      TEXT,

  habits            TEXT,

  notes             TEXT,

  doctor_id         UUID REFERENCES doctors(id)

);



-- قائمة المشاكل

CREATE TABLE problem_lists (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  ortho_case_id     UUID REFERENCES ortho_cases(id),

  category          VARCHAR(50), -- 'skeletal','dental','soft_tissue','functional','space'

  description       TEXT NOT NULL,

  severity          VARCHAR(20), -- 'mild','moderate','severe'

  sort_order        INTEGER DEFAULT 0,

  created_at        TIMESTAMPTZ DEFAULT NOW()

);



-- خطة العلاج التقويمي

CREATE TABLE treatment_plans (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  ortho_case_id     UUID REFERENCES ortho_cases(id),

  plan_version      INTEGER DEFAULT 1, -- A, B alternatives

  is_approved       BOOLEAN DEFAULT false,

  approved_by       UUID REFERENCES doctors(id),

  approved_at       TIMESTAMPTZ,

  appliance_type    VARCHAR(100),

  bracket_system    VARCHAR(100),

  initial_wire      VARCHAR(100),

  extraction_plan   VARCHAR(200),

  anchorage_plan    VARCHAR(200),

  use_tads          BOOLEAN DEFAULT false,

  use_elastics      BOOLEAN DEFAULT false,

  expected_duration INTEGER,

  retention_plan    TEXT,

  treatment_goals   TEXT,

  risks_limitations TEXT,

  created_at        TIMESTAMPTZ DEFAULT NOW()

);



-- زيارات التقويم

CREATE TABLE ortho_visits (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  ortho_case_id     UUID REFERENCES ortho_cases(id),

  visit_number      INTEGER NOT NULL,

  visit_date        DATE NOT NULL,

  visit_type        VARCHAR(100), -- 'activation','review','bonding','debonding'

  current_stage     VARCHAR(100),

  wire_upper        VARCHAR(100),

  wire_lower        VARCHAR(100),

  elastics_type     VARCHAR(100),

  current_overjet   DECIMAL(5,2),

  current_overbite  DECIMAL(5,2),

  midline_notes     TEXT,

  clinical_notes    TEXT,

  patient_instructions TEXT,

  next_appointment_date DATE,

  next_appointment_type VARCHAR(100),

  doctor_id         UUID REFERENCES doctors(id),

  created_at        TIMESTAMPTZ DEFAULT NOW()

);



-- مراحل العلاج

CREATE TABLE treatment_stages (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  ortho_case_id     UUID REFERENCES ortho_cases(id),

  stage_name        VARCHAR(100) NOT NULL, -- 'alignment','leveling','space_closure', etc.

  stage_order       INTEGER,

  started_at        DATE,

  completed_at      DATE,

  target_duration_months INTEGER,

  notes             TEXT,

  status            VARCHAR(50) DEFAULT 'pending' -- 'pending','active','completed'

);



-- Retention

CREATE TABLE retention_records (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  ortho_case_id     UUID REFERENCES ortho_cases(id),

  debond_date       DATE,

  upper_retainer    VARCHAR(100),

  lower_retainer    VARCHAR(100),

  instructions      TEXT,

  status            VARCHAR(50) DEFAULT 'active'

);



CREATE TABLE retention_visits (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  retention_id      UUID REFERENCES retention_records(id),

  visit_date        DATE,

  period            VARCHAR(50), -- '1 month', '3 months', etc.

  tooth_stability   VARCHAR(50),

  retainer_status   VARCHAR(50),

  notes             TEXT

);

3.3 التحليل السيفالومتري

sql-- التحليل السيفالومتري

CREATE TABLE ceph_analyses (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  ortho_case_id     UUID REFERENCES ortho_cases(id),

  analysis_date     DATE DEFAULT CURRENT_DATE,

  analysis_type     VARCHAR(50) NOT NULL, -- 'steiner','downs','mcnamara','ricketts','tweed','wits','jarabak'

  xray_file_url     TEXT,

  is_auto_traced    BOOLEAN DEFAULT false,

  ai_assisted       BOOLEAN DEFAULT false,

  doctor_id         UUID REFERENCES doctors(id),

  notes             TEXT,

  created_at        TIMESTAMPTZ DEFAULT NOW()

);



-- نقاط التتبع

CREATE TABLE ceph_landmarks (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  analysis_id       UUID REFERENCES ceph_analyses(id),

  landmark_key      VARCHAR(10) NOT NULL, -- 'S','N','A','B','Or', etc.

  landmark_name     VARCHAR(100),

  x_coord           DECIMAL(8,3),

  y_coord           DECIMAL(8,3),

  is_ai_placed      BOOLEAN DEFAULT false,

  confidence        DECIMAL(5,4) -- AI confidence 0-1

);



-- القياسات

CREATE TABLE ceph_measurements (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  analysis_id       UUID REFERENCES ceph_analyses(id),

  measurement_name  VARCHAR(100) NOT NULL, -- 'SNA','SNB','ANB', etc.

  measurement_value DECIMAL(8,3),

  normal_value      DECIMAL(8,3),

  std_deviation     DECIMAL(8,3),

  unit              VARCHAR(10), -- '°' or 'mm'

  deviation         DECIMAL(8,3), -- val - norm

  classification    VARCHAR(20) -- 'normal','mild','severe'

);



-- تشخيص السيفالومتري

CREATE TABLE ceph_diagnoses (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  analysis_id       UUID REFERENCES ceph_analyses(id),

  skeletal_class    VARCHAR(50),

  vertical_pattern  VARCHAR(50),

  incisor_inclination VARCHAR(50),

  soft_tissue_summary TEXT,

  ai_recommendation TEXT,

  doctor_approved   BOOLEAN DEFAULT false,

  final_diagnosis   TEXT

);



-- تحليل الموديل (Bolton، Space)

CREATE TABLE model_analyses (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  ortho_case_id     UUID REFERENCES ortho_cases(id),

  analysis_date     DATE,

  bolton_overall    DECIMAL(6,3),

  bolton_anterior   DECIMAL(6,3),

  upper_sum_12      DECIMAL(6,3),

  lower_sum_12      DECIMAL(6,3),

  upper_arch_length DECIMAL(6,3),

  lower_arch_length DECIMAL(6,3),

  upper_ald         DECIMAL(6,3), -- arch length discrepancy

  lower_ald         DECIMAL(6,3),

  pont_index        DECIMAL(6,3),

  notes             TEXT

);



-- قرار الخلع

CREATE TABLE extraction_decisions (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  ortho_case_id     UUID REFERENCES ortho_cases(id),

  decision          VARCHAR(50), -- 'no_extraction','extract_4','extract_2_upper','borderline'

  ai_recommendation VARCHAR(50),

  pro_extraction    JSONB, -- factors favoring extraction

  con_extraction    JSONB, -- factors against

  doctor_notes      TEXT,

  decided_by        UUID REFERENCES doctors(id),

  decided_at        TIMESTAMPTZ

);

3.4 طب الأسنان العام

sql-- مخطط الأسنان

CREATE TABLE dental_charts (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  patient_id        UUID REFERENCES patients(id),

  chart_date        DATE DEFAULT CURRENT_DATE,

  doctor_id         UUID REFERENCES doctors(id),

  updated_at        TIMESTAMPTZ DEFAULT NOW()

);



CREATE TABLE tooth_conditions (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  chart_id          UUID REFERENCES dental_charts(id),

  tooth_number      VARCHAR(5) NOT NULL, -- '11','21','36', etc.

  condition         VARCHAR(50), -- 'healthy','filled','rct','crown','decay','extracted','missing'

  surfaces_affected VARCHAR(20), -- 'MODBF' notation

  notes             TEXT,

  treatment_done    VARCHAR(200),

  updated_at        TIMESTAMPTZ DEFAULT NOW()

);



-- سجل علاجات طب الأسنان العام

CREATE TABLE general_treatments (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  patient_id        UUID REFERENCES patients(id),

  visit_id          UUID REFERENCES visits(id),

  treatment_type    VARCHAR(100) NOT NULL,

  tooth_number      VARCHAR(20),

  material_used     VARCHAR(200),

  anesthesia_type   VARCHAR(100),

  cost              DECIMAL(12,2),

  doctor_id         UUID REFERENCES doctors(id),

  notes             TEXT,

  created_at        TIMESTAMPTZ DEFAULT NOW()

);



-- تقييم اللثة

CREATE TABLE perio_assessments (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  patient_id        UUID REFERENCES patients(id),

  assessment_date   DATE,

  avg_pocket_depth  DECIMAL(4,2),

  bleeding_points   INTEGER,

  recession_level   VARCHAR(50),

  perio_stage       VARCHAR(20), -- 'Stage I', 'II', 'III', 'IV'

  recommendation    VARCHAR(200),

  doctor_id         UUID REFERENCES doctors(id)

);

3.5 جراحة الوجه والفكين

sql-- الحالات الجراحية

CREATE TABLE surgery_cases (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  case_number       VARCHAR(20) UNIQUE NOT NULL, -- SU-2024-022

  patient_id        UUID REFERENCES patients(id),

  doctor_id         UUID REFERENCES doctors(id),

  surgery_type      VARCHAR(200) NOT NULL,

  teeth_involved    VARCHAR(100),

  status            VARCHAR(50) DEFAULT 'scheduled',

  created_at        TIMESTAMPTZ DEFAULT NOW()

);



-- تقرير ما قبل الجراحة

CREATE TABLE preop_reports (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  surgery_case_id   UUID REFERENCES surgery_cases(id),

  surgery_date      DATE,

  surgery_location  VARCHAR(200),

  anesthesia_type   VARCHAR(100),

  checklist         JSONB, -- array of {item, completed}

  required_tests    JSONB, -- array of {test, status}

  consent_signed    BOOLEAN DEFAULT false,

  doctor_id         UUID REFERENCES doctors(id)

);



-- تقرير الجراحة

CREATE TABLE operative_reports (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  surgery_case_id   UUID REFERENCES surgery_cases(id),

  surgery_datetime  TIMESTAMPTZ,

  duration_minutes  INTEGER,

  anesthesia_used   VARCHAR(200),

  technique         VARCHAR(200),

  detailed_description TEXT,

  outcome           TEXT,

  complications     TEXT,

  sutures_count     INTEGER,

  specimen_sent     BOOLEAN DEFAULT false,

  doctor_id         UUID REFERENCES doctors(id),

  approved_at       TIMESTAMPTZ

);



-- تعليمات ومتابعة ما بعد الجراحة

CREATE TABLE postop_records (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  surgery_case_id   UUID REFERENCES surgery_cases(id),

  instructions      TEXT,

  prescription      JSONB, -- array of {drug, dose, duration}

  followup_schedule JSONB, -- array of {period, date, status}

  created_at        TIMESTAMPTZ DEFAULT NOW()

);



-- إحالات المستشفى

CREATE TABLE hospital_referrals (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  surgery_case_id   UUID REFERENCES surgery_cases(id),

  hospital_name     VARCHAR(200),

  reason            TEXT,

  referral_date     DATE,

  status            VARCHAR(50) DEFAULT 'pending',

  notes             TEXT

);

3.6 المواعيد والزيارات

sql-- المواعيد

CREATE TABLE appointments (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  patient_id        UUID REFERENCES patients(id),

  doctor_id         UUID REFERENCES doctors(id),

  branch_id         UUID REFERENCES branches(id),

  appointment_date  DATE NOT NULL,

  start_time        TIME NOT NULL,

  end_time          TIME NOT NULL,

  duration_minutes  INTEGER DEFAULT 30,

  appointment_type  VARCHAR(100) NOT NULL,

  specialty         VARCHAR(50), -- 'general','ortho','surgery'

  status            VARCHAR(50) DEFAULT 'scheduled',

  -- 'scheduled','confirmed','arrived','in_progress','completed','cancelled','no_show'

  confirmation_sent BOOLEAN DEFAULT false,

  notes             TEXT,

  created_by        UUID REFERENCES users(id),

  created_at        TIMESTAMPTZ DEFAULT NOW()

);



-- الزيارات العامة

CREATE TABLE visits (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  patient_id        UUID REFERENCES patients(id),

  appointment_id    UUID REFERENCES appointments(id),

  visit_date        DATE NOT NULL,

  visit_type        VARCHAR(100),

  specialty         VARCHAR(50),

  doctor_id         UUID REFERENCES doctors(id),

  chief_complaint   TEXT,

  clinical_notes    TEXT,

  treatment_done    TEXT,

  instructions      TEXT,

  cost              DECIMAL(12,2),

  next_visit_date   DATE,

  created_at        TIMESTAMPTZ DEFAULT NOW()

);

3.7 المالية

sql-- العقود

CREATE TABLE contracts (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  patient_id        UUID REFERENCES patients(id),

  specialty         VARCHAR(50),

  related_case_id   UUID, -- ortho_case_id or surgery_case_id

  total_amount      DECIMAL(12,2) NOT NULL,

  down_payment      DECIMAL(12,2) DEFAULT 0,

  installments_count INTEGER DEFAULT 1,

  installment_amount DECIMAL(12,2),

  start_date        DATE,

  discount_amount   DECIMAL(12,2) DEFAULT 0,

  discount_reason   TEXT,

  status            VARCHAR(50) DEFAULT 'active',

  notes             TEXT,

  created_by        UUID REFERENCES users(id),

  created_at        TIMESTAMPTZ DEFAULT NOW()

);



-- الدفعات

CREATE TABLE payments (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  contract_id       UUID REFERENCES contracts(id),

  patient_id        UUID REFERENCES patients(id),

  amount            DECIMAL(12,2) NOT NULL,

  payment_date      DATE DEFAULT CURRENT_DATE,

  payment_method    VARCHAR(50), -- 'cash','bank_transfer','card'

  specialty         VARCHAR(50),

  service_description VARCHAR(200),

  doctor_id         UUID REFERENCES doctors(id),

  branch_id         UUID REFERENCES branches(id),

  received_by       UUID REFERENCES users(id),

  receipt_number    VARCHAR(50) UNIQUE,

  notes             TEXT,

  created_at        TIMESTAMPTZ DEFAULT NOW()

);



-- سندات القبض (للطباعة)

CREATE TABLE receipts (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  payment_id        UUID REFERENCES payments(id),

  receipt_number    VARCHAR(50) UNIQUE NOT NULL,

  printed_at        TIMESTAMPTZ,

  printed_by        UUID REFERENCES users(id)

);

3.8 الصور والأشعة

sql-- الصور السريرية

CREATE TABLE clinical_photos (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  patient_id        UUID REFERENCES patients(id),

  ortho_case_id     UUID REFERENCES ortho_cases(id),

  photo_date        DATE DEFAULT CURRENT_DATE,

  category          VARCHAR(50), -- 'extraoral','intraoral'

  photo_type        VARCHAR(100), -- 'frontal_relaxed','frontal_smile', etc.

  file_url          TEXT NOT NULL,

  file_size         INTEGER,

  thumbnail_url     TEXT,

  stage             VARCHAR(100), -- 'initial','progress','final'

  notes             TEXT,

  uploaded_by       UUID REFERENCES users(id),

  created_at        TIMESTAMPTZ DEFAULT NOW()

);



-- الأشعة

CREATE TABLE radiographs (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  patient_id        UUID REFERENCES patients(id),

  xray_date         DATE DEFAULT CURRENT_DATE,

  xray_type         VARCHAR(100), -- 'OPG','lateral_ceph','PA_ceph','bitewing','periapical','CBCT'

  file_url          TEXT NOT NULL,

  tooth_related     VARCHAR(50),

  notes             TEXT,

  doctor_id         UUID REFERENCES doctors(id),

  uploaded_by       UUID REFERENCES users(id),

  created_at        TIMESTAMPTZ DEFAULT NOW()

);

3.9 الإحالات والمستندات

sql-- الإحالات الداخلية

CREATE TABLE internal_referrals (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  patient_id        UUID REFERENCES patients(id),

  from_doctor_id    UUID REFERENCES doctors(id),

  to_doctor_id      UUID REFERENCES doctors(id),

  reason            VARCHAR(200),

  priority          VARCHAR(20) DEFAULT 'normal', -- 'normal','urgent','emergency'

  notes             TEXT,

  status            VARCHAR(50) DEFAULT 'pending', -- 'pending','accepted','completed'

  accepted_at       TIMESTAMPTZ,

  created_at        TIMESTAMPTZ DEFAULT NOW()

);



-- المستندات والموافقات

CREATE TABLE documents (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  patient_id        UUID REFERENCES patients(id),

  document_type     VARCHAR(100), -- 'consent','contract','report','instruction'

  title             VARCHAR(200),

  file_url          TEXT,

  signed            BOOLEAN DEFAULT false,

  signed_at         TIMESTAMPTZ,

  created_at        TIMESTAMPTZ DEFAULT NOW()

);



-- الوصفات الطبية

CREATE TABLE prescriptions (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  patient_id        UUID REFERENCES patients(id),

  visit_id          UUID REFERENCES visits(id),

  doctor_id         UUID REFERENCES doctors(id),

  diagnosis         TEXT,

  drugs             JSONB NOT NULL, -- array of {name, dose, frequency, duration}

  notes             TEXT,

  created_at        TIMESTAMPTZ DEFAULT NOW()

);

3.10 النظام والإدارة

sql-- الأطباء

CREATE TABLE doctors (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  user_id           UUID REFERENCES users(id),

  name              VARCHAR(200) NOT NULL,

  specialty         VARCHAR(100),

  license_number    VARCHAR(100),

  branch_id         UUID REFERENCES branches(id),

  is_active         BOOLEAN DEFAULT true,

  color             VARCHAR(20), -- for calendar

  avatar_initials   VARCHAR(5)

);



-- الفروع

CREATE TABLE branches (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  name              VARCHAR(200) NOT NULL,

  address           TEXT,

  phone             VARCHAR(20),

  is_main           BOOLEAN DEFAULT false,

  is_active         BOOLEAN DEFAULT true

);



-- المستخدمون

CREATE TABLE users (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  username          VARCHAR(100) UNIQUE NOT NULL,

  email             VARCHAR(200) UNIQUE,

  password_hash     TEXT NOT NULL,

  role              VARCHAR(50) NOT NULL,

  -- 'admin','orthodontist','general_dentist','oral_surgeon','reception','accountant','assistant','branch_manager'

  branch_id         UUID REFERENCES branches(id),

  is_active         BOOLEAN DEFAULT true,

  last_login        TIMESTAMPTZ,

  two_factor_enabled BOOLEAN DEFAULT false,

  two_factor_secret TEXT,

  created_at        TIMESTAMPTZ DEFAULT NOW()

);



-- الأذونات والصلاحيات

CREATE TABLE role_permissions (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  role              VARCHAR(50) NOT NULL,

  resource          VARCHAR(100) NOT NULL, -- 'patients','ortho','finance', etc.

  can_view          BOOLEAN DEFAULT false,

  can_create        BOOLEAN DEFAULT false,

  can_edit          BOOLEAN DEFAULT false,

  can_delete        BOOLEAN DEFAULT false,

  can_export        BOOLEAN DEFAULT false,

  can_approve       BOOLEAN DEFAULT false

);



-- سجل النشاط (Audit Log)

CREATE TABLE audit_logs (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  user_id           UUID REFERENCES users(id),

  action            VARCHAR(50) NOT NULL, -- 'create','update','delete','view','export'

  resource          VARCHAR(100) NOT NULL,

  resource_id       UUID,

  old_data          JSONB,

  new_data          JSONB,

  ip_address        VARCHAR(50),

  user_agent        TEXT,

  created_at        TIMESTAMPTZ DEFAULT NOW()

);



-- الإشعارات

CREATE TABLE notifications (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  user_id           UUID REFERENCES users(id),

  type              VARCHAR(100),

  title             TEXT,

  body              TEXT,

  is_read           BOOLEAN DEFAULT false,

  related_entity    VARCHAR(100),

  related_id        UUID,

  created_at        TIMESTAMPTZ DEFAULT NOW()

);



-- الإعدادات

CREATE TABLE settings (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  key               VARCHAR(200) UNIQUE NOT NULL,

  value             TEXT,

  category          VARCHAR(100),

  updated_at        TIMESTAMPTZ DEFAULT NOW()

);



-- المخزون

CREATE TABLE inventory (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  name              VARCHAR(200) NOT NULL,

  category          VARCHAR(100), -- 'brackets','wires','consumables','medications'

  quantity          INTEGER DEFAULT 0,

  min_quantity      INTEGER DEFAULT 0,

  unit              VARCHAR(50),

  cost_per_unit     DECIMAL(10,2),

  branch_id         UUID REFERENCES branches(id),

  updated_at        TIMESTAMPTZ DEFAULT NOW()

);



-- المختبر وطلبات الأجهزة

CREATE TABLE lab_orders (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  patient_id        UUID REFERENCES patients(id),

  ortho_case_id     UUID REFERENCES ortho_cases(id),

  order_number      VARCHAR(50) UNIQUE,

  appliance_type    VARCHAR(200),

  lab_name          VARCHAR(200),

  sent_date         DATE,

  expected_date     DATE,

  received_date     DATE,

  status            VARCHAR(50) DEFAULT 'sent',

  -- 'sent','manufacturing','in_delivery','received','rejected'

  priority          VARCHAR(20) DEFAULT 'normal',

  instructions      TEXT,

  cost              DECIMAL(10,2),

  doctor_id         UUID REFERENCES doctors(id),

  created_at        TIMESTAMPTZ DEFAULT NOW()

);



-- توصيات الذكاء الاصطناعي

CREATE TABLE ai_recommendations (

  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),

  patient_id        UUID REFERENCES patients(id),

  context           VARCHAR(100), -- 'diagnosis','problem_list','treatment_plan','extraction'

  input_data        JSONB,

  recommendation    TEXT,

  confidence        DECIMAL(5,4),

  doctor_action     VARCHAR(50), -- 'approved','rejected','modified','pending'

  doctor_id         UUID REFERENCES doctors(id),

  action_at         TIMESTAMPTZ,

  created_at        TIMESTAMPTZ DEFAULT NOW()

);



4. هيكل API الكامل

4.1 Authentication

POST   /api/auth/login

POST   /api/auth/logout

POST   /api/auth/refresh-token

POST   /api/auth/verify-2fa

POST   /api/auth/forgot-password

POST   /api/auth/reset-password

GET    /api/auth/me

4.2 Patients

GET    /api/patients                    -- قائمة مع فلتر وبحث وصفحات

POST   /api/patients                    -- إنشاء مريض جديد

GET    /api/patients/{id}               -- ملف المريض الكامل

PUT    /api/patients/{id}               -- تعديل بيانات

DELETE /api/patients/{id}               -- أرشفة (soft delete)

GET    /api/patients/{id}/medical-history

PUT    /api/patients/{id}/medical-history

GET    /api/patients/{id}/dental-history

PUT    /api/patients/{id}/dental-history

GET    /api/patients/{id}/timeline      -- كل النشاط

GET    /api/patients/{id}/unified-file  -- الملف الموحّد بكل التخصصات

4.3 Appointments

GET    /api/appointments                -- مع فلتر by date/doctor/specialty

POST   /api/appointments

GET    /api/appointments/{id}

PUT    /api/appointments/{id}

DELETE /api/appointments/{id}

PUT    /api/appointments/{id}/status    -- confirm/arrive/complete/cancel

GET    /api/appointments/today          -- مواعيد اليوم

GET    /api/appointments/schedule       -- جدول أسبوعي/شهري

POST   /api/appointments/check-conflict -- فحص تعارض

4.4 Ortho Cases

GET    /api/ortho-cases

POST   /api/ortho-cases

GET    /api/ortho-cases/{id}

PUT    /api/ortho-cases/{id}

GET    /api/ortho-cases/{id}/clinical-exam

PUT    /api/ortho-cases/{id}/clinical-exam

GET    /api/ortho-cases/{id}/problem-list

POST   /api/ortho-cases/{id}/problem-list

GET    /api/ortho-cases/{id}/treatment-plan

POST   /api/ortho-cases/{id}/treatment-plan

PUT    /api/ortho-cases/{id}/treatment-plan/approve

GET    /api/ortho-cases/{id}/visits

POST   /api/ortho-cases/{id}/visits

GET    /api/ortho-cases/{id}/stages

PUT    /api/ortho-cases/{id}/stages/{stageId}

GET    /api/ortho-cases/{id}/extraction-decision

POST   /api/ortho-cases/{id}/extraction-decision

GET    /api/ortho-cases/{id}/retention

POST   /api/ortho-cases/{id}/retention

GET    /api/ortho-cases/{id}/debonding-summary

4.5 Cephalometric

GET    /api/ceph/{orthoCaseId}

POST   /api/ceph/{orthoCaseId}

GET    /api/ceph/{analysisId}/landmarks

POST   /api/ceph/{analysisId}/landmarks

POST   /api/ceph/{analysisId}/auto-trace  -- AI endpoint

GET    /api/ceph/{analysisId}/measurements

POST   /api/ceph/{analysisId}/calculate   -- احسب القياسات من النقاط

GET    /api/ceph/{analysisId}/diagnosis

POST   /api/ceph/{analysisId}/diagnosis/approve

GET    /api/ceph/{analysisId}/report

GET    /api/ceph/{orthoCaseId}/superimposition -- T0,T1,T2

GET    /api/ceph/{orthoCaseId}/vto

POST   /api/ceph/{orthoCaseId}/vto

4.6 General Dentistry

GET    /api/dental-chart/{patientId}

PUT    /api/dental-chart/{patientId}

POST   /api/dental-chart/{patientId}/teeth/{toothNumber}

GET    /api/general-treatments/{patientId}

POST   /api/general-treatments

PUT    /api/general-treatments/{id}

GET    /api/perio/{patientId}

POST   /api/perio/{patientId}

4.7 Surgery

GET    /api/surgery-cases

POST   /api/surgery-cases

GET    /api/surgery-cases/{id}

PUT    /api/surgery-cases/{id}

GET    /api/surgery-cases/{id}/preop

PUT    /api/surgery-cases/{id}/preop

GET    /api/surgery-cases/{id}/operative-report

POST   /api/surgery-cases/{id}/operative-report

PUT    /api/surgery-cases/{id}/operative-report/approve

GET    /api/surgery-cases/{id}/postop

PUT    /api/surgery-cases/{id}/postop

POST   /api/hospital-referrals

4.8 Finance

GET    /api/contracts

POST   /api/contracts

GET    /api/contracts/{id}

PUT    /api/contracts/{id}

GET    /api/payments

POST   /api/payments

GET    /api/payments/{id}

GET    /api/receipts/{paymentId}

GET    /api/finance/summary             -- إيراد + محصّل + متأخرات

GET    /api/finance/by-specialty        -- حسب تخصص

GET    /api/finance/by-doctor           -- حسب طبيب

GET    /api/finance/overdue             -- المتأخرات

GET    /api/finance/daily-report        -- تقرير يومي

GET    /api/finance/monthly-report      -- تقرير شهري

4.9 Photos & Radiographs

POST   /api/photos/upload

GET    /api/photos/{patientId}

GET    /api/photos/{patientId}/by-category

DELETE /api/photos/{id}

POST   /api/radiographs/upload

GET    /api/radiographs/{patientId}

DELETE /api/radiographs/{id}

4.10 Referrals

GET    /api/referrals

POST   /api/referrals

PUT    /api/referrals/{id}/accept

PUT    /api/referrals/{id}/complete

GET    /api/referrals/pending

4.11 Reports & Analytics

GET    /api/reports/center-summary

GET    /api/reports/ortho-cases

GET    /api/reports/general-dentistry

GET    /api/reports/surgery

GET    /api/reports/doctor-performance

GET    /api/reports/financial

GET    /api/reports/overdue

GET    /api/reports/growth-analysis

POST   /api/reports/generate-pdf        -- توليد PDF

GET    /api/analytics/kpis

GET    /api/analytics/appointments-heatmap

GET    /api/analytics/patient-funnel

4.12 System

GET    /api/branches

POST   /api/branches

GET    /api/users

POST   /api/users

PUT    /api/users/{id}/role

GET    /api/inventory

PUT    /api/inventory/{id}

GET    /api/lab-orders

POST   /api/lab-orders

PUT    /api/lab-orders/{id}/status

GET    /api/notifications

PUT    /api/notifications/{id}/read

GET    /api/audit-logs

GET    /api/settings

PUT    /api/settings

POST   /api/ai/recommendation          -- Claude API

POST   /api/ai/ceph-analysis           -- AI Ceph



5. هيكل المجلدات

Frontend (Next.js)

src/

├── app/

│   ├── (auth)/

│   │   └── login/

│   ├── (dashboard)/

│   │   ├── layout.tsx              -- Sidebar + Topbar

│   │   ├── page.tsx                -- Dashboard

│   │   ├── patients/

│   │   │   ├── page.tsx            -- قائمة المرضى

│   │   │   ├── new/page.tsx        -- مريض جديد

│   │   │   └── [id]/

│   │   │       ├── page.tsx        -- الملف الموحّد

│   │   │       ├── general/        -- طب عام

│   │   │       ├── ortho/          -- تقويم

│   │   │       └── surgery/        -- جراحة

│   │   ├── appointments/

│   │   ├── ortho/

│   │   │   ├── cases/

│   │   │   ├── ceph/               -- السيفالومتري

│   │   │   ├── vto/

│   │   │   └── retention/

│   │   ├── general-dentistry/

│   │   ├── surgery/

│   │   ├── referrals/

│   │   ├── finance/

│   │   │   ├── overview/

│   │   │   ├── contracts/

│   │   │   ├── payments/

│   │   │   └── receipts/

│   │   ├── lab-orders/

│   │   ├── inventory/

│   │   ├── reports/

│   │   ├── analytics/

│   │   └── settings/

│   └── api/                        -- Next.js API Routes (اختياري)

├── components/

│   ├── ui/                         -- Shadcn components

│   ├── layout/

│   │   ├── Sidebar.tsx

│   │   ├── Topbar.tsx

│   │   └── NotificationPanel.tsx

│   ├── patients/

│   │   ├── PatientTable.tsx

│   │   ├── PatientForm.tsx

│   │   ├── PatientBanner.tsx

│   │   └── UnifiedFile.tsx

│   ├── ortho/

│   │   ├── OrthoStages.tsx

│   │   ├── ClinicalExam.tsx

│   │   ├── ProblemList.tsx

│   │   ├── TreatmentPlan.tsx

│   │   ├── VisitTimeline.tsx

│   │   ├── ExtractionDecision.tsx

│   │   └── RetentionModule.tsx

│   ├── ceph/

│   │   ├── CephCanvas.tsx          -- Fabric.js canvas

│   │   ├── LandmarkList.tsx

│   │   ├── MeasurementsTable.tsx

│   │   ├── PolygonChart.tsx        -- D3.js radar

│   │   ├── Superimposition.tsx

│   │   ├── VTOModule.tsx

│   │   └── CephReport.tsx

│   ├── dental/

│   │   ├── DentalChart.tsx         -- Interactive tooth chart

│   │   ├── ToothSelector.tsx

│   │   └── TreatmentHistory.tsx

│   ├── surgery/

│   │   ├── SurgeryCases.tsx

│   │   ├── PreOpReport.tsx

│   │   ├── OperativeReport.tsx

│   │   └── PostOpModule.tsx

│   ├── finance/

│   │   ├── FinanceSummary.tsx

│   │   ├── ContractForm.tsx

│   │   ├── PaymentForm.tsx

│   │   ├── ReceiptPrinter.tsx

│   │   └── OverdueList.tsx

│   ├── appointments/

│   │   ├── AppointmentCalendar.tsx

│   │   ├── AppointmentForm.tsx

│   │   └── DaySchedule.tsx

│   ├── photos/

│   │   ├── PhotoGallery.tsx        -- Dolphin-style

│   │   ├── PhotoUploader.tsx

│   │   └── PhotoComparison.tsx

│   ├── reports/

│   │   ├── ReportViewer.tsx

│   │   └── PDFGenerator.tsx

│   └── shared/

│       ├── RTLProvider.tsx

│       ├── ArabicDatePicker.tsx

│       └── YemeniRiyal.tsx

├── lib/

│   ├── api.ts                      -- Axios instance

│   ├── auth.ts

│   └── utils.ts

├── stores/

│   ├── authStore.ts

│   ├── patientStore.ts

│   └── cephStore.ts

├── hooks/

│   ├── usePatients.ts

│   ├── useOrthoCase.ts

│   └── useCeph.ts

└── types/

    ├── patient.ts

    ├── ortho.ts

    ├── finance.ts

    └── api.ts

Backend (.NET 8)

AqlanDentalPro.sln

├── src/

│   ├── AqlanDentalPro.API/

│   │   ├── Controllers/

│   │   │   ├── AuthController.cs

│   │   │   ├── PatientsController.cs

│   │   │   ├── AppointmentsController.cs

│   │   │   ├── OrthoCasesController.cs

│   │   │   ├── CephalometricController.cs

│   │   │   ├── GeneralDentistryController.cs

│   │   │   ├── SurgeryController.cs

│   │   │   ├── FinanceController.cs

│   │   │   ├── ReportsController.cs

│   │   │   ├── InventoryController.cs

│   │   │   ├── LabOrdersController.cs

│   │   │   ├── ReferralsController.cs

│   │   │   ├── AIController.cs

│   │   │   └── SettingsController.cs

│   │   ├── Middleware/

│   │   │   ├── AuditLogMiddleware.cs

│   │   │   └── ErrorHandlingMiddleware.cs

│   │   └── Program.cs

│   ├── AqlanDentalPro.Application/

│   │   ├── Interfaces/

│   │   ├── Services/

│   │   │   ├── PatientService.cs

│   │   │   ├── OrthoCaseService.cs

│   │   │   ├── CephalometricService.cs

│   │   │   ├── CephCalculationService.cs  -- حساب القياسات

│   │   │   ├── FinanceService.cs

│   │   │   ├── ReportService.cs

│   │   │   ├── AIService.cs              -- Claude API integration

│   │   │   ├── WhatsAppService.cs

│   │   │   └── PDFService.cs

│   │   ├── DTOs/

│   │   ├── Validators/

│   │   └── Mappings/

│   ├── AqlanDentalPro.Domain/

│   │   ├── Entities/

│   │   ├── Enums/

│   │   └── ValueObjects/

│   └── AqlanDentalPro.Infrastructure/

│       ├── Data/

│       │   ├── AppDbContext.cs

│       │   └── Migrations/

│       ├── Repositories/

│       ├── Services/

│       │   ├── S3StorageService.cs

│       │   ├── RedisService.cs

│       │   └── EmailService.cs

│       └── BackgroundJobs/

│           ├── AppointmentReminderJob.cs

│           └── PaymentOverdueAlertJob.cs

└── tests/

    ├── AqlanDentalPro.UnitTests/

    └── AqlanDentalPro.IntegrationTests/



6. الصلاحيات الكاملة

┌────────────────────────┬───────┬──────┬─────┬────────┬───────┬─────────┐

│ الصلاحية               │ Admin │Ortho │Gen. │Surgery │Recept.│Accountant│

├────────────────────────┼───────┼──────┼─────┼────────┼───────┼─────────┤

│ ملفات المرضى (عرض)    │  ✓    │  ✓   │  ✓  │   ✓    │   ✓   │    ✓    │

│ ملفات المرضى (إنشاء)  │  ✓    │  ✓   │  ✓  │   ✓    │   ✓   │    -    │

│ ملفات المرضى (حذف)    │  ✓    │  -   │  -  │   -    │   -   │    -    │

│ التشخيص التقويمي      │  ✓    │  ✓   │  -  │   -    │   -   │    -    │

│ خطة العلاج            │  ✓    │  ✓   │  -  │   -    │   -   │    -    │

│ السيفالومتري           │  ✓    │  ✓   │  -  │   -    │   -   │    -    │

│ طب الأسنان العام      │  ✓    │  -   │  ✓  │   -    │   -   │    -    │

│ الجراحة               │  ✓    │  -   │  -  │   ✓    │   -   │    -    │

│ المواعيد              │  ✓    │  ✓   │  ✓  │   ✓    │   ✓   │    -    │

│ المالية (عرض)         │  ✓    │  -   │  -  │   -    │   ✓   │    ✓    │

│ المالية (إنشاء دفعة)  │  ✓    │  -   │  -  │   -    │   ✓   │    ✓    │

│ التقارير الكاملة      │  ✓    │  -   │  -  │   -    │   -   │    ✓    │

│ إدارة المستخدمين      │  ✓    │  -   │  -  │   -    │   -   │    -    │

│ إعدادات النظام        │  ✓    │  -   │  -  │   -    │   -   │    -    │

│ AI Recommendations    │  ✓    │  ✓   │  ✓  │   ✓    │   -   │    -    │

└────────────────────────┴───────┴──────┴─────┴────────┴───────┴─────────┘



7. وحدة السيفالومتري (WebCeph + Dolphin)

منطق الحسابات

typescript// نقاط ومعادلات Steiner Analysis

const STEINER_FORMULAS = {

  SNA:  ({ S, N, A })  => angleBetween3Points(S, N, A),

  SNB:  ({ S, N, B })  => angleBetween3Points(S, N, B),

  ANB:  (m) => m.SNA - m.SNB,

  SNMP: ({ S, N, Go, Me }) => angleBetweenLines([S,N], [Go,Me]),

  U1toSN: ({ U1t, U1r, S, N }) => angleBetweenLines([U1t,U1r], [S,N]),

  IMPA:   ({ L1t, L1r, Go, Me }) => angleBetweenLines([L1t,L1r], [Go,Me]),

  UpperLipEline: ({Ls, Pn, Pg}) => distancePointToLine(Ls, [Pn,Pg]),

  LowerLipEline: ({Li, Pn, Pg}) => distancePointToLine(Li, [Pn,Pg]),

  Wits: ({ A, B, ANS, Me }) => witsAppraisal(A, B, ANS, Me),

};



// القيم الطبيعية

const NORMS = {

  steiner: {

    SNA:  { norm: 82, sd: 2, unit: '°' },

    SNB:  { norm: 80, sd: 2, unit: '°' },

    ANB:  { norm: 2,  sd: 2, unit: '°' },

    // ...

  }

};

قائمة النقاط (21 نقطة)

Skeletal: S, N, A, B, Or, Po, Go, Gn, Me, Ba, ANS, PNS, Ar

Dental:   U1-tip, U1-root, L1-tip, L1-root

Soft:     Ls (Upper lip), Li (Lower lip), Pn (Pronasale), Pg' (Soft Pogonion)

التحليلات المدعومة

1. Steiner Analysis      — SNA, SNB, ANB, SN-MP, U1toSN, IMPA, etc.

2. Downs Analysis        — Facial Angle, Convexity, A-B Plane, etc.

3. Tweed Analysis        — FMA, FMIA, IMPA triangle

4. McNamara Analysis     — Nasomaxillary, Mandibular lengths

5. Ricketts Analysis     — Facial axis, Facial depth, etc.

6. Wits Appraisal        — AO-BO on occlusal plane

7. Soft Tissue Analysis  — E-line, nasolabial angle

8. Jarabak Analysis      — Posterior/Anterior face height ratio



8. مراحل التنفيذ

المرحلة 1 — الأساس (4-6 أسابيع)

✓ إعداد المشروع + CI/CD + Docker

✓ قاعدة البيانات + Migrations الكاملة

✓ نظام المصادقة (Login + JWT + 2FA)

✓ CRUD المرضى الكامل

✓ المواعيد والجدولة

✓ Dashboard أساسي

✓ الصلاحيات والأدوار

✓ سجل النشاط (Audit Log)

✓ إعدادات المركز (بيانات د. عقلان)

المرحلة 2 — الملف التقويمي (4-6 أسابيع)

✓ إنشاء الحالة التقويمية

✓ الفحص السريري الكامل

✓ قائمة المشاكل

✓ مراحل العلاج وتتبعها

✓ تسجيل الزيارات التقويمية

✓ خطة العلاج (Plan A/B)

✓ Debonding وRetention

✓ رفع الصور (9 صور سريرية)

✓ رفع الأشعة

المرحلة 3 — التحليل المتخصص (3-4 أسابيع)

✓ Canvas السيفالومتري (Fabric.js)

✓ تحديد النقاط يدوياً (21 نقطة)

✓ حساب القياسات تلقائياً

✓ 8 أنواع تحليل (Steiner, Downs, etc.)

✓ Polygon/Radar chart

✓ تحليل الموديل (Bolton + Space)

✓ دعم قرار الخلع

✓ تقرير سيفالومتري PDF

المرحلة 4 — التخصصات الأخرى (3-4 أسابيع)

✓ مخطط الأسنان التفاعلي (32 سن)

✓ سجل علاجات طب الأسنان العام

✓ وصفات طبية

✓ تقييم اللثة

✓ وحدة جراحة الوجه والفكين

✓ الإحالات الداخلية

✓ الملف الموحّد للمريض

المرحلة 5 — المالية والتقارير (3-4 أسابيع)

✓ العقود والأقساط

✓ تسجيل الدفعات

✓ سندات القبض (طباعة بهوية المركز)

✓ تقارير التحصيل والمتأخرات

✓ تقارير الأطباء والتخصصات

✓ PDF reports احترافية

✓ إحصائيات KPI

المرحلة 6 — الذكاء الاصطناعي (4-6 أسابيع)

✓ تتبع سيفالومتري تلقائي (AI)

✓ اقتراح Problem List

✓ دعم قرار الخلع AI

✓ Superimposition تلقائي

✓ VTO — Visual Treatment Objective

✓ ملخص الحالة التلقائي

✓ تنبيهات ذكية (حالات متأخرة، أقساط مستحقة)

المرحلة 7 — التكاملات والتطوير المتقدم (6-8 أسابيع)

✓ WhatsApp Business API (تذكير المواعيد)

✓ إدارة المختبر وطلبات الأجهزة

✓ إدارة المخزون

✓ بوابة المريض (اختياري)

✓ تطبيق جوال (React Native)

✓ دعم الفروع المتعددة

✓ النسخ الاحتياطي التلقائي



9. متطلبات الأداء والأمان

الأداء

- تحميل أي صفحة: < 2 ثانية

- البحث في المرضى: < 500ms

- حفظ السجل: < 1 ثانية

- تحميل الصور: Lazy loading + WebP

- API response: < 200ms (cached)

- Canvas السيفالومتري: 60 FPS

الأمان

- JWT + Refresh Tokens + HttpOnly Cookies

- 2FA (TOTP) للأطباء والAdmin

- تشفير كلمات المرور: Argon2id

- تشفير الملفات: AES-256

- HTTPS فقط

- Rate Limiting: 100 req/min

- CORS محدود

- SQL Injection prevention عبر ORM

- XSS protection

- Audit log لكل عملية حساسة

- Session timeout: 30 دقيقة

- Branch-based data isolation



10. النقاط الحرجة لـ Claude Code

1. RTL كامل — كل مكوّن يجب أن يكون direction:rtl

2. خط Tajawal Google Fonts للعربية

3. العملة: ريال يمني — لا dollar signs

4. كل توصية AI تُعرض كـ "مقترح" — لا قرارات نهائية

5. سجل Audit لأي تعديل على بيانات مريض

6. الصور والأشعة تُخزّن على Cloud مع URLs آمنة

7. مخطط الأسنان: FDI Notation (11-48) وليس Universal

8. السيفالومتري: الحساب يتم Server-side لضمان الدقة

9. كل حذف هو Soft Delete — لا Hard Delete إلا للAdmin

10. التقارير PDF تحمل هوية المركز (اسم + عنوان + أرقام د. عقلان)

11. Superimposition يُحفظ مرتبطاً بالحالة التقويمية

12. قرار الخلع يُسجَّل مع اسم الطبيب وتاريخ الاعتماد

13. المالية: كل دفعة ترتبط بعقد وبطبيب وبتخصص

14. Appointment conflict check قبل الحفظ

15. WhatsApp reminder: 24 ساعة + 2 ساعة قبل الموعد



11. ملاحظات للتسليم لـ Claude Code

الأمر الأول الذي تكتبه:

أنشئ نظام Aqlan Dental Pro — نظام ويب شامل لمركز طب وتقويم 

الأسنان. اقرأ هذه الوثيقة كاملاً ثم ابدأ بـ:



1. إنشاء المشروع (Next.js + .NET 8 + PostgreSQL)

2. قاعدة البيانات بكل الجداول المذكورة

3. نظام المصادقة

4. صفحة تسجيل الدخول بهوية المركز:

   - اسم: مركز د. عقلان الكامل

   - موقع: تعز، اليمن

   - هواتف: 04-253028 · 770-245745 · 711-752823

   - الفريق الطبي الخمسة

5. Dashboard الرئيسي

6. CRUD المرضى



ثم تابع المرحلة تلو الأخرى حسب الخطة.

الواجهة عربية RTL بالكامل. العملة: ريال يمني.# aqlan-dental
