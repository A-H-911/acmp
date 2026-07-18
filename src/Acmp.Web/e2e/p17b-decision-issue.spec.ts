import { test, expect } from '@playwright/test';
import { roleSession } from './apiHelpers';
import {
  apiPreparedTopic, apiScheduleMeeting, apiAddAgendaItem, apiPublishAgenda, apiStartMeeting, apiMarkAttendance,
  apiConfigureVote, apiOpenVote, apiCastBallot, apiCloseVote,
} from './scenario';

/*
 * P17b — live real-stack leg for the decision-issuance UI (the D-15 product slice + F-03 core-loop leg):
 *   AC-015  the chairman who CLOSED the coupled vote cannot issue its decision — SoD-3 co-attestation
 *           denies the issue (403) and the UI surfaces it inline; the vote stays Closed (not Ratified).
 *   AC-016  when a DIFFERENT actor (the secretary) closed the vote, the chairman issues it with an
 *           override + justification → the decision is Issued and the coupled vote is Ratified. This is
 *           the F-03 leg "chairman ratify → decision record (Issued)".
 *
 * Both drive the RecordDecisionDialog in the in-session MeetingWorkspace (chairman-gated). The conduct
 * chain (prepared topic → meeting → agenda → start → attendance → configure/open/cast/close a coupled,
 * meeting-linked vote) is API-seeded; only the record→issue action is exercised through the real UI.
 *
 * Outcome = Rejected (a NON-follow-up outcome) so the AC-029 downstream-link gate is skipped and the
 * SoD-3 gate is the one actually reached — otherwise a follow-up outcome would 409 before SoD-3 (the
 * gate ordering in IssueDecision.cs is AC-029 → vote-coupling → SoD-3).
 */

