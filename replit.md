# Aqlan Dental Pro

A full-featured dental clinic management system for Dr. Aqlan's dental & orthodontics center (مركز د. عقلان الكامل), Taiz, Yemen.

## Architecture

- **Frontend**: `artifacts/aqlan-dental/` — React + Vite (ported from Next.js). RTL Arabic UI using Tajawal font. Serves at `/`.
- **API Server**: `artifacts/api-server/` — Express.js backend serving at `/api`. Originally a .NET backend; the Express stub is the Replit-side API layer.
- **Database**: Pre-configured PostgreSQL via `lib/db/` (Drizzle ORM). The original app used a .NET + PostgreSQL backend.

## Key Features

The app is a multi-module clinic management SaaS:
- Staff login + patient portal (dual login on `/login`)
- Daily operations, appointments, patient records
- Orthodontics, cephalometry (ceph), surgery modules
- Finance, HR, inventory, lab modules
- Messaging (WhatsApp, SMS, internal), radiology
- Clinic queue display (kiosk screen at `/clinic-display`)
- Patient portal at `/portal/*`

## Porting Notes

- Ported from Next.js App Router → Vite + React + wouter
- `src/lib/nextNavCompat.ts` — shim for `next/navigation` (useRouter, usePathname, useSearchParams, useParams, redirect)
- `src/lib/nextLinkCompat.tsx` — shim for `next/link`
- `src/lib/nextImageCompat.tsx` — shim for `next/image`
- Tailwind v3 (not v4) — uses `postcss.config.cjs` + `tailwind.config.ts`
- Font: Tajawal woff2 files in `src/app/fonts/`, loaded via @font-face in `src/index.css`
- `NEXT_PUBLIC_*` env vars → `VITE_*` env vars
- The original .NET backend is in `.migration-backup/backend/` — the app communicates with it via `VITE_API_URL`

## User Preferences

- Preserve Arabic RTL layout throughout
- Brand colors: `#1a3a5c` (dark sky), `#3d7ab5` (sky blue), `#f5922e` (orange)
