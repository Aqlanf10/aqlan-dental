---
name: Parenthesized directory dynamic imports
description: React.lazy() breaks when the import path contains parentheses like (dashboard) because Vite URL-encodes them.
---

# Problem

`lazy(() => import('@/app/(dashboard)/layout'))` fails at runtime with "Failed to fetch dynamically imported module" because the browser URL-encodes `(dashboard)` → `%28dashboard%29`, which breaks Vite's module serving.

# Fix

Create a re-export shim at a clean path, then lazy-import the shim:

```tsx
// artifacts/aqlan-dental/src/layouts/DashboardLayout.tsx
export { default } from "@/app/(dashboard)/layout";

// artifacts/aqlan-dental/src/App.tsx
const DashboardLayout = lazy(() => import('@/layouts/DashboardLayout'));
```

**Why:** The shim file lives at a parenthesis-free path. Vite serves it cleanly. The static import inside the shim resolves at build time without URL issues.

**How to apply:** Any time a `lazy()` import would reference a `(group)` path, put a re-export shim under `src/layouts/` or `src/routes/`.
