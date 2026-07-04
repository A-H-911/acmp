import { test, type Page, type APIRequestContext } from '@playwright/test';
import { loginAs } from './login';
import { captureBearer } from './apiHelpers';
import { apiMembers } from './scenario';

/*
 * P11e visual-review capture (NOT an assertion suite) — screenshots the FR-068 Decision→ADR promotion in
 * EN-light + AR-RTL-dark for a human compare against "ACMP Decision, Voting & ADR.dc.html" (convertOpen):
 *   1. the issued Decision detail with the Chairman-only "Convert to ADR" action,
 *   2. the confirm dialog,
 *   3. the resulting promoted ADR (born Draft, pre-filled from the decision).
 * Seeds a real issued decision: record (Approved) → link a follow-up action (satisfies the AC-029 gate) →
 * issue. Outputs to e2e/vr-out/. Run (stack up): npx playwright test p11e-promote-vr --project=chromium
 */
const OUT = 'vr-out';
const JSON_H = { 'Content-Type': 'application/json' } as const;

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

// Record an Approved decision, satisfy the AC-029 downstream gate with a follow-up action, then issue it.
async function seedIssuedDecision(request: APIRequestContext, bearer: string): Promise<string> {
  const members = await apiMembers(request, bearer);
  const owner = members.find((m) => m.role === 'Chairman') ?? members[0];

  const rec = await request.post('/api/decisions', {
    headers: { Authorization: bearer, ...JSON_H },
    data: {
      topicId: crypto.randomUUID(), meetingId: null, outcome: 'Approved',
      title: { en: 'Adopt Keycloak as the standard identity provider', ar: 'اعتماد كيكلوك كموفّر الهوية المعياري' },
      statement: { en: 'The committee adopts Keycloak, realm per stream, as the standard identity provider.', ar: 'تعتمد اللجنة كيكلوك، نطاق لكل مسار، كموفّر الهوية المعياري.' },
      rationale: { en: 'Streams issue incompatible token lifetimes and maintain separate auth stacks; a single provider is auditable and revocable.', ar: 'تصدر المسارات أعمار رموز غير متوافقة وتدير حزم مصادقة منفصلة؛ موفّر واحد قابل للتدقيق والإلغاء.' },
      alternatives: { en: 'Build an in-house IdP — higher long-term burden.', ar: 'بناء موفّر داخلي — عبء أطول أمدًا.' },
      voteId: null, conditions: [],
    },
  });
  if (rec.status() !== 201) throw new Error(`record ${rec.status()} ${await rec.text()}`);
  const decision = (await rec.json()) as { id: string; key: string };

  const act = await request.post('/api/actions', {
    headers: { Authorization: bearer, ...JSON_H },
    data: {
      title: { en: 'Produce the Keycloak migration ADR', ar: 'إعداد سجل قرار ترحيل كيكلوك' },
      description: null, priority: 'Normal', ownerUserId: owner.publicId, ownerName: owner.fullName,
      dueDate: null, sourceType: 'Decision', sourceId: decision.id, sourceKey: decision.key, meetingKey: null,
    },
  });
  if (act.status() !== 201) throw new Error(`action ${act.status()} ${await act.text()}`);

  const issue = await request.post(`/api/decisions/${decision.id}/issue`, {
    headers: { Authorization: bearer, ...JSON_H },
    data: { chairOverride: false, overrideJustification: null },
  });
  if (issue.status() !== 204) throw new Error(`issue ${issue.status()} ${await issue.text()}`);

  return decision.key;
}

for (const mode of MODES) {
  test(`P11e promote VR — ${mode.name}`, async ({ page, request }) => {
    test.setTimeout(120_000);
    await setMode(page, mode.lang, mode.theme);
    await loginAs(page, 'chairman');

    const bearer = await captureBearer(page);
    const decisionKey = await seedIssuedDecision(request, bearer);

    // 1. Issued decision detail with the Convert-to-ADR action.
    await page.goto(`/decisions/${decisionKey}`);
    await page.getByRole('button', { name: /Convert to ADR|تحويل إلى سجل قرار/ }).waitFor({ timeout: 20_000 });
    await shot(page, `p11e-decision-detail-${mode.name}`);

    // 2. The confirm dialog.
    await page.getByRole('button', { name: /Convert to ADR|تحويل إلى سجل قرار/ }).click();
    await page.getByRole('dialog').waitFor({ timeout: 10_000 });
    await shot(page, `p11e-convert-dialog-${mode.name}`);

    // 3. Confirm → the promoted ADR (born Draft, pre-filled from the decision).
    await page.getByRole('button', { name: /Create ADR|إنشاء السجل/ }).click();
    await page.waitForURL(/\/adrs\/ADR-/, { timeout: 20_000 });
    await page.getByRole('heading', { level: 1 }).waitFor({ timeout: 20_000 });
    await shot(page, `p11e-promoted-adr-${mode.name}`);
  });
}
