# قائمة تنفيذ تقرير الفحص — Aqlan Dental Pro

> مرجع: `docs/agent-audit/AUDIT-REPORT-2026-06-24.md`
> تتبّع تنفيذ بنود التقرير على Sprintات متتابعة، كل سبرنت = فرع + PR مستقل + CI أخضر + دمج.
> الحالات: ⬜ Pending · 🔄 In Progress · ✅ Done · ⛔ Not Applicable · 🔬 Needs runtime verification

## ملاحظات تشغيلية (حواجز معروفة)
- **الدمج:** الجلسة تعمل بحساب المالك (Aqlanf10)؛ الدمج متاح إن مرّت الفحوص المطلوبة. إن منع حماية الفرع الدمج، يُوثَّق ويُكمَل بقية العمل.
- **التحقّق المحلي:** Backend `dotnet build/test -c Release` (مُتحقَّق محليًا)، Frontend `npm run lint/build/test`. CI يحجب الدمج.
- **القيود الصارمة:** لا إعادة بناء، لا حذف CashFlowTransaction، لا hardcoding للهوية، لا إضعاف صلاحيات/أمان لتمرير اختبار، لا تعديل خط أساس الهجرات أعمى.

---

## الجدول الرئيسي (Sprint → بند التقرير → الحالة → الدليل → PR → الاختبارات)

| Sprint | البند (من التقرير) | الخطورة | الحالة | الدليل (ملفات) | PR | الاختبارات |
|---|---|---|---|---|---|---|
| 0 | إنشاء قائمة التتبّع + إحضار التقرير إلى main | — | 🔄 | `docs/agent-audit/AUDIT-EXECUTION-CHECKLIST.md`, `AUDIT-REPORT-2026-06-24.md` | — | وثائق فقط |
| 1 | تزامن `Treasury.Balance` (تحديث ضائع) — §5.1/§11 | Critical | ⬜ | `TreasuryResolutionService.cs` | — | اختبارات تزامن دفعتين/مصروفين/تحويل |
| 2 | حارس الرصيد السالب «تحذيري» افتراضيًا — §5.2/§11 | High | ⬜ | `TreasuryResolutionService.cs`، مفتاح `finance.prevent_negative_treasury_balance` | — | منع السالب عند التفعيل + احترام وضع التحذير |
| 3 | إكمال الصلاحيات الدقيقة (FinanceV3/Commissions/Suppliers/Advances) — §5.6/§11/§13 | Medium | ⬜ | `FinanceV3Controller*.cs`, `CommissionsController.cs`, `FinanceV3SuppliersController.cs`, `AdvancePaymentController.cs` | — | منع غير المصرّح + سماح المصرّح + تجاوز الأدمن |
| 4 | تسريب `ex.Message` الخام — §5.7/§13 | Medium | ⬜ | ClinicQueue/AiSettings/Commissions/Backup/LabPayables/Patients controllers | — | استجابات آمنة عربية |
| 5 | تشديد CSP (`unsafe-inline/eval`) — §5.10/§13 | Medium | ⬜ | `SecurityHeadersMiddleware.cs` | — | فحص الرؤوس الأمنية |
| 6 | سير اعتماد السيفالو قبل PDF — §12 | High | ⬜ | `CephAnalysis.cs`, `CephController.cs`, ceph UI | — | لا PDF قبل الاعتماد + صلاحية المعتمِد |
| 7 | جودة بيانات السيفالو + عرض ثقة المعالم — §12 | Medium | ⬜ | `CephLandmark.Confidence`, ceph readiness/UI | — | منطق الجاهزية + تحذيرات |
| 8 | توسيع اختبارات التقويم والسيفالو — §15 | High | ⬜ | `tests/.../Ortho`, `tests/.../Ceph` | — | تغطية المسارات الحرجة |
| 9 | تشذيب السايد بار/الملاحة — §8 | Medium | ⬜ | `Sidebar.tsx`، مسارات stub | — | بناء الواجهة + مراجعة المسارات |
| 10 | أساس نظام التصميم (مكوّنات + توكنات) — §3/§7/§14 | High | ⬜ | `components/ui/*` | — | lint/type/build |
| 11 | تقسيم الصفحات العملاقة — §7/§14 | High | ⬜ | settings/daily-operations/Modals | — | بناء أخضر + سلوك ثابت |
| 12 | تشذيب متحكّمات/خدمات الخلفية — §6/§14 | Medium | ⬜ | Ortho/Lab/FinanceV3 controllers, FinanceService | — | عقود API ثابتة + اختبارات |
| 13 | توحيد عميل API (`portalApi` drift) — §8/§14 | Medium | ⬜ | `lib/api.ts`, `lib/portalApi.ts` | — | بناء + smoke مصادقة |
| 14 | عكس/تدقيق العمولة عند الاسترداد — §5.11/§11 | Medium | ⬜ | `FinanceService.cs` (Refund), `CommissionService.cs` | — | استرداد بعد عمولة + idempotency |
| 15 | لوحة مالية لحالة التقويم — §9/§10 | Medium | ⬜ | ortho case workspace + endpoint | — | اختبارات endpoint إن أُضيف |
| 16 | تحسين متابعة زيارات التقويم + مزامنة OrthoVisit↔Visit — §9/§10 | Medium | 🔬 | `Visit.cs`, ortho visit sync | — | اختبارات المزامنة |
| 17 | انعكاس أيقونات RTL — §3/§7 | Low | ⬜ | أيقونات Lucide الاتجاهية | — | بناء الواجهة |
| 18 | مراجعة TODO/FIXME + الـ stubs — §14 | Low | ⬜ | `docs/agent-audit/TECH-DEBT-REGISTER.md`، clinic-queue/patient-journey | — | — |
| 19 | تمريرة تحقّق نهائية + تقرير الإنجاز | — | ⬜ | `AUDIT-COMPLETION-REPORT.md` | — | بناء/اختبار نهائي |

---

## بنود «لا تُغيَّر الآن» (من §19 — تبقى كما هي عمدًا)
- ⛔ بنية Clean Architecture — سليمة.
- ⛔ آلية خط أساس الهجرات (`EnsureFreshDatabaseMigratedAsync`) — لا تُضَف `[Migration]`.
- ⛔ القيد المالي المزدوج (CashFlow + Journal) — يبقى حتى خطة Phase 7 منفصلة.
- ⛔ زرع الصلاحيات INSERT-ONLY — لا إعادة كتابة.
- ⛔ هوية التقارير من Settings — لا hardcoding.

## بنود تحتاج تحقّقًا وقت التشغيل (🔬)
- تزامن الخزينة الفعلي على PostgreSQL/Railway (Sprint 1 يضيف الحماية؛ التأكيد النهائي وقت تشغيل).
- مزامنة OrthoVisit↔Visit (Sprint 16).
- سلوك `portalApi` مع المصادقة الحيّة (Sprint 13).

---

## سجل التقدّم (يُحدَّث بعد كل دمج)
- **Sprint 0** — 🔄 قيد الإنشاء (هذا الـ PR).
