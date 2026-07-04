import { test, type Page, type APIRequestContext } from '@playwright/test';
import { loginAs } from './login';
import { captureBearer } from './apiHelpers';
import { apiCreateTopic, apiMembers } from './scenario';

/*
 * P10g visual-review capture (NOT an assertion suite) — screenshots the /reports Risk & Dependency
 * page in EN-light + AR-RTL-dark for a human compare against "ACMP Dashboards & Reports.dc.html".
 * The reference-backed surface is the risk-exposure 3×3 matrix; we seed 6 active risks whose
 * (impact, likelihood) exactly reproduce the design's authored matrix (High row 0/1/1, Med 1/1/0,
 * Low 2/0/0 → "6 active"), plus a blocking dependency, across two streams. Outputs to e2e/vr-out/.
 * Run (stack up): npx playwright test p10g-reports-vr --project=chromium
 */
const OUT = 'vr-out';
const JSON_H = { 'Content-Type': 'application/json' } as const;

const MODES = [
  { name: 'en-light', lang: 'en', theme: 'light' },
  { name: 'ar-dark', lang: 'ar', theme: 'dark' },
] as const;

// The design's authored matrix cells, as (impact, likelihood) pairs → 6 active risks.
const RISK_CELLS: { impact: 'Low' | 'Medium' | 'High'; likelihood: 'Low' | 'Medium' | 'High' }[] = [
  { impact: 'High', likelihood: 'Medium' },
  { impact: 'High', likelihood: 'High' },
  { impact: 'Medium', likelihood: 'Low' },
  { impact: 'Medium', likelihood: 'Medium' },
  { impact: 'Low', likelihood: 'Low' },
  { impact: 'Low', likelihood: 'Low' },
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
      data: {
        title: { en: `Risk ${i + 1}: ${c.impact}×${c.likelihood}`, ar: `مخاطرة ${i + 1}` },
        description: null,
        likelihood: c.likelihood,
        impact: c.impact,
        ownerUserId: owner.publicId,
        ownerName: owner.fullName,
        subjectType: 'Topic',
        subjectId: topic.id,
        subjectKey: topic.key,
        initialMitigation: null,
      },
    });
    if (res.status() !== 201) throw new Error(`risk ${i} ${res.status()} ${await res.text()}`);
  }

  // A blocking dependency across the two topics → deps stat "Blocked" + by-stream bars light.
  const dep = await request.post('/api/dependencies', {
    headers: { Authorization: bearer, ...JSON_H },
    data: {
      fromType: 'Topic', fromId: topic.id, fromKey: topic.key, fromTitle: topic.title,
      toType: 'Topic', toId: other.id, toKey: other.key, toTitle: other.title,
      kind: 'Blocks', note: null,
    },
  });
  if (dep.status() !== 201) throw new Error(`dep ${dep.status()} ${await dep.text()}`);
}

for (const mode of MODES) {
  test(`P10g reports VR — ${mode.name}`, async ({ page, request }) => {
    test.setTimeout(120_000);
    await setMode(page, mode.lang, mode.theme);
    await loginAs(page, 'secretary');

    const bearer = await captureBearer(page);
    await seedReports(request, bearer);

    await page.goto('/reports');
    await page.getByRole('heading', { name: /Risk exposure|التعرّض للمخاطر/ }).waitFor({ timeout: 20_000 });
    await shot(page, `p10g-reports-${mode.name}`);
  });
}
