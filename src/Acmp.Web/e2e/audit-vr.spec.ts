import { test, type Page, type APIRequestContext } from '@playwright/test';
import { loginAs } from './login';
import { captureBearer } from './apiHelpers';
import { apiMembers, apiPreparedTopic, apiScheduleMeeting } from './scenario';

/*
 * PR4 audit-register visual-review capture (NOT an assertion suite) — screenshots the /audit trail
 * in EN-light + AR-RTL-dark for a human compare against "ACMP Lists & Registers.dc.html" isAudit
 * (gAudit: Timestamp · Actor · Action · Artifact · Detail), the read-only header chip, and the
 * in-card footer banner.
 *
 * There is no seeded Auditor E2E user; Secretary is an authorized audit reader (ADR-0027:
 * {Auditor, Chairman, Secretary}) so it drives both login and the seed. We generate a spread of
 * governed state changes (prepared topics → submit/accept/prepare; a scheduled meeting) so the log
 * shows several subject types + action verb-tone chips + the Success outcome, all attributed to the
 * acting Secretary. Outputs to e2e/vr-out/. Run (stack up): npx playwright test audit-vr --project=chromium
 */
const OUT = 'vr-out';

const MODES = [
  { name: 'en-light', lang: 'en', theme: 'light' },
  { name: 'ar-dark', lang: 'ar', theme: 'dark' },
] as const;

async function setMode(page: Page, lang: string, theme: string): Promise<void> {
  await page.addInitScript(([l, t]) => {
    localStorage.setItem('i18nextLng', l);
    localStorage.setItem('acmp-theme', t);
  }, [lang, theme]);
}

async function shot(page: Page, name: string): Promise<void> {
  await page.waitForTimeout(500);
  await page.screenshot({ path: `${OUT}/${name}.png`, fullPage: true });
}

// A spread of governed state changes → a populated, varied audit log (Topic + Meeting subject types,
// submit/accept/prepare/schedule actions, all Success, actor = the acting Secretary).
async function seedAuditActivity(request: APIRequestContext, bearer: string): Promise<void> {
  const members = await apiMembers(request, bearer);
  // ⚠ NAMED, NOT POSITIONAL — and users.ts asked for exactly this re-check the next time the user list
  // grew. GET /api/members is OrderBy(FullName) with no role filter, so members[0] is whichever member
  // happens to sort first among those that have signed in by now. ac011-guest-scope runs before this
  // file (alphabetical, workers: 1) and creates a real guest presenter called "E2E Guest ...", which
  // sorts ahead of "E2E Member" and "E2E Secretary" — so the positional pick could hand this fixture a
  // GUEST and then make them the owner of four topics and the CHAIR of a meeting. The four sibling VR
  // specs already resolve by role; this was the one that did not.
  const owner = members.find((m) => m.role === 'Secretary') ?? members[0];
  for (let i = 1; i <= 4; i++) {
    await apiPreparedTopic(request, bearer, `Audit trail sample topic ${i}`, owner);
  }
  await apiScheduleMeeting(request, bearer, 'Audit trail sample meeting', owner);
}

for (const mode of MODES) {
  test(`PR4 audit VR — ${mode.name}`, async ({ page, request }) => {
    test.setTimeout(120_000);
    await setMode(page, mode.lang, mode.theme);
    await loginAs(page, 'secretary');

    const bearer = await captureBearer(page);
    await seedAuditActivity(request, bearer);

    await page.goto('/audit');
    // Header renders + at least one row present (the seeded activity).
    await page.getByRole('heading', { level: 1, name: /Audit trail|سجل التدقيق/ }).waitFor({ timeout: 20_000 });
    await page.locator('tbody tr').first().waitFor({ timeout: 20_000 });
    await shot(page, `pr4-audit-register-${mode.name}`);
  });
}
