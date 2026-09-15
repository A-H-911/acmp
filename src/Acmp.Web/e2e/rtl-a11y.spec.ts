import { test, expect, type Page } from '@playwright/test';
import { createRequire } from 'node:module';
import { readFileSync } from 'node:fs';
import { loginAs } from './login';
import { captureBearer, meMember, roleSession } from './apiHelpers';
import {
  apiAddAgendaItem, apiCastBallot, apiCloseVote, apiConfigureVote, apiCreateAction, apiCreateTopic, apiIssueDecision, apiMembers,
  apiOpenVote, apiPreparedTopic, apiRecordDecision, apiScheduleMeeting,
} from './scenario';
import { A11Y_ROUTES, type A11yPrincipal } from '../src/test/a11yRoutes';

/*
 * S6b-3 (ADR-0016 §2) — the RTL/Arabic + accessibility pass, the last E2E slice. Proves the real
 * app flips to `dir="rtl"` under Arabic and runs an automated axe sweep on key authenticated
 * screens in BOTH locales. Uses the already-installed `axe-core` — no new dependency.
 *
 * The app ships a strict CSP (`script-src 'self'`), so `addScriptTag` (inline injection) is blocked
 * — we run the axe source through `page.evaluate` instead, which executes via CDP and bypasses page
 * CSP.
 *
 * ⚠ `color-contrast` IS ON (AC-172, WBS-40.8). It was switched off here from S6b-3 until 2026-09-15 on the
 * grounds that contrast was a design-token concern, so the only contrast check was the token-pair table in
 * src/styles/contrast.test.ts - which paired `--text-3` with `--surface` alone while the app painted it on five
 * surfaces (DEF-186, 147 rendered failures). The rendered check sees what the table cannot: the surface a
 * token actually lands on. It is the same axe rule Lighthouse's contrast audit runs.
 */
const require = createRequire(import.meta.url);
const AXE_SOURCE = readFileSync(require.resolve('axe-core/axe.min.js'), 'utf8');
// D-23: include WCAG 2.2 AA — the machine-testable addition over 2.1 is `target-size` (SC 2.5.8, >=24x24px).
const WCAG = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

interface Violation {
  id: string;
  impact: string | null;
  nodes: number;
  /** The first few offending nodes, with axe's own summary (for color-contrast: the two colours and the ratio),
   *  so a failure names WHAT failed, not only how many - AC-172's controls must name the pairs. */
  sample: string[];
}

interface AxeRun {
  violations: Violation[];
  /** Elements the color-contrast rule actually evaluated (passed, failed or needs review). */
  contrastChecked: number;
  /** Elements carrying Arabic text that the rule DECIDED (passed or failed). The Arabic sweep's subject (DEF-189). */
  arabicContrastChecked: number;
  /** Elements inside the `subject` selector passed to runAxe that the rule decided (AC-173: the formerly dimmed one). */
  subjectContrastChecked: number;
}

/*
 * ⛔ DEF-189: axe-core SKIPS ARABIC TEXT IN color-contrast UNLESS TOLD NOT TO. The rule tests an element only if
 * one of its text nodes is not an "icon ligature", and axe decides that on a canvas - the first character drawn
 * alone against the whole string, 15% apart in pixels and in width. Arabic letters change shape when joined,
 * so ordinary Arabic words pass that test, and after three hits axe caches the verdict for the whole FONT FAMILY:
 * every later node in the Arabic font stack, Latin included, is dropped. A dropped element is not a violation
 * and not "incomplete" - it is simply absent, so the Arabic sweep passed while checking 25 of 56 elements on
 * /admin/users and missing a 2.9:1 chip the English sweep caught.
 *
 * The app has no icon fonts (its icons are inline SVG), so the heuristic protects nothing here. Before each run
 * every font family on the page is entered in axe's own per-font cache as "seen three times, never a ligature",
 * which is the state in which isIconLigature answers false at once. axe._cache is internal API; if a later axe
 * stops reading it, the Arabic subject check below fails by name rather than letting the sweep go blind again.
 */
