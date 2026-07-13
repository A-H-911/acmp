import { test, type Page, type APIRequestContext } from '@playwright/test';
import { loginAs } from './login';
import { captureBearer } from './apiHelpers';

/*
 * P15b visual-review capture (NOT an assertion suite) — screenshots the Research UI in EN-light +
 * AR-RTL-dark for a human compare against "ACMP Research & Knowledge.dc.html" (isResearch):
 *   - register → missions list  (Mission · Lead · Findings/Recs counts · Status · Created)
 *   - detail   → mission header + Findings + Recommendations section cards.
 * Guardrail #14 (design-only, no backend — NOT rendered, so the capture is honestly reduced):
 *   Hypotheses + Acceptance-criteria sections, the Sources & artifacts aside, the register Topic
 *   column, and the Convert / Import-from-Keystone actions.
 * We seed 3 missions; one is Activated with 2 findings + 2 recommendations, so the register shows
 * status-chip variety (Active vs Proposed) + non-zero counts, and the detail shows populated
 * section cards under an Active status chip. Outputs to e2e/vr-out/.
 * Run (isolated stack up): npx playwright test p15b-research-vr --project=chromium
 */
const OUT = 'e2e/vr-out';
const JSON_H = { 'Content-Type': 'application/json' } as const;

const MODES = [
  { name: 'en-light', lang: 'en', theme: 'light' },
  { name: 'ar-dark', lang: 'ar', theme: 'dark' },
] as const;

const L = (en: string, ar: string) => ({ en, ar });

interface FindingSeed {
  summary: { en: string; ar: string };
  detail: { en: string; ar: string } | null;
  confidence: 'Low' | 'Medium' | 'High';
}
interface RecSeed {
  statement: { en: string; ar: string };
  rationale: { en: string; ar: string } | null;
  priority: 'Low' | 'Medium' | 'High';
}
interface MissionSeed {
  title: { en: string; ar: string };
  question: { en: string; ar: string };
  activate: boolean;
  findings: FindingSeed[];
  recommendations: RecSeed[];
}

