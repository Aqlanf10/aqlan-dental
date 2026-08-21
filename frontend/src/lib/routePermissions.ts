// Route-level permission definitions
// Each route maps to the roles that can access it
// If a role is not listed, access is denied

export interface RoutePermission {
  path: string;
  allowedRoles: readonly string[];  // Roles that can access this route
  navigationRoles?: readonly string[];  // Optional narrower sidebar visibility
  requiredPermissions?: readonly string[];  // Optional specific permissions
}

const SUPER_ADMIN_ROLE = 'SuperAdmin';

export const ROUTE_MANIFEST: readonly RoutePermission[] = [
  // Main pages
  { path: '/', allowedRoles: ['Admin'] },
  { path: '/clinic-command-center', allowedRoles: ['Admin'] },
  { path: '/daily-operations', allowedRoles: ['Admin', 'Reception', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  { path: '/patients', allowedRoles: ['Admin', 'Reception', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  
  // Clinical pages - doctors only
  // FE-03: Aligned with sidebar — doctors need to view their own schedules
  { path: '/schedule', allowedRoles: ['Admin', 'Reception', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  { path: '/doctor-clinic', allowedRoles: ['Admin', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  // More specific path first (isRouteAllowed matches exact paths or child routes): '/ortho/new'
  // (creating a fresh orthodontic case) stays orthodontist-only, while '/ortho/{id}'
  // must also admit OralSurgeon — a surgeon reaches their linked joint-planning case
  // via /ortho/{orthoCaseId}?tab=surgical (from /surgery's pending-review list, the
  // reciprocal link on /surgery/[id], and the patient file's ortho-surgical sub-tab).
  // Without this, the layout's route guard silently bounces every surgeon back to
  // /daily-operations before they ever see the shared workspace tab. The general
  // orthodontics sidebar entry remains hidden through navigationRoles. The ortho case's
  // OTHER tabs remain protected at the API layer (OrthoCasesController requires the
  // OrthoAccess policy = Admin/Orthodontist), so this only widens frontend navigation,
  // not backend authorization.
  { path: '/ortho/new', allowedRoles: ['Admin', 'Orthodontist'] },
  {
    path: '/ortho',
    allowedRoles: ['Admin', 'Orthodontist', 'OralSurgeon'],
    navigationRoles: ['Admin', 'Orthodontist'],
  },
  { path: '/ceph', allowedRoles: ['Admin', 'Orthodontist'] },
  { path: '/general', allowedRoles: ['Admin', 'GeneralDentist'] },
  { path: '/surgery', allowedRoles: ['Admin', 'OralSurgeon'] },
  { path: '/ortho-surgical', allowedRoles: ['Admin', 'Orthodontist', 'OralSurgeon'] },
  
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
  // More specific path first — same roles as /reports
  { path: '/reports/operations', allowedRoles: ['Admin', 'Accountant'] },
  { path: '/reports', allowedRoles: ['Admin', 'Accountant'] },
  
  // HR - Admin only
  { path: '/employees', allowedRoles: ['Admin'] },
  { path: '/branches', allowedRoles: ['Admin'] },
  
  // System - lab settings exceptions must precede the generic Admin-only /settings rule.
  { path: '/settings/labs', allowedRoles: ['Admin', 'BranchManager'] },
  { path: '/settings/lab-work-types', allowedRoles: ['Admin', 'BranchManager'] },
  { path: '/settings/lab-pricing', allowedRoles: ['Admin', 'BranchManager'] },
  { path: '/settings', allowedRoles: ['Admin'] },

  // Additional sidebar routes
  { path: '/prescriptions', allowedRoles: ['Admin', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  // Spec 010: radiology referrals live beside prescriptions — same clinical roles
  { path: '/radiology-orders', allowedRoles: ['Admin', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  // FE-03: Aligned with sidebar — all clinical staff use lab orders
  { path: '/lab/dashboard', allowedRoles: ['Admin', 'BranchManager', 'Accountant'] },
  { path: '/lab/reports', allowedRoles: ['Admin', 'BranchManager', 'Accountant'] },
  { path: '/lab/payables', allowedRoles: ['Admin', 'BranchManager', 'Accountant'] },
  {
    path: '/lab/overdue',
    allowedRoles: ['Admin', 'Reception', 'Orthodontist', 'GeneralDentist', 'OralSurgeon', 'Assistant', 'BranchManager'],
    navigationRoles: ['Admin', 'Reception', 'Orthodontist', 'BranchManager'],
  },
  { path: '/lab', allowedRoles: ['Admin', 'Reception', 'Orthodontist', 'GeneralDentist', 'OralSurgeon', 'Assistant', 'BranchManager'] },
  { path: '/doctors', allowedRoles: ['Admin'] },
  { path: '/hr', allowedRoles: ['Admin'] },
  // SEQ-03: /users is a redirect stub → /settings?tab=permissions (user management
  // lives in the settings hub). Explicit entry per the default-deny rule below.
  { path: '/users', allowedRoles: [SUPER_ADMIN_ROLE] },
  // More specific path first: isRouteAllowed matches exact paths or child routes, so
  // '/appointments/recall' must precede '/appointments' (Reception needs access here)
  { path: '/appointments/recall', allowedRoles: ['Admin', 'Reception', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  { path: '/appointments', allowedRoles: ['Admin', 'Reception', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  { path: '/clinic-queue', allowedRoles: ['Admin', 'Reception', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  { path: '/patient-journey', allowedRoles: ['Admin', 'Reception', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },

  // FE-02: Previously missing — booking-requests fell through to default-allow. Admin + Reception
  // confirm public booking requests (the public /home/book flow creates them).
  { path: '/booking-requests', allowedRoles: ['Admin', 'Reception'] },
];

// Compatibility export for existing tests and consumers. New navigation code reads
// from ROUTE_MANIFEST through getNavigationRoles so route and sidebar roles cannot drift.
export const ROUTE_PERMISSIONS = ROUTE_MANIFEST;

function withSuperAdmin(roles: readonly string[]): readonly string[] {
  return roles.includes(SUPER_ADMIN_ROLE) ? roles : [SUPER_ADMIN_ROLE, ...roles];
}

export function getNavigationRoles(pathname: string): readonly string[] {
  const route = findRoutePermission(pathname);
  if (!route) {
    throw new Error(`Dashboard navigation route is not registered: ${pathname}`);
  }

  // SuperAdmin is the owner role and must inherit every Admin/navigation capability.
  return withSuperAdmin(route.navigationRoles ?? route.allowedRoles);
}

export function getNavigationGroupRoles(...paths: string[]): readonly string[] {
  return [...new Set(paths.flatMap((path) => getNavigationRoles(path)))];
}

export function isRouteAllowed(pathname: string, userRole: string | null): boolean {
  if (!userRole) return false;

  const matched = findRoutePermission(pathname);

  // SuperAdmin is the single owner and can access every registered dashboard route.
  if (userRole === SUPER_ADMIN_ROLE) return Boolean(matched);

  // /users is owner-only even though Admin retains broad operational access elsewhere.
  if (matched?.path === '/users') return false;

  // Admin has access to every other registered route.
  if (userRole === 'Admin') return Boolean(matched);

  // FE-02 / SEC-17 FIX: Default DENY if no specific rule matches.
  if (!matched) return false;

  return matched.allowedRoles.includes(userRole);
}

function findRoutePermission(pathname: string): RoutePermission | undefined {
  return ROUTE_MANIFEST.find((permission) => isRouteMatch(pathname, permission.path));
}

function isRouteMatch(pathname: string, routePath: string): boolean {
  return pathname === routePath || pathname.startsWith(`${routePath}/`);
}
