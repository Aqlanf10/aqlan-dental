# P2 Implementation Brief — Records & Images (مُولّد من تحليل الفجوات 2026-06-12)
المرجع الكامل في سجل الجلسة. الخلاصة التنفيذية:
- OrthoClinicalPhotos: إضافة Category, Subtype, TreatmentPhase (Initial/Progress/Final), IsSelectedForReport + فهرس مركب
- Radiographs/Documents: إضافة OrthoCaseId nullable FK (الواجهات في P2b)
- Checklist auto-derive: مطابقة (Category,Subtype) بدل البحث في الكابشن
- المقارنة قبل/بعد: تفضيل وسم المرحلة على التاريخ
- رفع الصور: قوائم فئة/نوع فرعي/مرحلة + خانة (للتقرير)
- هجرة واحدة إضافية فقط — يجب أن تحمل سمة [Migration] وملف Designer وتحديث Snapshot (عكس الهجرات القديمة المكسورة)
