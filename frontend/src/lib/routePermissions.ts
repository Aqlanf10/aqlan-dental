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
  { path: '/daily-operations', allowedRoles: ['Admin', 'Reception', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  { path: '/patients', allowedRoles: ['Admin', 'Reception', 'Accountant', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  
  // Clinical pages - doctors only
  { path: '/schedule', allowedRoles: ['Admin', 'Reception'] },
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
  { path: '/finance-v3', allowedRoles: ['Admin', 'Accountant', 'Reception'] },
  
  // Inventory - Admin only
  { path: '/inventory', allowedRoles: ['Admin'] },
  
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
  { path: '/lab', allowedRoles: ['Admin', 'Orthodontist'] },
  { path: '/doctors', allowedRoles: ['Admin'] },
  { path: '/hr', allowedRoles: ['Admin'] },
  // More specific path first: isRouteAllowed matches with startsWith, so
  // '/appointments/recall' must precede '/appointments' (Reception needs access here)
  { path: '/appointments/recall', allowedRoles: ['Admin', 'Reception', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  { path: '/appointments', allowedRoles: ['Admin', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  { path: '/clinic-queue', allowedRoles: ['Admin', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
  { path: '/patient-journey', allowedRoles: ['Admin', 'GeneralDentist', 'OralSurgeon', 'Orthodontist'] },
];

export function isRouteAllowed(pathname: string, userRole: string | null): boolean {
  if (!userRole) return false;
  
  // Admin has access to everything
  if (userRole === 'Admin') return true;
  
  // Find matching permission for this route
  const matched = ROUTE_PERMISSIONS.find(p => pathname.startsWith(p.path));
  if (!matched) return true; // Default allow if no specific rule
  
  return matched.allowedRoles.includes(userRole);
}
