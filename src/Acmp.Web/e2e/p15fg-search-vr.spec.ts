import { test, type Page, type APIRequestContext } from '@playwright/test';
import { loginAs } from './login';
import { captureBearer } from './apiHelpers';

/*
 * P15f/g visual-review capture (NOT an assertion suite) — screenshots the global search results page in
 * EN-light + AR-RTL-dark for a human sanity check. No-reference composition (INV-014 — there is no search
 * .dc.html): built from the shared design system. Seeds two searchable artifacts across two types (a Topic
 * and a wiki Document, both mentioning "Keycloak") so the results render grouped (AC-060). Run (isolated
 * stack up): npx playwright test p15fg-search-vr --project=chromium
 */
const OUT = 'e2e/vr-out';
const JSON_H = { 'Content-Type': 'application/json' } as const;
const L = (en: string, ar: string) => ({ en, ar });

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

async function post(request: APIRequestContext, bearer: string, url: string, data?: unknown): Promise<Record<string, unknown>> {
  const r = await request.post(url, { headers: { Authorization: bearer, ...JSON_H }, ...(data ? { data } : {}) });
  if (!r.ok()) throw new Error(`POST ${url} → ${r.status()} ${await r.text()}`);
  const text = await r.text();
  return text ? (JSON.parse(text) as Record<string, unknown>) : {};
}

async function seed(request: APIRequestContext, bearer: string): Promise<void> {
  await post(request, bearer, '/api/topics', {
    title: 'Adopt Keycloak as the identity provider', description: 'Consolidate per-stream IdPs onto Keycloak.',
    justification: 'Cuts duplicated maintenance.', type: 'ArchitectureDecision', urgency: 'Normal',
    source: 'CommitteeMember', streams: ['IAM'], systems: [], tags: [],
  });
  const doc = await post(request, bearer, '/api/knowledge/documents', {
    title: L('Keycloak identity guide', 'دليل هوية كيكلوك'),
    category: 'Standards', body: L('How to configure Keycloak for the committee.', 'كيفية تكوين كيكلوك للجنة.'),
    tags: ['identity'],
  });
  await post(request, bearer, `/api/knowledge/documents/${doc.id}/publish`);
}

for (const mode of MODES) {
  test(`P15fg search VR — ${mode.name}`, async ({ page, request }) => {
    test.setTimeout(120_000);
    await setMode(page, mode.lang, mode.theme);
    await loginAs(page, 'secretary');

    const bearer = await captureBearer(page);
    await page.waitForLoadState('networkidle');
    await seed(request, bearer);
    await page.waitForTimeout(1500); // let the full-text catalogs populate (the LIKE booster matches regardless)

    await page.goto('/search?q=Keycloak');
    await page.getByRole('heading', { level: 1 }).waitFor({ timeout: 20_000 });
    await page.getByText(/TOP-|DOC-/).first().waitFor({ timeout: 20_000 }).catch(() => {});
    await shot(page, `p15fg-search-${mode.name}`);
  });
}
