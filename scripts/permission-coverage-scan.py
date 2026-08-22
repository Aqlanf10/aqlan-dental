import re, os, json, collections

SRC = "backend/src/AqlanDentalPro.API/Controllers"
VERB2ACTION = {"HttpGet":"view","HttpPost":"create","HttpPut":"edit","HttpPatch":"edit","HttpDelete":"delete"}

# route prefix -> permission resource shown in the roles screen
ROUTE2RES = {
  "api/appointments":"appointments", "api/patients":"patients", "api/visits":"visits",
  "api/clinic-queue":"clinic_queue", "api/clinic-display":"clinic_display",
  "api/patient-journey":"patient_journey", "api/booking-requests":"booking_requests",
  "api/daily-operations":"daily_operations", "api/rooms":"rooms", "api/reports":"reports",
  "api/settings":"settings", "api/users":"users",
}

rows=[]
for fn in sorted(os.listdir(SRC)):
    if not fn.endswith(".cs"): continue
    src=open(os.path.join(SRC,fn),encoding="utf-8").read()
    m=re.search(r'\[Route\("([^"]+)"\)\]', src)
    if not m: continue
    prefix=m.group(1)
    res=ROUTE2RES.get(prefix)
    if not res: continue
    cls=re.search(r'\[Authorize\(Policy = (?:"([^"]+)"|AuthorizationPolicyNames\.(\w+))\)\]', src)
    clspol=(cls.group(1) or cls.group(2)) if cls else "(none)"

    for mm in re.finditer(r'\[(Http(?:Get|Post|Put|Patch|Delete))(?:\("([^"]*)"\))?\]', src):
        verb, sub = mm.group(1), mm.group(2) or ""
        tail = src[mm.end():]
        # stop at the next endpoint — no char cap: a guard placed after a per-patient
        # check sits well past any fixed window, and undercounting reads as a defect.
        nxt = tail.find("\n    [Http")
        if nxt>0: tail = tail[:nxt]
        mpol = re.search(r'\[Authorize\(Policy = (?:"([^"]+)"|AuthorizationPolicyNames\.(\w+))\)\]', tail)
        pol = (mpol.group(1) or mpol.group(2)) if mpol else clspol
        guarded = bool(re.search(r'CanAsync\("|PermissionGuard\.HasAsync', tail))
        rows.append({"file":fn,"route":f"{prefix}/{sub}".rstrip("/"),"verb":verb,
                     "resource":res,"action":VERB2ACTION[verb],"policy":pol,"permission_checked":guarded})

by_res=collections.defaultdict(lambda: collections.defaultdict(list))
for r in rows: by_res[r["resource"]][r["action"]].append(r)

print(f"{'resource':<18}{'action':<8}{'endpoints':>10}{'checked':>9}{'unchecked':>11}")
print("-"*58)
tot_u=0
for res in sorted(by_res):
    for act in ("view","create","edit","delete"):
        eps=by_res[res][act]
        if not eps: continue
        c=sum(1 for e in eps if e["permission_checked"]); u=len(eps)-c; tot_u+=u
        print(f"{res:<18}{act:<8}{len(eps):>10}{c:>9}{u:>11}")
print("-"*58)
print("total endpoints whose switch is not read:", tot_u)
json.dump(rows, open("/tmp/matrix.json","w"), ensure_ascii=False, indent=1)
