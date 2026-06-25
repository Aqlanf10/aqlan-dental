# قائمة تنفيذ تقرير الفحص — Aqlan Dental Pro (الحالة النهائية)

> مرجع: `docs/agent-audit/AUDIT-REPORT-2026-06-24.md`
> تتبّع تنفيذ بنود التقرير على Sprintات متتابعة، كل سبرنت = فرع + PR مستقل + CI أخضر + دمج.
> الحالات: ⬜ Pending · 🔄 In Progress · ✅ Done · ⛔ Not Applicable · 🔬 Needs runtime verification · ⏸️ Deferred
>
> **آخِر تحديث: Sprint 18-19 — PR #545. كل السبرنتات الـ 19 مُغلَقة.**

## ملاحظات تشغيلية (حواجز معروفة)
- **الدمج:** الجلسة تعمل بحساب المالك (Aqlanf10)؛ الدمج متاح إن مرّت الفحوص المطلوبة. إن منع حماية الفرع الدمج، يُوثَّق ويُكمَل بقية العمل.
- **التحقّق المحلي:** Backend `dotnet build/test -c Release` (مُتحقَّق محليًا)، Frontend `npm run lint/build/test`. CI يحجب الدمج.
- **القيود الصارمة:** لا إعادة بناء، لا حذف CashFlowTransaction، لا hardcoding للهوية، لا إضعاف صلاحيات/أمان لتمرير اختبار، لا تعديل خط أساس الهجرات أعمى.

---

## الجدول الرئيسي النهائي (Sprint → بند التقرير → الحالة → الدليل → PR → الاختبارات)

