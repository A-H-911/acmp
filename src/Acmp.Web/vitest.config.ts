import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

// Separate from vite.config.ts: keeps vitest's bundled Vite types out of the
// app build (Vite 8/rolldown vs vitest's nested Vite would clash under tsc).
// Not referenced by any tsconfig, so `tsc -b` never typechecks it.
export default defineConfig({
  // @ts-expect-error — plugin typed against the app's Vite 8; runtime is fine.
  plugins: [react()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: './src/test/setup.ts',
    css: false,
    // Unit/component tests live under src/. The Playwright E2E suite in e2e/ has its
    // own runner (playwright.config.ts) and must NOT be collected by vitest.
    include: ['src/**/*.{test,spec}.{ts,tsx}'],
    coverage: {
      // Basis: ADR-0016. ≥95% lines on real, assertable product code.
      provider: 'v8',
      all: true, // count files no test imports, so the denominator is honest
      include: ['src/**/*.{ts,tsx}'],
      exclude: [
        'src/main.tsx', // app bootstrap (ReactDOM.createRoot) — no assertable logic
        'src/components/shell/DevRoleSwitcher.tsx', // dev-only role switcher, not shipped behavior
        'src/test/**', // test harness (renderWithAuth, setup)
        'src/**/*.d.ts',
        'src/vite-env.d.ts',
      ],
      reporter: ['text', 'json-summary', 'html'],
      // S7: hard gate. Basis = ADR-0016 ≥95% LINES on assertable product code, enforced
      // global + per-file (perFile: true) so a 0% file can't hide behind the average.
      // Lines only — functions/branches are not the basis. Evaluated when CI runs `test:cov`.
      thresholds: {
        lines: 95,
        perFile: true,
      },
    },
  },
})
