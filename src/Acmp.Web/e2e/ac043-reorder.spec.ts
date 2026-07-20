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

// The topic keys in the Triage column, in displayed (priority) order.
async function triageOrder(page: Page): Promise<string[]> {
  await page.getByRole('button', { name: 'Kanban' }).click();
  return page.locator('.kb-col').filter({ hasText: 'Triage' }).locator('.bk-key').allInnerTexts();
}

test.describe('AC-043 — keyboard priority reorder', () => {
  test('move-down swaps a backlog card with its neighbour and persists across a reload', async ({ page, request }) => {
    const bearer = await secretarySession(page);
    const stamp = Date.now();
    const a = await apiCreateTopic(request, bearer, `AC043 reorder A ${stamp}`);
    const b = await apiCreateTopic(request, bearer, `AC043 reorder B ${stamp}`);

    await page.goto('/backlog');

    // Both new topics start at priority 0; the (Priority, CreatedAt, Key) tiebreak puts A (created first) above B.
    const before = await triageOrder(page);
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
    const after = await triageOrder(page);
    expect(after.indexOf(b.key)).toBeLessThan(after.indexOf(a.key));
  });
});
