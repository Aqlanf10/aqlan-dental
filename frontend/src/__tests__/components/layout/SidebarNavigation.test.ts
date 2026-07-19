import { describe, expect, it } from 'vitest';
import { NAV } from '@/components/layout/Sidebar';
import { hasPermission, PERMISSION_KEYS } from '@/hooks/usePermissions';
import { getNavigationRoles } from '@/lib/routePermissions';

describe('sidebar navigation contract', () => {
  it('CORE-P1-S1: exposes the canonical appointments entry to Reception', () => {
    const appointments = NAV.find(
      (entry) => entry.kind !== 'group' && entry.href === '/appointments',
    );

    expect(appointments).toBeDefined();
    expect(appointments?.roles).toContain('Reception');
    expect(
      hasPermission({ role: 'Reception' }, PERMISSION_KEYS.APPOINTMENTS_VIEW),
    ).toBe(true);
  });

  it('CORE-P1-S3: derives every navigation leaf role list from the route manifest', () => {
    const leaves = NAV.flatMap((entry) =>
      entry.kind === 'group' ? entry.children : [entry],
    );

    for (const leaf of leaves) {
      expect(leaf.roles).toBe(getNavigationRoles(leaf.href));
    }
  });

  it('derives group visibility from the union of its visible child roles', () => {
    const groups = NAV.filter((entry) => entry.kind === 'group');

    for (const group of groups) {
      const childRoles = [...new Set(group.children.flatMap((child) => child.roles))];
      expect(group.roles).toEqual(childRoles);
    }
  });
});
