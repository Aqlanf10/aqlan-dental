# خطة المالية المتبقّية — تسليم لوكيل آخر (Finance Permissions Handoff)

> آخر تحديث: 2026-06-23 — بعد دمج PR #516.
> الهدف: استكمال **تفعيل صلاحيات المالية الدقيقة** (database-driven) عبر بقية المتحكّمات المالية، بنفس النمط الذي رُسّخ في #516.

---

## 0) الخلفية المعمارية (اقرأها أولًا)

- **آلية التفعيل:** `PermissionGuard.HasAsync(AppDbContext db, ICurrentUserService currentUser, string resource, string action)`
  - الموقع: `backend/src/AqlanDentalPro.API/Authorization/PermissionGuard.cs`.
  - **Admin يتجاوز دائمًا** (يرجع `true` فورًا).
  - يقرأ صفّ `RolePermissions` حسب (Role, Resource) ويحوّل `action` ∈ {view, create, edit, delete, export, approve} إلى العمود `Can*`.
  - `PermissionGuard` في طبقة **API** — لا يمكن استدعاؤه من خدمات **Infrastructure** (لا تَعتمد API). لو احتجت فحصًا داخل خدمة Infrastructure استعمل نمط `HasFinancePermissionAsync` الذي أضفناه في `PatientJourneyService` (يفحص `IsInRole` مقابل صفوف `RolePermissions`).

