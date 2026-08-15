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
    /*
     * AC-101 says "on Chrome AND Edge", and until now playwright.config.ts declared exactly one
     * project, so Edge had NEVER run (AV-158 gap 2).
     *
     * ⚠ SCOPED WITH testMatch RATHER THAN RUN WHOLE, deliberately. A bare second project doubles a
     * 78-test suite that already drives a seven-service stack through a real Keycloak — paying ~4
     * minutes of CI on every PR to re-prove things the AC never asked about a second browser for.
     * What the AC asks is that RTL artifacts are absent on both engines, so Edge runs exactly the
     * RTL surfaces.
     *
     * ⚠⚠ `vr-sweep` IS NAMED HERE BUT CANNOT MATCH IN CI, AND THAT IS NOT AN OVERSIGHT — it is
     * `.gitignore`d (`src/Acmp.Web/.gitignore`: "the ad-hoc sweep driver … kept local so they never
     * commit or run in CI", PR #57). So the file exists only on a developer's machine, where this
     * pattern DOES give it Edge coverage, and in CI this project resolves to rtl-a11y alone. It is
     * measurable rather than assumed: the local projection is 86 tests and CI collects 82 — exactly
     * vr-sweep's 2 tests times 2 projects.
     *
     * ⚠ THE CONSEQUENCE IS BIGGER THAN THIS CONFIG AND IS TRACKED SEPARATELY: AC-101's own "when a
     * visual regression test captures every page" has NO instrument in CI at all, which is why the
     * property-level guard in src/test/rtl-physical-direction.test.ts is not a supplement to the
     * sweep — it is the only mechanical RTL detection the pipeline actually runs.
     *
     * ⚠ `channel: 'msedge'` USES THE REAL EDGE BINARY, not Chromium wearing a user-agent — which is
     * the only version of this that could ever find an engine difference. That is also why e2e.yml
     * must install it: `playwright install --with-deps chromium` alone leaves every Edge test dying
     * at launch instead of failing informatively.
     */
    {
      name: 'msedge',
      use: { ...devices['Desktop Edge'], channel: 'msedge' },
      testMatch: /(vr-sweep|rtl-a11y)\.spec\.ts/,
    },
  ],
});
