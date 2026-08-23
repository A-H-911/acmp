import { test, expect } from '@playwright/test';
import { roleSession } from './apiHelpers';
import {
  apiPreparedTopic, apiScheduleMeeting, apiAddAgendaItem, apiPublishAgenda, apiStartMeeting,
  apiDraftMinutes, apiSubmitMinutes, apiApproveMinutes,
} from './scenario';

/*
 * P17b — live real-stack leg for the soft SoD-2 warning (AC-014):
 *   AC-014  when the secretary who AUTHORED a MoM also approves it, the approval is flagged
 *           `approvedBySoleAuthor` server-side; the minutes view surfaces it as a warning badge so the
 *           segregation-of-duties gap is visible (soft SoD-2 — the approval is not blocked, only marked).
 *
 * The conduct chain + the MoM draft → submit → approve are API-seeded with ONE bearer (the secretary),
 * making that secretary both author and approver → sole-author. Then the minutes screen is loaded through
 * the real UI and the badge is asserted (its rendering is the AC-under-test; server-set flag proven live).
 */

test.describe('P17b — minutes sole-author warning (AC-014)', () => {
  test('an approved MoM signed off by its sole author renders the sole-author warning badge', async ({ page, browser }) => {
    test.setTimeout(120_000);
    const { bearer: secBearer, member: sec } = await roleSession(page, 'secretary', 'Secretary');

    const chairCtx = await browser.newContext();
    const chairPage = await chairCtx.newPage();
    try {
      const { member: chair } = await roleSession(chairPage, 'chairman', 'Chairman');

      const stamp = Date.now();
      const topic = await apiPreparedTopic(page.request, secBearer, `P17b SoleAuthor Topic ${stamp}`, chair);
      const meeting = await apiScheduleMeeting(page.request, secBearer, `P17b SoleAuthor Meeting ${stamp}`, chair);
      await apiAddAgendaItem(page.request, secBearer, meeting.id, topic, chair);
      await apiPublishAgenda(page.request, secBearer, meeting.id);
      await apiStartMeeting(page.request, secBearer, meeting.id);

      // The SAME secretary drafts, submits, and approves → author === approver → approvedBySoleAuthor.
      const mom = await apiDraftMinutes(page.request, secBearer, meeting.id, 'Sole-author minutes body.');
      await apiSubmitMinutes(page.request, secBearer, mom.id);
      await apiApproveMinutes(page.request, secBearer, mom.id);

      // The author-approver loads the minutes; the banner warns that a sole author signed off (soft SoD-2).
      await page.goto(`/meetings/${meeting.key}/minutes`);
      await expect(page.getByText('Approved by sole author')).toBeVisible();
      expect(sec.role).toBe('Secretary'); // sanity: the seeding bearer is the secretary (author + approver)
    } finally {
      await chairCtx.close();
    }
  });
});
