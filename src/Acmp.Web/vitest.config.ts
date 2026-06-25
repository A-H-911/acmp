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
  },
})