const MISSIONS: MissionSeed[] = [
  {
    title: L('Event-sourcing for the audit ledger', 'مصادر الأحداث لسجل التدقيق'),
    question: L('Should the immutable audit ledger move to an event-sourced store to strengthen tamper-evidence?',
      'هل ينبغي نقل سجل التدقيق غير القابل للتغيير إلى مخزن قائم على مصادر الأحداث لتعزيز كشف العبث؟'),
    activate: true,
    findings: [
      { confidence: 'High',
        summary: L('A hash-chained append-only log already gives per-entry tamper detection today.',
          'يوفّر السجل المتسلسل التجزئة القابل للإلحاق فقط كشفًا للعبث لكل مدخلة اليوم.'),
        detail: L('Evidence: the current AuditChainVerifier reports the first broken link on demand.',
          'الدليل: يبلّغ مدقّق سلسلة التدقيق الحالي عن أول رابط مكسور عند الطلب.') },
      { confidence: 'Medium',
        summary: L('Event-sourcing adds replay + projections but also operational complexity for ≤20 users.',
          'تضيف مصادر الأحداث إعادة التشغيل والإسقاطات لكنها تزيد التعقيد التشغيلي لعدد مستخدمين ≤ ٢٠.'),
        detail: null },
    ],
    recommendations: [
      { priority: 'High',
        statement: L('Keep the hash-chained ledger; defer event-sourcing until a measured need appears.',
          'الإبقاء على السجل المتسلسل التجزئة، وتأجيل مصادر الأحداث حتى تظهر حاجة مقاسة.'),
        rationale: L('INV-002 forbids new datastores/patterns without a recorded, measured need.',
          'يمنع الثابت INV-002 إضافة مخازن أو أنماط جديدة دون حاجة مسجّلة ومقاسة.') },
      { priority: 'Medium',
        statement: L('Add a nightly chain-verify job so tampering is detected without a manual check.',
          'إضافة مهمة تحقق ليلية من السلسلة ليُكتشف العبث دون فحص يدوي.'),
        rationale: null },
    ],
  },
  {
    title: L('Search backend: SQL FTS vs OpenSearch', 'خلفية البحث: فهرسة SQL مقابل OpenSearch'),
    question: L('Is SQL Server full-text search adequate for Arabic committee terminology, or is OpenSearch needed?',
      'هل بحث النص الكامل في SQL Server كافٍ لمصطلحات اللجنة العربية، أم يلزم OpenSearch؟'),
    activate: false,
    findings: [],
    recommendations: [],
  },
  {
    title: L('Bilingual PDF export options', 'خيارات تصدير PDF ثنائي اللغة'),
    question: L('What is the lightest way to produce faithful bilingual, RTL-correct PDF reports on-prem?',
      'ما أخفّ طريقة لإنتاج تقارير PDF ثنائية اللغة وصحيحة من اليمين لليسار محليًا؟'),
    activate: false,
    findings: [],
    recommendations: [],
  },
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

async function seedResearch(request: APIRequestContext, bearer: string): Promise<string> {
  let firstActiveKey = '';
  for (const m of MISSIONS) {
    const create = await request.post('/api/research', {
      headers: { Authorization: bearer, ...JSON_H },
      data: { title: m.title, question: m.question },
    });
    if (!create.ok()) throw new Error(`create mission ${create.status()} ${await create.text()}`);
    const mission = (await create.json()) as { id: string; key: string };

    if (m.activate) {
      const act = await request.post(`/api/research/${mission.id}/activate`, { headers: { Authorization: bearer } });
      if (!act.ok()) throw new Error(`activate ${act.status()} ${await act.text()}`);

      for (const f of m.findings) {
        const r = await request.post(`/api/research/${mission.id}/findings`, {
          headers: { Authorization: bearer, ...JSON_H },
          data: { summary: f.summary, detail: f.detail, confidence: f.confidence },
        });
        if (!r.ok()) throw new Error(`add finding ${r.status()} ${await r.text()}`);
      }
      for (const rec of m.recommendations) {
        const r = await request.post(`/api/research/${mission.id}/recommendations`, {
          headers: { Authorization: bearer, ...JSON_H },
          data: { statement: rec.statement, rationale: rec.rationale, priority: rec.priority, linkedTopicId: null },
        });
        if (!r.ok()) throw new Error(`add recommendation ${r.status()} ${await r.text()}`);
      }
      if (!firstActiveKey) firstActiveKey = mission.key;
    }
  }
  return firstActiveKey;
}

for (const mode of MODES) {
  test(`P15b research VR — ${mode.name}`, async ({ page, request }) => {
    test.setTimeout(120_000);
    await setMode(page, mode.lang, mode.theme);
    await loginAs(page, 'secretary');

    const bearer = await captureBearer(page);
    // The /backlog load above fires a login-time Membership.ProfileSynced audit append. Let it commit
    // before the seed burst: SqlAuditSink takes no app-lock, so two appends racing the audit hash-chain's
    // PreviousHash unique index make the loser 500 (the ADR-0009/0026-accepted concurrency window). A real
    // user never drives two HTTP clients in the same 100ms — this wait restores that reality for the capture.
    await page.waitForLoadState('networkidle');
    const activeKey = await seedResearch(request, bearer);

    // Register (isResearch, list view). Wait for a seeded mission row via its locale-independent key.
    await page.goto('/research');
    await page.getByRole('heading', { level: 1 }).waitFor({ timeout: 20_000 });
    await page.getByText(/RMS-\d{4}-/).first().waitFor({ timeout: 20_000 });
    await shot(page, `p15b-research-register-${mode.name}`);

    // Detail (isResearch, detail view) — the Active mission with populated Findings + Recommendations.
    await page.goto(`/research/${activeKey}`);
    await page.getByRole('heading', { level: 1 }).waitFor({ timeout: 20_000 });
    await page.getByText(activeKey).first().waitFor({ timeout: 20_000 });
    await shot(page, `p15b-research-detail-${mode.name}`);
  });
}
