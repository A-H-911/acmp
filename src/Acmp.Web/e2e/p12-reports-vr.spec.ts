import { test, type Page, type APIRequestContext } from '@playwright/test';
import { loginAs } from './login';
import { captureBearer } from './apiHelpers';
import { apiMembers, apiCreateTopic } from './scenario';

/*
 * P12-PR3 visual-review capture (NOT an assertion suite) — screenshots the full Reports shell
 * (`/reports`) against "ACMP Dashboards & Reports.dc.html". One populated tab per renderer type
 * proves fidelity of all five renderers (the tabs share them): the Executive tab exercises
 * matrix + stack + stat, the Committee tab exercises bars + columns. Captured EN-light + AR-dark.
 * Seeds the risk cells that reproduce the design's authored 3×3 matrix, plus a blocking dependency
 * and two streams; decision/action-derived cards populate from whatever the stack already holds
 * (empty on a fresh CI DB → the card renders its zeros/empty). Outputs to e2e/vr-out/.
 * Run (stack up): npx playwright test p12-reports-vr --project=chromium
 */
const OUT = 'vr-out';
const JSON_H = { 'Content-Type': 'application/json' } as const;

const MODES = [
  { name: 'en-light', lang: 'en', theme: 'light' },
  { name: 'ar-dark', lang: 'ar', theme: 'dark' },
] as const;

const RISK_CELLS: { impact: 'Low' | 'Medium' | 'High'; likelihood: 'Low' | 'Medium' | 'High' }[] = [
  { impact: 'High', likelihood: 'Medium' }, { impact: 'High', likelihood: 'High' },
  { impact: 'Medium', likelihood: 'Low' }, { impact: 'Medium', likelihood: 'Medium' },
  { impact: 'Low', likelihood: 'Low' }, { impact: 'Low', likelihood: 'Low' },
];

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

async function seedReports(request: APIRequestContext, bearer: string): Promise<void> {
  const members = await apiMembers(request, bearer);
  const owner = members.find((m) => m.role === 'Secretary') ?? members[0];
  const topic = await apiCreateTopic(request, bearer, 'Adopt Keycloak as the standard IdP');
  const other = await apiCreateTopic(request, bearer, 'Payments tokenization');
  for (let i = 0; i < RISK_CELLS.length; i++) {
    const c = RISK_CELLS[i];
    const res = await request.post('/api/risks', {
      headers: { Authorization: bearer, ...JSON_H },
      data: { title: { en: `Risk ${i + 1}`, ar: `مخاطرة ${i + 1}` }, description: null, likelihood: c.likelihood, impact: c.impact, ownerUserId: owner.publicId, ownerName: owner.fullName, subjectType: 'Topic', subjectId: topic.id, subjectKey: topic.key, initialMitigation: null },
    });
    if (res.status() !== 201) throw new Error(`risk ${i} ${res.status()} ${await res.text()}`);
  }
  const dep = await request.post('/api/dependencies', {
    headers: { Authorization: bearer, ...JSON_H },
    data: { fromType: 'Topic', fromId: topic.id, fromKey: topic.key, fromTitle: topic.title, toType: 'Topic', toId: other.id, toKey: other.key, toTitle: other.title, kind: 'Blocks', note: null },
  });
  if (dep.status() !== 201) throw new Error(`dep ${dep.status()} ${await dep.text()}`);
}

for (const mode of MODES) {
  test(`P12 reports VR — ${mode.name}`, async ({ page, request }) => {
    test.setTimeout(120_000);
    await setMode(page, mode.lang, mode.theme);
    await loginAs(page, 'secretary');
    const bearer = await captureBearer(page);
    await seedReports(request, bearer);

    await page.goto('/reports');
    await page.getByRole('heading', { name: /Risk exposure|التعرّض للمخاطر/ }).waitFor({ timeout: 20_000 });
    await shot(page, `p12-reports-executive-${mode.name}`); // matrix + stack + stat

    await page.getByRole('tab', { name: /Committee|اللجنة/ }).click();
    await page.getByRole('heading', { name: /Topic aging|عمر المواضيع/ }).waitFor({ timeout: 20_000 });
    await shot(page, `p12-reports-committee-${mode.name}`); // bars + columns
  });
}
