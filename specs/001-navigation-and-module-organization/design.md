# 001 Navigation And Module Organization Design

Spec ID: `001-navigation-and-module-organization`

This is a proposed final navigation design only. Do not change code from this document without a future implementation task.

## Current Owners

- Canonical sidebar: `frontend/src/components/layout/Sidebar.tsx`.
- Route guard: `frontend/src/lib/routePermissions.ts`.
- Dashboard shell/redirect behavior: `frontend/src/app/(dashboard)/layout.tsx`.
- Dashboard route: `frontend/src/app/(dashboard)/page.tsx`.
- Admin command center route: `frontend/src/app/(dashboard)/clinic-command-center/page.tsx`.
- Settings hub: `frontend/src/app/(dashboard)/settings/page.tsx` and `frontend/src/app/(dashboard)/settings/_components/_shared.ts`.
- Daily operations owner: `frontend/src/app/(dashboard)/daily-operations/`.

## Proposed Final Sidebar Groups

1. **الرئيسية**
   - Keep `/` as the canonical dashboard.
   - Owner confirmation: `/clinic-command-center` should be merged into `/`, hidden from the sidebar, or renamed as an admin-only operational summary.

2. **التشغيل اليومي**
   - Keep `/daily-operations`.
   - Keep as direct links if the owner wants them surfaced: `/booking-requests`, `/appointments/recall`.
   - Hide `/clinic-queue`, `/patient-journey`, and `/patient-journey/[patientId]` because they are redirect stubs.

3. **المرضى**
   - Keep `/patients`.
   - Owner confirmation: `/patient-segments` should live under patients or communication/follow-up.
   - Hide detail/create/print routes such as `/patients/new` and `/patients/[id]`.

4. **المواعيد**
   - Keep `/appointments` and `/schedule`.
   - Keep or merge under daily operations: `/appointments/recall`, `/booking-requests`.
   - Hide detail/create/edit routes.

5. **عيادة الطبيب**
   - Keep `/doctor-clinic`.
   - Move candidate: `/prescriptions` fits better here than near inventory/lab.
   - Contextual links may continue to point into patient details and daily operations.

6. **التقويم والسيفالو**
   - Keep `/ortho` and `/ceph`.
   - Contextual only: `/ortho-surgical`, `/ortho/[id]/model-analysis`, and deep ceph routes.
   - Owner confirmation: decide whether `/ortho-surgical` needs a visible child link.

7. **الجراحة وطب الأسنان العام**
   - Keep `/general` and `/surgery`.
   - Owner confirmation: `/general` route permissions include `OralSurgeon`, but the sidebar hides it from that role.
   - Hide surgery detail/create/edit routes.

8. **المختبر**
   - Keep `/lab`, `/lab/overdue`, `/lab/reports`, `/lab/payables`.
   - Rename candidate: `/lab/dashboard` should be described as a lab overview/summary, not another control panel.
   - Permission review required before exposing `/lab/dashboard`, `/lab/reports`, or `/lab/payables` to `Accountant`.
   - Owner confirmation: decide whether lab settings links belong here or only in the settings hub.

9. **المخزون**
   - Keep `/inventory`, `/inventory/suppliers`, `/inventory/purchases`.
   - Confirm supplier ownership does not duplicate finance suppliers under `/finance-v3?tab=suppliers`.

10. **المالية**
    - Keep one sidebar entry: `/finance-v3`.
    - Do not add sidebar entries for finance tabs such as collections, invoices, contracts, commissions, cashier, or suppliers.

11. **التقارير**
    - Keep `/reports`.
    - Hide/contextual unless explicitly requested: `/reports/operations`.

12. **الرسائل والتواصل**
    - Keep `/messages`, `/sms`, `/whatsapp`.
    - Owner confirmation: move `/patient-segments` here only if used mainly for follow-up/segmentation campaigns.

13. **الإدارة**
    - Keep `/doctors`, `/employees`, `/branches`, and `/hr/*`.
    - Hide detail routes from the sidebar.

14. **الإعدادات**
    - Keep `/settings` as the canonical settings hub.
    - Hide specific settings routes from the sidebar unless the owner confirms operational need.
    - Permission review required before `BranchManager` can access lab settings subroutes.

## Routes To Keep