- **النمط القياسي داخل المتحكّم** (مأخوذ من وحدة المختبر و#516):
  ```csharp
  private Task<bool> CanAsync(string action) =>
      PermissionGuard.HasAsync(db, currentUser, "finance.<resource>", action);
  private IActionResult Deny() =>
      StatusCode(403, new { message = "غير مصرح لك بهذا الإجراء المالي" });
  // وفي بداية كل أكشن:
  if (!await CanAsync("view")) return Deny();
  ```
  - السياسة `[Authorize]` على مستوى الفئة **تبقى** كخط أساس (ASP.NET إضافية لا بديلة) — `PermissionGuard` يضيف الطبقة الدقيقة فوقها.
  - الرفض **403 برسالة عربية** (قاعدة المشروع: كل 4xx يحمل `message` عربي). لا تستعمل `Forbid()` المجرّدة.

- **مفاتيح `finance.*` المزروعة** (`DbSeeder.SeedPermissionsAsync`, كتلة INSERT-ONLY ~سطر 610-650):
  `finance.dashboard, finance.payments, finance.receipts, finance.cashier_session, finance.patient_balance, finance.account_statement, finance.reports, finance.treasuries, finance.expenses, finance.commissions`.
  - **⚠️ لا يوجد `finance.invoices` ولا `finance.contracts`** — يجب إضافتهما (INSERT-ONLY) قبل تفعيلهما، مع قرار نطاق الاستقبال (انظر §3).
  - الزرع **INSERT-ONLY**: لا تُعِد الكتابة على صفّ موجود (`if (existingPermissions.ContainsKey((role, resource))) continue;`) كي تبقى تعديلات المالك من الإعدادات.

- **خريطة السياسات → الأدوار** (`AuthorizationPolicyConfiguration.cs`):
  - `FinanceAccess` = Admin + Reception + Accountant ← **هنا الاستقبال أوسع من اللازم** (الأولوية).
  - `ReportsAccess` = Admin + Accountant (يستبعد الاستقبال أصلًا).
  - `CommissionView/Edit/Approve/Pay` = Admin + Accountant.

---

## 1) ما أُنجز في #516 (لا تُعِده)

1. **حدّ اعتماد المصروفات قابل للتهيئة** — `finance.expenses.approval_threshold` في `FinanceSettingsKeys` (افتراضي "50000")، يُقرأ في `OperationalExpensesController.Create` عبر `FinanceSettingsReader`.
2. **`PaymentsController`** مفعَّل بالكامل: view/create/edit على `finance.payments`، ملخص المريض→`finance.patient_balance.view`، كشف الحساب PDF→`finance.account_statement.export`، سند القبض PDF→`finance.receipts.view`. الحذف/الاسترداد `AdminOnly`.
3. **`PatientJourneyService.GetDailySummaryAsync`** — طبقات رصيد الـ checkout صارت permission-driven: كامل عبر `finance.patient_balance.view`، محدود (كاشير) عبر `finance.payments.view` (يكشف فقط المستحق/المتأخر/آخر دفعة/الحالة). أُبقي «آخر دفعة» لأن شاشة العمليات اليومية تستخدمه لإعادة طباعة السند.
4. **`CashierSessionsController`** مفعَّل: فتح/إغلاق→`create`، عرض/قائمة/تفاصيل/نشطة/ملخص-اليوم→`view`، تسوية→`approve`.

اختبارات #516: `PaymentsPermissionEnforcementTests`, `CashierSessionPermissionEnforcementTests`, 3 اختبارات tiering في `PatientJourneyFinancialRulesTests`.

---

## 2) الفخاخ المعروفة (دروس مدفوعة الثمن)

- **تغيير المُنشئ يكسر الاختبارات** (CS7036): أي إضافة لمعامل في ctor المتحكّم تكسر كل مواقع `new XController(...)` في الاختبارات. الحل: حدّث مواقع الإنشاء (أضِف الاعتمادية الجديدة). مثال #516: 4 مواقع لـ Payments، 11 لـ Expenses.
- **`[FromServices]` على الأكشن يكسر الاختبارات التي تستدعي الأكشن مباشرة**. تجنّبه إن كانت هناك اختبارات تستدعي الأكشن.
- **مشكلة الـ fixture**: اختبارات تبني المتحكّم بمستخدم **غير Admin** وقاعدة `RolePermissions` فارغة → `HasAsync` يرجع false → 403 يكسر الاختبار. الحلّان:
  1. اجعل mock الدور = Admin إن كان الاختبار لا يعنى بالصلاحيات (Admin يتجاوز).
  2. ازرع `RolePermission` المناسب في إعداد الاختبار (انظر `CashierSessionActiveEndpointTests.SeedBranchAndCashier` و`TechnicalDebtCleanupTests` في #516 كنموذج).
- **ترتيب الفحص**: ضع فحص الصلاحية **أولًا** في الأكشن (authorization قبل المنطق)، ثم حُرّاس الفرع/التحقق. انتبه لاختبارات «حارس الفرع يرجع 403» — ازرع الصلاحية كي يبقى الاختبار يفحص حارس الفرع لا بوابة الصلاحية.
- **الأنواع المجهولة عبر التجميعات**: لا تستعمل `dynamic` على كائن مجهول أُنشئ في تجميعة أخرى (RuntimeBinderException). استعمل reflection (`GetProperty`) إن لزم — كما في اختبارات tiering.
- **Admin**: في `ICurrentUserService` هناك `IsAdmin` **و** `Role`. `PermissionGuard` يفحص `Role == UserRole.Admin`. بعض الاختبارات تضبط `IsAdmin` فقط — اضبط `Role` أيضًا.

---

## 3) ⚠️ قرارات تصميم تحتاج موافقة المالك (قبل التنفيذ)

`finance.invoices` و`finance.contracts` **غير موجودين**. الاستقبال حاليًا (عبر `FinanceAccess`) يستطيع إنشاء/تعديل/حذف الفواتير والعقود — وهو أوسع من اللازم. يجب:

1. **إضافة المفتاحين** إلى `DbSeeder` (INSERT-ONLY) و`FinanceSettingsKeys` إن لزم.
2. **تحديد نطاق الأدوار** — التوصية المقترحة (تحتاج تأكيد المالك):

   | المفتاح | Admin | Accountant | Reception |
   |---|---|---|---|
   | `finance.invoices` | الكل | view, create, edit, export, (approve=issue) | **view فقط** (لرؤية ما يُحصّل؛ لا إنشاء/تعديل/حذف مباشر) |
   | `finance.contracts` | الكل | view, create, edit, export | **view فقط** |

   - مبرّر «view فقط» للاستقبال: مسوّدات الفواتير عند الـ checkout تُنشأ عبر `CheckoutService` (مسار الـ journey) لا عبر `InvoicesController`، فلا يحتاج الاستقبال create/edit مباشرًا. لو ثبت أن الاستقبال يُنشئ فواتير يدويًا من واجهته → امنحه `create`.
   - **إصدار/إلغاء الفاتورة** (`issue`/`cancel`) أفعال مالية حسّاسة → عيّنها لـ `approve` أو `edit` (Accountant/Admin فقط).

**لا تُفعّل Invoices/Contracts قبل حسم هذا الجدول مع المالك** (نفس مستوى حساسية قرار `patient_balance`).

---

## 4) المهام المتبقّية (مرتّبة بالأولوية)

### المجموعة أ — متحكّمات `FinanceAccess` (الاستقبال أوسع من اللازم — الأولوية)

#### A1. `InvoicesController` (`FinanceAccess`) — الأهم
- ctor فيه `db` + `currentUser` ✅ (لا تغيير ctor). أضِف `CanAsync`/`Deny`.
- المفتاح: `finance.invoices` (يُضاف أولًا — §3).
- الأفعال (مع الربط المقترح):
  - `POST /` (Create, سطر 31) → `create`
  - `GET /` (سطر 226) و`GET /{id}` (285) و`GET /patients/{id}/invoices` (422) → `view`
  - `PUT /{id}` (454) → `edit`
  - `PATCH /{id}/issue` (650) → `approve` (أو edit حسب قرار §3)
  - `PATCH /{id}/cancel` (713) → `approve`
  - `GET /{id}/pdf` (818) → `view` (أو `export`)
- اختبارات الإنشاء: `PatientFinanceLedgerTests` (InvoicesController_GetInvoicePdf), وغيرها — افحص `new InvoicesController(` وحدّث/ازرع.

#### A2. `ContractsController` (`FinanceAccess`)
- **⚠️ ctor = `(IFinanceService service, FinanceSettingsReader financeSettings)` — لا `db` ولا `currentUser`**. يجب إضافتهما → تغيير ctor → حدّث مواقع الإنشاء في الاختبارات.
- المفتاح: `finance.contracts` (§3).
- الأفعال: `GET`(15)/`GET {id}`(26)→`view`، `POST`(33)→`create`، `PUT {id}`(45)→`edit`، `PATCH {id}/status`(62)→`approve`.

#### A3. `TreasuriesController` (`FinanceAccess`)
- ctor فيه `db` + `currentUser` ✅. المفتاح `finance.treasuries` **موجود** (Admin كامل، Accountant view/create/edit، Reception **غائب** → سيُمنع تلقائيًا، صحيح).
- الأفعال: `GET`(24)/`GET {id}/transactions`(196)→`view`، `POST`(57)→`create` (موجود به `Roles="Admin"` بالفعل — أبقِه)، `POST {id}/recalculate`(122)→`edit`.

#### A4. `VaultTransfersController` (`FinanceAccess`)
- ctor فيه `db` + `currentUser` ✅. اقترح مفتاحًا: استعمل `finance.treasuries` (التحويلات بين الخزائن) — view للقائمة، create للتحويل، approve/reject للاعتماد. (أو أضِف `finance.vault_transfers` إن رغب المالك بفصلها.)

### المجموعة ب — متحكّمات `ReportsAccess` (الاستقبال مُستبعَد أصلًا — أولوية أقل)
الاستقبال محجوب هنا بالفعل؛ التفعيل الدقيق يميّز **Accountant عن Admin** فقط (مثلًا منع المحاسب من الحذف).

#### B1. `OperationalExpensesController` (`ReportsAccess`)
- ctor فيه `db`+`currentUser`+`financeSettings` ✅. المفتاح `finance.expenses` موجود.
- الأفعال: `GET`(246)/`GET pending`(328)/`GET voucher pdf`(42)→`view`، `POST`(60)→`create`، `DELETE {id}`(522)→`delete`. الاعتماد/الرفض (355/482) `AdminAccess` بالفعل → اربطها بـ `approve` (Accountant مُنح approve في الزرع — قرار: هل يعتمد المحاسب؟).

#### B2. `SupplierBillsController` (`ReportsAccess`)
- المفتاح المقترح `finance.expenses` (فواتير الموردين نفقات). `POST`(40)→create، `GET`→view، `POST {id}/pay`(319)→`approve`/create، `DELETE`(499) `AdminAccess`→delete.

#### B3. `AdvancePaymentController` (`ReportsAccess`)
- أغلب أفعاله `AdminOnly` بالفعل. مفتاح مقترح `finance.payments` أو `finance.expenses`. أولوية منخفضة.

### المجموعة ج — مفعَّلة جزئيًا أو لا تحتاج عملًا
- **`CommissionsController`** — يستعمل سياسات `Commission*` الدقيقة بالفعل (Admin+Accountant). اختياري: استبدالها/دعمها بـ `finance.commissions` عبر `PermissionGuard` لجعلها قابلة لتهيئة المالك. أولوية منخفضة.
- **`FinanceV3Controller`** — `ReportsAccess`. اختياري: فحوص دقيقة `finance.dashboard.view` / `finance.reports.export`.
- **`LabPayablesController` وأخوات المختبر** — تستعمل `PermissionGuard` بالفعل (`lab_*`). لا عمل.

---

## 5) بنود مالية أخرى من التدقيق (خارج الصلاحيات)
- **FIN-11**: حدّ اعتماد الخصم على الفواتير (مثل ما فعلنا بحدّ المصروفات) → مفتاح `finance.invoices.max_discount` أو إعادة استخدام `finance.max_discount_percentage` الموجود.
- **FIN-14/20**: تدقيق (audit logging) على مسارات مالية ناقصة.
- **Phase 7 (مؤجّل/كبير)**: إزالة كتابة `CashFlowTransaction` المزدوجة بعد التحقق من `JournalLine` في الإنتاج — لا تبدأه دون توجيه صريح.

---

## 6) سير العمل والتحقّق (إلزامي لكل PR)
- **البرانش:** `claude/wizardly-carson-qys671` (أو برانش مخصّص للمهمة إن أذن المالك). لا تدفع لبرانش آخر دون إذن.
- **لكل متحكّم = commit/PR مستقل** (تجنّب PR ضخمًا على مسار المال؛ سهّل المراجعة).
- **البناء:** `cd backend && export PATH="$HOME/.dotnet:$PATH" && dotnet build AqlanDentalPro.sln -c Release` → 0 أخطاء.
- **الاختبار:** `dotnet test tests/AqlanDentalPro.UnitTests/AqlanDentalPro.UnitTests.csproj -c Release` → كل الاختبارات خضراء (كانت 1872 بعد #516).
- أضِف لكل متحكّم **اختبار تفعيل مخصّص** (allow + deny + admin-bypass) على نمط `PaymentsPermissionEnforcementTests`.
- CI أخضر قبل الدمج. لا تفتح PR إلا بطلب صريح.

## 7) القيود الصارمة (من CLAUDE.md)
- لا توسّع `ReportsAccess` عالميًا. الاستقبال يبقى cashier-safe فقط (ما لم يمنحه المالك من الإعدادات).
- لا hardcoding للقيم المالية (Settings). كل 4xx/5xx برسالة `message` عربية. لا تسريب تفاصيل الاستثناءات في HTTP.
- لا تحذف هجرات. لا تُعِد الكتابة على بذور الصلاحيات (INSERT-ONLY). الزرع الجديد للمفاتيح يكون INSERT-ONLY أيضًا.
- إن لمست الواجهة: إظهار/إخفاء حسب الصلاحية فقط.
