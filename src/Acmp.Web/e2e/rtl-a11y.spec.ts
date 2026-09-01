import { test, expect, type Page } from '@playwright/test';
import { createRequire } from 'node:module';
import { readFileSync } from 'node:fs';
import { loginAs } from './login';
import { captureBearer } from './apiHelpers';
import { apiAddAgendaItem, apiCreateTopic, apiMembers, apiPreparedTopic, apiScheduleMeeting } from './scenario';

/*
 * S6b-3 (ADR-0016 §2) — the RTL/Arabic + accessibility pass, the last E2E slice. Proves the real
 * app flips to `dir="rtl"` under Arabic and runs an automated axe sweep on key authenticated
 * screens in BOTH locales. Uses the already-installed `axe-core` — no new dependency.
 *
 * The app ships a strict CSP (`script-src 'self'`), so `addScriptTag` (inline injection) is blocked
 * — we run the axe source through `page.evaluate` instead, which executes via CDP and bypasses page
 * CSP. `color-contrast` is disabled to match the S4 unit convention: contrast is a
 * design-token/fidelity concern, out of scope for this slice.
 */
const require = createRequire(import.meta.url);
const AXE_SOURCE = readFileSync(require.resolve('axe-core/axe.min.js'), 'utf8');
// D-23: include WCAG 2.2 AA — the machine-testable addition over 2.1 is `target-size` (SC 2.5.8, >=24x24px).
const WCAG = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

interface Violation {
  id: string;
  impact: string | null;
  nodes: number;
}

async function axeViolations(page: Page): Promise<Violation[]> {
  await page.evaluate(AXE_SOURCE); // defines window.axe; CDP eval bypasses the page CSP
  return page.evaluate(async (tags) => {
    // axe is injected as a page global by addScriptTag.
    const result = await (window as unknown as { axe: { run: (ctx: Document, opts: unknown) => Promise<{ violations: Array<{ id: string; impact: string | null; nodes: unknown[] }> }> } }).axe.run(
      document,
      { runOnly: { type: 'tag', values: tags }, rules: { 'color-contrast': { enabled: false } } },
    );
    return result.violations.map((v) => ({ id: v.id, impact: v.impact, nodes: v.nodes.length }));
  }, WCAG);
}

async function switchToArabic(page: Page): Promise<void> {
  await page.getByRole('button', { name: /Switch to/ }).click();
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  await expect(page.locator('html')).toHaveAttribute('lang', 'ar');
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
    await apiScheduleMeeting(page.request, bearer, `A11y calendar sweep ${Date.now()}`, members[0]);

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
});