| # | Sprint | البند (من التقرير) | الخطورة | الحالة | الدليل (ملفات) | PR | الاختبارات |
|---|---|---|---|---|---|---|---|
| 1 | 1 | تزامن `Treasury.Balance` (تحديث ضائع) — §5.1/§11 | Critical | ✅ + 🔬 | `TreasuryResolutionService.cs` (قفل صفّ/تحديث ذرّي) | #529 | اختبارات تزامن دفعتين/مصروفين/تحويل |
| 2 | 2 | حارس الرصيد السالب «تحذيري» افتراضيًا — §5.2/§11 | High | ✅ | `TreasuryResolutionService.cs`، مفتاح `finance.prevent_negative_treasury_balance` | #530 | منع السالب عند التفعيل + احترام وضع التحذير |
| 3 | 8 | توسيع اختبارات التقويم والسيفالو — §15 | High | ✅ | `tests/.../Ortho`, `tests/.../Ceph` (ملفات اختبار جديدة) | #537 | تغطية مسارات P3+ + توليد التقارير |
| 4 | 11A/B | تقسيم الصفحات العملاقة — §7/§14 | High | ✅ | `settings/page.tsx` (2577→تبويبات)، `daily-operations/page.tsx` (2238→_modules) | #540 / #541 | بناء أخضر + سلوك ثابت + tsc 0 + lint 0 |
| 5 | 10 | أساس نظام التصميم (مكوّنات + توكنات) — §3/§7/§14 | High | ✅ | `components/ui/*`، design tokens | #539 | lint/type/build |
| 6 | 3 | إكمال الصلاحيات الدقيقة (FinanceV3/Commissions/Suppliers/Advances) — §5.6/§11/§13 | Medium | ✅ | `FinanceV3Controller*.cs`, `CommissionsController.cs`, `FinanceV3SuppliersController.cs`, `AdvancePaymentController.cs` + `[PermissionGuard]` | #531 | منع غير المصرّح + سماح المصرّح + تجاوز الأدمن |
| 7 | 4 | تسريب `ex.Message` الخام — §5.7/§13 | Medium | ✅ | ClinicQueue/AiSettings/Commissions/Backup/LabPayables/Patients controllers (لُفَّت في ServiceException) | #532 | استجابات آمنة عربية + اختبار يحظر التسريب |
| 8 | 9 | ازدواجية مسار السيفالو (`/ceph` عام + داخل `/ortho/[id]`) — §5.8/§12 | Medium | ✅ | `Sidebar.tsx`، `ceph/[id]/page.tsx` (عام = عرض فقط؛ التحرير من داخل الحالة) | #524 / #538 | بناء الواجهة + مراجعة المسارات |
| 9 | 16 | تحسين متابعة زيارات التقويم + مزامنة OrthoVisit↔Visit — §9/§10 | Medium | ✅ + 🔬 | `OrthoService.cs` (`AddVisitAsync` — توثيق CLIN-05 atomic mirror)، `OrthoOverviewTab.tsx` (شارات التأخر) | #544 | توثيق + شارات UI؛ التحقق وقت التشغيل |
| 10 | 5 | تشديد CSP (`unsafe-inline/eval`) + 2FA — §5.10/§13 | Medium | ✅ (CSP) / ⏸️ (2FA) | `SecurityHeadersMiddleware.cs` (CSP مُشدَّد) | #533 | فحص الرؤوس الأمنية؛ 2FA مؤجَّل كميزة كبيرة |
| 11 | 14 | عكس/تدقيق العمولة عند الاسترداد — §5.11/§11 | Medium | ✅ | `FinanceService.cs` (`RefundPaymentAsync` idempotency + `LogCommissionAdjustmentWarningAsync`) | #543 | استرداد بعد عمولة + idempotency (9 اختبارات) |
| 12 | 6 | سير اعتماد السيفالو قبل PDF — §12 | High | ✅ | `CephAnalysis.IsApproved` + صلاحية المعتمِد + حظر PDF قبل الاعتماد | #534 | لا PDF قبل الاعتماد + صلاحية المعتمِد |
| 13 | 7 | جودة بيانات السيفالو + عرض ثقة المعالم — §12 | Medium | ✅ | `CephLandmark.Confidence`، ceph readiness/UI، تحقّق المعايرة | #536 | منطق الجاهزية + تحذيرات + اختبارات |
| 14 | 9 | تشذيب السايد بار/الملاحة — §8 | Medium | ✅ | `Sidebar.tsx` (8 أقسام موحّدة)، حذف المسارات المكرّرة | #524 / #538 | بناء الواجهة + مراجعة المسارات |
| 15 | 17 | انعكاس أيقونات RTL — §3/§7 | Low | ✅ | `frontend/src/lib/rtlIcons.ts` + 5 مواضع تنقّح اتجاهي | #544 | بناء الواجهة + tsc 0 |
| 16 | 13 | توحيد عميل API (`portalApi` drift) — §8/§14 | Medium | ✅ | `lib/apiClient.ts` (factory مشتركة)، `api.ts` + `portalApi.ts` (يستوردان factory) | #543 | بناء + smoke مصادقة + vitest 166/166 |
| 17 | 15 | لوحة مالية لحالة التقويم — §9/§10 | Medium | ✅ | `OrthoFinanceTab.tsx` (67→292 سطرًا: رسوم/مُحصَّص/متبقّي/آخر دفعة) | #544 | يستهلك endpoint قائم؛ احترام صلاحية canViewPatientFinance |
| 18 | 12 | تشذيب متحكّمات/خدمات الخلفية — §6/§14 | Medium | ✅ (جزئي) | `LabOrdersController.cs` (مُعالَج)؛ FinanceService + OrthoCasesController مؤجَّلة | #542 | عقود API ثابتة + اختبارات (LabOrders) |
| — | 18 | مراجعة TODO/FIXME + الـ stubs — §14 | Low | ✅ | `docs/agent-audit/TECH-DEBT-REGISTER.md` (هذا السبرنت) | #545 | وثائق + إزالة تعليق تاريخي واحد |
| — | 19 | تمريرة تحقّق نهائية + تقرير الإنجاز | — | ✅ | `docs/agent-audit/AUDIT-COMPLETION-REPORT.md` (هذا السبرنت) | #545 | بناء/اختبار نهائي |

---

## بنود «لا تُغيَّر الآن» (من §19 — تبقى كما هي عمدًا) — ⛔
- ⛔ بنية Clean Architecture — سليمة.
- ⛔ آلية خط أساس الهجرات (`EnsureFreshDatabaseMigratedAsync`) — لا تُضَف `[Migration]`.
- ⛔ القيد المالي المزدوج (CashFlow + Journal) — يبقى حتى خطة Phase 7 منفصلة.
- ⛔ زرع الصلاحيات INSERT-ONLY — لا إعادة كتابة.
- ⛔ هوية التقارير من Settings — لا hardcoding.
- ⛔ عزل وصول الطبيب (`PatientAccessFilter`) — لا توسيعه عشوائيًا.
- ⛔ محرّك السيفالو (24 معلمًا / 7 تحاليل) — أساس متين؛ ابنِ عليه الاعتماد والتدقيق قبل الـ AI.

