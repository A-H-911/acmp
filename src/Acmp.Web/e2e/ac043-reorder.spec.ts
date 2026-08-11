import { test, expect, type Page } from '@playwright/test';
import { loginAs } from './login';
import { captureBearer } from './apiHelpers';
import { apiCreateTopic } from './scenario';

/*
 * AC-043 / FR-034 — the keyboard alternative to drag-and-drop for backlog priority. The move-up/down
 * buttons on a kanban card send a ±1 priority delta; the reorder must persist (survive a reload), which
 * the InMemory-DbContext unit tests cannot prove. Robust to other topics in the column: asserts the
 * RELATIVE order of the two topics under test, never an absolute position.
 */

async function secretarySession(page: Page): Promise<string> {
  await loginAs(page, 'secretary');
  const bearer = await captureBearer(page);
  // Idempotent: force JIT provisioning to finish before the API seed.
  await page.request.post('/api/members/me', { headers: { Authorization: bearer } });
  return bearer;
}

/**
 * Narrow the backlog to this run's two topics before reading the column.
 *
 * WITHOUT THIS THE TEST CANNOT SEE ITS OWN FIXTURES on any environment that is not freshly built.
 * GetBacklog pages at 25; measured on UAT there were 75 topics with 52 in Triage. Both topics are
 * created at priority 0 and the kanban sorts (Priority, CreatedAt, Key) ascending, so the newest
 * rows sort LAST and land two pages beyond the fold — `indexOf` returned -1 for both, and the
 * relative-order assertion compared -1 with -1. The existing "robust to other topics in the column"
 * note is true about ORDERING and silent about VOLUME, which is the failure that actually occurred.
 *
 * The search box is component state, not a URL param, so it must be re-applied after every reload.
 */
async function filterToRun(page: Page, stamp: number): Promise<void> {
  // GetBacklog matches Search against Title OR Key. The stamp is the only fragment common to BOTH
  // fixtures ("AC043 reorder A <stamp>" / "...B <stamp>") — searching the shared prefix plus the
  // stamp matches neither, because the A/B sits between them.
  await page.locator('.bk-filters').getByRole('searchbox').fill(String(stamp)); // 300 ms debounce → refetch at page 1
}

// The topic keys in the Triage column, in displayed (priority) order. Filtered to this run first.
async function triageOrder(page: Page, stamp: number): Promise<string[]> {
  await page.getByRole('button', { name: 'Kanban' }).click();
  await filterToRun(page, stamp);
  const col = page.locator('.kb-col').filter({ hasText: 'Triage' });
  await expect(col.locator('.bk-key')).toHaveCount(2); // both fixtures visible before we read order
  return col.locator('.bk-key').allInnerTexts();
}

test.describe('AC-043 — keyboard priority reorder', () => {
  test('move-down swaps a backlog card with its neighbour and persists across a reload', async ({ page, request }) => {
    const bearer = await secretarySession(page);
    const stamp = Date.now();
    const a = await apiCreateTopic(request, bearer, `AC043 reorder A ${stamp}`);
    const b = await apiCreateTopic(request, bearer, `AC043 reorder B ${stamp}`);

    await page.goto('/backlog');

    // Both new topics start at priority 0; the (Priority, CreatedAt, Key) tiebreak puts A (created first) above B.
    const before = await triageOrder(page, stamp);
    expect(before.indexOf(a.key)).toBeLessThan(before.indexOf(b.key));

    // Move A DOWN via its keyboard button → B rises above A.
    await page.getByRole('button', { name: `Move ${a.key} down in priority` }).click();
    await expect
      .poll(async () => {
        const now = await page.locator('.kb-col').filter({ hasText: 'Triage' }).locator('.bk-key').allInnerTexts();
        return now.indexOf(b.key) < now.indexOf(a.key);
      })
      .toBe(true);

    // Persisted: a full reload re-fetches priority-sorted from the server and the new order holds.
    await page.reload();
    const after = await triageOrder(page, stamp); // reload clears the box — triageOrder re-applies it
    expect(after.indexOf(b.key)).toBeLessThan(after.indexOf(a.key));
  });
});
