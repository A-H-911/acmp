import { test, expect } from '@playwright/test';
import { roleSession } from './apiHelpers';
import { apiCreateTopic, apiConfigureVote, apiOpenVote, apiCastBallot, apiCloseVote } from './scenario';

/*
 * P17b — live real-stack leg for the voting ACs (bin (a) of the P17b-0 triage):
 *   AC-024  closing a vote below the cast quorum is rejected client-drives-the-close → 409, vote stays Open
 *   AC-023  a closed vote renders every ballot attributed to its voter (ADR-0010: never anonymous)
 *
 * The vote lifecycle is seeded through the API with real captured bearers (scenario.ts); the UI is reserved
 * for the behaviour under test — the secretary's Close click (AC-024) and the closed-roster view (AC-023).
 *
 * Casting is Vote.Cast (Chairman/Member) — the Secretary manages the vote but does not vote — so AC-023's
 * attributed ballot is cast by a second, genuinely-logged-in chairman (mirrors core-loop.spec's multi-context
 * pattern), then the secretary views the closed roster.
 */

test.describe('P17b — voting (AC-023 / AC-024)', () => {
  test('closing a vote below the cast quorum is rejected, and the vote stays open', async ({ page }) => {
    test.setTimeout(120_000);
    const { bearer, member } = await roleSession(page, 'secretary', 'Secretary');
    const topic = await apiCreateTopic(page.request, bearer, `P17b Vote Quorum ${Date.now()}`);

    // One eligible voter, cast quorum 1, zero ballots cast — the close must fail. The eligible-voter identity
    // is immaterial to the MinCast guard under test (nobody casts), so the secretary's own row is fine.
    const vote = await apiConfigureVote(page.request, bearer, {
      topicId: topic.id,
      eligibleVoters: [{ userId: member.keycloakUserId, name: member.fullName }],
      minCast: 1,
    });
    await apiOpenVote(page.request, bearer, vote.id);

    await page.goto(`/votes/${vote.key}`);
    const closeBtn = page.getByRole('button', { name: 'Close voting' });
    await expect(closeBtn).toBeVisible();
    await closeBtn.click();

    // castCount 0 < minCast 1 → server rejects; VotePage surfaces the inline announced error and the vote
    // stays Open (Fork 2 — a failed close is a 409, never a resting quorum_failed state).
    await expect(page.getByRole('alert')).toContainText(
      'Voting could not be closed — the required votes have not been cast.',
    );
    await expect(page.getByText('Voting open')).toBeVisible();
    await expect(closeBtn).toBeVisible(); // still open + still manageable
  });

  test('a closed vote shows each ballot attributed to its voter', async ({ page, browser }) => {
    test.setTimeout(120_000);
    const { bearer: secBearer } = await roleSession(page, 'secretary', 'Secretary');

    const chairCtx = await browser.newContext();
    const chairPage = await chairCtx.newPage();
    try {
      const { bearer: chairBearer, member: chair } = await roleSession(chairPage, 'chairman', 'Chairman');
      const topic = await apiCreateTopic(page.request, secBearer, `P17b Vote Attributed ${Date.now()}`);

      const vote = await apiConfigureVote(page.request, secBearer, {
        topicId: topic.id,
        eligibleVoters: [{ userId: chair.keycloakUserId, name: chair.fullName }],
        minCast: 1,
      });
      await apiOpenVote(page.request, secBearer, vote.id);
      await apiCastBallot(page.request, chairBearer, vote.id, 'Approve'); // chairman casts (Vote.Cast)
      await apiCloseVote(page.request, secBearer, vote.id); // castCount 1 ≥ minCast 1 → Closed

      // Secretary views the closed vote: the roster attributes the ballot to the chairman by name + choice.
      await page.goto(`/votes/${vote.key}`);
      await expect(page.getByText('Vote closed & locked')).toBeVisible();
      const row = page.locator('.voter-row', { hasText: chair.fullName });
      await expect(row).toBeVisible();
      await expect(row).toContainText('Approve');
    } finally {
      await chairCtx.close();
    }
  });
});
