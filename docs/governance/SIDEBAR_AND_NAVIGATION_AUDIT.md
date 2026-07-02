# Sidebar And Navigation Audit

Spec ID: `001-navigation-and-module-organization`

This report is documentation-only. No runtime code, migrations, package files, deployment files, or secrets were changed.

Evidence was gathered from the current sidebar, route guard, dashboard pages, daily operations redirect stubs, settings hub, lab routes, finance route, ortho/surgery routes, and module map.

## Direct Answers

### Is there duplication between command center and dashboard?

Yes. There is a medium-risk overlap between:

- `/` in `frontend/src/app/(dashboard)/page.tsx`, the canonical dashboard.
- `/clinic-command-center` in `frontend/src/app/(dashboard)/clinic-command-center/page.tsx`, an admin command/operations overview.

They are not identical pages, but both present KPI/shortcut/control-panel concepts. The proposed direction is to keep `/` as the official dashboard and require owner confirmation before keeping, hiding, renaming, or merging `/clinic-command-center`.

### Are modules duplicated in the sidebar?

No exact duplicate module appears in the main sidebar, but several items need cleanup:

- Lab settings appear both under the lab sidebar group and inside the settings hub.
- `/clinic-command-center` can feel like a second dashboard next to `/`.
- `/lab/dashboard` can sound like another dashboard/control panel unless renamed as a lab overview.

### Do multiple routes lead to the same workflow?

Yes. These routes are compatibility redirect stubs:

- `/clinic-queue` redirects to `/daily-operations?tab=queue`.
- `/patient-journey` redirects to `/daily-operations`.
- `/patient-journey/[patientId]` redirects to `/patients/[patientId]?focus=journey`.

Future links should point directly to the canonical daily operations or patient detail destinations.

### Which routes should stay?

`/`, `/daily-operations`, `/patients`, `/appointments`, `/appointments/recall`, `/booking-requests`, `/schedule`, `/doctor-clinic`, `/ortho`, `/ceph`, `/general`, `/surgery`, `/referrals`, `/messages`, `/sms`, `/whatsapp`, `/finance-v3`, `/inventory`, `/inventory/suppliers`, `/inventory/purchases`, `/lab`, `/lab/overdue`, `/lab/reports`, `/lab/payables`, `/reports`, `/doctors`, `/employees`, `/branches`, `/hr/*`, and `/settings`.

`/patient-segments`, `/prescriptions`, `/clinic-command-center`, `/lab/dashboard`, and `/ortho-surgical` need owner confirmation about grouping, visibility, or label.

### Which routes should be hidden from the sidebar?

Hide `/clinic-queue`, `/patient-journey`, `/patient-journey/[patientId]`, detail/create/edit/print routes, `/reports/operations`, `/clinic-display`, and `/ortho-surgical` unless the owner explicitly requests a visible shared ortho-surgery entry.

### Which routes should be merged or renamed?

- Consider merging `/clinic-command-center` into `/`, hiding it, or renaming it as an operational summary.
- Keep `/clinic-queue` and `/patient-journey` as redirects only.
- Consider merging `/lab/dashboard` into `/lab/reports` or renaming it as a lab overview.

### Which routes need owner confirmation?

`/clinic-command-center`, `/patient-segments`, `/ortho-surgical`, `/general` for `OralSurgeon`, `/patients` for `Accountant`, lab settings links for `BranchManager`, and `/lab/dashboard`.

## Proposed Final Sidebar Groups

1. الرئيسية
2. التشغيل اليومي
3. المرضى
4. المواعيد
5. عيادة الطبيب
6. التقويم والسيفالو
7. الجراحة وطب الأسنان العام
8. المختبر
9. المخزون
10. المالية
11. التقارير
12. الرسائل والتواصل
13. الإدارة
14. الإعدادات

## First 5 Recommended Implementation Tasks

