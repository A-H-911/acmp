import { defineConfig, devices } from '@playwright/test';

/*
 * S6 (ADR-0016 §2). E2E against the REAL self-contained Docker Compose stack
 * (web :8088 / api :8080 / Keycloak :8085) through the genuine Keycloak
 * authorization-code + PKCE round-trip. The stack is brought up by the CI
 * workflow (deploy/docker-compose.yml) or locally with `npm run e2e:up`;
 * global-setup waits for it to be healthy and seeds deterministic per-role
 * test users via the Keycloak admin API (the prod realm export is untouched).
 *
 * This config does NOT own the container lifecycle on purpose — bringing 7
 * services up/down belongs to CI (and the local up/down scripts), not to a
 * test runner that may be invoked many times.
 */
const WEB_BASE_URL = process.env.E2E_WEB_URL ?? 'http://localhost:8088';

export default defineConfig({
  testDir: './e2e',
  globalSetup: './e2e/global-setup.ts',
  // The real PKCE redirect chain is multi-hop; give it room without masking real hangs.
  timeout: 60_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: WEB_BASE_URL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
});