async function runAxe(page: Page, subject?: string): Promise<AxeRun> {
  // ⚠ A CSS TRANSITION STILL RUNNING IS A COLOUR IN BETWEEN. AC-173's control put `opacity: 0.3` back on the graph's
  // dimmed nodes, whose card transitions opacity over 0.15s: a dark run caught them mid-fade and passed, while a
  // run a moment later measured 2.34:1. So axe waits for transitions (not infinite keyframe animations) to finish.
  await page.waitForFunction(() =>
    document.getAnimations().every((a) => !(a instanceof CSSTransition) || a.playState !== 'running'),
  );
  await page.evaluate(AXE_SOURCE); // defines window.axe; CDP eval bypasses the page CSP
  return page.evaluate(async ([tags, subjectSelector]) => {
    type AxeNode = { target: unknown[]; failureSummary?: string };
    type Result = { id: string; impact: string | null; nodes: AxeNode[] };
    // axe is a page global defined by the evaluate above.
    const axe = (window as unknown as {
      axe: {
        _cache: { set: (key: string, value: unknown) => void };
        run: (ctx: Document, opts: unknown) => Promise<{ violations: Result[]; passes: Result[]; incomplete: Result[] }>;
      };
    }).axe;

    const families = new Set(Array.from(document.querySelectorAll('*'), (el) => getComputedStyle(el).fontFamily));
    axe._cache.set('fonts', Object.fromEntries([...families].map((f) => [f, { occurrences: 3, numLigatures: 0 }])));

    const result = await axe.run(document, { runOnly: { type: 'tag', values: tags } });

    const isArabic = (s: string) => Array.from(s).some((ch) => ch.charCodeAt(0) >= 0x0600 && ch.charCodeAt(0) <= 0x06ff);
    const contrastOf = (rs: Result[]) => rs.filter((r) => r.id === 'color-contrast').flatMap((r) => r.nodes);
    const contrastNodes = [...contrastOf(result.passes), ...contrastOf(result.violations), ...contrastOf(result.incomplete)];
    // DECIDED = passed or failed. An "incomplete" is axe saying it could not tell (e.g. text over the graph's SVG edge
    // layer), so it proves nothing about the element and does not count towards a subject clause.
    const decided = [...contrastOf(result.passes), ...contrastOf(result.violations)];
    const elementOf = (n: AxeNode) => (typeof n.target[0] === 'string' ? document.querySelector(n.target[0]) : null);
    const textOf = (n: AxeNode) => elementOf(n)?.textContent ?? '';
    return {
      violations: result.violations.map((v) => ({
        id: v.id,
        impact: v.impact,
        nodes: v.nodes.length,
        sample: v.nodes.slice(0, 3).map((n) => `${n.target.join(' ')} :: ${(n.failureSummary ?? '').replace(/\s+/g, ' ').slice(0, 220)}`),
      })),
      contrastChecked: contrastNodes.length,
      arabicContrastChecked: decided.filter((n) => isArabic(textOf(n))).length,
      subjectContrastChecked: subjectSelector ? decided.filter((n) => elementOf(n)?.closest(subjectSelector)).length : 0,
    };
  }, [WCAG, subject ?? ''] as const);
}

async function axeViolations(page: Page): Promise<Violation[]> {
  return (await runAxe(page)).violations;
}

/*
 * AC-172: every manifest route is swept in all four combinations of theme and language. Both are read from
 * localStorage at start-up (theme.ts; i18next's detector), so an init script sets them before every
 * navigation - including the sign-in round trip, where it is harmless on Keycloak's origin.
 */
interface Mode {
  lang: 'en' | 'ar';
  theme: 'light' | 'dark';
}
const MODES: readonly Mode[] = [
  { lang: 'en', theme: 'light' },
  { lang: 'ar', theme: 'light' },
  { lang: 'en', theme: 'dark' },
  { lang: 'ar', theme: 'dark' },
];
const modeName = (m: Mode) => `${m.lang === 'ar' ? 'Arabic/RTL' : 'English'}, ${m.theme} theme`;

async function useMode(page: Page, mode: Mode): Promise<void> {
  await page.addInitScript(([l, t]) => {
    localStorage.setItem('i18nextLng', l);
    localStorage.setItem('acmp-theme', t);
  }, [mode.lang, mode.theme] as const);
}

/** Proof the page is in the mode the sweep claims. Without it a dark sweep that rendered light would report the
 *  light result twice and call it both (LL-060: an instrument must report on itself). */
async function expectMode(page: Page, mode: Mode): Promise<void> {
  await expect(page.locator('html')).toHaveAttribute('dir', mode.lang === 'ar' ? 'rtl' : 'ltr');
  await expect(page.locator('html')).toHaveAttribute('data-theme', mode.theme);
}

async function switchToArabic(page: Page): Promise<void> {
  await page.getByRole('button', { name: /Switch to/ }).click();
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  await expect(page.locator('html')).toHaveAttribute('lang', 'ar');
}

/*
 * ROUTE COVERAGE - AC-170 (WBS-40.6, DEC-196), which grew out of the operator decision of 2026-09-01 (DEF-126).
 *
 * ⛔ THE LIST IS NOT HERE ANY MORE, AND THAT IS THE POINT. The routes come from src/test/a11yRoutes.ts, which
 * src/test/a11yRouteCoverage.test.ts compares with App.tsx's own route table: a route added to the app and not
 * to the manifest fails THAT test by name, so this sweep cannot quietly fall behind the app again. Before
 * AC-170 this file held a hand-kept list of 17 routes and reached 23 static routes in all; the parameterised
 * routes (topics/:key, meetings/:key and its tabs, decisions/:key, ...) were excluded because each needs a
 * seeded record. They are now opened on records seedRecords() creates first.
 *
 * WHY ONE TEST PER LOCALE RATHER THAN ONE PER ROUTE. An audit wants the WHOLE picture from one run: a
 * per-route test stops the suite at the first offender and each CI cycle then reveals exactly one more.
 * Collecting a route->violations map and asserting it empty names every offending route at once.
 */

