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
    /*
     * DEF-067 — vitest's 5s default is too tight for THIS suite under coverage, and the fix belongs
     * here rather than on individual tests.
     *
     * THE MEASUREMENT, not the guess. DEF-067 was filed against one test (DecisionPage's
     * "requires a condition when the successor is Conditionally Approved") failing intermittently
     * under `npm run test:cov` and never under a plain `vitest run`. Reproducing it by looping the
     * full coverage suite showed the failure is `Test timed out in 5000ms` at ~5.0-5.1s — the test
     * was still EXECUTING, not asserting against an unsettled DOM. Then a SECOND, unrelated test
     * (RecordDecisionDialog's override-flag case) tripped the same 5s ceiling on the next loop.
     *
     * ⚠ THAT SECOND FAILURE IS WHY THIS IS A CONFIG CHANGE. It is not one flaky test, it is a CLASS:
     * component tests that drive a dozen or more userEvent interactions, where v8 coverage
     * instrumentation slows every one of them, so the body overruns on a loaded runner and finishes
     * comfortably on an idle developer machine. WHICH test trips is a function of load, so pinning
     * per-test timeouts is whack-a-mole against a moving target.
     *
     * ⚠ THIS IS NOT THE "add a timeout to hide a race" ANTI-PATTERN DEF-067 WARNED ABOUT, and the
     * distinction is the whole justification. A timeout hides a RACE because it gives a racy
     * assertion more chances to get lucky. There is no race here: every assertion still runs, and
     * still fails if the behaviour breaks — the tests only needed enough wall-clock to reach them.
     * `findBy*`/`waitFor` were tried FIRST on the original test and the failure persisted, which is
     * how the duration diagnosis was reached rather than assumed.
     *
     * 15s is ~3x the observed overrun: ample headroom for an instrumented, loaded CI runner while
     * still failing loudly on a genuine hang. A far larger number would blunt that signal.
     */
    testTimeout: 15_000,
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