test.describe('P17b — record → issue decision (AC-015/016, F-03)', () => {
  test('AC-015: the chair who closed the vote is denied issue (SoD-3 403), surfaced inline; vote stays Closed', async ({ page, browser }) => {
    test.setTimeout(120_000);
    const { bearer: chairBearer, member: chair } = await roleSession(page, 'chairman', 'Chairman');

    const memberCtx = await browser.newContext();
    const memberPage = await memberCtx.newPage();
    try {
      const { bearer: memBearer, member: mem } = await roleSession(memberPage, 'member', 'Member');

      const stamp = Date.now();
      const topic = await apiPreparedTopic(page.request, chairBearer, `P17b Issue-403 Topic ${stamp}`, chair);
      const meeting = await apiScheduleMeeting(page.request, chairBearer, `P17b Issue-403 Meeting ${stamp}`, chair);
      await apiAddAgendaItem(page.request, chairBearer, meeting.id, topic, chair);
      await apiPublishAgenda(page.request, chairBearer, meeting.id);
      await apiStartMeeting(page.request, chairBearer, meeting.id);
      await apiMarkAttendance(page.request, chairBearer, meeting.id, { userId: chair.publicId, name: chair.fullName, role: 'Chair', isVotingEligible: true });
      await apiMarkAttendance(page.request, chairBearer, meeting.id, { userId: mem.publicId, name: mem.fullName, role: 'Member', isVotingEligible: true });

      // A meeting-linked vote the decision will couple to. The member casts; the CHAIR closes it → the
      // chair is the vote's counter of record, so SoD-3 forbids the same chair from issuing.
      const vote = await apiConfigureVote(page.request, chairBearer, {
        topicId: topic.id,
        meetingId: meeting.id,
        eligibleVoters: [
          { userId: chair.keycloakUserId, name: chair.fullName },
          { userId: mem.keycloakUserId, name: mem.fullName },
        ],
        minCast: 1,
      });
      await apiOpenVote(page.request, chairBearer, vote.id);
      await apiCastBallot(memberPage.request, memBearer, vote.id, 'Reject');
      await apiCloseVote(page.request, chairBearer, vote.id);

      await page.setViewportSize({ width: 1280, height: 1400 }); // RecordDecisionDialog is tall
      await page.goto(`/meetings/${meeting.key}/notes`);
      await page.getByRole('button', { name: 'Record decision' }).click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();
      // The dialog couples the closed vote for this agenda item (topic + meeting match).
      await expect(dialog.getByText(new RegExp(`Ratifies ${vote.key}`))).toBeVisible();

      // Rejected → skips AC-029, so the issue reaches (and is denied by) the SoD-3 gate, not a 409.
      await dialog.getByRole('button', { name: 'Outcome' }).click();
      await dialog.getByRole('option', { name: 'Rejected' }).click();
      await dialog.getByRole('textbox', { name: 'Title' }).fill('Reject the Keycloak migration');
      await dialog.getByRole('textbox', { name: 'Decision statement' }).fill('The committee rejects the proposal.');
      await dialog.getByRole('textbox', { name: 'Rationale' }).fill('Insufficient rollback evidence.');

      const [issueRes] = await Promise.all([
        page.waitForResponse((r) => /\/api\/decisions\/[^/]+\/issue$/.test(r.url()) && r.request().method() === 'POST'),
        dialog.getByRole('button', { name: 'Record & issue' }).click(),
      ]);
      expect(issueRes.status()).toBe(403);
      await expect(dialog.getByRole('alert')).toContainText('cannot be the vote');

      // The vote was NOT ratified — the denied issue did not transition it.
      const after = await page.request.get(`/api/votes/${vote.key}`, { headers: { Authorization: chairBearer } });
      expect((await after.json()).status).toBe('Closed');
    } finally {
      await memberCtx.close();
    }
  });

  test('AC-016 (F-03): a secretary closed the vote, so the chair issues it with an override → decision Issued + vote Ratified', async ({ page, browser }) => {
    test.setTimeout(150_000);
    const { bearer: chairBearer, member: chair } = await roleSession(page, 'chairman', 'Chairman');

    const secretaryCtx = await browser.newContext();
    const memberCtx = await browser.newContext();
    const secretaryPage = await secretaryCtx.newPage();
    const memberPage = await memberCtx.newPage();
    try {
      const { bearer: secBearer } = await roleSession(secretaryPage, 'secretary', 'Secretary');
      const { bearer: memBearer, member: mem } = await roleSession(memberPage, 'member', 'Member');

      const stamp = Date.now();
      const topic = await apiPreparedTopic(page.request, chairBearer, `P17b Issue-OK Topic ${stamp}`, chair);
      const meeting = await apiScheduleMeeting(page.request, chairBearer, `P17b Issue-OK Meeting ${stamp}`, chair);
      await apiAddAgendaItem(page.request, chairBearer, meeting.id, topic, chair);
      await apiPublishAgenda(page.request, chairBearer, meeting.id);
      await apiStartMeeting(page.request, chairBearer, meeting.id);
      await apiMarkAttendance(page.request, chairBearer, meeting.id, { userId: chair.publicId, name: chair.fullName, role: 'Chair', isVotingEligible: true });
      await apiMarkAttendance(page.request, chairBearer, meeting.id, { userId: mem.publicId, name: mem.fullName, role: 'Member', isVotingEligible: true });

      // The member casts; the SECRETARY closes → the counter of record is the secretary, so the chair
      // is an independent co-attester and SoD-3 permits the chair to issue.
      const vote = await apiConfigureVote(page.request, chairBearer, {
        topicId: topic.id,
        meetingId: meeting.id,
        eligibleVoters: [
          { userId: chair.keycloakUserId, name: chair.fullName },
          { userId: mem.keycloakUserId, name: mem.fullName },
        ],
        minCast: 1,
      });
      await apiOpenVote(page.request, chairBearer, vote.id);
      await apiCastBallot(memberPage.request, memBearer, vote.id, 'Approve');
      await apiCloseVote(secretaryPage.request, secBearer, vote.id);

      await page.setViewportSize({ width: 1280, height: 1400 });
      await page.goto(`/meetings/${meeting.key}/notes`);
      await page.getByRole('button', { name: 'Record decision' }).click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();
      await expect(dialog.getByText(new RegExp(`Ratifies ${vote.key}`))).toBeVisible();

      await dialog.getByRole('button', { name: 'Outcome' }).click();
      await dialog.getByRole('option', { name: 'Rejected' }).click();
      await dialog.getByRole('textbox', { name: 'Title' }).fill('Reject after review');
      await dialog.getByRole('textbox', { name: 'Decision statement' }).fill('The committee rejects the proposal on review.');
      await dialog.getByRole('textbox', { name: 'Rationale' }).fill('Later evidence overturned the ballot.');
      // Exercise the chair-override leg (AC-016): the flag + a justification are collected and recorded.
      await dialog.getByRole('checkbox', { name: /Issue against the vote/ }).check();
      await dialog.getByRole('textbox', { name: 'Override justification' }).fill('New evidence emerged after the ballot closed.');

      const [issueRes] = await Promise.all([
        page.waitForResponse((r) => /\/api\/decisions\/[^/]+\/issue$/.test(r.url()) && r.request().method() === 'POST'),
        dialog.getByRole('button', { name: 'Record & issue' }).click(),
      ]);
      expect(issueRes.status()).toBe(204);

      // On success the dialog routes to the new decision; assert it is Issued and the vote is Ratified.
      await expect(page).toHaveURL(/\/decisions\/DECN-/);
      const decKey = page.url().split('/decisions/')[1];
      const dec = await page.request.get(`/api/decisions/${decKey}`, { headers: { Authorization: chairBearer } });
      expect((await dec.json()).status).toBe('Issued');
      const voteAfter = await page.request.get(`/api/votes/${vote.key}`, { headers: { Authorization: chairBearer } });
      expect((await voteAfter.json()).status).toBe('Ratified');
    } finally {
      await secretaryCtx.close();
      await memberCtx.close();
    }
  });
});
