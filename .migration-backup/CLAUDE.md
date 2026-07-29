# CLAUDE.md — Aqlan Dental Pro

## SpecKit Governance — Mandatory Before Editing

Before any code or behavior change, read and follow:

- **`docs/governance/MANDATORY_SPRINT_QUEUE.md` — read this FIRST.** It is the single
  binding, ordered backlog. Find the first not-done item and work on ONLY that item,
  regardless of which agent/model/session executes the work — no skipping ahead
  except the narrow exceptions the file itself defines (P0 production incidents,
  read-only audits, finishing an already-open PR, merge-conflict fixes).
- `.specify/constitution.md`
- `.specify/agent-policy.md`
- `.specify/model-routing-policy.md`
- `.specify/spec-drift-policy.md`
- `specs/000-master-system/module-map.md`
- `docs/governance/SPEC_KIT_WORKFLOW.md`
- `docs/governance/CHEAP_MODEL_SAFE_MODE.md`

No implementation without a linked spec ID. If the requested feature has no spec, create or update the spec first. If unsure, write a report, not code.

ذاكرة المشروع الدائمة لوكلاء Claude. اقرأ هذا الملف أولًا في كل جلسة.

## سياق المالك وأولوياته (مهم جدًا)

- **المالك أخصائي تقويم أسنان** (د. عقلان — مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان، تعز، اليمن).
- النظام يجب أن **يخدم عمل أخصائي التقويم أولًا** ويُسهّل يومه: حالات التقويم (OrthoCases)، زيارات التقويم، خطط العلاج، الأشعة والصور، التحاليل السيفالومترية.
- **مشاكل المركز الفعلية التي يجب أن يحلها النظام:**
  1. **الزحمة** — قائمة الانتظار وشاشة العرض ونداء المرضى يجب أن تكون سريعة وموثوقة، وتقليل النقرات في شاشة العمليات اليومية أولوية دائمة.
  2. **تراكم المواعيد** — جدولة ذكية، كشف التعارضات، تذكيرات، إدارة عدم الحضور (no-show) وإعادة الاستدعاء (recall).
  3. **تراكم التراكيب (أعمال المختبر)** — متابعة أوامر المختبر المتأخرة، تنبيهات الاستحقاق، وربط التكاليف بالمالية والعمولات.
- معيار الجودة المطلوب: **أنظمة المستشفيات الحديثة** — استقرار، دقة مالية، عربي RTL سليم، لا أعطال صامتة.
- **هوية التقارير (قرار المالك):** كل تقرير/PDF/عرض حالة يحمل اسم المركز كاملًا + «د. عقلان الكامل — أخصائي تقويم الأسنان» وتحته سطر المؤهل «جامعة مانيلا المركزية — الفلبين» (الافتراضي في تقارير التقويم والسيفالو) + بيانات التواصل في التذييل — تُقرأ من مفاتيح Settings: `clinic.name`, `clinic.lead_doctor`, `clinic.lead_doctor_title`, `clinic.lead_doctor_credentials` — لا hardcoding.
- **السيفالومتري:** WebCeph هو النموذج المستهدف المعتمد (التفاصيل في `docs/ortho-module/CEPH-EPIC.md`)؛ مراحل التقويم P3+ مؤجلة حتى اكتمال السيفالو وتقاريره.

## القواعد الصارمة

- لا تحذف هجرات. لا تعد بناء النظام من الصفر. لا تحذف ميزات قائمة. لا تبدّل إطار العمل.
- كل القيم المالية والقواعد التشغيلية قابلة للتهيئة (جدول `Settings`) — لا hardcoding.
- كل رسائل الأخطاء للمستخدم بالعربية. أي 4xx/5xx يحمل حقل `message` عربي.
- استقرار الإنتاج مقدم على أي تحسين شكلي. Railway (باك إند+PostgreSQL) + Vercel (واجهة) ينشران من `main`.
- لا تكشف تفاصيل الاستثناءات في استجابات HTTP (أُزيل تسريب سابق — لا تعيده).

## البنية والأوامر

- **Backend:** ASP.NET Core 8، Clean Architecture في `backend/src/` (API/Application/Domain/Infrastructure)، PostgreSQL عبر EF Core، QuestPDF للعربية (خط Noto Naskh Arabic في `backend/Fonts/`).
  - بناء واختبار: `dotnet build -c Release` ثم `dotnet test tests/AqlanDentalPro.UnitTests/... -c Release`
- **Frontend:** Next.js 14 App Router + TypeScript + Tailwind في `frontend/`، React Query، Zustand.
  - فحص: `npx tsc --noEmit && npm run lint && npx vitest run && npm run build`
- **CI:** `.github/workflows/ci.yml` — يجب أن يبقى أخضر قبل أي PR.

## فخاخ معروفة (لا تقع فيها)

- **التاريخ المحلي:** استخدم `localDateString()` من `frontend/src/lib/utils.ts` لأي "اليوم" — لا تستخدم `toISOString().slice(0,10)` أبدًا (اليمن UTC+3، كان يسبب انزياح يوم مساءً).
- **سلسلة الهجرات تاريخيًا مكسورة للقواعد الفارغة**: 31 هجرة بلا سمة `[Migration]`. القواعد الفارغة تُبنى عبر خط أساس من نموذج EF في `StartupDatabaseMaintenance.EnsureFreshDatabaseMigratedAsync` — لا تحاول "إصلاح" السلسلة بإضافة السمات (سيكسر الإنتاج).
- `LabOrders.DoctorId` يشير إلى `Doctors.Id` وليس `Users.Id` — حوّل دائمًا عبر `Doctors.UserId`.
- أي صرف من الخزينة عبر `TreasuryResolutionService.DecrementTreasuryBalanceAsync` (يحترم مفتاح الإعدادات `finance.prevent_negative_treasury_balance`).
- الدفع/الاسترداد/صرف العمولات يتطلب وردية كاشير مفتوحة — لا تتجاوز هذا الفحص.
- العمولات لها مساران بالتصميم: مستحقة (من الفواتير) ومكتسبة (من التحصيل الفعلي مع خصم تكاليف المختبر/المواد).

## مراجع التدقيق والخارطة

- **ترتيب العمل الإلزامي (الحالي، الملزم لكل وكيل):** `docs/governance/MANDATORY_SPRINT_QUEUE.md` — هذا هو الترتيب الوحيد المعتمد. لا تتبع أي ترتيب سبرنتات مذكور في مستندات أخرى (بما فيها السطر السابق في هذا الملف والذي أُبقي أدناه كسجل تاريخي فقط).
- التدقيق الكامل والديون الفنية والخارطة (مؤرشف — سجل تاريخي، ليس مصدر ترتيب): `docs/agent-audit/` (آخر تحديث 2026-06-12)، `docs/technical-debt-register.md`.
- ~~أولويات السبرنتات القادمة (سطر تاريخي مؤرشف — استُبدل بـ`MANDATORY_SPRINT_QUEUE.md` أعلاه): أمان جلسات بوابة المرضى ← تعيينات غرف الأطباء ← شاشة إعدادات موحدة ← تحسين العمليات اليومية لتقليل الزحمة ← وحدة تقويم محسّنة ← تنبيهات تأخر التراكيب.~~
