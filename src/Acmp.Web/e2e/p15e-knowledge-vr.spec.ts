import { test, type Page, type APIRequestContext } from '@playwright/test';
import { loginAs } from './login';
import { captureBearer } from './apiHelpers';

/*
 * P15e visual-review capture (NOT an assertion suite) — screenshots the Knowledge wiki + template manager in
 * EN-light + AR-RTL-dark for a human compare against "ACMP Research & Knowledge.dc.html" (wiki) and
 * "ACMP Administration.dc.html" (templates table). Seeds a showcase set that exercises every surface:
 *   - 3 documents across 2 categories ("spaces"); one Published+versioned (v2) with tags + a linked topic,
 *     one Draft (manager-only tree row), one Published in a second space
 *   - a Document→Topic traceability edge → the reading view's Linked-artifacts panel is non-empty
 *   - 3 templates of varied TargetType
 * Captures per mode: wiki tree + reading view, wiki split editor, version-history panel, template table,
 * template form dialog. Run (isolated stack up): npx playwright test p15e-knowledge-vr --project=chromium
 */
const OUT = 'e2e/vr-out';
const JSON_H = { 'Content-Type': 'application/json' } as const;

const MODES = [
  { name: 'en-light', lang: 'en', theme: 'light' },
  { name: 'ar-dark', lang: 'ar', theme: 'dark' },
] as const;

const L = (en: string, ar: string) => ({ en, ar });
const EDIT = /Edit|تحرير/;
const HISTORY = /History|السجل/;
const NEW_TEMPLATE = /New template|قالب جديد/;

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

async function put(request: APIRequestContext, bearer: string, url: string, data: unknown): Promise<void> {
  const r = await request.put(url, { headers: { Authorization: bearer, ...JSON_H }, data });
  if (!r.ok()) throw new Error(`PUT ${url} → ${r.status()} ${await r.text()}`);
}

const BODY_EN = `A short overview of how the committee governs architecture decisions.

## Scope

This page covers the decision lifecycle and how it links to ADRs. See [[TOP-2026-001]] for the originating topic.

- Intake and triage
- Agenda and quorum
- Decision and ADR

> Published pages stay immutable once superseded.`;
const BODY_AR = `نظرة موجزة على كيفية حوكمة اللجنة لقرارات المعمارية.

## النطاق

تغطي هذه الصفحة دورة حياة القرار وارتباطها بسجلات القرار. انظر [[TOP-2026-001]] للموضوع الأصلي.

- الاستلام والفرز
- جدول الأعمال والنصاب
- القرار والسجل

> تبقى الصفحات المنشورة غير قابلة للتعديل بعد استبدالها.`;

