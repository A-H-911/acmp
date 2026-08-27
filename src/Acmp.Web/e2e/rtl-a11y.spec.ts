import { test, expect, type Page } from '@playwright/test';
import { createRequire } from 'node:module';
import { readFileSync } from 'node:fs';
import { loginAs } from './login';
import { captureBearer } from './apiHelpers';
import { apiCreateTopic } from './scenario';

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

    await page.goto('/backlog');
    await page.getByRole('button', { name: 'Calendar' }).click();
    // The month grid is the frame; it renders whether or not any meeting is scheduled, so this
    // wait does not depend on seeded data.
    await expect(page.locator('.cal-grid')).toBeVisible();
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
});