1. Fix lab subroute permissions for `Accountant`, or hide the lab subroute links from that role.
2. Fix lab settings route permissions for `BranchManager`, or hide the lab settings links from that role.
3. Decide whether `/clinic-command-center` should be merged, hidden, or retained with a clearer label.
4. Replace any dashboard queue shortcut that points to `/clinic-queue` with `/daily-operations?tab=queue`.
5. Decide whether `Accountant` should access `/patients` and whether `OralSurgeon` should access `/general`; then align sidebar and route guard.

## Findings

### Finding 1: Lab subroute links are visible to Accountant but blocked by generic route guard

- Severity: High
- Evidence: `frontend/src/components/layout/Sidebar.tsx`, `frontend/src/lib/routePermissions.ts`
- Impact: `Accountant` can see `/lab/dashboard`, `/lab/reports`, and `/lab/payables` in the sidebar, but route guard matches `/lab` first and `/lab` excludes `Accountant`.
- Recommendation: Add specific `/lab/dashboard`, `/lab/reports`, and `/lab/payables` rules before `/lab`, or remove `Accountant` from those sidebar children.
- Needs runtime verification: yes

### Finding 2: BranchManager sees lab settings links but generic `/settings` blocks them

- Severity: High
- Evidence: `frontend/src/components/layout/Sidebar.tsx`, `frontend/src/lib/routePermissions.ts`
- Impact: `BranchManager` can see `/settings/labs`, `/settings/lab-work-types`, and `/settings/lab-pricing`, but `/settings` allows only `Admin`.
- Recommendation: Add specific lab settings route rules before `/settings`, or hide these links from `BranchManager`.
- Needs runtime verification: yes

### Finding 3: Dashboard and command center overlap

- Severity: Medium
- Evidence: `frontend/src/app/(dashboard)/page.tsx`, `frontend/src/app/(dashboard)/clinic-command-center/page.tsx`, `frontend/src/components/layout/Sidebar.tsx`
- Impact: Admin users may see two control surfaces: the dashboard and command center.
- Recommendation: Keep `/` as canonical dashboard. Owner should decide whether command center is merged, hidden, or renamed.
- Needs runtime verification: yes

### Finding 4: Dashboard queue card links to redirect stub

- Severity: Medium
- Evidence: `frontend/src/app/(dashboard)/page.tsx`, `frontend/src/app/(dashboard)/clinic-queue/page.tsx`
- Impact: Opening queue from the dashboard goes through `/clinic-queue`, then redirects to daily operations.
- Recommendation: Future implementation should link directly to `/daily-operations?tab=queue`.
- Needs runtime verification: yes

### Finding 5: Queue and patient journey routes are legacy compatibility routes

- Severity: Medium
- Evidence: `frontend/src/app/(dashboard)/clinic-queue/page.tsx`, `frontend/src/app/(dashboard)/patient-journey/page.tsx`, `frontend/src/app/(dashboard)/patient-journey/[patientId]/page.tsx`
- Impact: Future agents may treat these redirects as active independent modules and recreate duplicate workflows.
- Recommendation: Keep them documented as redirect stubs; do not add sidebar entries.
- Needs runtime verification: no

### Finding 6: `/ortho-surgical` is allowed but hidden

- Severity: Medium
- Evidence: `frontend/src/lib/routePermissions.ts`, `frontend/src/app/(dashboard)/ortho-surgical/page.tsx`, `frontend/src/app/(dashboard)/ortho/[id]/_components/OrthoSurgicalPlanningTab.tsx`
- Impact: It may be intentional contextual navigation, but future agents may add a duplicate sidebar item without understanding the workflow.
- Recommendation: Owner should confirm hidden contextual access versus a visible child under ortho/surgery.
- Needs runtime verification: yes

### Finding 7: Accountant can access `/patients` by route guard but has no sidebar link

- Severity: Medium
- Evidence: `frontend/src/lib/routePermissions.ts`, `frontend/src/components/layout/Sidebar.tsx`
- Impact: Direct URL access and visible navigation do not match.
- Recommendation: Confirm whether `Accountant` should use `/patients`, finance patient accounts only, or both.
- Needs runtime verification: yes