async function seed(request: APIRequestContext, bearer: string): Promise<string> {
  // A topic → the linked-artifacts edge target.
  const topic = await post(request, bearer, '/api/topics', {
    title: 'Adopt a single identity provider', description: 'Consolidate per-stream IdPs.',
    justification: 'Cuts duplicated maintenance.', type: 'ArchitectureDecision', urgency: 'Normal',
    source: 'CommitteeMember', streams: ['IAM'], systems: [], tags: [],
  });

  // doc1: Governance, versioned (create → edit = v2) → publish; the showcase page.
  const doc1 = await post(request, bearer, '/api/knowledge/documents', {
    title: L('Committee governance model', 'نموذج حوكمة اللجنة'),
    category: 'Governance', body: L(BODY_EN, BODY_AR), tags: ['governance', 'process', 'reference'],
  });
  await put(request, bearer, `/api/knowledge/documents/${doc1.id}`, {
    title: L('Committee governance model', 'نموذج حوكمة اللجنة'),
    category: 'Governance', body: L(BODY_EN, BODY_AR),
  });
  await post(request, bearer, `/api/knowledge/documents/${doc1.id}/publish`);

  // doc2: Governance, Draft (manager-only tree row). doc3: Standards, Published (a 2nd space).
  await post(request, bearer, '/api/knowledge/documents', {
    title: L('Decision lifecycle (draft)', 'دورة حياة القرار (مسودة)'),
    category: 'Governance', body: L('Work in progress.', 'قيد الإعداد.'), tags: ['draft'],
  });
  const doc3 = await post(request, bearer, '/api/knowledge/documents', {
    title: L('Writing an ADR (MADR)', 'كتابة سجل قرار (MADR)'),
    category: 'Standards', body: L('# ADR guide\n\nUse the MADR-lite format.', '# دليل السجل\n\nاستخدم صيغة MADR المبسطة.'), tags: ['standards'],
  });
  await post(request, bearer, `/api/knowledge/documents/${doc3.id}/publish`);

  // Document → Topic edge (best-effort; the panel still renders empty if this ever changes).
  await post(request, bearer, '/api/traceability', {
    sourceType: 'Document', sourceId: doc1.id, sourceKey: doc1.key, sourceTitle: 'Committee governance model',
    targetType: 'Topic', targetId: topic.id, targetKey: topic.key, targetTitle: 'Adopt a single identity provider',
    relType: 'References', notes: null,
  }).catch(() => { /* non-fatal for the capture */ });

  // Templates of varied TargetType.
  for (const tpl of [
    { name: L('Standard topic submission', 'تقديم موضوع قياسي'), targetType: 'Topic', body: '## Summary\n\n{{summary}}' },
    { name: L('Architecture Decision Record', 'سجل قرار معماري'), targetType: 'Adr', body: '## Context\n\n{{context}}' },
    { name: L('Minutes of meeting', 'محضر اجتماع'), targetType: 'MinutesOfMeeting', body: '## Attendees\n\n{{attendees}}' },
  ]) {
    await post(request, bearer, '/api/knowledge/templates', tpl);
  }

  return doc1.key as string;
}

for (const mode of MODES) {
  test(`P15e knowledge VR — ${mode.name}`, async ({ page, request }) => {
    test.setTimeout(120_000);
    await setMode(page, mode.lang, mode.theme);
    await loginAs(page, 'secretary');

    const bearer = await captureBearer(page);
    await page.waitForLoadState('networkidle');
    const docKey = await seed(request, bearer);

    // Wiki — tree ("spaces") + reading view (serif title, meta, markdown body, linked-artifacts panel).
    await page.goto(`/wiki/${docKey}`);
    await page.getByRole('heading', { level: 1 }).waitFor({ timeout: 20_000 });
    await shot(page, `p15e-wiki-reading-${mode.name}`);

    // Version-history panel (Published+edited doc has v1 + v2).
    await page.getByRole('button', { name: HISTORY }).first().click();
    await page.getByRole('dialog').waitFor({ timeout: 10_000 }).catch(() => {});
    await shot(page, `p15e-wiki-history-${mode.name}`);
    await page.keyboard.press('Escape');
    await page.getByRole('dialog').waitFor({ state: 'detached', timeout: 10_000 }).catch(() => {});

    // Split editor (markdown | preview).
    await page.getByRole('button', { name: EDIT }).first().click();
    await page.waitForTimeout(600);
    await shot(page, `p15e-wiki-editor-${mode.name}`);

    // Template manager — the .gTpl table.
    await page.goto('/templates');
    await page.getByRole('heading', { level: 1 }).waitFor({ timeout: 20_000 });
    await page.getByText(/TPL-\d{4}-/).first().waitFor({ timeout: 20_000 }).catch(() => {});
    await shot(page, `p15e-templates-table-${mode.name}`);

    // Template create form dialog.
    await page.getByRole('button', { name: NEW_TEMPLATE }).first().click();
    await page.getByRole('dialog').waitFor({ timeout: 10_000 });
    await shot(page, `p15e-template-form-${mode.name}`);
  });
}
