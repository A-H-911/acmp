import { test, type Page, type APIRequestContext } from '@playwright/test';
import { loginAs } from './login';
import { captureBearer } from './apiHelpers';
import { apiMembers } from './scenario';

/*
 * P11d visual-review capture (NOT an assertion suite) — screenshots the Invariant UI in EN-light +
 * AR-RTL-dark for a human compare against the design:
 *   - register  → "ACMP Lists & Registers.dc.html" isInvTab (gInv: ID · Statement · Category · Scope · Status)
 *   - create    → "ACMP Create Flows & Dialogs.dc.html" invariant form (Category · Scope · Statement ·
 *                 Rationale · Owner)
 *   - detail    → NO-REFERENCE composition (guardrail #14) — a sanity capture of the read-only record.
 * We seed 5 invariants across categories/scopes; two are proposed+approved (→ Active), three stay Draft,
 * so the register shows category-dot variety + Active/Draft status chips. Outputs to e2e/vr-out/.
 * Run (stack up): npx playwright test p11d-invariant-vr --project=chromium
 */
const OUT = 'vr-out';
const JSON_H = { 'Content-Type': 'application/json' } as const;

const MODES = [
  { name: 'en-light', lang: 'en', theme: 'light' },
  { name: 'ar-dark', lang: 'ar', theme: 'dark' },
] as const;

interface Seed {
  category: string;
  scope: string;
  statement: { en: string; ar: string };
  rationale: { en: string; ar: string };
  activate: boolean;
}

const SEEDS: Seed[] = [
  { category: 'Security', scope: 'Platform', activate: true,
    statement: { en: 'Every committee-governed service must authenticate through the standard identity provider.', ar: 'يجب أن تُصادَق كل خدمة خاضعة للحوكمة عبر موفّر الهوية المعياري.' },
    rationale: { en: 'Centralized identity is auditable and revocable; scattered credential stores are the top breach vector.', ar: 'الهوية المركزية قابلة للتدقيق والإلغاء؛ ومخازن الاعتماد المبعثرة أبرز ناقل اختراق.' } },
  { category: 'Data', scope: 'MultiStream', activate: true,
    statement: { en: 'No service may share a mutable database schema across stream boundaries.', ar: 'لا يجوز لأي خدمة مشاركة مخطّط قاعدة بيانات قابل للتغيير عبر حدود المسارات.' },
    rationale: { en: 'Shared mutable schemas couple deployments and erase ownership boundaries.', ar: 'المخططات المشتركة القابلة للتغيير تربط عمليات النشر وتمحو حدود الملكية.' } },
  { category: 'Performance', scope: 'OrgWide', activate: false,
    statement: { en: 'All synchronous inter-service calls must complete within a 500ms p99 budget.', ar: 'يجب أن تكتمل جميع الاستدعاءات المتزامنة بين الخدمات ضمن ميزانية ٥٠٠ ملّي ثانية للمئين ٩٩.' },
    rationale: { en: 'A hard latency budget keeps synchronous fan-out from compounding into user-visible stalls.', ar: 'ميزانية زمن استجابة صارمة تمنع التوزّع المتزامن من التراكم إلى تعثّرات يراها المستخدم.' } },
  { category: 'Interoperability', scope: 'SingleStream', activate: false,
    statement: { en: 'All public APIs must publish a versioned OpenAPI 3 contract.', ar: 'يجب أن تنشر جميع واجهات البرمجة العامة عقد OpenAPI 3 مُصدَّرًا بإصدار.' },
    rationale: { en: 'A machine-readable contract is the basis for consumer-driven compatibility checks.', ar: 'العقد القابل للقراءة آليًا هو أساس فحوص التوافق المدفوعة بالمستهلك.' } },
  { category: 'Compliance', scope: 'Platform', activate: false,
    statement: { en: 'All personal data at rest must be encrypted with a rotating, committee-managed key.', ar: 'يجب تشفير جميع البيانات الشخصية المخزّنة بمفتاح دوّار تديره اللجنة.' },
    rationale: { en: 'Encryption at rest with key rotation is the baseline control for the compliance regime.', ar: 'التشفير أثناء التخزين مع تدوير المفاتيح هو الضابط الأساسي لنظام الامتثال.' } },
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

async function seedInvariants(request: APIRequestContext, bearer: string): Promise<string> {
  const members = await apiMembers(request, bearer);
  const owner = members.find((m) => m.role === 'Secretary') ?? members[0];
  let firstActiveKey = '';

  for (const s of SEEDS) {
    const create = await request.post('/api/invariants', {
      headers: { Authorization: bearer, ...JSON_H },
      data: {
        category: s.category, scope: s.scope,
        statement: s.statement, rationale: s.rationale,
        exceptionsPolicy: null,
        ownerUserId: owner.publicId, ownerName: owner.fullName,
      },
    });
    if (create.status() !== 201) throw new Error(`create invariant ${create.status()} ${await create.text()}`);
    const inv = (await create.json()) as { id: string; key: string };

    if (s.activate) {
      const propose = await request.post(`/api/invariants/${inv.id}/propose`, { headers: { Authorization: bearer } });
      if (!propose.ok()) throw new Error(`propose ${propose.status()} ${await propose.text()}`);
      const approve = await request.post(`/api/invariants/${inv.id}/approve`, { headers: { Authorization: bearer } });
      if (!approve.ok()) throw new Error(`approve ${approve.status()} ${await approve.text()}`);
      if (!firstActiveKey) firstActiveKey = inv.key;
    }
  }
  return firstActiveKey;
}

for (const mode of MODES) {
  test(`P11d invariant VR — ${mode.name}`, async ({ page, request }) => {
    test.setTimeout(120_000);
    await setMode(page, mode.lang, mode.theme);
    await loginAs(page, 'secretary');

    const bearer = await captureBearer(page);
    const activeKey = await seedInvariants(request, bearer);

    // Register (isInvTab).
    await page.goto('/invariants');
    // The active governance tab is a nav item marked aria-current="page" (not role=tab).
    await page.locator('[aria-current="page"]').filter({ hasText: /Architecture Invariants|الثوابت المعمارية/ }).waitFor({ timeout: 20_000 });
    await page.getByRole('cell', { name: /500ms|٥٠٠/ }).first().waitFor({ timeout: 20_000 });
    await shot(page, `p11d-invariants-register-${mode.name}`);

    // Create dialog (invariant form).
    await page.getByRole('button', { name: /New invariant|ثابت جديد/ }).click();
    await page.getByRole('dialog').waitFor({ timeout: 10_000 });
    await shot(page, `p11d-invariant-create-${mode.name}`);
    await page.keyboard.press('Escape');

    // Detail (no-reference composition) — an Active record with the metadata aside + traceability.
    await page.goto(`/invariants/${activeKey}`);
    await page.getByRole('heading', { level: 1 }).waitFor({ timeout: 20_000 });
    await shot(page, `p11d-invariant-detail-${mode.name}`);
  });
}
