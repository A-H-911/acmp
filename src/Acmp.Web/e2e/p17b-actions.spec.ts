import { test, expect } from '@playwright/test';
import { roleSession } from './apiHelpers';
import { apiCreateTopic, apiCreateAction, apiStartAction, apiCompleteAction } from './scenario';

/*
 * P17b — live real-stack leg for the actions SoD-1 AC (bin (a) of the P17b-0 triage):
 *   AC-013  a non-owner (Secretary) verifies a Completed action → Verified (SoD-1 positive).
 *
 * SoD-1 (VerifyAction): the verifier may be neither the action's owner nor its completer, keyed by Keycloak
 * sub. So the action is seeded owned + completed by a genuinely-logged-in chairman (via a 2nd context), and
 * the secretary — a different sub — drives the Verify transition through the UI. The API re-checks and audits
 * the denial, but the positive path is what this proves live: the Verify affordance exists and reaches 204.
 */

test.describe('P17b — actions (AC-013)', () => {
  test('a non-owner secretary verifies a completed action (SoD-1 positive)', async ({ page, browser }) => {
    test.setTimeout(120_000);
    const { bearer: secBearer } = await roleSession(page, 'secretary', 'Secretary');

    const chairCtx = await browser.newContext();
    const chairPage = await chairCtx.newPage();
    try {
      const { bearer: chairBearer, member: chair } = await roleSession(chairPage, 'chairman', 'Chairman');

      // Chairman owns AND completes the action → the secretary (a different sub) is an independent verifier.
      const topic = await apiCreateTopic(page.request, secBearer, `P17b Action Verify ${Date.now()}`);
      const action = await apiCreateAction(page.request, chairBearer, {
        title: `P17b verify me ${Date.now()}`,
        ownerUserId: chair.keycloakUserId,
        ownerName: chair.fullName,
        sourceId: topic.id,
      });
      await apiStartAction(page.request, chairBearer, action.id);
      await apiCompleteAction(page.request, chairBearer, action.id);

      // Secretary opens the completed action; SoD-1 lets a non-owner/non-completer see + drive Verify.
      await page.goto(`/actions/${action.key}`);
      const verifyBtn = page.getByRole('button', { name: 'Verify' });
      await expect(verifyBtn).toBeVisible();
      await verifyBtn.click();

      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();
      const [res] = await Promise.all([
        page.waitForResponse((r) => r.url().includes(`/api/actions/${action.id}/verify`) && r.request().method() === 'POST'),
        dialog.getByRole('button', { name: 'Verify' }).click(),
      ]);
      expect(res.status()).toBe(204);
      await expect(dialog).toHaveCount(0);

      // The action is now Verified: the status chip reflects it and the (terminal) Verify affordance is gone.
      await expect(page.getByText('Verified').first()).toBeVisible();
      await expect(page.getByRole('button', { name: 'Verify' })).toHaveCount(0);
    } finally {
      await chairCtx.close();
    }
  });
});
