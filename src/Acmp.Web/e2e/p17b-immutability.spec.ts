import { test, expect } from '@playwright/test';
import { roleSession } from './apiHelpers';
import {
  apiCreateTopic, apiCreateAction, apiStartAction, apiCompleteAction,
  apiConfigureVote, apiOpenVote, apiCastBallot, apiCloseVote,
  apiRecordDecision, apiIssueDecision,
} from './scenario';

/*
 * P17b — live real-stack legs for the (c) interpretation ACs (P17b-0 triage). Each AC presumes a UI attempt
 * the design PREVENTS, so the live proof is an absence / prevention / API-immutability assertion, recorded
 * with an interpretation note + OQ in the audit. (AC-026 forward-only is immutable-by-absence with no drivable
 * leg — it stays on VoteTests, docs interpretation only, no test here.)
 *
 *   AC-012  SoD-1 by prevention: the owner/completer never sees the Verify affordance.
 *   AC-022  Fork 1: a voter who already cast is offered "Change vote" (editable-until-close), NOT a rejection.
 *           The one-ballot invariant is enforced by the API (409 + DB unique index), not by a UI rejection.
 *   AC-025  a ballot is immutable after Close — POST /change on a closed vote is refused (409).
 *   AC-027  a decision has no edit surface — the read-only detail exposes no editable field.
 */

test.describe('P17b — immutability / prevention (AC-012 / AC-022 / AC-025 / AC-027)', () => {
  test('the owner of a completed action is not offered Verify (AC-012, SoD-1 by prevention)', async ({ page }) => {
    test.setTimeout(120_000);
    const { bearer, member: sec } = await roleSession(page, 'secretary', 'Secretary');
    const topic = await apiCreateTopic(page.request, bearer, `P17b SoD1 ${Date.now()}`);
    // The secretary owns AND completes the action, so on SoD-1 they may not verify their own work.
    const action = await apiCreateAction(page.request, bearer, {
      title: `P17b own action ${Date.now()}`,
      ownerUserId: sec.keycloakUserId,
      ownerName: sec.fullName,
      sourceId: topic.id,
    });
    await apiStartAction(page.request, bearer, action.id);
    await apiCompleteAction(page.request, bearer, action.id);

    await page.goto(`/actions/${action.key}`);
    // The action IS in a verifiable (Completed) state, yet the owner/completer sees no Verify button.
    await expect(page.getByText('Completed').first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Verify' })).toHaveCount(0);
  });

  test('a voter who already cast is offered Change vote, not a rejection (AC-022, Fork 1)', async ({ page }) => {
    test.setTimeout(120_000);
    const { bearer, member: chair } = await roleSession(page, 'chairman', 'Chairman');
    const topic = await apiCreateTopic(page.request, bearer, `P17b Revote ${Date.now()}`);
    const vote = await apiConfigureVote(page.request, bearer, {
      topicId: topic.id,
      eligibleVoters: [{ userId: chair.keycloakUserId, name: chair.fullName }],
      minCast: 1,
    });
    await apiOpenVote(page.request, bearer, vote.id);
    await apiCastBallot(page.request, bearer, vote.id, 'Approve'); // the chairman has now cast

    await page.goto(`/votes/${vote.key}`);
    // The SPA routes a re-vote to editing the existing ballot (Fork 1) — the button says "Change vote" and
    // the recorded ballot is shown. There is NO "you have already voted" rejection in-product.
    await expect(page.getByText('Your recorded vote')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Change vote' })).toBeVisible();
  });

  test('a ballot cannot be changed after the vote closes (AC-025, immutable after Close)', async ({ page }) => {
    test.setTimeout(120_000);
    const { bearer, member: chair } = await roleSession(page, 'chairman', 'Chairman');
    const topic = await apiCreateTopic(page.request, bearer, `P17b BallotImmutable ${Date.now()}`);
    const vote = await apiConfigureVote(page.request, bearer, {
      topicId: topic.id,
      eligibleVoters: [{ userId: chair.keycloakUserId, name: chair.fullName }],
      minCast: 1,
    });
    await apiOpenVote(page.request, bearer, vote.id);
    await apiCastBallot(page.request, bearer, vote.id, 'Approve');
    await apiCloseVote(page.request, bearer, vote.id); // Closed — tally + ballots frozen

    // Attempt to modify the ballot on the closed vote via the real API → refused (immutable-by-domain).
    const changeRes = await page.request.post(`/api/votes/${vote.id}/change`, {
      headers: { Authorization: bearer, 'Content-Type': 'application/json' },
      data: { choice: 'Reject', comment: null },
    });
    expect(changeRes.status()).toBe(409);
  });

  test('an issued decision exposes no edit surface (AC-027, immutable detail)', async ({ page }) => {
    test.setTimeout(120_000);
    const { bearer } = await roleSession(page, 'chairman', 'Chairman');
    const topic = await apiCreateTopic(page.request, bearer, `P17b DecnImmutable ${Date.now()}`);
    const statement = `Immutable decision statement ${Date.now()}`;
    const decn = await apiRecordDecision(page.request, bearer, {
      topicId: topic.id,
      title: 'P17b immutable decision',
      statement,
      rationale: 'Rationale.',
    });
    await apiIssueDecision(page.request, bearer, decn.id);

    await page.goto(`/decisions/${decn.key}`);
    // The statement is readable as static prose, and the decision detail region has no editable field —
    // there is no in-place edit affordance (corrections go through Supersede, a new record).
    await expect(page.locator('.dec-statement')).toContainText(statement);
    await expect(page.locator('.dec-detail').getByRole('textbox')).toHaveCount(0);
  });
});
