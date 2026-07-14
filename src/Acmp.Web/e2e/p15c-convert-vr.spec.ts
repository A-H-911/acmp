import { test, type Page, type APIRequestContext } from '@playwright/test';
import { loginAs } from './login';
import { captureBearer } from './apiHelpers';

/*
 * P15c-2 visual-review capture (NOT an assertion suite) — screenshots the research→topic convert flow in
 * EN-light + AR-RTL-dark for a human compare against "ACMP Research & Knowledge.dc.html" (the convert button)
 * and the no-reference compositions (convert dialog + Linked-items panel, guardrail #14). It seeds ONE
 * showcase mission that exercises every new affordance at once:
 *   - created WITH a source topic  → header shows the navigable "Linked topic: TOP-…" xref (FR-115)
 *   - Completed                    → header "Convert to execution topic" button (backend needs Completed)
 *   - one recommendation converted → an edge-driven "Converted → TOP-…" link, Convert action hidden
 *   - one recommendation accepted  → a per-recommendation "Convert" action
 *   - the shared Linked-items panel (FR-114) + its "Open graph" affordance (FR-113)
 * Captures: the mission detail, the convert dialog open, and the impact graph focused on the mission.
 * Run (isolated stack up): npx playwright test p15c-convert-vr --project=chromium
 */
const OUT = 'e2e/vr-out';
const JSON_H = { 'Content-Type': 'application/json' } as const;

const MODES = [
  { name: 'en-light', lang: 'en', theme: 'light' },
  { name: 'ar-dark', lang: 'ar', theme: 'dark' },
] as const;

const L = (en: string, ar: string) => ({ en, ar });

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

async function seed(request: APIRequestContext, bearer: string): Promise<string> {
  // 1) A source topic → the mission's FR-115 "linked topic" xref (create-time Topic→Mission edge).
  const topic = await post(request, bearer, '/api/topics', {
    title: 'Unify identity providers across streams',
    description: 'Consolidate per-stream IdPs onto one platform.',
    justification: 'Cuts duplicated maintenance and closes token-format gaps.',
    type: 'ResearchDiscovery', urgency: 'Normal', source: 'CommitteeMember',
    streams: ['IAM'], systems: [], tags: [],
  });

  // 2) The mission, seeded from that topic.
  const mission = await post(request, bearer, '/api/research', {
    title: L('Evaluate a unified identity provider', 'تقييم موفّر هوية موحّد'),
    question: L('Does one IdP cut per-stream maintenance without weakening isolation?',
      'هل يقلّل موفّر هوية واحد صيانة كل مسار دون إضعاف العزل؟'),
    sourceTopicId: topic.id,
  });
  const mid = mission.id as string;
  const mkey = mission.key as string;

  await post(request, bearer, `/api/research/${mid}/activate`);

  // 3) Two recommendations; capture their ids from the detail read (the add call returns 204).
  for (const rec of [
    { statement: L('Adopt Keycloak with a realm per stream.', 'اعتماد Keycloak بنطاق لكل مسار.'),
      rationale: L('Proven isolation with one platform to operate.', 'عزل مُثبَت مع منصة واحدة للتشغيل.'), priority: 'High' },
    { statement: L('Require a tested rollback before any cutover.', 'اشتراط تراجع مُختبَر قبل أي تحويل.'),
      rationale: null, priority: 'Medium' },
  ]) {
    await post(request, bearer, `/api/research/${mid}/recommendations`,
      { statement: rec.statement, rationale: rec.rationale, priority: rec.priority, linkedTopicId: null });
  }

  const detail = await (await request.get(`/api/research/${mkey}`, { headers: { Authorization: bearer } })).json() as
    { recommendations: { id: string }[] };
  const [rec1, rec2] = detail.recommendations;

  // 4) Accept both; convert the first → an edge-driven "Converted → TOP-…" link. Leave the second Accepted
  //    so its per-recommendation "Convert" action shows too.
  await post(request, bearer, `/api/research/${mid}/recommendations/${rec1.id}/status`, { status: 'Accepted' });
  await post(request, bearer, `/api/research/${mid}/recommendations/${rec2.id}/status`, { status: 'Accepted' });

  const converted = await post(request, bearer, '/api/topics/from-research', {
    missionId: mid, recommendationId: rec1.id,
    title: 'Adopt Keycloak, realm per stream',
    description: 'Stand up Keycloak and migrate each stream to its own realm.',
    justification: 'The accepted recommendation from the unified-IdP mission.',
    type: 'ArchitectureDecision', urgency: 'Urgent', streams: ['IAM'], systems: [], tags: [],
  });
  await post(request, bearer, `/api/research/${mid}/recommendations/${rec1.id}/convert`, { topicId: converted.id });

  // 5) Complete the mission → the header "Convert to execution topic" button appears.
  await post(request, bearer, `/api/research/${mid}/complete`);
  return mkey;
}

for (const mode of MODES) {
  test(`P15c convert VR — ${mode.name}`, async ({ page, request }) => {
    test.setTimeout(120_000);
    await setMode(page, mode.lang, mode.theme);
    await loginAs(page, 'secretary');

    const bearer = await captureBearer(page);
    // Let the login-time audit append commit before the seed burst (ADR-0009/0026 concurrency window).
    await page.waitForLoadState('networkidle');
    const mkey = await seed(request, bearer);

    // Mission detail — convert button, linked-topic xref, converted + accepted recs, Linked-items panel.
    await page.goto(`/research/${mkey}`);
    await page.getByRole('heading', { level: 1 }).waitFor({ timeout: 20_000 });
    await page.getByText(mkey).first().waitFor({ timeout: 20_000 });
    await page.getByText(/TOP-\d{4}-/).first().waitFor({ timeout: 20_000 });
    await shot(page, `p15c-mission-detail-${mode.name}`);

    // Convert dialog (no-reference composition) — open from the header button.
    await page.getByRole('button', { name: /Convert to execution topic|تحويل إلى موضوع تنفيذي/ }).click();
    await page.getByRole('dialog').waitFor({ timeout: 10_000 });
    await shot(page, `p15c-convert-dialog-${mode.name}`);
    await page.keyboard.press('Escape');
    await page.getByRole('dialog').waitFor({ state: 'detached', timeout: 10_000 });

    // Impact graph focused on the mission (FR-113) — via the panel's "Open dependency graph".
    await page.getByRole('link', { name: /Open dependency graph|فتح مخطط الاعتماديات/ }).first().click();
    await page.getByText(/GRAPH|RMS-\d{4}-|TOP-\d{4}-/).first().waitFor({ timeout: 20_000 }).catch(() => {});
    await shot(page, `p15c-mission-graph-${mode.name}`);
  });
}
