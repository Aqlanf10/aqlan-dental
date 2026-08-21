import { describe, expect, it } from 'vitest';
import {
  getNavigationRoles,
  isRouteAllowed,
  ROUTE_MANIFEST,
} from '@/lib/routePermissions';

describe('route permissions', () => {
  it('allows Accountant to reach approved lab subroutes without opening all lab orders', () => {
    expect(isRouteAllowed('/lab/dashboard', 'Accountant')).toBe(true);
    expect(isRouteAllowed('/lab/reports', 'Accountant')).toBe(true);
    expect(isRouteAllowed('/lab/payables', 'Accountant')).toBe(true);
    expect(isRouteAllowed('/lab', 'Accountant')).toBe(false);
    expect(isRouteAllowed('/lab/overdue', 'Accountant')).toBe(false);
  });

  it('allows BranchManager to reach approved lab settings without opening all settings', () => {
    expect(isRouteAllowed('/settings/labs', 'BranchManager')).toBe(true);
    expect(isRouteAllowed('/settings/lab-work-types', 'BranchManager')).toBe(true);
    expect(isRouteAllowed('/settings/lab-pricing', 'BranchManager')).toBe(true);
    expect(isRouteAllowed('/settings', 'BranchManager')).toBe(false);
    expect(isRouteAllowed('/settings/backup', 'BranchManager')).toBe(false);
  });

  it('keeps sidebar-hidden patient/general access closed for mismatched roles', () => {
    expect(isRouteAllowed('/patients', 'Accountant')).toBe(false);
    expect(isRouteAllowed('/patients/123', 'Accountant')).toBe(false);
    expect(isRouteAllowed('/general', 'OralSurgeon')).toBe(false);
    expect(isRouteAllowed('/general/123', 'OralSurgeon')).toBe(false);
  });

  it('lets Reception reach daily-operations redirect stubs', () => {
    expect(isRouteAllowed('/clinic-queue', 'Reception')).toBe(true);
    expect(isRouteAllowed('/patient-journey', 'Reception')).toBe(true);
    expect(isRouteAllowed('/patient-journey/123', 'Reception')).toBe(true);
  });

  it('CORE-P1-S1: lets Reception use the canonical appointment workflow', () => {
    expect(isRouteAllowed('/appointments', 'Reception')).toBe(true);
    expect(isRouteAllowed('/appointments/new', 'Reception')).toBe(true);
    expect(isRouteAllowed('/appointments/appointment-1', 'Reception')).toBe(true);
    expect(isRouteAllowed('/appointments/appointment-1/edit', 'Reception')).toBe(true);
    expect(isRouteAllowed('/appointments/recall', 'Reception')).toBe(true);
  });

  it('keeps Admin override and default deny behavior intact', () => {
    expect(isRouteAllowed('/', 'Admin')).toBe(true);
    expect(isRouteAllowed('/', 'Reception')).toBe(false);
    expect(isRouteAllowed('/settings/backup', 'Admin')).toBe(true);
    expect(isRouteAllowed('/unknown-dashboard-route', 'Reception')).toBe(false);
    expect(isRouteAllowed('/daily-operations', null)).toBe(false);
  });

  it('makes SuperAdmin the single owner role for user management while inheriting registered routes', () => {
    expect(isRouteAllowed('/', 'SuperAdmin')).toBe(true);
    expect(isRouteAllowed('/finance-v3', 'SuperAdmin')).toBe(true);
    expect(isRouteAllowed('/ortho/case-1', 'SuperAdmin')).toBe(true);
    expect(isRouteAllowed('/users', 'SuperAdmin')).toBe(true);
    expect(isRouteAllowed('/users', 'Admin')).toBe(false);
    expect(getNavigationRoles('/')).toContain('SuperAdmin');
    expect(getNavigationRoles('/finance-v3')).toContain('SuperAdmin');
  });

  it('keeps users/permissions redirect stubs owner-only', () => {
    expect(isRouteAllowed('/users', 'SuperAdmin')).toBe(true);
    expect(isRouteAllowed('/users', 'Admin')).toBe(false);
    expect(isRouteAllowed('/users', 'Reception')).toBe(false);
    expect(isRouteAllowed('/users', 'Accountant')).toBe(false);

    // Settings remains an operational Admin route; the backend owner-only guard protects
    // /api/users, /api/roles and /api/permissions even if an Admin opens the settings shell.
    expect(isRouteAllowed('/settings/users', 'Admin')).toBe(true);
    expect(isRouteAllowed('/settings/permissions', 'Admin')).toBe(true);
    expect(isRouteAllowed('/settings/users', 'Reception')).toBe(false);
    expect(isRouteAllowed('/settings/permissions', 'BranchManager')).toBe(false);
  });

  it('does not allow similarly named paths through prefix matching', () => {
    expect(isRouteAllowed('/labyrinth', 'BranchManager')).toBe(false);
    expect(isRouteAllowed('/settings-labs', 'BranchManager')).toBe(false);
    expect(isRouteAllowed('/appointments-recall', 'Reception')).toBe(false);
    expect(isRouteAllowed('/clinic-queue-old', 'Reception')).toBe(false);
    expect(isRouteAllowed('/patients-archive', 'Reception')).toBe(false);
  });

  it('CORE-P1-S3: keeps manifest paths unique and navigation roles within route access', () => {
    const paths = ROUTE_MANIFEST.map((route) => route.path);
    expect(new Set(paths).size).toBe(paths.length);

    for (const route of ROUTE_MANIFEST) {
      for (const role of route.navigationRoles ?? route.allowedRoles) {
        expect(route.allowedRoles).toContain(role);
      }
    }
  });

  it('keeps intentional navigation visibility narrower without blocking deep links', () => {
    expect(isRouteAllowed('/ortho/case-1', 'OralSurgeon')).toBe(true);
    expect(getNavigationRoles('/ortho')).not.toContain('OralSurgeon');

    expect(isRouteAllowed('/lab/overdue', 'GeneralDentist')).toBe(true);
    expect(getNavigationRoles('/lab/overdue')).not.toContain('GeneralDentist');
  });

  it('fails closed when the sidebar references an unregistered dashboard route', () => {
    expect(() => getNavigationRoles('/unregistered-dashboard-route')).toThrow(
      'Dashboard navigation route is not registered: /unregistered-dashboard-route',
    );
  });
});
