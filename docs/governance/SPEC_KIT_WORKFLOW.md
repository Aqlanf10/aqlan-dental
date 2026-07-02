# SpecKit Workflow

القاعدة: No implementation without spec.

## خطوات العمل

1. اقرأ `.specify/constitution.md`.
2. اقرأ `specs/000-master-system/requirements.md`.
3. اقرأ `specs/000-master-system/module-map.md`.
4. اختر feature spec المناسب.
5. حدّث requirements إذا كان السلوك المطلوب غير موثق.
6. حدّث design وحدد الملفات المسموحة والممنوعة.
7. اكسر العمل إلى tasks صغيرة.
8. نفذ task صغير واحد.
9. شغل الاختبارات المناسبة أو اكتب لماذا لم تعمل.
10. إذا تغير السلوك، حدّث spec.
11. افتح PR يذكر spec folder وrequirement IDs.

## عند الشك

إذا لم تعرف owner module أو controller أو service أو DTO أو permission، توقف واكتب تقريرا. التقرير أفضل من كود خاطئ.

## قبل PR

- تأكد أن `CLAUDE.md` وSpecKit متسقان.
- تأكد أن runtime code لم يتغير خارج scope.
- تأكد أن migrations لم تتغير إلا بموافقة spec قوية.
- تأكد من عدم لمس secrets.
- ضع `Needs runtime verification` لأي سلوك لا يمكن إثباته بالقراءة.
