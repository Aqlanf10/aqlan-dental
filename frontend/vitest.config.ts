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
      // Aspirational audit targets are 60% lines / 50% branches; current
      // baseline (measured 2026-06-23) is lines 57.48% / branches 54.19%.
      // Thresholds set to current-2% so CI stays green while still catching
      // significant regressions. Ratchet up as more tests are added.
      provider: 'v8',
      reporter: ['text', 'html', 'json-summary', 'json'],
      reportsDirectory: './coverage',
      thresholds: {
        lines: 55,
        branches: 52,
      },
    },
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
});
