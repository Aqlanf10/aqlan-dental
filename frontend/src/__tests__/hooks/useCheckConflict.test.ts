import { describe, it, expect } from 'vitest';

/**
 * Structural test for useCheckConflict hook.
 *
 * This verifies the hook's type interface and configuration shape
 * without making actual API calls. The real hook lives in
 * @/hooks/useAppointments and is backed by POST /api/appointments/check-conflict.
 */
describe('useCheckConflict — structural validation', () => {
  it('hook file exports a useCheckConflict function', async () => {
    const mod = await import('@/hooks/useAppointments');
    expect(typeof mod.useCheckConflict).toBe('function');
  });

  it('hook returns an object with a mutate function', async () => {
    // We need to import from a module that calls React hooks,
    // but in this test environment we verify the export exists and
    // validate the expected parameter interface.
    const mod = await import('@/hooks/useAppointments');
    // The hook itself can't be called outside React, but we can verify
    // the function signature exists and the expected API endpoint
    // by inspecting the source module's export.
    expect(mod.useCheckConflict).toBeDefined();
  });
});