/** One place the sweep stops: the URL, and for a seeded record the text that proves the RECORD rendered. */
interface Stop {
  url: string;
  subject?: string;
}

/**
 * Seed one record of every kind the parameterised routes open, and map each pattern to its URL.
 *
 * ⛔ A parameterised route that renders its "not found" state still renders the shell and #main, so the #main
 * wait alone would sweep an empty page and call it clean (LL-041, DEF-126). Each stop therefore carries a
 * SUBJECT - the record's key - that must be on the page before axe runs.
 */
async function seedRecords(page: Page): Promise<Record<string, Stop>> {
  const bearer = await captureBearer(page);
  const me = await meMember(page, bearer);
  const H = { Authorization: bearer, 'Content-Type': 'application/json' };
  const post = async (url: string, data: unknown): Promise<{ id: string; key: string }> => {
    const res = await page.request.post(url, { headers: H, data });
    if (!res.ok()) throw new Error(`[a11y seed] POST ${url} ${res.status()} ${await res.text()}`);
    return res.json();
  };
  const L = (s: string) => ({ en: s, ar: s });
  const stamp = Date.now().toString(36);

  const topic = await apiCreateTopic(page.request, bearer, `A11y sweep topic ${stamp}`);
  const prepared = await apiPreparedTopic(page.request, bearer, `A11y sweep prepared ${stamp}`, me);
  const meeting = await apiScheduleMeeting(page.request, bearer, `A11y sweep meeting ${stamp}`, me);
  await apiAddAgendaItem(page.request, bearer, meeting.id, prepared, me);
  const decision = await apiRecordDecision(page.request, bearer, {
    topicId: topic.id, title: `A11y sweep decision ${stamp}`, statement: 'Sweep statement.', rationale: 'Sweep rationale.',
  });
  const vote = await apiConfigureVote(page.request, bearer, {
    topicId: topic.id, eligibleVoters: [{ userId: me.keycloakUserId, name: me.fullName }],
  });
  const action = await apiCreateAction(page.request, bearer, {
    title: `A11y sweep action ${stamp}`, ownerUserId: me.keycloakUserId, ownerName: me.fullName, sourceId: topic.id, dueDate: '2026-12-01',
  });
  const adr = await post('/api/adrs', {
    title: L(`A11y sweep ADR ${stamp}`), context: L('Sweep context'), decisionDrivers: null, decisionText: L('Sweep decision'),
    consequencesPositive: null, consequencesNegative: null, options: null,
  });
  const invariant = await post('/api/invariants', {
    category: 'Security', scope: 'Platform', statement: L(`A11y sweep invariant ${stamp}`), rationale: L('Sweep rationale'),
    exceptionsPolicy: null, ownerUserId: me.keycloakUserId, ownerName: me.fullName,
  });
  const risk = await post('/api/risks', {
    title: L(`A11y sweep risk ${stamp}`), description: null, likelihood: 'High', impact: 'Medium',
    ownerUserId: me.keycloakUserId, ownerName: me.fullName, subjectType: 'Topic', subjectId: topic.id,
    subjectKey: topic.key, initialMitigation: L('Sweep mitigation'),
  });
  const dependency = await post('/api/dependencies', {
    fromType: 'Topic', fromId: topic.id, fromKey: topic.key, fromTitle: topic.title,
    toType: 'Action', toId: action.id, toKey: action.key, toTitle: `A11y sweep action ${stamp}`,
    kind: 'BlockedBy', note: null,
  });
  const mission = await post('/api/research', {
    title: L(`A11y sweep mission ${stamp}`), question: L('Does the sweep reach the research page?'),
  });
  const doc = await post('/api/knowledge/documents', {
    title: L(`A11y sweep page ${stamp}`), category: 'Governance', body: L('Sweep body.'), tags: [],
  });

  const m = `/meetings/${meeting.key}`;
  return {
    '/session/preview': { url: `/session/preview?meetingId=${meeting.id}&topicId=${prepared.id}`, subject: prepared.title },
    '/topics/:key': { url: `/topics/${topic.key}`, subject: topic.key },
    '/topics/:key/edit': { url: `/topics/${topic.key}/edit`, subject: topic.key },
    '/meetings/:key': { url: m, subject: meeting.key },
    '/meetings/:key/agenda': { url: `${m}/agenda`, subject: meeting.key },
    '/meetings/:key/attendance': { url: `${m}/attendance`, subject: meeting.key },
    '/meetings/:key/notes': { url: `${m}/notes`, subject: meeting.key },
    '/meetings/:key/minutes': { url: `${m}/minutes`, subject: meeting.key },
    '/meetings/:key/recording': { url: `${m}/recording`, subject: meeting.key },
    '/decisions/:key': { url: `/decisions/${decision.key}`, subject: decision.key },
    '/votes/:key': { url: `/votes/${vote.key}`, subject: vote.key },
    '/actions/:key': { url: `/actions/${action.key}`, subject: action.key },
    '/adrs/:key': { url: `/adrs/${adr.key}`, subject: adr.key },
    '/invariants/:key': { url: `/invariants/${invariant.key}`, subject: invariant.key },
    '/risks/:key': { url: `/risks/${risk.key}`, subject: risk.key },
    '/dependencies/:key': { url: `/dependencies/${dependency.key}`, subject: dependency.key },
    '/traceability/:type/:key': { url: `/traceability/Topic/${topic.key}`, subject: topic.key },
    '/research/:key': { url: `/research/${mission.key}`, subject: mission.key },
    '/wiki/:key': { url: `/wiki/${doc.key}`, subject: doc.key },
  };
}

