import { describe, expect, it } from 'vitest';
import { NAV } from '@/components/layout/Sidebar';
import { hasPermission, PERMISSION_KEYS } from '@/hooks/usePermissions';

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
});
