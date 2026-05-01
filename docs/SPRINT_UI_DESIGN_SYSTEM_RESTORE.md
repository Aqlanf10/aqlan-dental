# Sprint UI — Restore and Lock Aqlan Dental Pro Design System

## Goal

Restore the approved Aqlan Dental Pro visual design and prevent future Agents from accidentally reverting it.

This sprint is UI-only. It must not change backend logic, database schema, API contracts, authentication, or business rules.

## Background

An earlier design prototype was created and applied previously, but later Agent work partially reverted the UI to an older/simple layout.

The design reference package contains:

- `Aqlan Dental Pro.html`
- `Patient Portal.html`
- `components/layout.jsx`
- `components/LoginPage.jsx`
- `components/DashboardPage.jsx`
- `components/PatientsPage.jsx`
- `components/OrthoPage.jsx`
- `components/CephPage.jsx`
- `components/MessagingPage.jsx`
- `components/AdvancedFeatures.jsx`
- `components/ExtraPages.jsx`
- `components/OtherPages.jsx`
- `components/data.jsx`
- `screenshots/login.png`
- `uploads/logo_upload-1777339394562.png`

Use this package as a visual reference only. Do not copy mock data into production.

## Current Repository Status

The current web app already uses Next.js + TypeScript + Tailwind and contains real APIs.

Do not replace the app with the prototype. Convert the visual identity into the existing app components.

## Visual Identity to Restore

### Primary Colors

Use these design tokens:

```text
Navy / sidebar background: #0d2137
Navy darker: #0a1c30
Navy light: #1a3a5c
Primary blue: #3d7ab5
Primary blue hover: #2d5e8e
Accent orange: #f5922e
Accent orange hover: #e07d1e
Page background: #f7fafd or #f0f5fb
Card border: #e8f0f9
Text dark: #0d2137
Text muted: #64748b
```

### Typography

- Font: Tajawal
- Arabic RTL by default
- Keep text readable and spacious
- Do not mix font families randomly

### Layout Style

- Dark navy right sidebar
- Sidebar supports collapsed and expanded states later
- Logo visible in sidebar header
- Top header with page title, search, notifications, and actions
- Cards with subtle shadows and light blue-gray borders
- Rounded corners around 12px
- Dashboard stats cards with icon blocks
- Clean spacing and grouped sections
- Professional dental-clinic visual tone

## Sidebar Requirements

The approved design uses a dark navy sidebar, not a plain white sidebar.

Sidebar must include grouped navigation sections:

1. رئيسي
   - لوحة التحكم
   - المرضى
   - المواعيد
2. تخصصات
   - التقويم
   - السيفالومتري المتقدم
   - VTO لاحقًا
   - مخطط الأسنان
   - جراحة الوجه والفكين
3. التواصل
   - الرسائل
   - تذكيرات SMS/WhatsApp لاحقًا
   - نظام الاستدعاء لاحقًا
4. عمليات
   - المالية
   - الاستبيانات لاحقًا
   - الإحالات
   - طلبات المختبر
   - المخزون
5. تقارير
   - التقارير والإحصائيات
6. النظام
   - الإعدادات

Important:

- Keep real existing routes.
- If a module is not implemented, either hide it or show a clear disabled/future item.
- Do not add clickable dead links.
- Preserve role-based permissions from the current sidebar.
- Preserve unread message badge.

## Logo Requirements

Use the uploaded center logo from the reference package if available.

Preferred path:

```text
frontend/public/logo.png
```

or keep the existing asset path if the project already has a logo.

Do not hardcode an `uploads/...` path from the prototype unless the asset is actually copied into `public`.

## Login Page Requirements

Restore the approved login visual style:

- Full-screen dark navy gradient background
- Centered login card
- Clinic logo at the top
- Aqlan Dental Pro title
- Arabic RTL labels
- Professional dental center identity
- Clear validation errors
- Preserve existing authentication flow

Do not break login API or token handling.

## Dashboard Requirements

Dashboard should use:

- Stat cards similar to the prototype
- Subtle shadows
- Blue/orange accent colors
- Charts/cards using current real API data if available
- Empty states if data is missing

No fake dashboard numbers in production.

## Patient Pages Requirements

Keep the Sprint 2 unified patient file with 20 tabs.

Only improve styling to match the design system.

Do not remove:

- patient profile tabs
- medical/dental history editing
- patient-linked messages
- photos/radiographs upload/delete
- archive/restore status
- timeline

## Components to Create or Update

Create or update a reusable design layer:

```text
frontend/src/components/ui/AppCard.tsx
frontend/src/components/ui/AppButton.tsx
frontend/src/components/ui/AppBadge.tsx
frontend/src/components/ui/StatCard.tsx
frontend/src/components/layout/Sidebar.tsx
frontend/src/components/layout/Header.tsx or Topbar.tsx
frontend/src/app/globals.css
frontend/tailwind.config.* if needed
```

## Tailwind/CSS Variables

Add stable CSS variables for the design system:

```css
:root {
  --clinic-navy: #0d2137;
  --clinic-navy-dark: #0a1c30;
  --clinic-navy-light: #1a3a5c;
  --clinic-blue: #3d7ab5;
  --clinic-blue-hover: #2d5e8e;
  --clinic-orange: #f5922e;
  --clinic-orange-hover: #e07d1e;
  --clinic-bg: #f7fafd;
  --clinic-card-border: #e8f0f9;
  --clinic-text: #0d2137;
  --clinic-muted: #64748b;
}
```

Add Tailwind utilities/classes if needed:

```css
.clinic-navy-gradient
.clinic-card
.clinic-button-primary
.clinic-button-outline
.clinic-sidebar
```

## Dark Mode

The prototype contains a dark mode concept, but this sprint should not implement full dark mode unless it already exists.

Allowed:

- Prepare tokens for future dark mode.

Not allowed:

- Large unstable dark-mode rewrite.

## Do Not Do

1. Do not copy mock data into the production app.
2. Do not replace Next.js routing with the prototype router.
3. Do not remove API calls.
4. Do not break authentication.
5. Do not break role-based navigation permissions.
6. Do not remove Sprint 2 patient tabs.
7. Do not add non-working navigation links.
8. Do not change database schema.
9. Do not start unrelated features.
10. Do not rebuild from scratch.

## Required Tests

### Build

- Frontend build passes:

```bash
cd frontend
npm run build
```

### Visual/Functional Checks

1. Login page loads and login works.
2. Sidebar is dark navy and uses clinic identity.
3. Sidebar navigation still respects roles.
4. Dashboard loads.
5. Patients list loads.
6. Patient detail page with 20 tabs still loads.
7. Messages unread badge still works.
8. Mobile sidebar still opens/closes correctly.
9. RTL layout is preserved.
10. No console errors from missing logo/assets.
11. No broken routes caused by design migration.

## Acceptance Criteria

This sprint is complete when:

- The app visually matches the approved dark-navy clinic design direction.
- The old white/simple sidebar is replaced by the approved dark sidebar.
- Login page uses the approved identity and gradient style.
- Core pages still function with real API data.
- No dummy data is introduced.
- Build passes.
- The design system is documented and reusable.

## PR Requirements

Open a Pull Request titled:

```text
style: restore approved Aqlan Dental Pro design system
```

PR report must include:

1. Screenshots of login, dashboard, patients list, and patient profile.
2. Files changed.
3. Design tokens added.
4. Components updated.
5. Confirmation that frontend build passes.
6. Confirmation that no backend/database changes were made.
7. Known visual issues, if any.
