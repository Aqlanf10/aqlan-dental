---
name: Next.js → Vite port shims
description: How next/navigation, next/link, and next/image are shimmed in this Vite port of the Aqlan Dental Pro Next.js app.
---

# Shim locations

- `artifacts/aqlan-dental/src/lib/nextNavCompat.ts` — replaces `next/navigation`. Exports: `useRouter`, `usePathname`, `useSearchParams`, `useParams`, `redirect`, `notFound`.
- `artifacts/aqlan-dental/src/lib/nextLinkCompat.tsx` — replaces `next/link`. Default export `Link` that uses wouter's `setLocation`.
- `artifacts/aqlan-dental/src/lib/nextImageCompat.tsx` — replaces `next/image`. Default export `Image` renders an `<img>` tag.

# Bulk replacement

```bash
find artifacts/aqlan-dental/src -type f \( -name "*.ts" -o -name "*.tsx" \) | xargs sed -i \
  's|from "next/navigation"|from "@/lib/nextNavCompat"|g'
```

**Why:** Next.js-specific modules don't exist in a Vite build; these shims give identical import paths so source files need only an import-path change (done via sed) not logic rewriting.

**How to apply:** When adding new source files with `next/*` imports, run the same sed patterns or import from the shims directly.
