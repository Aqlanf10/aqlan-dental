# سجل الديون التقنية — Sprint 18 (TECH-DEBT-REGISTER)

> مرجع: `docs/agent-audit/AUDIT-REPORT-2026-06-24.md` (الفحص الأصلي)
> تكميلي لـ `docs/agent-audit/technical-debt.md` (سابق تدقيق 2026-06-12).
> يصنّف كل علامة `TODO|FIXME|HACK|PLACEHOLDER` وكل stub مسار متبقٍ في الكود.
> آخِر تحديث: Sprint 18 — PR #545.

---

## 1. منهجية المسح

```bash
rg -n "TODO|FIXME|HACK|PLACEHOLDER" frontend/src/ backend/src/ \
  --type ts --type cs 2>/dev/null
# + تمريرة ثانية بدون قيد type لضمان شمولية البحث
```

**النتيجة:** 4 ملفات فقط تحوي علامات فعلية (21 ظهور إجمالي):
- `backend/src/AqlanDentalPro.Infrastructure/Data/Seed/CephNormSeeder.cs` — 18 ظهورًا
- `backend/src/AqlanDentalPro.Application/Services/AuthService.cs` — 1 ظهور
- `backend/src/AqlanDentalPro.API/Controllers/CashierSessionsController.cs` — 1 ظهور
- `frontend/src/app/(dashboard)/doctor-clinic/_components/Modals.tsx` — 1 ظهور (تاريخي، أُزيل هذا السبرنت)

> ملاحظة: التقرير الأصلي §14 ذكر «96 ملفًا فيها TODO/FIXME/placeholder» — الرقم
> كان يشمل سمة `placeholder=` HTML (نصوص إدخال عربية) وليس علامات ديون تقنية.
> المسح الدقيق هنا على `TODO/FIXME/HACK/PLACEHOLDER` ككلمات مفتاحية يُرجع 4 ملفات
> فقط. لا يوجد دين تقني مخفي غير مُتتبَّع.

---

## 2. تصنيف العلامات المتبقية

كل علامة مُتبقية وُضعت في إحدى ثلاث فئات:

### ✅ فئة A — «تبقى عمدًا، مرتبطة بعمل مستقبلي مُتتبَّع»

| الملف:السطر | النص المختصر | لماذا يُترك | أين يُتتبَّع |
|---|---|---|---|
| `CephNormSeeder.cs` (18 ظهورًا) | `TODO: verify against the clinic owner's reference population` | معايير سيفالو افتراضية من أدبيات Bishara/Steiner/Tweed؛ المالك (أخصائي تقويم) عليه التحقق منها ضد مرجعه السكاني. هذا عمل سريري/إحصائي وليس كودًا. | CEPH-EPIC (`docs/ortho-module/CEPH-EPIC.md`) — يبقى حتى يُحدّد المالك معاييره |
| `AuthService.cs:195` | `SEC-02 TODO: Legacy hash format from Phase 1 (fixed global salt, DOP=1) — MUST be removed once all users have been migrated to per-user salts` | الترحيل تلقائي على تسجيل الدخول (`VerifyPasswordWithMigrationFlag`) وعند `ChangePasswordAsync`. القاعدة: «زرع الصلاحيات INSERT-ONLY لا إعادة كتابة» — نفس المنطق ينطبق على تجزئة كلمات المرور. | SEC-02 — التتبع عبر سجل الإهمال (deprecation log) المذكور في السطور 197–201 |
| `CashierSessionsController.cs:485` | `TODO: Once the CashFlowTransaction dual-write is removed, delete this endpoint and route Reception through V3 with an appropriate policy` | القيد المالي المزدوج (CashFlow + Journal) مقصود حتى يكتمل التحقق الإنتاجي من JournalLine — Phase 7 مؤجّل صراحةً في CLAUDE.md (`لا تحذف CashFlowTransaction`). | Phase 7 — مؤجَّل صراحةً (§19 من التقرير) |

### 🗑️ فئة B — «عفا عليه الزمن، أُزيل هذا السبرنت»

| الملف:السطر | النص المختصر | الإجراء |
|---|---|---|
| `doctor-clinic/_components/Modals.tsx:1271` (سابقًا) | `FE-14: the previous 'TODO: Connect to lab order API when backend is ready' comment and the 'سيتم حفظ طلب المختبر كملاحظة سريرية' info note were STALE and misleading — the backend IS ready...` | تاريخي بحت (يصف إصلاحًا سابقًا). اختُصر التعليق ليصف الحالة الراهنة فقط — «Lab Order Panel — creates a real lab order via /api/lab-orders (createLabOrderMutation in doctor-clinic/page.tsx).» |

### 📋 فئة C — «يُحوَّل إلى متابعة/قضية» (لا شيء)

لا توجد علامات تستحق التحويل إلى قضية GitHub منفصلة — كل العلامات إما فئة A
(مُتتبَّعة أصلاً في سجل المشروع) أو فئة B (عفا عليها الزمن، أُزيلت).

---

## 3. مراجعة stubs إعادة التوجيه (`/clinic-queue` + `/patient-journey`)

أُنشئت هذه الـ stubs في PR #524 (تحرير السايد بار/الملاحة — Sprint 9 جزئيًا)،
وأُكِّدت في PR #538 (NAV-CEPH-FIX — Sprint 9). المساران موجودان كمكوّنات خادم
رقيقة (thin server components) تستدعي `redirect()` من `next/navigation`:

