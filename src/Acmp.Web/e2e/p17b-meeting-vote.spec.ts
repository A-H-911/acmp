import { test, expect } from '@playwright/test';
import { roleSession } from './apiHelpers';
import {
  apiPreparedTopic, apiScheduleMeeting, apiAddAgendaItem, apiPublishAgenda, apiStartMeeting, apiMarkAttendance,
} from './scenario';

/*
 * P17b — live real-stack leg for the vote configure+open AC (bin (a) of the P17b-0 triage):
 *   AC-021  a vote configured + opened from the meeting workspace locks its configuration and rosters only
 *           its eligible voters.
 *
 * This drives the CallVoteDialog in the in-session MeetingWorkspace (the UI path the API-seeded voting specs
 * skip). The meeting-conduct chain is seeded via the API (prepared topic → schedule → agenda → publish →
 * start → mark the two voting-eligible members Present, so the meeting-linked vote's present-quorum gate is
 * satisfied on open). Then the secretary configures the vote through the dialog and opens it on the vote page.
 */

test.describe('P17b — meeting-workspace vote (AC-021)', () => {
  test('a secretary configures + opens a vote from the workspace; config locks, only eligible voters are rostered', async ({ page, browser }) => {
    test.setTimeout(120_000);
    const { bearer: secBearer } = await roleSession(page, 'secretary', 'Secretary');

    const chairCtx = await browser.newContext();
    const memberCtx = await browser.newContext();
    const chairPage = await chairCtx.newPage();
    const memberPage = await memberCtx.newPage();
    try {
      // Chairman + Member are the voting-eligible roster (Secretary is not eligible); both must be provisioned.
      const { member: chair } = await roleSession(chairPage, 'chairman', 'Chairman');
      const { member: mem } = await roleSession(memberPage, 'member', 'Member');

      const stamp = Date.now();
      const topic = await apiPreparedTopic(page.request, secBearer, `P17b Vote-Open Topic ${stamp}`, chair);
      const meeting = await apiScheduleMeeting(page.request, secBearer, `P17b Vote-Open Meeting ${stamp}`, chair);
      await apiAddAgendaItem(page.request, secBearer, meeting.id, topic, chair);
      await apiPublishAgenda(page.request, secBearer, meeting.id);
      await apiStartMeeting(page.request, secBearer, meeting.id);
      // Both eligible voters Present → the meeting-linked vote can meet its present quorum (2) on open.
      await apiMarkAttendance(page.request, secBearer, meeting.id, { userId: chair.publicId, name: chair.fullName, role: 'Chair', isVotingEligible: true });
      await apiMarkAttendance(page.request, secBearer, meeting.id, { userId: mem.publicId, name: mem.fullName, role: 'Member', isVotingEligible: true });

      // The workspace auto-activates the first agenda item; the secretary calls a vote from it.
      await page.setViewportSize({ width: 1280, height: 1400 }); // CallVoteDialog is tall (voter checkboxes)
      await page.goto(`/meetings/${meeting.key}/notes`);
      await page.getByRole('button', { name: 'Call vote' }).click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();
      // Eligible voters are pre-selected (chairman + member); set the required quorum to 2 (AC-021).
      await dialog.getByRole('spinbutton', { name: 'Required quorum' }).fill('2');

      const [cfg] = await Promise.all([
        page.waitForResponse((r) => r.url().endsWith('/api/votes') && r.request().method() === 'POST'),
        dialog.getByRole('button', { name: 'Configure vote' }).click(),
      ]);
      expect(cfg.status()).toBe(201);
      const vote = (await cfg.json()) as { key: string };

      // Configured vote → the dialog routes to /votes/{key} (not_open); the chair/secretary opens it.
      await expect(page).toHaveURL(new RegExp(`/votes/${vote.key}`));
      const [openRes] = await Promise.all([
        page.waitForResponse((r) => /\/api\/votes\/[^/]+\/open$/.test(r.url()) && r.request().method() === 'POST'),
        page.getByRole('button', { name: 'Open voting' }).click(),
      ]);
      expect(openRes.status()).toBe(204);

      // Config is now locked (vote Open) and the roster is exactly the two eligible voters.
      await expect(page.getByText('Voting open')).toBeVisible();
      await expect(page.locator('.voter-row')).toHaveCount(2);
      await expect(page.locator('.voter-row', { hasText: chair.fullName })).toBeVisible();
      await expect(page.locator('.voter-row', { hasText: mem.fullName })).toBeVisible();
    } finally {
      await chairCtx.close();
      await memberCtx.close();
    }
  });
});
