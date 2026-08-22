import json, subprocess, collections, io

ALL_STAFF = ["Admin","Reception","Accountant","Orthodontist","GeneralDentist","OralSurgeon","Assistant","BranchManager"]
pol = json.load(open("/tmp/policies.json")); pol["StaffOnly"]=list(ALL_STAFF); pol["(none)"]=list(ALL_STAFF)
rows = json.load(open("/tmp/matrix.json"))

out = subprocess.run(["psql","-h","127.0.0.1","-p","5433","-U","postgres","-d","golive","-tA","-F","|","-c",
  'SELECT "Role","Resource","CanView","CanCreate","CanEdit","CanDelete" FROM "RolePermissions";'],
  capture_output=True, text=True, env={"PGPASSWORD":"postgres","PATH":"/usr/bin:/bin"})
grant={}
for line in out.stdout.strip().splitlines():
    p=line.split("|")
    if len(p)<6: continue
    for act,v in zip(("view","create","edit","delete"), p[2:6]): grant[(p[0],p[1],act)] = (v=="t")

removed=collections.defaultdict(set)
for r in rows:
    if r["permission_checked"]: continue
    for role in pol.get(r["policy"], ALL_STAFF):
        if role=="Admin": continue
        k=(role,r["resource"],r["action"])
        if k in grant and not grant[k]:
            removed[role].add((r["resource"],r["action"]))

b=io.StringIO()
w=b.write
w("# ما الذي تتحكم به شاشة الصلاحيات فعليًا — GOLIVE-PERM-001\n\n")
w("مولَّد من الكود ومن قاعدة بيانات البروفة، لا مكتوبًا يدويًا. أعِد توليده بعد أي تغيير.\n\n")
w("## الخلاصة\n\n")
tot=sum(1 for r in rows if not r["permission_checked"])
w(f"- نقاط النهاية في الموارد المعروضة بالشاشة: **{len(rows)}**\n")
w(f"- منها لا يقرأ مفتاحه أي حارس على الخادم: **{tot}**\n")
w("- الحماية الفعلية لهذه المسارات تأتي من **سياسة الدور** (`[Authorize(Policy=…)]`)، لا من المفتاح.\n")
w("  فالمفتاح المُطفأ يخفي الزر في الواجهة، ولا يمنع الطلب إذا أُرسل مباشرة.\n\n")
w("### أُثبت حيًا على نسخة البروفة\n\n")
w("| الدور | العملية | المفتاح | النتيجة قبل الإصلاح |\n|---|---|---|---|\n")
w("| استقبال | حذف موعد | `appointments.delete` مُطفأ | **200 — حُذف الموعد** (أُصلح: 403) |\n")
w("| استقبال | تعديل مريض | `patients.edit` مُطفأ | **200 — عُدّل السجل** (قرار المالك: يُمنح المفتاح) |\n\n")
w("## ما نُفِّذ (قرار المالك 2026-08-23)\n\n")
w("- **الطابور: الحماية مفعّلة** على 13 نقطة — نداء، إعادة نداء، بدء، دخول غرفة، إنهاء،\n")
w("  عدم حضور، تنبيه، إعادة ترتيب، أولوية، غرفة، وصول، إنشاء، إلغاء.\n")
w("- **مُنِحت قبل التفعيل** حتى لا يفقدها أحد: `visits.edit` للأطباء الثلاثة،\n")
w("  `patients.edit` للاستقبال، والطابور والمواعيد ورحلة المريض للمساعد.\n")
w("- **سُحبت بقرارك:** المحاسب لم يعد يحجز المواعيد أو يبدأ الزيارات.\n")
w("- **الأطباء لم يُمنحوا تحريك الطابور** — الاستقبال يديره. يبقى للطبيب بدء الزيارة\n")
w("  عبر `patient_journey`، فلا ينكسر عمله.\n")
w("- **البذرة لم تعد تُعيد الكتابة عند كل إقلاع** — ما تغيّره في الشاشة يبقى بعد النشر.\n\n")
w("## أثر التفعيل الكامل — ماذا يفقد كل دور (المتبقي)\n\n")
w("هذه القدرات متاحة اليوم بحكم سياسة الدور، ومفاتيحها مُطفأة. تفعيل الحماية **يسحبها**.\n\n")
for role in sorted(removed, key=lambda r:-len(removed[r])):
    items=sorted(removed[role])
    w(f"### {role} — {len(items)} صلاحية\n\n")
    for res,act in items: w(f"- `{res}.{act}`\n")
    w("\n")
w("## لماذا لا يمكن التفعيل آليًا\n\n")
w("من 11 نقطة POST تحت `api/clinic-queue`، **واحدة فقط** تُنشئ شيئًا. الباقي انتقالات حالة\n")
w("(نداء، إعادة نداء، دخول غرفة، إنهاء، عدم حضور). اشتقاق الإجراء من فعل HTTP يضع\n")
w("«من ينادي المريض» خلف مفتاح مكتوب عليه «إنشاء» على «الطابور».\n\n")
w("والواجهة كانت قد اخترعت ترجمتها الخاصة وناقضت نفسها: النداء→`create`،\n")
w("إعادة النداء→`edit`، دخول الغرفة→`approve`.\n\n")
w("لذلك كُتبت الترجمة صراحةً مرة واحدة في `contracts/permission-action-map.json`،\n")
w("ويقرأها الطرفان. المفردات مثبّتة على الأعمدة الستة في `RolePermission` — إضافة فعل\n")
w("جديد مثل «نداء» تتطلب هجرة، ولا تستحقها.\n\n")
w("## مفاتيح لا أثر لها بحكم التصميم\n\n")
w("- `patients.delete` — لا يصل الحذف إلا المدير، والمدير يتجاوز `PermissionGuard`.\n")
w("  فالمفتاح ظاهر ولا يقرر شيئًا. تُرك ظاهرًا ولم يُضَف له حارس ميت.\n")
open("docs/audits/PERMISSIONS_ENFORCEMENT_2026-08-23.md","w",encoding="utf-8").write(b.getvalue())
print("written", len(b.getvalue()), "chars")
