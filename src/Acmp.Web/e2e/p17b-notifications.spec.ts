import { test, expect } from '@playwright/test';
import { roleSession } from './apiHelpers';
import { apiCreateTopic, apiConfigureVote, apiOpenVote } from './scenario';

/*
 * P17b — live real-stack leg for the notification deep-link AC (bin (a) of the P17b-0 triage):
 *   AC-052  an eligible voter's "vote is Open" notification deep-links straight to the voting UI.
 *
 * Reuses the vote seed (scenario.ts): the secretary configures + opens a vote with the member as the sole
 * eligible voter, which fans out a VoteOpened notification (deep link /votes/{key}) to that member's sub.
 * The member then opens their notification center and clicks the notification — the UI under test — and
 * must land directly on the vote page with no extra steps.
 */

test.describe('P17b — notifications (AC-052)', () => {
  test('a vote-opened notification deep-links straight to the voting UI', async ({ page, browser }) => {
    test.setTimeout(120_000);
    const { bearer: secBearer } = await roleSession(page, 'secretary', 'Secretary');

    const memberCtx = await browser.newContext();
    const memberPage = await memberCtx.newPage();
    try {
      const { member } = await roleSession(memberPage, 'member', 'Member');

      const topic = await apiCreateTopic(page.request, secBearer, `P17b Notif DeepLink ${Date.now()}`);
      const vote = await apiConfigureVote(page.request, secBearer, {
        topicId: topic.id,
        eligibleVoters: [{ userId: member.keycloakUserId, name: member.fullName }],
        minCast: 1,
      });
      await apiOpenVote(page.request, secBearer, vote.id); // fans out VoteOpened → the member's sub

      // The member loads the app; the unread bell confirms the notification arrived (≤5s synchronous write).
      await memberPage.goto('/');
      await expect(memberPage.locator('.notif-badge')).toBeVisible();

      // Open the notification center and click the vote-opened row — one click must reach the voting UI.
      await memberPage.getByRole('button', { name: /Notifications/ }).click();
      const panel = memberPage.getByRole('dialog', { name: 'Notifications' });
      await expect(panel).toBeVisible();
      await panel.locator('.notif-row-msg', { hasText: vote.key }).click();

      await expect(memberPage).toHaveURL(new RegExp(`/votes/${vote.key}`));
      await expect(memberPage.getByText('Voting open')).toBeVisible(); // landed on the open vote
    } finally {
      await memberCtx.close();
    }
  });
});
