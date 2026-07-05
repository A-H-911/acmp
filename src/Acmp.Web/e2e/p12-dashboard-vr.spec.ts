import { test, type Page, type APIRequestContext } from '@playwright/test';
import { loginAs } from './login';
import { captureBearer } from './apiHelpers';
import { apiMembers, apiPreparedTopic, apiScheduleMeeting, apiAddAgendaItem } from './scenario';

/*
 * P12-PR2 visual-review capture (NOT an assertion suite) — screenshots the role Dashboard at `/`
 * for the three committee-role variants in EN-light + AR-RTL-dark, for a human compare against
 * "ACMP Dashboards & Reports.dc.html". Seeds only the cheaply-seedable committee-wide data
 * (backlog with varied urgency, a scheduled meeting, actions incl. one escalated-overdue). Cards
 * whose data needs multi-step transitions on a fresh stack (SLA-breach via aging, deferred≥2,
 * Closed votes, escalated risks, issued decisions) render their honest empty state — which still
 * verifies the card shell + RTL + light/dark, since every variant shares the same card components.
 * Run (stack up): npx playwright test p12-dashboard-vr --project=chromium
 */
const OUT = 'vr-out';
const JSON_H = { 'Content-Type': 'application/json' } as const;

const MODES = [
  { name: 'en-light', lang: 'en', theme: 'light' },
  { name: 'ar-dark', lang: 'ar', theme: 'dark' },
] as const;

const ROLES = ['member', 'secretary', 'chairman'] as const;

async function setMode(page: Page, lang: string, theme: string): Promise<void> {
  await page.addInitScript(([l, t]) => {
    localStorage.setItem('i18nextLng', l);
    localStorage.setItem('acmp-theme', t);
  }, [lang, theme]);
}

async function shot(page: Page, name: string): Promise<void> {
  await page.locator('.dash-greeting').waitFor({ timeout: 20_000 });
  await page.waitForTimeout(600);
  await page.screenshot({ path: `${OUT}/${name}.png`, fullPage: true });
}

async function createTopic(request: APIRequestContext, bearer: string, title: string, urgency: string) {
  const res = await request.post('/api/topics', {
    headers: { Authorization: bearer, ...JSON_H },
    data: { type: 'ArchitectureDecision', title, description: 'VR setup.', justification: 'VR setup.', streams: ['Platform'], systems: [], urgency, source: 'CommitteeMember', tags: [] },
  });
  if (res.status() !== 201) throw new Error(`topic ${res.status()} ${await res.text()}`);
  return res.json() as Promise<{ id: string; key: string }>;
}

async function createAction(request: APIRequestContext, bearer: string, owner: { publicId: string; fullName: string }, topic: { id: string; key: string }, titleEn: string, dueDate: string | null) {
  const res = await request.post('/api/actions', {
    headers: { Authorization: bearer, ...JSON_H },
    data: {
      title: { en: titleEn, ar: titleEn }, description: null, priority: 'Normal',
      ownerUserId: owner.publicId, ownerName: owner.fullName, dueDate,
      sourceType: 'Topic', sourceId: topic.id, sourceKey: topic.key, meetingKey: null,
    },
  });
  if (res.status() !== 201) throw new Error(`action ${res.status()} ${await res.text()}`);
}

async function seed(request: APIRequestContext, bearer: string): Promise<void> {
  const members = await apiMembers(request, bearer);
  const owner = members.find((m) => m.role === 'Secretary') ?? members[0];
  await createTopic(request, bearer, 'Adopt Keycloak as the standard IdP', 'Critical');
  await createTopic(request, bearer, 'Unify audit logging schema', 'Urgent');
  await createTopic(request, bearer, 'API pagination standard', 'Normal');
  const prepared = await apiPreparedTopic(request, bearer, 'SSO session policy alignment', owner);
  const mtg = await apiScheduleMeeting(request, bearer, 'Weekly Architecture Committee', owner);
  await apiAddAgendaItem(request, bearer, mtg.id, prepared, owner);
  await createAction(request, bearer, owner, prepared, 'Produce Keycloak migration ADR', null);
  await createAction(request, bearer, owner, prepared, 'Risk review: dual-running auth', '2026-06-01T00:00:00.000Z');
}

test.describe.serial('P12 dashboard VR', () => {
  test('seed (secretary)', async ({ page, request }) => {
    test.setTimeout(120_000);
    await loginAs(page, 'secretary');
    const bearer = await captureBearer(page);
    await seed(request, bearer);
  });

  for (const role of ROLES) {
    for (const mode of MODES) {
      test(`${role} — ${mode.name}`, async ({ page }) => {
        test.setTimeout(120_000);
        await setMode(page, mode.lang, mode.theme);
        await loginAs(page, role);
        await shot(page, `p12-dash-${role}-${mode.name}`);
      });
    }
  }
});