### `/clinic-queue/page.tsx`
```tsx
export default function ClinicQueueRedirectPage() {
  redirect("/daily-operations?tab=queue");
}
```
- يحوّل المسار القديم (1,064 سطر سابقًا) إلى مساحة العمل الموحّدة `/daily-operations?tab=queue`.
- واجهة برمجة تطبيقات الـ backend (`/api/clinic-queue/*`) لم تُمسَّس — تستهلكها `ClinicQueueView` داخل daily-operations مباشرة.
- أي روابط خارجية أو إشارات مرجعية قديمة لا تنكسر.

### `/patient-journey/page.tsx` + `/patient-journey/[patientId]/page.tsx`
```tsx
export default function PatientJourneyRedirectPage() { redirect("/daily-operations"); }
// و
export default async function PatientJourneyRedirectPage({ params, searchParams }) {
  // ...
  redirect(`/patients/${patientId}?${query.toString()}`);  // ?focus=journey
}
```
- `/patient-journey` (الفهرس) → `/daily-operations`.
- `/patient-journey/[patientId]` → `/patients/[patientId]?focus=journey` (مع الحفاظ على search params).
- الخطّاف `usePatientJourney` (`src/hooks/usePatientJourney.ts`) لم يُمسَّس — يُستهلك من daily-operations وصفحات أخرى.

### الحكم: آمنة للإبقاء ✅

**القرار:** إبقاء الـ stubs إلى أجل غير مسمّى. أسباب:
1. **توافق الروابط:** يمنع كسر الإشارات المرجعية القديمة وروابط رسائل واتساب المُرسلة سابقًا.
2. **التكلفة صفر:** كل ملف = 1 استدعاء `redirect()` (لا منطق، لا DB، لا client bundle).
3. **الأمان:** `redirect()` في Next.js App Router يُعيد 307 — لا يُكشف أي معلومات.
4. **التوثيق:** كل ملف يحمل تعليق `NAV-CEPH-FIX` يشرح سبب الوجود.

### ملاحظة متابعة (Low Priority — TD-REG-01)

الدليل `patient-journey/_components/` (Cards.tsx 949 سطرًا + Modals.tsx 873 سطرًا)
و `patient-journey/_lib/constants.ts` (61 سطرًا) — قائمة بقاء بعد تحويل الصفحة
إلى stub. فحص الاستيراد:

```bash
rg "from ['\"].*patient-journey" frontend/src/   # لا نتائج
rg "patient-journey/_components|patient-journey/_lib" frontend/src/
# → مرجع تعليقي واحد فقط في PaymentModal.tsx:7 (ليس import فعلًا)
```

هذه الملفات **يبدو أنها كود ميت**، لكن مهمة هذا السبرنت تمنع حذف كود الإنتاج
(«لا تُحذف ميزات قائمة» في CLAUDE.md). يُتْرَك الحذف لسبرنت متابعة منفصل بعد
تشخيص أدق (ربما تُستهلك عبر path alias غير ظاهر، أو يحتاجها build للتوليد
الثابت). مُسجَّل هنا فقط — لا يُحذف في هذا PR.

---

## 4. العناصر المؤجَّلة (من خطة §17/§19 — تبقى صراحةً)

هذه ليست TODOs داخل الكود، بل قرارات تأجيل موثّقة في CLAUDE.md والتقرير الأصلي:

| العنصر | أين مؤجَّل | السبب |
|---|---|---|
| **2FA للطاقم** | Sprint 5 (ملاحظة التأجيل) | ميزة كبيرة (TOTP/Backup codes/UI) — ليست إصلاحًا سريعًا. CSP شُدِّد في PR #533 (Sprint 5). |
| **استخراج `FinanceService`** (1911 سطرًا) | Sprint 12 (ملاحظة التأجيل) | god-service قائمة؛ الاستخراج بأمان يحتاج عدة PRs متتابعة. LabOrdersController فقط عولج في PR #542 (Sprint 12). |
| **استخراج `OrthoCasesController`** (2264 سطرًا) | Sprint 12 (ملاحظة التأجيل) | نفس المبدأ — تقسيم بأمان عبر PRs منفصلة بعد تأمين عقود الـ API باختبارات. |
| **إزالة الكتابة المزدوجة `CashFlowTransaction`** | Phase 7 (CLAUDE.md صريح) | القيد المزدوج مقصود حتى يكتمل التحقق الإنتاجي من JournalLine. «لا تحذف CashFlowTransaction». |
| **الكشف الآلي للمعالم السيفالومترية (Vision AI)** | CEPH-EPIC C-D | يتطلب نموذج رؤية مخصص — placeholder صريح «قريبًا» في `ceph/[id]/page.tsx:433`. |

---

## 5. خلاصة Sprint 18

| الفئة | العدد | الإجراء |
|---|---|---|
| علامات TODO/FIXME كلية | 4 ملفات / 21 ظهور | — |
| فئة A (تبقى عمدًا) | 3 ملفات / 20 ظهورًا | مُتتبَّعة في سجلات قائمة |
| فئة B (عفا عليها الزمن، أُزيلت) | 1 ملف / 1 ظهور | أُزيل التعليق التاريخي في `doctor-clinic/Modals.tsx` |
| فئة C (تحويل إلى قضية) | 0 | لا شيء |
| Stubs المسارات (`/clinic-queue`, `/patient-journey`) | 3 ملفات redirect | أُكِّدت آمنة — تُترك للأبد |
| عناصر مؤجَّلة بقرار | 5 عناصر | موثّقة في §4 + CLAUDE.md |

**الخلاصة:** لا توجد علامات دين تقني صامتة في الكود. كل علامة `TODO` متبقية
مرتبطة بعمل مستقبلي مُتتبَّع صراحةً (CEPH-EPIC، SEC-02، Phase 7)، أو معايير
سريرية يملؤها المالك، أو تعليق تاريخي وُضع في سياقه الصحيح.

*انتهى سجل Sprint 18.*