/** The manifest's routes for one principal, each resolved to a place to stop. A parameterised pattern with no
 *  seeded record is an error, never a skip: a route the sweep silently cannot reach is the gap AC-170 closes. */
function stopsFor(as: A11yPrincipal, seeded: Record<string, Stop> = {}): Stop[] {
  return A11Y_ROUTES.filter((r) => r.as === as).map((r) => {
    if (seeded[r.pattern]) return seeded[r.pattern];
    if (r.pattern.includes(':')) throw new Error(`[a11y] no seeded record for ${r.pattern} - add it to seedRecords()`);
    return { url: r.pattern };
  });
}

/**
 * Visit each stop, prove it rendered, and collect its violations.
 *
 * ⛔ THE `#main` WAIT IS THE SUBJECT CLAUSE (DEF-126, LL-041). Without it a route that redirected to login, or
 * rendered nothing, would be swept and reported clean. `#main` is AppShell's own landmark, so it is present
 * only when the authenticated shell actually rendered the route - and a seeded stop must also show its record.
 */
async function violationsByRoute(page: Page, stops: readonly Stop[], mode: Mode): Promise<Record<string, Violation[]>> {
  const offenders: Record<string, Violation[]> = {};
  let checked = 0;
  for (const stop of stops) {
    await page.goto(stop.url);
    await expect(page.locator('#main'), `${stop.url} rendered the shell`).toBeVisible();
    if (stop.subject) await expect(page.locator('#main'), `${stop.url} shows its record`).toContainText(stop.subject);
    await expectMode(page, mode);
    const run = await runAxe(page);
    expectContrastSubject(run, mode, stop.url);
    checked += run.contrastChecked;
    if (run.violations.length > 0) offenders[stop.url] = run.violations;
  }
  // The sweep reports on itself (LL-060): the report shows how many routes each run actually visited, in which
  // mode, and how many elements the contrast rule evaluated, so a green result that swept nothing cannot read
  // like one that swept everything.
  test.info().annotations.push({
    type: 'a11y-routes-swept',
    description: `${modeName(mode)} - ${stops.length} routes, ${checked} contrast checks: ${stops.map((s) => s.url).join(' ')}`,
  });
  return offenders;
}

/** The contrast rule must have looked at something - and, in Arabic, at Arabic text (DEF-189). Every page carries
 *  the shell's navigation, so a zero here means the rule went blind, not that the page was empty. */
function expectContrastSubject(run: AxeRun, mode: Mode, where: string): void {
  expect(run.contrastChecked, `${where} (${modeName(mode)}): the color-contrast rule evaluated no element`).toBeGreaterThan(0);
  if (mode.lang === 'ar') {
    expect(run.arabicContrastChecked, `${where} (${modeName(mode)}): the color-contrast rule evaluated no Arabic text (DEF-189)`).toBeGreaterThan(0);
  }
}