## بنود تحتاج تحقّقًا وقت التشغيل (🔬)
- 🔬 تزامن الخزينة الفعلي على PostgreSQL/Railway (Sprint 1 يضيف الحماية؛ التأكيد النهائي وقت تشغيل). **PR #529.**
- 🔬 مزامنة OrthoVisit↔Visit (Sprint 16 — وُثِّقت، التحقق النهائي وقت تشغيل). **PR #544.**
- 🔬 سلوك `portalApi` مع المصادقة الحيّة (Sprint 13 — factory موحَّد، التحقق النهائي وقت تشغيل). **PR #543.**

## بنود مؤجَّلة بقرار صريح (⏸️)
- ⏸️ **2FA للطاقم** — Sprint 5 لاحظ التأجيل؛ ميزة كبيرة (TOTP + Backup codes + UI)، ليست إصلاحًا سريعًا. CSP شُدِّد.
- ⏸️ **استخراج `FinanceService`** (1911 سطرًا) — god-service قائمة؛ الاستخراج بأمان يحتاج عدة PRs. LabOrdersController فقط عولج.
- ⏸️ **استخراج `OrthoCasesController`** (2264 سطرًا) — نفس المبدأ.
- ⏸️ **إزالة الكتابة المزدوجة `CashFlowTransaction`** — Phase 7 مؤجَّل صراحةً في CLAUDE.md.
- ⏸️ **الكشف الآلي للمعالم السيفالومترية (Vision AI)** — CEPH-EPIC C-D؛ يتطلب نموذج رؤية مخصص.

---

## إحصاءات الإغلاق النهائية

| الفئة | العدد |
|---|---|
| إجمالي بنود التقرير القابلة للتنفيذ (الجدول) | 18 |
| ✅ Done | 17 (منها 3 🔬 يحتاج تحقّقًا وقت التشغيل، 1 جزئي) |
| ⏸️ Deferred (بقرار موثّق) | 5 (2FA + 3 استخراجات + Phase 7 + Ceph AI) |
| ⛔ Not Applicable (لا تُغيَّر) | 7 (بنود §19) |
| ⬜ Pending | 0 |

**كل السبرنتات الـ 19 مُغلَقة.** النظام الآن في وضع «صالح للاعتماد الكامل المشروط» —
تشغيل إنتاجي في المسارات اليومية + تحقّق وقت التشغيل لـ 3 عناصر (تزامن الخزينة،
مزامنة الزيارات، portalApi حيّ) + تنفيذ العناصر المؤجَّلة عند توفّر النطاق.

---

## سجل التقدّم
- **Sprint 0** — ✅ قائمة التتبّع + إحضار التقرير إلى main. **PR #528.**
- **Sprint 1** — ✅ تحصين تزامن الخزينة. **PR #529.**
- **Sprint 2** — ✅ حارس الرصيد السالب. **PR #530.**
- **Sprint 3** — ✅ إكمال الصلاحيات الدقيقة. **PR #531.**
- **Sprint 4** — ✅ لفّ `ex.Message`. **PR #532.**
- **Sprint 5** — ✅ تشديد CSP (2FA مؤجَّل). **PR #533.**
- **Sprint 6** — ✅ سير اعتماد السيفالو. **PR #534.**
- **Sprint 7** — ✅ جودة بيانات السيفالو. **PR #536.**
- **Sprint 8** — ✅ اختبارات التقويم/السيفالو. **PR #537.**
- **Sprint 9** — ✅ تشذيب السايد بار + توحيد مساحة السيفالو. **PR #524 / #538.**
- **Sprint 10** — ✅ أساس نظام التصميم. **PR #539.**
- **Sprint 11A/B** — ✅ تقسيم الصفحات العملاقة (settings + daily-ops). **PR #540 / #541.**
- **Sprint 12** — ✅ تشذيب LabOrdersController (FinanceService + OrthoCasesController مؤجَّلة). **PR #542.**
- **Sprint 13** — ✅ توحيد عميل API. **PR #543.**
- **Sprint 14** — ✅ عكس/تدقيق العمولة عند الاسترداد. **PR #543.**
- **Sprint 15** — ✅ لوحة مالية لحالة التقويم. **PR #544.**
- **Sprint 16** — ✅ متابعة زيارات التقويم + توثيق المزامنة. **PR #544.**
- **Sprint 17** — ✅ انعكاس أيقونات RTL. **PR #544.**
- **Sprint 18** — ✅ مراجعة TODO/FIXME + الـ stubs. **PR #545.**
- **Sprint 19** — ✅ تمريرة تحقّق نهائية + تقرير الإنجاز. **PR #545.**

*انتهى تتبّع التنفيذ — جميع بنود التقرير الأصلي مُغلَقة أو مُؤجَّلة بقرار موثّق.*
