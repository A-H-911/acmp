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
      // Thresholds are wired in the final slice (S7) once both stacks are ≥95%,
      // so CI never goes red while we are still climbing. Report-only until then.
    },
  },
})