test.describe('S6b-3 — RTL/Arabic + accessibility', () => {
  test('the app flips to RTL Arabic from the top-bar control', async ({ page }) => {
    await loginAs(page, 'secretary');
    await page.goto('/backlog');
    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');

    await switchToArabic(page);
    // i18n really switched: the toggle now offers the way back to English.
    await expect(page.getByRole('button', { name: /English/ })).toBeVisible();
  });

  test('Backlog is axe-clean in both English and Arabic', async ({ page }) => {
    await loginAs(page, 'secretary');
    await page.goto('/backlog');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    expect(await axeViolations(page), 'Backlog (EN) axe violations').toEqual([]);

    await switchToArabic(page);
    expect(await axeViolations(page), 'Backlog (AR/RTL) axe violations').toEqual([]);
  });

  test('Submit-Topic is axe-clean in both English and Arabic', async ({ page }) => {
    await loginAs(page, 'secretary');
    await page.goto('/backlog/submit');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    expect(await axeViolations(page), 'Submit-Topic (EN) axe violations').toEqual([]);

    await switchToArabic(page);
    expect(await axeViolations(page), 'Submit-Topic (AR/RTL) axe violations').toEqual([]);
  });

  // D-23: the kanban view is not the default, so its cards + the AC-043 reorder buttons + drag handles were
  // never scanned. Seed one topic so cards render, switch to kanban, and sweep (this is where `target-size`
  // actually bites — the reorder buttons must be >=24x24px).
  test('Backlog kanban with the AC-043 reorder controls is axe-clean in both English and Arabic', async ({ page, request }) => {
    await loginAs(page, 'secretary');
    const bearer = await captureBearer(page);
    await page.request.post('/api/members/me', { headers: { Authorization: bearer } });
    await apiCreateTopic(request, bearer, `wcag22 kanban ${Date.now()}`);

    await page.goto('/backlog');
    await page.getByRole('button', { name: 'Kanban' }).click();
    await expect(page.locator('.kb-card').first()).toBeVisible();
    expect(await axeViolations(page), 'Kanban (EN) axe violations').toEqual([]);

    await switchToArabic(page);
    expect(await axeViolations(page), 'Kanban (AR/RTL) axe violations').toEqual([]);
  });

  // WBS-24.2's second obligation (DEC-072 d2 / SC-032): DW-071's first trigger clause fires
  // "whenever a new route ships — that is the moment the ratio gets worse, and the moment it is
  // cheapest to add the route to the sweep". The calendar is a VIEW within /backlog rather than
  // its own route, so it is swept the way the kanban above is: navigate, switch view, run axe.
  test('Backlog calendar with its scheduled-meeting markers is axe-clean in both English and Arabic', async ({ page }) => {
    await loginAs(page, 'secretary');
    const bearer = await captureBearer(page);
    await page.request.post('/api/members/me', { headers: { Authorization: bearer } });

    // ⛔ SEEDED HERE RATHER THAN INHERITED. Until DEF-126 this test relied on meetings other specs
    // happened to leave behind — and `workers: 1` makes the run serial without making cross-FILE order a
    // contract, so the subject of the assertion was an accident either way. One meeting of its own costs
    // a request and makes the case self-sufficient; the default slot is now the 15th of the CURRENT
    // month (scenario.ts), so it always lands in the month the grid opens on.
    const members = await apiMembers(page.request, bearer);
    const meeting = await apiScheduleMeeting(page.request, bearer, `A11y calendar sweep ${Date.now()}`, members[0]);

    // ⛔ AND AN AGENDA ITEM, BECAUSE WBS-26.5 PUT A NEW INTERACTIVE ELEMENT IN THE DAY CELL. The grid now
    // renders a `.cal-topic` chip per agenda topic beneath the meeting chip, and those chips carry their
    // own WCAG target-size obligation (ADR-0045). A meeting with NO agenda items renders zero of them, so
    // this case would sweep the meeting chip, pass, and say nothing whatever about the new ones — LL-041's
    // shape, and precisely the vacuous pass DEF-126 records this very test having had for weeks.
    const topic = await apiPreparedTopic(page.request, bearer, `A11y calendar topic ${Date.now()}`, members[0]);
    await apiAddAgendaItem(page.request, bearer, meeting.id, topic, members[0]);

    await page.goto('/backlog');
    await page.getByRole('button', { name: 'Calendar' }).click();
    await expect(page.locator('.cal-grid')).toBeVisible();

    // ⛔⛔ DEF-126: THE ASSERTION BELOW MUST HAVE A SUBJECT, AND FOR WEEKS IT DID NOT.
    // The line that stood here read "the month grid renders whether or not any meeting is scheduled, so
    // this wait does not depend on seeded data" — true, and it is precisely why the sweep was worthless:
    // the GRID does not depend on seeded data, but the CHIPS are the only thing on this view that can
    // violate anything, and the seed put every meeting one day outside the rendered month. axe ran, found
    // an empty calendar, and reported clean, from WBS-24.2 until the clock reached 2026-09-01.
    //
    // WBS-24.2's row recorded that this sweep was "PROVEN TO RUN, NOT INFERRED FROM A GREEN JOB" because
    // the e2e count moved 86 → 88. That was sound and insufficient: a test-count delta proves the SPEC
    // EXECUTED, never that the ASSERTION HAD A SUBJECT. This line is the missing half of LL-013 — a
    // scanner must be shown to have looked at SOMETHING, not merely to have started.
    await expect(page.locator('.cal-event').first()).toBeVisible();

    expect(await axeViolations(page), 'Calendar (EN) axe violations').toEqual([]);

    await switchToArabic(page);
    expect(await axeViolations(page), 'Calendar (AR/RTL) axe violations').toEqual([]);
  });

  // WBS-24.6's second obligation (DEC-072 d2 / SC-032), named in AC-152: DW-071's first trigger clause
  // fires "whenever a new route ships — that is the moment the ratio gets worse, and the moment it is
  // cheapest to add the route to the sweep". /audit is a real route, unlike WBS-24.2's calendar view.
  //
  // ⚠ THE POPOVER IS OPENED BEFORE THE SWEEP ON PURPOSE. A closed Menu renders nothing but its trigger,
  // so scanning the page as it loads would score the new interactive surface — the panel, its role=menu
  // labelling, and the menuitem target sizes wcag22aa's target-size rule cares about — without ever
  // looking at it. That is a true zero over the wrong set (LL-015).
  //
  // ⚠ Secretary, not Auditor: ADR-0027's set is {Auditor, Chairman, Secretary} and Secretary is the
  // account the rest of this spec already uses. Administrator would 403 — that refusal is proven in
  // AuditExportApiTests, which is the right place for it.
  test('Audit trail with the Export log menu open is axe-clean in both English and Arabic', async ({ page }) => {
    await loginAs(page, 'secretary');
    const bearer = await captureBearer(page);
    await page.request.post('/api/members/me', { headers: { Authorization: bearer } });

    await page.goto('/audit');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    // exact:true — getByRole matches `name` as a case-insensitive SUBSTRING, which is how WBS-24.2's
    // {name:'AR'} also hit "Regular" and "Extraordinary".
    const exportBtn = page.getByRole('button', { name: 'Export log', exact: true });
    await exportBtn.click();
    await expect(page.getByRole('menu')).toBeVisible();
    expect(await axeViolations(page), 'Audit + export menu (EN) axe violations').toEqual([]);

    await page.keyboard.press('Escape'); // the language toggle is behind the menu's backdrop
    await switchToArabic(page);
    await page.getByRole('button', { name: 'تصدير السجل', exact: true }).click();
    await expect(page.getByRole('menu')).toBeVisible();
    expect(await axeViolations(page), 'Audit + export menu (AR/RTL) axe violations').toEqual([]);
  });

  // WBS-24.8's axe obligation (DEC-072 d2 / SC-032), named in AC-155. DW-071's first trigger clause
  // fires "whenever a new route ships", and /session/preview is a genuinely new route — guarded to
  // Chairman and Secretary, so it is reachable by the account this spec already uses.
  //
  // ⚠⚠ THE SLOT IS SEEDED BEFORE THE SWEEP, AND THAT IS THE WHOLE POINT. With no presenter assigned the
  // page renders its EMPTY STATE, and scanning that would score an EmptyState component this suite
  // already covers elsewhere while never looking at the topic card, the slot card or the materials list
  // this item actually added. WBS-24.6 hit the same shape one item earlier with a closed Menu: a true
  // zero over the wrong set (LL-015). So a meeting, a prepared topic and an assigned presenter are
  // created first, and the assertions below confirm the real shell rendered before axe runs.
  test('Presenter preview is axe-clean in both English and Arabic', async ({ page }) => {
    await loginAs(page, 'secretary');
    const bearer = await captureBearer(page);
    await page.request.post('/api/members/me', { headers: { Authorization: bearer } });

    const members = await apiMembers(page.request, bearer);
    const presenter = members[0];
    const topic = await apiPreparedTopic(page.request, bearer, 'Preview sweep topic', presenter);
    const meeting = await apiScheduleMeeting(page.request, bearer, 'Preview sweep meeting', presenter);
    await apiAddAgendaItem(page.request, bearer, meeting.id, topic, presenter);

    await page.goto(`/session/preview?meetingId=${meeting.id}&topicId=${topic.id}`);

    // The CONTROL that the seeded slot actually rendered. Without it a regression that turned this page
    // into its empty state would still sweep clean, and the sweep would report a passing route while
    // covering none of the surface the route was added for.
    await expect(page.getByRole('heading', { name: 'Preview sweep topic', exact: true })).toBeVisible();
    expect(await axeViolations(page), 'Presenter preview (EN) axe violations').toEqual([]);

    await switchToArabic(page);
    await expect(page.getByRole('heading', { name: 'Preview sweep topic', exact: true })).toBeVisible();
    expect(await axeViolations(page), 'Presenter preview (AR/RTL) axe violations').toEqual([]);
  });

  // DEF-126's SECOND HALF. /meetings has its own List ⇄ Calendar toggle and its own chip component
  // (MeetingsCalendar, `.mt-cal-grid`/`.mt-cal-event`) — a DIFFERENT component from the Backlog calendar
  // this spec already sweeps, and it carried the SAME target-size violation in a worse form: an <a> at
  // 9.5px, about 17px tall.
  //
  // ⛔ NOTHING HAD EVER LOOKED AT IT. Before this test the sweep visited /backlog, /backlog/submit and
  // /audit only, so a whole route with a near-identical component was outside every instrument. That is
  // the sibling-copy failure this project keeps paying for — a correction applied to one artifact leaves
  // the survivor as the one the next session reads — and fixing `.mt-cal-event` without adding this test
  // would have left the fix itself unguarded.
  test('Meetings calendar view is axe-clean in both English and Arabic', async ({ page }) => {
    await loginAs(page, 'secretary');
    const bearer = await captureBearer(page);
    await page.request.post('/api/members/me', { headers: { Authorization: bearer } });

    const members = await apiMembers(page.request, bearer);
    await apiScheduleMeeting(page.request, bearer, `A11y meetings sweep ${Date.now()}`, members[0]);

    await page.goto('/meetings');
    // exact:true throughout — getByRole matches `name` as a case-insensitive SUBSTRING, which is how
    // WBS-24.2's {name:'AR'} also matched "Regular" and "Extraordinary".
    await page.getByRole('button', { name: 'Calendar', exact: true }).click();
    await expect(page.locator('.mt-cal-grid')).toBeVisible();

    // The subject clause again (DEF-126, LL-013): the grid renders with or without meetings, so without
    // this the assertion below can pass over an empty month exactly as the Backlog one did.
    await expect(page.locator('.mt-cal-event').first()).toBeVisible();
    expect(await axeViolations(page), 'Meetings calendar (EN) axe violations').toEqual([]);

    await switchToArabic(page);
    await expect(page.locator('.mt-cal-event').first()).toBeVisible();
    expect(await axeViolations(page), 'Meetings calendar (AR/RTL) axe violations').toEqual([]);
  });

  // ---- route coverage: AC-170 (WBS-40.6, DEC-196), in every theme and language: AC-172 (WBS-40.8, DEC-199) ----
  // Driven by src/test/a11yRoutes.ts. These carry their own timeout: forty-odd full page loads plus an axe
  // pass each is well past Playwright's default, and a sweep that dies on the default timeout reports nothing.

  for (const mode of MODES) {
    test(`every Secretary route, static and parameterised, is axe-clean in ${modeName(mode)}`, async ({ page }) => {
      test.setTimeout(420_000);
      await useMode(page, mode);
      await loginAs(page, 'secretary');
      const stops = stopsFor('secretary', await seedRecords(page));
      const offenders = await violationsByRoute(page, stops, mode);
      expect(offenders, `axe violations by route (${modeName(mode)}), ${stops.length} routes swept`).toEqual({});
    });

    // The admin area needs its own login: RequireRole gates /admin on `administrator` alone, so the Secretary the
    // rest of this spec uses would be bounced and the sweep would prove nothing.
    test(`every Administrator route is axe-clean in ${modeName(mode)}`, async ({ page }) => {
      test.setTimeout(120_000);
      await useMode(page, mode);
      await loginAs(page, 'administrator');
      const stops = stopsFor('administrator');
      expect(await violationsByRoute(page, stops, mode), `Administration (${modeName(mode)}) axe violations`).toEqual({});
    });
  }

  // The sign-in page is the one screen a person sees signed OUT, so it is opened with no session at all. It has
  // no AppShell (#main), so its own call to action is the proof that it rendered.
  test('the signed-out sign-in page is axe-clean in every theme and language', async ({ page }) => {
    const stops = stopsFor('signedOut');
    const offenders: Record<string, Violation[]> = {};
    for (const stop of stops) {
      for (const mode of MODES) {
        await page.goto(stop.url);
        await page.evaluate(([l, t]) => {
          localStorage.setItem('i18nextLng', l);
          localStorage.setItem('acmp-theme', t);
        }, [mode.lang, mode.theme] as const);
        await page.reload();
        await expectMode(page, mode);
        await expect(page.locator('.login-cta')).toBeVisible();
        const run = await runAxe(page);
        expectContrastSubject(run, mode, stop.url);
        if (run.violations.length > 0) offenders[`${stop.url} (${modeName(mode)})`] = run.violations;
      }
    }
    test.info().annotations.push({ type: 'a11y-routes-swept', description: `signed out - ${stops.length} x ${MODES.length} modes` });
    expect(offenders, `axe violations signed out, ${stops.length} route(s) x ${MODES.length} modes`).toEqual({});
  });

  // ---- AC-173 (WBS-40.8, DEC-200): the states that used to dim their content with opacity ----
  // The manifest sweep opens each route in its default state; these five states only exist once a record has been
  // retired, superseded or closed, or a view toggle is on, so they are put in place first. The sixth, superseded
  // minutes, never reaches the screen (the page shows the newest version) and is guarded by opacity.test.ts alone.
  for (const mode of MODES) {
    test(`the formerly dimmed states are axe-clean in ${modeName(mode)} (AC-173)`, async ({ page }) => {
      test.setTimeout(240_000);
      await useMode(page, mode);
      // Chairman: the one role that may supersede a decision (DecisionChairApprove), and it holds every other step.
      const { bearer, member: me } = await roleSession(page, 'chairman', 'Chairman');
      const offenders: Record<string, Violation[]> = {};
      const states = await seedDimmedStates(page, bearer, me);
      for (const state of states) {
        await page.goto(state.url);
        await expect(page.locator('#main'), `${state.name} rendered the shell`).toBeVisible();
        if (state.act) await state.act(page);
        // The state's own marker: without it the check would sweep the record in its ordinary state and prove nothing.
        await expect(page.locator(state.marker).first(), `${state.name}: ${state.marker} is on the page`).toBeVisible();
        await expectMode(page, mode);
        const run = await runAxe(page, state.marker);
        expectContrastSubject(run, mode, state.name);
        expect(run.subjectContrastChecked, `${state.name}: the color-contrast rule evaluated nothing inside ${state.marker}`).toBeGreaterThan(0);
        if (run.violations.length > 0) offenders[state.name] = run.violations;
      }
      test.info().annotations.push({ type: 'a11y-routes-swept', description: `${modeName(mode)} - AC-173 states: ${states.map((s) => s.name).join(', ')}` });
      expect(offenders, `axe violations on the formerly dimmed states (${modeName(mode)})`).toEqual({});
    });
  }
});

