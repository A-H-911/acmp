import { test, expect, type Page } from '@playwright/test';
import { loginAs } from './login';
import { captureBearer } from './apiHelpers';
import { apiMembers, apiScheduleMeeting } from './scenario';

/*
 * DEF-128 — THE MONTH GRIDS' COLUMNS MUST STAY EVEN, AND THIS IS THE ONLY INSTRUMENT IN THIS REPOSITORY
 * THAT CAN SAY SO.
 *
 * ⛔ WHY IT IS AN E2E SPEC AND NOT A UNIT TEST. Nothing here reads pixels — DEF-047's finding, restated by
 * DEF-128's own row. vitest runs under JSDOM, which does not lay out, so `getBoundingClientRect()` returns
 * zeros and a unit test asserting on column widths would pass identically before and after the fix. axe
 * checks contrast, roles and target size; an uneven grid violates none of them, and /backlog's axe sweep
 * passed straight over the 125.8px spread this guards. The e2e project is the only place in this
 * repository with a real layout engine.
 *
 * WHAT THE DEFECT WAS. `.cal-grid` and `.mt-cal-grid` were `repeat(7, 1fr)`. `1fr` is shorthand for
 * `minmax(auto, 1fr)`, and that `auto` MINIMUM is the item's min-content width — so one long meeting title
 * in one day cell widened its whole column and squeezed the other six. Measured: `.cal-cell` at
 * [129, 150.4, 254.8], a 125.8px spread, while `.cal-weekdays` — a SEPARATE grid with no long content —
 * stayed uniform at 150, so the weekday labels no longer sat over the columns they name.
 *
 * ⚠⚠ THE SUBJECT CLAUSE, AND IT IS THE WHOLE REASON THIS FILE SEEDS ANYTHING (DEF-126, LL-013, LL-041).
 * The fault is CONDITIONAL: it appears only when some cell's content is wider than its fair share. A
 * calendar with short titles — or with no meetings at all, which is what the axe sweep asserted over for
 * weeks — is perfectly even BEFORE the fix, so a test that does not force a long title passes vacuously
 * and would keep passing if the CSS were reverted. The seeded title below is deliberately long, and
 * `expect(...toBeVisible())` on the chip proves the grid actually rendered it before anything is measured.
 *
 * ⚠ WHY THE TOLERANCE IS 1px RATHER THAN 0. Seven equal `fr` columns inside a container whose width is not
 * a multiple of seven differ by a rounding remainder — the browser distributes the leftover sub-pixel. The
 * defect was 125.8px, so a 1px ceiling discriminates by two orders of magnitude and cannot be tightened to
 * 0 without making the assertion flaky on an arbitrary viewport.
 *
 * ⚠ WHAT THIS DOES NOT COVER, so the file does not read as complete: it asserts EVENNESS, not that any
 * particular width is correct, and it says nothing about the chips inside the cells (ADR-0045's target-size
 * floor is rtl-a11y.spec.ts's job). It measures at the default viewport only.
 */

/** Widths of every element matching `selector`, read from the live layout engine. */
async function widths(page: Page, selector: string): Promise<number[]> {
  return page.$$eval(selector, (els) => els.map((el) => el.getBoundingClientRect().width));
}

const spread = (xs: number[]): number => Math.max(...xs) - Math.min(...xs);

/*
 * A title long enough to blow out a 1/7th column at any reasonable viewport. `.cal-event-label` is
 * `white-space: nowrap`, so its min-content width is the WHOLE string — which is exactly the property
 * `minmax(0, 1fr)` neutralises.
 */
const LONG_TITLE = 'Architecture Committee Extraordinary Session on Platform Modernisation and Governance';

async function switchToArabic(page: Page): Promise<void> {
  await page.getByRole('button', { name: /Switch to/ }).click();
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
}

