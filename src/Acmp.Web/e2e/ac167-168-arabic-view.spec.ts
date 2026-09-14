import { test, expect, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { loginAs } from './login';
import { captureBearer, meMember } from './apiHelpers';
import { apiCreateAction, apiCreateTopic, apiMembers, apiRecordDecision, apiScheduleMeeting } from './scenario';
import { realPdf } from './realFiles';

/*
 * AC-167 + AC-168 (DEC-192 r5): the Arabic-view pass. The operator found "secretary Seeded · 14/09/2026" by
 * looking; no test read the Arabic screens as a reader does. This walks the key screens in Arabic, over real data
 * made through the API, and collects EVERY problem before failing, so one run is a sweep of the platform:
 *   - an Arabic-Indic digit (AC-167: Latin digits everywhere);
 *   - a raw stream CODE where its Arabic name belongs (AC-168);
 *   - an English UI phrase from the catalogue, or the server's English health words (AC-168);
 *   - a person's name that is not isolated from the date or label beside it (AC-168).
 * Names, file names, keys and other user data may be Latin script; only UI text and digits are judged.
 */

const SERVER_ENGLISH = [/\b(Healthy|Unhealthy|Degraded)\b/, /\b\d+(\.\d+)?\s?ms\b/];

type Json = { [k: string]: Json } | string;
const leaves = (o: Json): string[] => (typeof o === 'string' ? [o] : Object.values(o).flatMap(leaves));
const catalogue = (lng: string) => leaves(JSON.parse(readFileSync(fileURLToPath(new URL(`../src/i18n/locales/${lng}.json`, import.meta.url)), 'utf8')));

/** English UI phrases that have no business on an Arabic screen: multi-word, no interpolation, and not text the
 *  Arabic catalogue itself carries (brand names and format lists legitimately appear in both). */
function englishPhrases(): string[] {
  const ar = catalogue('ar');
  return [...new Set(catalogue('en'))].filter((s) => !s.includes('{{') && s.length >= 8
    && /^[A-Za-z][A-Za-z ,.'’&/-]*[A-Za-z.]$/.test(s) && s.trim().split(/\s+/).length >= 2
    && !ar.some((a) => a.includes(s)));
}

async function settle(page: Page, url: string): Promise<void> {
  await page.goto(url);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(300);
}

/** Every problem on the current screen, labelled with the screen's name. */
async function audit(page: Page, screen: string, ctx: { phrases: string[]; codes: string[]; names: string[] }): Promise<string[]> {
  const found = await page.evaluate(({ phrases, codes, names }) => {
    const out: string[] = [];
    const body = document.body.innerText;
    if (document.documentElement.dir !== 'rtl' || !/[؀-ۿ]/.test(body)) out.push('NOT IN ARABIC - nothing below was judged');
    const indic = body.match(/[٠-٩۰-۹][٠-٩۰-۹\s.,/:٫٬]*/);
    if (indic) out.push(`Arabic-Indic digits "${indic[0]}"`);
    for (const p of phrases) if (body.includes(p)) out.push(`English UI text "${p}"`);

    const visible = (el: Element) => el.getClientRects().length > 0;
    for (const el of Array.from(document.body.querySelectorAll('*'))) {
      const own = Array.from(el.childNodes).filter((n) => n.nodeType === Node.TEXT_NODE).map((n) => n.nodeValue).join('').trim();
      // An <option> of a closed <select> has no layout box of its own: it is shown when its select is.
      const shown = el.tagName === 'OPTION' ? !!el.closest('select') && visible(el.closest('select')!) : visible(el);
      if (own && codes.includes(own) && shown) out.push(`stream code "${own}" in <${el.tagName.toLowerCase()} class="${el.className}">`);
    }

    // A name is isolated when a <bdi> or a dir attribute wraps it below its block, or when it stands alone in its
    // block (a block - or a flex/grid item, which is blockified - never reorders against its siblings).
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    for (let n = walker.nextNode(); n; n = walker.nextNode()) {
      const name = names.find((x) => (n!.nodeValue ?? '').includes(x));
      const start = n.parentElement;
      if (!name || !start || !visible(start)) continue;
      if ((n.nodeValue ?? '').includes(`⁨${name}⁩`)) continue; // isolated in the text itself (FSI...PDI)
      let e: HTMLElement | null = start;
      let isolated = false;
      while (e && e !== document.body) {
        if (e.tagName === 'BDI' || e.hasAttribute('dir')) { isolated = true; break; }
        if (!getComputedStyle(e).display.startsWith('inline')) break;
        e = e.parentElement;
      }
      const block = (e?.innerText ?? '').trim();
      if (!isolated && block !== name) out.push(`name "${name}" not isolated in "${block.replace(/\s+/g, ' ').slice(0, 90)}"`);
    }
    return out;
  }, ctx);
  for (const re of SERVER_ENGLISH) {
    const m = (await page.evaluate(() => document.body.innerText)).match(re);
    if (m) found.push(`server English "${m[0]}"`);
  }
  return [...new Set(found)].map((f) => `${screen}: ${f}`);
}

test.describe('AC-167/168 - the Arabic view, screen by screen', () => {
  test('committee screens in Arabic: Latin digits, localized streams, no English UI, isolated names', async ({ page }) => {
    test.setTimeout(240_000);
    await page.addInitScript(() => localStorage.setItem('i18nextLng', 'ar'));
    await loginAs(page, 'secretary');
    const bearer = await captureBearer(page);
    const headers = { Authorization: bearer, 'Content-Type': 'application/json' };

    const streams = (await (await page.request.get('/api/members/streams', { headers: { Authorization: bearer } })).json()) as Array<{ code: string; nameAr: string; isWildcard: boolean }>;
    const assignable = streams.filter((s) => !s.isWildcard);
    // The caller's own member row (provisioned on demand): on a fresh database it may be the only one.
    const me = await meMember(page, bearer);
    const members = await apiMembers(page.request, bearer);
    const ctx = {
      phrases: englishPhrases(),
      codes: assignable.filter((s) => s.nameAr !== s.code).map((s) => s.code),
      names: [...new Set(members.map((m) => m.fullName).filter((n) => n.length > 3))],
    };
    expect(ctx.phrases.length, 'the English catalogue must give the check a subject').toBeGreaterThan(200);
    expect(ctx.codes.length).toBeGreaterThan(0);

    // Real data through the API: a topic on two streams with an attachment (an attachment line and history lines),
    // a meeting, an action with a due date, a decision, and a wiki page with two versions.
    const topic = await apiCreateTopic(page.request, bearer, `AC168 ${Date.now()}`, assignable.slice(0, 2).map((s) => s.code));
    // A second topic on ANOTHER stream, linked, so the impact graph draws a cross-stream edge and names its stream.
    const other = await apiCreateTopic(page.request, bearer, `AC168 upstream ${Date.now()}`, [(assignable[2] ?? assignable[1]).code]);
    expect((await page.request.post('/api/traceability', { headers, data: {
      sourceType: 'Topic', sourceId: topic.id, sourceKey: topic.key, sourceTitle: topic.title,
      targetType: 'Topic', targetId: other.id, targetKey: other.key, targetTitle: other.title, relType: 'DependsOn', notes: null,
    } })).status()).toBe(201);
    const attach = await page.request.post(`/api/topics/${topic.id}/attachments`, {
      headers: { Authorization: bearer },
      multipart: { file: { name: 'evidence.pdf', mimeType: 'application/pdf', buffer: realPdf('AC-168') } },
    });
    expect(attach.status()).toBe(201);
    const meeting = await apiScheduleMeeting(page.request, bearer, `AC168 ${Date.now()}`, me);
    const action = await apiCreateAction(page.request, bearer, {
      title: `AC168 action ${Date.now()}`, ownerUserId: me.keycloakUserId, ownerName: me.fullName, sourceId: topic.id, dueDate: '2026-10-01',
    });
    const decision = await apiRecordDecision(page.request, bearer, { topicId: topic.id, title: 'AC168 decision', statement: 'Statement.', rationale: 'Rationale.' });
    const doc = { title: { en: 'AC168 page', ar: 'صفحة AC168' }, category: 'Governance', body: { en: 'Body.', ar: 'نص.' } };
    const created = await (await page.request.post('/api/knowledge/documents', { headers, data: { ...doc, tags: [] } })).json() as { id: string; key: string };
    expect((await page.request.put(`/api/knowledge/documents/${created.id}`, { headers, data: { ...doc, body: { en: 'Body v2.', ar: 'نص 2.' } } })).ok()).toBe(true);

    const problems: string[] = [];
    const visit = async (screen: string, url: string, then?: () => Promise<void>) => {
      await settle(page, url);
      if (then) { await then(); await page.waitForTimeout(300); }
      problems.push(...await audit(page, screen, ctx));
    };

    await visit('dashboard', '/');

    // CONTROL: a pass that finds nothing is only worth something if it CAN find something. Plant one of each problem
    // on a real screen and require the checker to report all four, then take them away.
    await page.evaluate(({ code, name, phrase }) => {
      const d = document.createElement('p');
      d.id = 'ac168-control';
      for (const text of [code, '٣ دقائق', phrase, `عدّله ${name} · 14/09/2026`]) d.append(Object.assign(document.createElement('span'), { textContent: text }));
      document.querySelector('main')!.append(d);
    }, { code: ctx.codes[0], name: me.fullName, phrase: ctx.phrases[0] });
    const planted = await audit(page, 'control', ctx);
    await page.evaluate(() => document.getElementById('ac168-control')!.remove());
    for (const kind of ['stream code', 'Arabic-Indic digits', 'English UI text', 'not isolated']) {
      expect(planted.some((p) => p.includes(kind)), `the checker must detect a planted ${kind}: ${planted.join(' | ')}`).toBe(true);
    }
    await visit('backlog', '/backlog');
    await visit('backlog kanban', '/backlog', () => page.getByText('كانبان', { exact: true }).click());
    await visit('reports', '/reports');
    await visit('impact graph', `/traceability/Topic/${topic.key}`);
    await visit('impact list', `/traceability/Topic/${topic.key}`, () => page.locator('.ig-seg-btn').nth(1).click());
    await visit('topic', `/topics/${topic.key}`);
    await visit('topic attachments', `/topics/${topic.key}`, () => page.getByRole('tab', { name: /المرفقات/ }).click());
    await visit('topic history', `/topics/${topic.key}`, () => page.getByRole('tab', { name: /السجل/ }).click());
    await visit('meetings', '/meetings');
    await visit('meeting', `/meetings/${meeting.key}`);
    await visit('meeting recording', `/meetings/${meeting.key}/recording`);
    await visit('decision', `/decisions/${decision.key}`);
    await visit('actions', '/actions');
    await visit('action', `/actions/${action.key}`);
    await visit('wiki', `/wiki/${created.key}`);
    await visit('wiki history', `/wiki/${created.key}`, async () => {
      await page.getByRole('button', { name: /History|السجل/ }).first().click();
      await page.getByRole('dialog').waitFor({ timeout: 10_000 });
    });

    expect(problems).toEqual([]);
  });

  test('system health in Arabic: translated checks and durations, Latin digits', async ({ page }) => {
    await page.addInitScript(() => localStorage.setItem('i18nextLng', 'ar'));
    await loginAs(page, 'administrator');
    const bearer = await captureBearer(page);
    const members = await apiMembers(page.request, bearer);
    await settle(page, '/admin/users');
    await expect(page.locator('main')).toContainText(/\d/); // durations are on screen: the digit check has a subject
    const problems = await audit(page, 'system health', { phrases: englishPhrases(), codes: [], names: members.map((m) => m.fullName) });
    expect(problems).toEqual([]);
  });
});
