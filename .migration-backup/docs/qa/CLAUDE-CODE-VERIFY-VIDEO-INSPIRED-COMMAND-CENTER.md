# Claude Code Verification Prompt — Video-Inspired Clinic Command Center

Repository: `Aqlanf10/aqlan-dental`

Branch to review: `codex/video-inspired-clinic-polish`

Goal:
Verify the video-inspired clinic workflow polish. The change should improve navigation and manager visibility without changing production data, finance rules, auth, messaging, or database schema.

Context:
The reference video showed a dental clinic system with:
- A compact command/dashboard screen.
- Quick links to daily work, reports, receipts, inventory, patient groups, and finance.
- KPI cards for operational and financial status.
- Side navigation access to the new command area.

What was implemented:
1. New frontend page:
   - `frontend/src/app/(dashboard)/clinic-command-center/page.tsx`
   - Arabic manager command center.
   - Reads existing APIs only:
     - `GET /api/dashboard/stats`
     - `GET /api/daily-operations/report`
     - `GET /api/finance-v3/dashboard`
     - `GET /api/patient-segments`
   - Shows operational KPI cards and quick shortcuts.
   - Uses settled API loading so a single failed secondary API does not break the page.

2. Route guard:
   - `frontend/src/lib/routePermissions.ts`
   - `/clinic-command-center` is Admin-only.

3. Sidebar link:
   - `frontend/src/components/layout/Sidebar.tsx`
   - Adds "مركز القيادة" below "لوحة التحكم".
   - Admin-only.

Review rules:
- Do not add new features.
- Do not change backend APIs.
- Do not change database/migrations.
- Do not change auth/passwords.
- Do not send SMS/WhatsApp.
- Do not touch production data.
- Do not merge unless CI is green and owner approves.

Required checks:
1. Confirm exact changed files.
2. Confirm `/clinic-command-center` is Admin-only in routePermissions and Sidebar.
3. Confirm the page does not expose the command center to Accountant, Reception, Doctor, Assistant, or BranchManager.
4. Confirm all shortcut URLs point to existing routes:
   - `/daily-operations`
   - `/appointments/new`
   - `/finance-v3?tab=collections`
   - `/finance-v3?tab=invoices`
   - `/reports`
   - `/inventory`
   - `/patient-segments`
   - `/settings/templates`
5. Confirm there are no writes/mutations in the new page.
6. Confirm API failures are handled with `Promise.allSettled` and do not crash the whole screen.
7. Run:
   - `npx tsc --noEmit`
   - `npm run lint`
   - `npm run build`
8. If possible, run a browser smoke test:
   - Log in as Admin.
   - Open `/clinic-command-center`.
   - Confirm the page loads.
   - Confirm sidebar link is visible to Admin.
   - Confirm key cards and quick shortcuts render.
   - Confirm Console has no new errors caused by this page.

Final report must include:
- PR link.
- Commit SHA.
- Exact files changed.
- Build/check results.
- Confirmation that this is frontend-only.
- Confirmation no database, auth, finance logic, messaging, or production data changed.
- Any remaining UX concerns.