### Finding 8: OralSurgeon can access `/general` by route guard but has no sidebar link

- Severity: Medium
- Evidence: `frontend/src/lib/routePermissions.ts`, `frontend/src/components/layout/Sidebar.tsx`
- Impact: Role expectations are inconsistent.
- Recommendation: Confirm whether surgeons need general dentistry access. Align sidebar and route guard.
- Needs runtime verification: yes

### Finding 9: Lab settings appear in two navigation surfaces

- Severity: Medium
- Evidence: `frontend/src/components/layout/Sidebar.tsx`, `frontend/src/app/(dashboard)/settings/page.tsx`, `frontend/src/app/(dashboard)/settings/_components/_shared.ts`
- Impact: Admin sees settings through the settings hub; lab group also exposes some settings routes, which may confuse staff when permissions differ.
- Recommendation: Keep settings hub canonical; only expose lab settings in the lab group if owner confirms and permissions align.
- Needs runtime verification: yes

### Finding 10: Root dashboard is not an explicit route permission entry

- Severity: Low
- Evidence: `frontend/src/lib/routePermissions.ts`, `frontend/src/app/(dashboard)/layout.tsx`
- Impact: `Admin` bypass allows `/`; non-admin users default-deny. This is probably safe, but the spec says every dashboard route should be explicit.
- Recommendation: Add tests or documented special handling for root dashboard without making `/` a catch-all allow.
- Needs runtime verification: yes

### Finding 11: Settings subroutes rely on generic `/settings`

- Severity: Medium
- Evidence: `frontend/src/lib/routePermissions.ts`, `frontend/src/app/(dashboard)/settings/**/page.tsx`
- Impact: Any future non-Admin access to a settings subroute needs a specific rule before `/settings`; otherwise sidebar links become dead ends.
- Recommendation: Use specific-prefix route rules for approved non-Admin settings areas.
- Needs runtime verification: no

### Finding 12: Clinic display is a special route outside dashboard

- Severity: Medium
- Evidence: `frontend/src/app/clinic-display/page.tsx`, `frontend/src/components/shared/WorkflowNav.tsx`, `frontend/src/hooks/usePermissions.ts`
- Impact: It is not part of `(dashboard)` and should not be treated like ordinary staff sidebar navigation without security/runtime review.
- Recommendation: Keep it out of the sidebar until call-screen access and deployment behavior are verified.
- Needs runtime verification: yes

### Finding 13: Finance navigation is correctly single-entry, but deep links must stay contextual

- Severity: Low
- Evidence: `frontend/src/components/layout/Sidebar.tsx`, `frontend/src/app/(dashboard)/finance-v3/page.tsx`
- Impact: Re-adding finance tab sidebar links could recreate duplicate finance modules.
- Recommendation: Keep one `/finance-v3` sidebar entry; use tabs/deep links within workflows only.
- Needs runtime verification: no

### Finding 14: Lab dashboard label can sound like another dashboard

- Severity: Low
- Evidence: `frontend/src/components/layout/Sidebar.tsx`, `frontend/src/app/(dashboard)/lab/dashboard/page.tsx`
- Impact: Staff may perceive multiple dashboards/control panels.
- Recommendation: Rename to a lab overview/summary label or merge with lab reports after owner confirmation.
- Needs runtime verification: yes

### Finding 15: Prescriptions placement is not ideal

- Severity: Low
- Evidence: `frontend/src/components/layout/Sidebar.tsx`
- Impact: Prescriptions are listed after inventory and before lab; this is not the clearest clinical grouping.
- Recommendation: Move under doctor clinic/patient care group in a future sidebar cleanup.
- Needs runtime verification: yes

### Finding 16: Patient segments placement needs owner confirmation

- Severity: Low
- Evidence: `frontend/src/components/layout/Sidebar.tsx`, `frontend/src/app/(dashboard)/patient-segments/page.tsx`
- Impact: It can be understood as patient management, follow-up, or marketing/communication.
- Recommendation: Owner should choose between patients group and communication/follow-up group.
- Needs runtime verification: yes
