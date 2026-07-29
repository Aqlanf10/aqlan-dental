# Design Comparison Note — style/match-approved-design-reference

## Current UI vs. Approved ZIP Reference

### Color Tokens (ALREADY MATCHING ✓)
- Navy: `#0d2137` ✓
- Blue: `#3d7ab5` ✓
- Orange: `#f5922e` ✓
- Green: `#22c55e` ✓
- Red: `#ef4444` ✓
- Purple: `#a855f7` ✓
- Page bg: `#eef3f9` ✓
- Card border: `#e8f0f9` ✓
- Card shadow: `0 1px 3px rgba(13,33,55,0.06), 0 1px 10px rgba(13,33,55,0.04)` ✓

### Identified Visual Differences

#### 1. Logo (HIGH PRIORITY)
- **Current**: Uses `/logo.svg` (generic SVG)
- **ZIP**: Uses `uploads/logo_upload-1777339394562.png` (official Aqlan logo with orange heart + blue script)
- **Fix**: Replace logo.svg with official PNG, update Sidebar + Login + loading screen references

#### 2. Dashboard — Charts Styling (HIGH PRIORITY)
- **Current**: `DashboardCharts.tsx` uses Tailwind generic classes (`border-gray-200`, `shadow-sm`, `rounded-xl`, `text-gray-900`)
- **ZIP**: Uses exact design tokens (`border: 1px solid #e8f0f9`, custom card shadow, `border-radius: 12px`, `color: #0d2137`)
- **Fix**: Replace generic Tailwind classes with inline styles matching ZIP tokens

#### 3. Dashboard — TodaySchedule Styling (HIGH PRIORITY)
- **Current**: Uses `border-gray-200`, `shadow-sm`, `divide-gray-50` generic classes
- **ZIP**: Uses `border: 1px solid #e8f0f9`, card shadow, `border-bottom: 1px solid #f8fafc`
- **Fix**: Replace generic classes with ZIP-matched inline styles

#### 4. Dashboard — Missing Sections (MEDIUM PRIORITY)
- **Current**: Stats → Charts+Schedule → 2 extra stat cards
- **ZIP**: Stats → TodayAppts+RevenueChart+DoctorPerf → RecentPatients+QuickActions+LatestPayments
- **Fix**: Add Recent Patients, Quick Actions, Doctor Performance, Latest Payments sections with API-backed data or gracefully hidden sections

#### 5. Patient Table — Filter Buttons (MEDIUM PRIORITY)
- **Current**: Uses `<select>` dropdowns for status/gender/doctor filters
- **ZIP**: Uses inline pill-style buttons for type filter (الكل/تقويم/طب عام/جراحة)
- **Fix**: Keep existing functional dropdowns (they work with API), but add pill-style type filter buttons alongside for visual match

#### 6. Charts — Recharts Colors (MEDIUM PRIORITY)
- **Current**: Revenue chart uses `#2563EB` (Tailwind blue-600), ortho donut uses `#2563EB/#059669/#6B7280`
- **ZIP**: Uses `#3d7ab5` (clinic-blue) for bars, `#dce8f5` for inactive bars
- **Fix**: Update Recharts fill colors to use clinic tokens

#### 7. Sidebar Width (LOW PRIORITY)
- **Current**: `w-64` (256px)
- **ZIP**: 258px expanded, 64px collapsed
- **Fix**: Adjust to 258px for exact match

### Files That Will Be Updated
1. `frontend/public/logo.png` — NEW: official logo file
2. `frontend/src/components/layout/Sidebar.tsx` — logo reference + width
3. `frontend/src/app/(auth)/login/page.tsx` — logo reference
4. `frontend/src/app/(dashboard)/layout.tsx` — loading screen logo + sidebar offset
5. `frontend/src/app/(dashboard)/page.tsx` — dashboard layout matching ZIP
6. `frontend/src/components/dashboard/DashboardCharts.tsx` — styling fix
7. `frontend/src/components/dashboard/TodaySchedule.tsx` — styling fix
8. `frontend/src/components/dashboard/StatsCard.tsx` — minor style refinements
9. `frontend/src/components/patients/PatientTable.tsx` — filter button styling
10. `frontend/src/components/patient/tabs/*.tsx` — consistency sweep (borders/shadows)