interface DimmedState {
  name: string;
  url: string;
  /** The class that marks the state - the element that used to be dimmed. */
  marker: string;
  /** A view toggle to switch on after the page loads, for the states that are a view rather than a record. */
  act?: (page: Page) => Promise<void>;
}

/** Put one record into each state AC-173 sweeps: retired ADR and invariant, superseded decision, closed vote; and
 *  the two view states, the Overdue filter and the graph's cross-stream highlight. */
async function seedDimmedStates(page: Page, bearer: string, me: { keycloakUserId: string; fullName: string }): Promise<DimmedState[]> {
  const H = { Authorization: bearer, 'Content-Type': 'application/json' };
  const call = async (url: string, data?: unknown): Promise<{ id: string; key: string }> => {
    const res = await page.request.post(url, { headers: H, data: data ?? {} });
    const body = await res.text();
    if (!res.ok()) throw new Error(`[a11y seed] POST ${url} ${res.status()} ${body}`);
    return body ? JSON.parse(body) : { id: '', key: '' }; // the transitions answer 204 No Content
  };
  const L = (s: string) => ({ en: s, ar: s });
  const stamp = Date.now().toString(36);

  const adr = await call('/api/adrs', {
    title: L(`A11y retired ADR ${stamp}`), context: L('Retired context'), decisionDrivers: null, decisionText: L('Retired decision'),
    consequencesPositive: null, consequencesNegative: null, options: null,
  });
  await call(`/api/adrs/${adr.id}/propose`);
  await call(`/api/adrs/${adr.id}/approve`);
  await call(`/api/adrs/${adr.id}/deprecate`, { reason: L('Retired for the contrast sweep') });

  const invariant = await call('/api/invariants', {
    category: 'Security', scope: 'Platform', statement: L(`A11y retired invariant ${stamp}`), rationale: L('Retired rationale'),
    exceptionsPolicy: null, ownerUserId: me.keycloakUserId, ownerName: me.fullName,
  });
  await call(`/api/invariants/${invariant.id}/propose`);
  await call(`/api/invariants/${invariant.id}/approve`);
  await call(`/api/invariants/${invariant.id}/retire`, { reason: L('Retired for the contrast sweep') });

  const topic = await apiCreateTopic(page.request, bearer, `A11y dimmed-states topic ${stamp}`);
  const decision = await apiRecordDecision(page.request, bearer, {
    topicId: topic.id, title: `A11y superseded decision ${stamp}`, statement: 'Prior statement.', rationale: 'Prior rationale.',
  });
  await apiIssueDecision(page.request, bearer, decision.id);
  await call(`/api/decisions/${decision.id}/supersede`, {
    outcome: 'Deferred', title: L(`A11y successor decision ${stamp}`), statement: L('Successor statement.'),
    rationale: L('Successor rationale.'), alternatives: null, conditions: null, reason: L('Superseded for the contrast sweep'),
  });

  const vote = await apiConfigureVote(page.request, bearer, {
    topicId: topic.id, eligibleVoters: [{ userId: me.keycloakUserId, name: me.fullName }], minCast: 1,
  });
  await apiOpenVote(page.request, bearer, vote.id);
  await apiCastBallot(page.request, bearer, vote.id, 'Approve');
  await apiCloseVote(page.request, bearer, vote.id);

  // Something for the graph to de-highlight: an action blocking the topic (the graph draws dependency links).
  const action = await apiCreateAction(page.request, bearer, {
    title: `A11y dimmed-states action ${stamp}`, ownerUserId: me.keycloakUserId, ownerName: me.fullName, sourceId: topic.id, dueDate: '2026-12-01',
  });
  await call('/api/dependencies', {
    fromType: 'Topic', fromId: topic.id, fromKey: topic.key, fromTitle: topic.title,
    toType: 'Action', toId: action.id, toKey: action.key, toTitle: `A11y dimmed-states action ${stamp}`, kind: 'BlockedBy', note: null,
  });

  return [
    { name: 'deprecated ADR', url: `/adrs/${adr.key}`, marker: '.adr-body-muted' },
    { name: 'retired invariant', url: `/invariants/${invariant.key}`, marker: '.adr-body-muted' },
    { name: 'superseded decision', url: `/decisions/${decision.key}`, marker: '.dec-body-muted' },
    { name: 'closed vote', url: `/votes/${vote.key}`, marker: '.vote-closed-sub' },
    {
      name: 'Overdue filter on', url: '/actions', marker: '.act-toggle-on',
      act: async (p) => { await p.locator('button.fchip[aria-pressed="false"]').click(); },
    },
    {
      // The CROSS-STREAM highlight, not the blocked one: the graph draws dependency links only, so it holds the topic
      // and the action blocking it - both "blocked", so the blocked highlight would leave nothing to dim. Neither is
      // cross-stream, so this highlight dims the action (the focused topic never dims).
      name: 'graph highlight on', url: `/traceability/Topic/${topic.key}`, marker: '.ig-node--dim',
      act: async (p) => { await p.locator('.ig-toggle--cross').click(); },
    },
  ];
}