- `/`
- `/daily-operations`
- `/patients`
- `/patient-segments` after owner confirms grouping
- `/appointments`
- `/appointments/recall`
- `/booking-requests`
- `/schedule`
- `/doctor-clinic`
- `/prescriptions`
- `/ortho`
- `/ceph`
- `/general`
- `/surgery`
- `/referrals`
- `/messages`
- `/sms`
- `/whatsapp`
- `/finance-v3`
- `/inventory`
- `/inventory/suppliers`
- `/inventory/purchases`
- `/lab`
- `/lab/overdue`
- `/lab/reports`
- `/lab/payables`
- `/reports`
- `/doctors`
- `/employees`
- `/branches`
- `/hr/*`
- `/settings`

## Routes To Hide From Sidebar

- `/clinic-queue` because it redirects to `/daily-operations?tab=queue`.
- `/patient-journey` because it redirects to `/daily-operations`.
- `/patient-journey/[patientId]` because it redirects to `/patients/[patientId]?focus=journey`.
- Detail/create/edit/print routes such as `/patients/new`, `/appointments/new`, `/surgery/[id]`, `/ceph/[id]`, and `/ortho/[id]`.
- `/reports/operations` unless the owner requests an explicit reports child.
- `/ortho-surgical` unless the owner requests a visible shared ortho-surgery workspace.
- `/clinic-display` until display/security behavior is runtime-verified.

## Routes To Merge Or Redirect

- Keep `/clinic-queue` as a redirect stub, but future links should point directly to `/daily-operations?tab=queue`.
- Keep `/patient-journey` as a redirect stub, but future links should point directly to `/daily-operations`.
- Keep `/patient-journey/[patientId]` as a redirect stub to `/patients/[patientId]?focus=journey`.
- Consider merging `/clinic-command-center` into `/` or hiding it as an admin-only overview route.
- Consider merging `/lab/dashboard` into `/lab/reports` or renaming it to avoid a second-dashboard label.

## Routes To Rename

- `/clinic-command-center`: rename from command-center language to an operational/admin summary if retained.
- `/lab/dashboard`: rename to a lab overview/summary label if retained.
- `/patient-segments`: keep as patient segments only after choosing patients versus communication/follow-up grouping.
- `/reports/operations`: use an operations report label if surfaced.

## Routes Needing Owner Confirmation

- `/clinic-command-center`: keep visible, merge into dashboard, or hide?
- `/patient-segments`: under patients or communication/follow-up?
- `/ortho-surgical`: hidden contextual route or visible child under ortho/surgery?
- `/general` for `OralSurgeon`: should surgeons see it in the sidebar?
- `/patients` for `Accountant`: should accountants see patient list or only finance patient accounts?
- Lab settings under lab group: should `BranchManager` access these routes?
- `/lab/dashboard`: separate page or merged with lab reports?

## Permissions That Must Be Reviewed

- Add specific route permission entries before `/lab` for `/lab/dashboard`, `/lab/reports`, and `/lab/payables` if `Accountant` should access them.
- Add specific route permission entries before `/settings` for `/settings/labs`, `/settings/lab-work-types`, and `/settings/lab-pricing` if `BranchManager` should access them.
- Decide whether `/patients` should remain allowed for `Accountant`.
- Decide whether `/general` should remain allowed for `OralSurgeon`.
- Document the hidden/contextual intent for `/ortho-surgical`.
- Document special handling for `/` root dashboard because the `Admin` bypass currently makes it accessible without an explicit route entry.

## Risks

- High: sidebar-visible lab links can become click-to-redirect dead ends for `Accountant` and `BranchManager`.
- High: widening settings or lab permissions without backend confirmation may expose sensitive configuration.
- Medium: command center plus dashboard can create a perceived second control panel.
- Medium: redirect stubs preserve compatibility but can confuse audit tools and future agents.
- Medium: route label ambiguity can cause staff to pick the wrong workflow.
- Low: submodule dashboard labels can make the app feel like it has many dashboards.

## Rollback Plan

For future implementation PRs, revert sidebar and route permission changes together. Do not leave a sidebar link without a matching route guard rule. If a route is hidden, preserve direct URL compatibility through existing redirect stubs unless the owner explicitly approves removal.
