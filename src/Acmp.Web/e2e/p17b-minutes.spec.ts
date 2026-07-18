import { test, expect, type APIRequestContext } from '@playwright/test';
import { roleSession } from './apiHelpers';
import {
  apiPreparedTopic, apiScheduleMeeting, apiAddAgendaItem, apiPublishAgenda, apiStartMeeting,
  apiDraftMinutes, apiSubmitMinutes, apiApproveMinutes, apiPublishMinutes, type ApiMember, type ApiMeeting,
} from './scenario';

/*
 * P17b — live real-stack leg for the MoM (minutes) lifecycle ACs (bin (a) of the P17b-0 triage):
 *   AC-038  approve & publish a MoM (InReview → Published) from the minutes page
 *   AC-037  request changes on a MoM (InReview → Draft)
 *   AC-036  supersede a Published MoM → a new published version (v2); prior stays readable, no in-place edit
 *
 * The meeting-conduct + minutes lifecycle is seeded via the API up to each AC's prior state; the UI drives
 * only the transition under test (the secretary owns draft + approve — the approve soft-SoD is non-blocking).
 * The minutes page renders once the meeting is InProgress, so no end step is needed.
 */

/** Seed a started meeting with one agenda item; the secretary is owner/chair/presenter. */
async function seedStartedMeeting(request: APIRequestContext, bearer: string, sec: ApiMember, stamp: number): Promise<ApiMeeting> {
  const topic = await apiPreparedTopic(request, bearer, `P17b MoM Topic ${stamp}`, sec);
  const meeting = await apiScheduleMeeting(request, bearer, `P17b MoM Meeting ${stamp}`, sec);
  await apiAddAgendaItem(request, bearer, meeting.id, topic, sec);
  await apiPublishAgenda(request, bearer, meeting.id);
  await apiStartMeeting(request, bearer, meeting.id);
  return meeting;
}

test.describe('P17b — minutes lifecycle (AC-036 / AC-037 / AC-038)', () => {
  test('approve & publish a MoM from the minutes page (AC-038)', async ({ page }) => {
    test.setTimeout(120_000);
    const { bearer, member: sec } = await roleSession(page, 'secretary', 'Secretary');
    const meeting = await seedStartedMeeting(page.request, bearer, sec, Date.now());
    const mom = await apiDraftMinutes(page.request, bearer, meeting.id, 'Draft minutes body.');
    await apiSubmitMinutes(page.request, bearer, mom.id); // → InReview

    await page.goto(`/meetings/${meeting.key}/minutes`);
    await page.getByRole('button', { name: 'Approve & publish' }).click();

    // Both transitions (approve → publish) run; the MoM locks as Published.
    await expect(page.getByText('Published & locked — this record is immutable and audit-logged.')).toBeVisible();
  });

  test('request changes returns a MoM to Draft (AC-037)', async ({ page }) => {
    test.setTimeout(120_000);
    const { bearer, member: sec } = await roleSession(page, 'secretary', 'Secretary');
    const meeting = await seedStartedMeeting(page.request, bearer, sec, Date.now());
    const mom = await apiDraftMinutes(page.request, bearer, meeting.id, 'Draft minutes body.');
    await apiSubmitMinutes(page.request, bearer, mom.id); // → InReview

    await page.goto(`/meetings/${meeting.key}/minutes`);
    await page.getByRole('button', { name: 'Request changes' }).click();

    // Back to an editable Draft (no publish, no in-place lock).
    await expect(page.getByText('Editable — save your changes as you write.')).toBeVisible();
  });

  test('supersede a published MoM creates a new version (AC-036)', async ({ page }) => {
    test.setTimeout(120_000);
    const { bearer, member: sec } = await roleSession(page, 'secretary', 'Secretary');
    const meeting = await seedStartedMeeting(page.request, bearer, sec, Date.now());
    const mom = await apiDraftMinutes(page.request, bearer, meeting.id, 'Original minutes body.');
    await apiSubmitMinutes(page.request, bearer, mom.id);
    await apiApproveMinutes(page.request, bearer, mom.id);
    await apiPublishMinutes(page.request, bearer, mom.id); // → Published (v1)

    await page.setViewportSize({ width: 1280, height: 1400 }); // supersede dialog holds a full body editor
    await page.goto(`/meetings/${meeting.key}/minutes`);
    await page.getByRole('button', { name: 'Supersede' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await dialog.getByRole('textbox', { name: 'Minutes body' }).fill('Corrected minutes body.');
    await dialog.getByRole('textbox', { name: 'Reason for superseding' }).fill('Fixing a recorded error.');

    const [res] = await Promise.all([
      page.waitForResponse((r) => /\/api\/minutes\/[^/]+\/supersede$/.test(r.url()) && r.request().method() === 'POST'),
      dialog.getByRole('button', { name: 'Publish correction' }).click(),
    ]);
    expect(res.status()).toBe(201);
    const successor = (await res.json()) as { version: number };
    expect(successor.version).toBe(2); // a NEW version, not an in-place edit
    await expect(dialog).toHaveCount(0);
  });
});