test.describe('DEF-128 — calendar month grids keep even columns', () => {
  test('the Backlog calendar keeps even day columns, and its weekday header stays over them, in both locales', async ({
    page,
  }) => {
    await loginAs(page, 'secretary');
    const bearer = await captureBearer(page);
    await page.request.post('/api/members/me', { headers: { Authorization: bearer } });

    const members = await apiMembers(page.request, bearer);
    await apiScheduleMeeting(page.request, bearer, `${LONG_TITLE} ${Date.now()}`, members[0]);

    await page.goto('/backlog');
    await page.getByRole('button', { name: 'Calendar' }).click();
    await expect(page.locator('.cal-grid')).toBeVisible();

    for (const locale of ['EN', 'AR/RTL'] as const) {
      if (locale === 'AR/RTL') await switchToArabic(page);

      // The subject clause: without a rendered chip carrying the long title there is nothing that COULD
      // have widened a column, and an even grid would prove nothing at all.
      await expect(page.locator('.cal-event').first()).toBeVisible();

      const cells = await widths(page, '.cal-cell');
      expect(cells.length, `${locale}: .cal-cell count`).toBeGreaterThanOrEqual(28);
      expect(spread(cells), `${locale}: .cal-cell width spread (was 125.8px on DEF-128)`).toBeLessThan(1);

      // `.cal-weekdays` is a SEPARATE grid from `.cal-grid`, which is why the header drifted off its
      // columns rather than moving with them. Comparing the two grids to each other is the assertion the
      // defect actually needs; measuring the header alone returned a clean spread of 0 and a confident
      // answer about the wrong subject (LL-043).
      const weekdays = await widths(page, '.cal-weekday');
      expect(weekdays.length, `${locale}: .cal-weekday count`).toBe(7);
      expect(
        Math.abs(Math.max(...weekdays) - Math.max(...cells)),
        `${locale}: weekday header column vs day column`,
      ).toBeLessThan(1);
    }
  });

  /*
   * THE SIBLING, AND IT IS HERE BECAUSE FIXING ONE GRID AND NOT THE OTHER IS THE FAILURE THIS PROJECT KEEPS
   * PAYING FOR. DEF-128 was reported against /backlog; /meetings has its own calendar component with the
   * same `repeat(7, 1fr)` and its own nowrap chip, measured at an 81.3px spread before the fix. DEF-126 was
   * the same shape one week earlier — a target-size violation fixed on `.cal-event` while `.mt-cal-event`
   * carried it in a worse form, unguarded because nothing had ever looked at that route.
   *
   * ⚠ ONE DIFFERENCE WORTH KNOWING BEFORE READING THE ASSERTIONS: `.mt-cal-dow` and `.mt-cal-cell` live in
   * the SAME `.mt-cal-grid`, so the header cannot drift off its columns here — it shares them. That is why
   * this test has no header-alignment leg and the Backlog one does.
   */
  test('the Meetings calendar keeps even day columns in both locales', async ({ page }) => {
    await loginAs(page, 'secretary');
    const bearer = await captureBearer(page);
    await page.request.post('/api/members/me', { headers: { Authorization: bearer } });

    const members = await apiMembers(page.request, bearer);
    await apiScheduleMeeting(page.request, bearer, `${LONG_TITLE} ${Date.now()}`, members[0]);

    await page.goto('/meetings');
    // exact:true — getByRole matches `name` as a case-insensitive SUBSTRING, which is how a {name:'AR'}
    // locator once also matched "Regular" and "Extraordinary".
    await page.getByRole('button', { name: 'Calendar', exact: true }).click();
    await expect(page.locator('.mt-cal-grid')).toBeVisible();

    for (const locale of ['EN', 'AR/RTL'] as const) {
      if (locale === 'AR/RTL') await switchToArabic(page);

      await expect(page.locator('.mt-cal-event').first()).toBeVisible();

      const cells = await widths(page, '.mt-cal-cell');
      expect(cells.length, `${locale}: .mt-cal-cell count`).toBeGreaterThanOrEqual(28);
      expect(spread(cells), `${locale}: .mt-cal-cell width spread (was 81.3px on DEF-128)`).toBeLessThan(1);
    }
  });
});
