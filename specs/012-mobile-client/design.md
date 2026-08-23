# 012 Mobile Client Design

## الملكية والحدود

- المالك: تطبيق Expo Router داخل `mobile/app/` ومكوّناته داخل `mobile/src/`.
- البيانات: `mobile/src/lib/api.ts` فوق API النظام الحالي فقط.
- الجلسة: `mobile/src/auth/SessionProvider.tsx` و`tokenStore.ts`.
- الهوية: endpoint العام القائم `/api/public/website-settings` مع assets الرسمية المحلية
  كبديل موثوق عند انقطاع الشبكة.

## نظام التصميم

- Primary/Navy: `#1a3a5c`
- Secondary/Blue: `#3d7ab5`
- Accent/Orange: `#f5922e`
- Background: `#f0f5fb`
- أسطح بيضاء، حدود هادئة، نصف قطر 12–24، ظلال Android/iOS خفيفة.
- اتجاه RTL صريح للنصوص والصفوف، وتباين لا يقل عن الاستخدام العملي على الهاتف.
- الشعار الرسمي هو `mobile/assets/logo.png`؛ الأبيض للخلفيات الداكنة؛ الأيقونة
  `mobile/assets/icon.png`.

## الثبات والاسترداد

`GestureHandlerRootView` يلف التطبيق قبل SessionProvider. ويضاف Error Boundary أعلى
الملاحة يعرض رسالة آمنة، تفاصيل تقنية محلية قابلة للتحديد، وزري إعادة المحاولة والعودة
لتسجيل الدخول. لا يرسل سجلات أو بيانات مرضى خارجيًا.

## التنقل

تبقى أسماء ومسارات Expo Router الحالية. يُعاد تصميم Tabs فقط، ولا تضاف مساحة تشغيل أو
API موازية. العناصر المخفية تبقى شاشات داخلية لا تبويبات جديدة.

## الملفات المسموحة لهذه الشريحة

- `mobile/**`
- `specs/012-mobile-client/**`
- `specs/000-master-system/module-map.md`
- `docs/mobile/MOBILE_V1_EXECUTION.md`

## الملفات الممنوعة

- `backend/**`, `frontend/**`, migrations، إعدادات النشر، سياسات التفويض، قواعد المالية
  أو المنطق السريري.

## التحقق

- فحص TypeScript.
- Expo export لـAndroid وiOS مع رابط API الإنتاجي.
- بناء APK Release مع التحقق من وجود `index.android.bundle` داخل الحزمة.
- اختبار جهاز فعلي لكل تبويب رئيسي. هذا الأخير `Needs runtime verification` حتى يختبره المالك.

