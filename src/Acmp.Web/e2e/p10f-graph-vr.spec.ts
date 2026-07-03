import { test, type Page, type APIRequestContext } from '@playwright/test';
import { loginAs } from './login';
import { captureBearer } from './apiHelpers';
import { apiCreateTopic } from './scenario';

/*
 * P10f PR2 visual-review capture (NOT an assertion suite) — screenshots the impact-graph page in
 * EN-light + AR-RTL-dark for a human compare against "ACMP Traceability & Dependencies.dc.html".
 * Seeds a small multi-tier graph via the self-describing edge APIs (real topics only where the
 * Topic-scope cross-stream flag needs a real AffectedStreams read). Outputs to e2e/vr-out/.
 * Run (stack up): npx playwright test p10f-graph-vr --project=chromium
 */
const OUT = 'vr-out';
const JSON = { 'Content-Type': 'application/json' } as const;

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
  await page.screenshot({ path: `${OUT}/${name}.png` });
}

/** Seed a focus topic + a 3-tier subgraph around it; returns the focus key for navigation. */
async function seedGraph(request: APIRequestContext, bearer: string): Promise<string> {
  const focus = await apiCreateTopic(request, bearer, 'Adopt Keycloak as the standard IdP');
  // A real second topic on a different stream so the Topic-scope cross-stream flag actually lights.
  const other = await apiCreateTopic(request, bearer, 'Payments tokenization');

  const rel = async (
    src: { type: string; id: string; key: string; title: string },
    tgt: { type: string; id: string; key: string; title: string },
    relType: string,
  ) => {
    const res = await request.post('/api/traceability', {
      headers: { Authorization: bearer, ...JSON },
      data: {
        sourceType: src.type, sourceId: src.id, sourceKey: src.key, sourceTitle: src.title,
        targetType: tgt.type, targetId: tgt.id, targetKey: tgt.key, targetTitle: tgt.title,
        relType, notes: null,
      },
    });
    if (res.status() !== 201) throw new Error(`rel ${relType} ${res.status()} ${await res.text()}`);
  };

  const D1 = { type: 'Decision', id: crypto.randomUUID(), key: 'DECN-2026-008', title: 'Approve Keycloak, conditionally' };
  const A1 = { type: 'Action', id: crypto.randomUUID(), key: 'ACT-2026-040', title: 'Produce migration ADR' };
  const ADR1 = { type: 'Adr', id: crypto.randomUUID(), key: 'ADR-2026-003', title: 'Keycloak as standard IdP' };
  const R1 = { type: 'Risk', id: crypto.randomUUID(), key: 'RSK-2026-006', title: 'Dual-running auth migration' };
  const UP = { type: 'Topic', id: crypto.randomUUID(), key: 'TOP-2026-022', title: 'API pagination standard' };
  const F = { type: 'Topic', id: focus.id, key: focus.key, title: focus.title };

  await rel(F, D1, 'DecidedBy');       // focus → decision (downstream)
  await rel(D1, A1, 'Produces');       // decision → action (tier 2)
  await rel(D1, ADR1, 'RecordedAs');   // decision → adr (tier 2)
  await rel(F, R1, 'Mitigates');       // focus ~ risk (related)
  await rel(F, UP, 'DependsOn');       // focus depends on a topic (upstream)

  // Governed dependency: focus (Platform) BLOCKS the Payments topic → blocked + cross-stream node.
  const dep = await request.post('/api/dependencies', {
    headers: { Authorization: bearer, ...JSON },
    data: {
      fromType: 'Topic', fromId: focus.id, fromKey: focus.key, fromTitle: focus.title,
      toType: 'Topic', toId: other.id, toKey: other.key, toTitle: other.title,
      kind: 'Blocks', note: null,
    },
  });
  if (dep.status() !== 201) throw new Error(`dep ${dep.status()} ${await dep.text()}`);

  return focus.key;
}

for (const mode of MODES) {
  test(`P10f graph VR — ${mode.name}`, async ({ page, request }) => {
    test.setTimeout(120_000);
    await setMode(page, mode.lang, mode.theme);
    await loginAs(page, 'secretary');

    const bearer = await captureBearer(page);
    const focusKey = await seedGraph(request, bearer);

    // Warm path: open the graph from the topic's own detail-page panel button.
    await page.goto(`/topics/${focusKey}`);
    await page.getByRole('link', { name: /Open graph|فتح المخطط/ }).click();
    await page.waitForURL(/\/traceability\/Topic\//, { timeout: 20_000 });
    await shot(page, `p10f-graph-depth2-${mode.name}`);

    // Depth 3 + both highlight toggles on (blocked + cross-stream lit).
    await page.locator('.ig-depth-btn', { hasText: '3' }).click();
    await page.getByRole('button', { name: /Blocked work|عمل محجوب/ }).click();
    await page.getByRole('button', { name: /Cross-stream|عبر المسارات/ }).click();
    await shot(page, `p10f-graph-highlights-${mode.name}`);

    // List fallback.
    await page.getByRole('button', { name: /^List$|^قائمة$/ }).click();
    await shot(page, `p10f-graph-list-${mode.name}`);
  });
}
