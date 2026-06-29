// Route-level permission definitions
// Each route maps to the roles that can access it
// If a role is not listed, access is denied

export interface RoutePermission {
  path: string;
  allowedRoles: string[];  // Roles that can access this route
  requiredPermissions?: string[];  // Optional specific permissions
}

export const ROUTE_PERMISSIONS: RoutePermission[] = [
  // Main pages
  { path: '/clinic-command-center', allowedRoles: ['Admin'] },
  { path: '/daily-operations', allowedRoles: ['Admin', 'Reception', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  { path: '/patients', allowedRoles: ['Admin', 'Reception', 'Accountant', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  
  // Clinical pages - doctors only
  // FE-03: Aligned with sidebar — doctors need to view their own schedules
  { path: '/schedule', allowedRoles: ['Admin', 'Reception', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  { path: '/doctor-clinic', allowedRoles: ['Admin', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  { path: '/ortho', allowedRoles: ['Admin', 'Orthodontist'] },
  { path: '/ceph', allowedRoles: ['Admin', 'Orthodontist'] },
  { path: '/general', allowedRoles: ['Admin', 'GeneralDentist', 'OralSurgeon'] },
  { path: '/surgery', allowedRoles: ['Admin', 'OralSurgeon'] },
  
  // Communication
  { path: '/referrals', allowedRoles: ['Admin', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  { path: '/messages', allowedRoles: ['Admin', 'Reception', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  { path: '/whatsapp', allowedRoles: ['Admin'] },
  { path: '/sms', allowedRoles: ['Admin', 'Reception'] },
  
  // Finance - Admin and Accountant only
  // NAV-CEPH-FIX (audit §4 — Finance): Reception removed — page guard returns AccessDenied for
  // Reception (page.tsx:78,103-105 checks Admin || Accountant only), so allowing Reception here
  // was a dead-end loop (sidebar → routePermissions allow → AccessDenied → /daily-operations).
  // Now Reception is cleanly redirected by the layout's route guard instead.
  { path: '/finance-v3', allowedRoles: ['Admin', 'Accountant'] },
  
  // Inventory - Admin only
  { path: '/inventory', allowedRoles: ['Admin'] },

  // YOLO-S5: Patient segments — pre-built dynamic + admin-managed custom.
  // Backend (PatientSegmentsController) is [Authorize(Policy = "AdminOnly")].
  { path: '/patient-segments', allowedRoles: ['Admin'] },
  
  // Reports - specific roles
  // More specific path first (startsWith matching) — same roles as /reports
  { path: '/reports/operations', allowedRoles: ['Admin', 'Accountant'] },
  { path: '/reports', allowedRoles: ['Admin', 'Accountant'] },
  
  // HR - Admin only
  { path: '/employees', allowedRoles: ['Admin'] },
  { path: '/branches', allowedRoles: ['Admin'] },
  
  // System - Admin only
  { path: '/settings', allowedRoles: ['Admin'] },

  // Additional sidebar routes
  { path: '/prescriptions', allowedRoles: ['Admin', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  // FE-03: Aligned with sidebar — all clinical staff use lab orders
  { path: '/lab', allowedRoles: ['Admin', 'Reception', 'Orthodontist', 'GeneralDentist', 'OralSurgeon', 'Assistant', 'BranchManager'] },
  { path: '/doctors', allowedRoles: ['Admin'] },
  { path: '/hr', allowedRoles: ['Admin'] },
  // More specific path first: isRouteAllowed matches with startsWith, so
  // '/appointments/recall' must precede '/appointments' (Reception needs access here)
  { path: '/appointments/recall', allowedRoles: ['Admin', 'Reception', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  { path: '/appointments', allowedRoles: ['Admin', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  { path: '/clinic-queue', allowedRoles: ['Admin', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  { path: '/patient-journey', allowedRoles: ['Admin', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },

  // FE-02: Previously missing — booking-requests fell through to default-allow. Admin + Reception
  // confirm public booking requests (the public /home/book flow creates them).
  { path: '/booking-requests', allowedRoles: ['Admin', 'Reception'] },
];

export function isRouteAllowed(pathname: string, userRole: string | null): boolean {
  if (!userRole) return false;

  // Admin has access to everything
  if (userRole === 'Admin') return true;

  // Find matching permission for this route
  const matched = ROUTE_PERMISSIONS.find(p => pathname.startsWith(p.path));
  // FE-02 / SEC-17 FIX: Default DENY if no specific rule matches. Previously this returned
  // true (default-allow), which let any authenticated user reach admin-only screens like
  // /commissions, /booking-requests, /settings/audit, /settings/backup, /surgery/[id]/edit,
  // /ortho/new, /ceph/new, /referrals/new. The backend [Authorize(Policy=...)] still rejected
  // API calls, so no data leaked — but the UX was broken (flash of page chrome + 403 fetches).
  // Now unmatched routes are denied; every dashboard route MUST have an explicit entry above.
  if (!matched) return false;

  return matched.allowedRoles.includes(userRole);
}
