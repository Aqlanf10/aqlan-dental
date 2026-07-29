import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/__tests__/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    coverage: {
      // TEST-19: coverage collection + thresholds (safety-net baseline).
      // Aspirational audit targets are 60% lines / 50% branches.
      // Policy: thresholds sit at current-2% so CI stays green while still
      // catching significant regressions; ratchet up as more tests are added.
      //
      // Re-baselined 2026-07-10 (was 55/52, measured 2026-06-23 at
      // 57.48/54.19): v8 only counts files IMPORTED during the test run, and
      // ORTHO-TASK-002's component tests are the first to import the large
      // ortho tab components (OrthoDiagnosisTab, OrthoTreatmentPlansTab,
      // OrthoSurgicalPlanningTab, CasePresentationPanel + their hook/service
      // imports) — a much bigger denominator, so the global percentage moved
      // to 45.72/42.24 even though strictly MORE code is tested than before.
      // Old and new numbers are not comparable; this is a denominator change,
      // not a coverage regression.
      provider: 'v8',
      reporter: ['text', 'html', 'json-summary', 'json'],
      reportsDirectory: './coverage',
      thresholds: {
        lines: 43,
        branches: 40,
      },
    },
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
});
