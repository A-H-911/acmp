import { test, expect } from '@playwright/test';
import { loginAs } from './login';
import { E2E_USERS } from './users';
import { captureBearer, prepareTopic } from './apiHelpers';

/*
 * S6b (ADR-0016 §2) — the core governance loop end-to-end against the REAL stack:
 *   submit topic → accept → prepare → schedule meeting → build agenda → publish (= notify)
 *   → start → conduct → end → minutes-gate → both recipients see the notification.
 *
 * Honest reconciliations (the loop names screens that don't all ship in v1):
 *  - "minutes" is an honest PLACEHOLDER (MoM module = P7); we assert its gate renders, never
 *    fake a minutes screen.
 *  - "notify" is the publish-agenda fan-out to all committee members; we verify the bell for
 *    BOTH recipients (member + chairman), satisfying the "≥2 members" mandate.
 *  - Two steps have no v1 UI and are API-assisted (see apiHelpers): capturing the secretary's
 *    bearer, and marking the topic Prepared. Submit/accept/schedule/build/publish/conduct/end
 *    are all driven through the real UI.
 *
 * Setup: the stack boots empty, so recipients must self-provision (a login creates an *active*
 * member) BEFORE the fan-out — we log each recipient in once, in its own context, and keep the
 * contexts open to read their notification bells at the end.
 */
test.describe('core loop — topic → agenda → meeting → conduct → notify', () => {
  test('a topic travels the full governance loop and notifies the committee', async ({ page, browser, request }) => {
    test.setTimeout(180_000);

    const stamp = Date.now();
    const title = `E2E Core Loop ${stamp}`;
    const memberName = `${E2E_USERS.member.firstName} ${E2E_USERS.member.lastName}`; // "E2E Member"

    const memberCtx = await browser.newContext();
    const chairCtx = await browser.newContext();
    const memberPage = await memberCtx.newPage();
    const chairPage = await chairCtx.newPage();

    try {
      await test.step('provision the two recipient members (chairman + member)', async () => {
        await loginAs(chairPage, 'chairman');
        await loginAs(memberPage, 'member');
      });

      let bearer = '';
      await test.step('secretary signs in (PKCE) and we capture their bearer', async () => {
        await loginAs(page, 'secretary');
        bearer = await captureBearer(page);
      });

      let topic: { id: string; key: string } = { id: '', key: '' };
      await test.step('secretary submits a topic', async () => {
        await page.goto('/backlog/submit');
        await page.getByRole('button', { name: 'Arch. Decision' }).click();
        // getByRole uses the accessible name (the required "*" is aria-hidden), unlike getByLabel
        // which matches the <label> text "Title*".
        await page.getByRole('textbox', { name: 'Title', exact: true }).fill(title);
        await page.getByRole('textbox', { name: 'Description', exact: true }).fill('E2E description for the core-loop spec.');
        await page.getByRole('textbox', { name: 'Why now', exact: true }).fill('E2E justification — exercises the full loop.');
        const streams = page.getByRole('textbox', { name: 'Affected streams', exact: true });
        await streams.fill('Platform');
        await streams.press('Enter');

        const [createRes] = await Promise.all([
          page.waitForResponse((r) => r.url().endsWith('/api/topics') && r.request().method() === 'POST'),
          page.getByRole('button', { name: 'Submit for triage' }).click(),
        ]);
        expect(createRes.status()).toBe(201);
        topic = await createRes.json();
        await expect(page).toHaveURL(new RegExp(`/topics/${topic.key}`));
      });

      await test.step('secretary accepts the topic (Kanban → owner = member)', async () => {
        await page.goto('/backlog');
        await page.getByRole('button', { name: 'Kanban' }).click();
        const card = page.locator('.kb-card', { hasText: topic.key });
        await card.focus();
        await card.press('m'); // keyboard move popover (more deterministic than native drag)
        await page.getByRole('button', { name: 'Accepted' }).click();
        // Scope to the dialog — the Backlog's disabled "Owner" filter chip is still in the DOM behind it.
        const acceptDialog = page.getByRole('dialog');
        await acceptDialog.getByRole('button', { name: 'Owner' }).click();
        await page.getByRole('option', { name: memberName, exact: true }).click();
        await acceptDialog.getByRole('button', { name: 'Accept', exact: true }).click();
        await expect(page.getByRole('dialog')).toHaveCount(0);
      });

      await test.step('mark the topic Prepared (API — no v1 UI path)', async () => {
        await prepareTopic(request, bearer, topic.id);
      });

      let meetingKey = '';
      await test.step('secretary schedules a meeting', async () => {
        await page.goto('/meetings/new');
        await page.getByRole('textbox', { name: 'Title', exact: true }).fill(`E2E Meeting ${stamp}`);
        await page.getByRole('button', { name: 'Date', exact: true }).click();
        await page.locator('.datepicker-day.is-today').click();
        await page.getByLabel('Start time').fill('14:00');
        await page.getByLabel('End time').fill('15:00');

        const [schedRes] = await Promise.all([
          page.waitForResponse((r) => r.url().endsWith('/api/meetings') && r.request().method() === 'POST'),
          page.getByRole('button', { name: 'Schedule', exact: true }).click(),
        ]);
        expect(schedRes.status()).toBe(201);
        meetingKey = (await schedRes.json()).key;
        await expect(page).toHaveURL(new RegExp(`/meetings/${meetingKey}`));
      });

      await test.step('secretary builds the agenda and publishes (= notify)', async () => {
        // P6a IA: the schedule lands on the meeting Overview; the agenda builder is its own route.
        await page.goto(`/meetings/${meetingKey}/agenda`);
        await page.getByRole('button', { name: `Add ${topic.key} to the agenda` }).click();
        await expect(page.locator('.mt-agenda-list')).toContainText(topic.key);

        // Every agenda item needs a presenter before the agenda can be published (domain invariant).
        await page.getByRole('button', { name: `Presenter for ${topic.key}` }).click();
        await page.getByRole('option', { name: memberName, exact: true }).click();

        await page.getByRole('button', { name: 'Publish & notify' }).first().click();
        const pubDialog = page.getByRole('dialog');
        const [pubRes] = await Promise.all([
          page.waitForResponse((r) => r.url().includes('/agenda/publish') && r.request().method() === 'POST'),
          pubDialog.getByRole('button', { name: 'Publish & notify' }).click(),
        ]);
        expect(pubRes.status()).toBe(200);
        await expect(page.getByText('Published', { exact: true })).toBeVisible();
      });

      await test.step('secretary starts and conducts the meeting', async () => {
        // Start is the shell header's lifecycle action once the agenda is published (phase=ready).
        await Promise.all([
          page.waitForResponse((r) => r.url().includes('/start') && r.request().method() === 'POST'),
          page.getByRole('button', { name: 'Start meeting' }).click(),
        ]);
        // Conduct is its own route (Attendance + Notes composition); open the live workspace.
        await page.goto(`/meetings/${meetingKey}/notes`);
        await expect(page.getByRole('timer')).toBeVisible();

        await page.getByRole('button', { name: `Toggle attendance for ${memberName}` }).click();
        const note = page.getByLabel('Discussion notes', { exact: true });
        await note.fill('Discussed in the E2E run.');
        await note.blur();
        await expect(page.getByText('Autosaved')).toBeVisible();
      });

      await test.step('secretary ends the meeting', async () => {
        await page.getByRole('button', { name: 'End → Minutes' }).click();
        await expect(page).toHaveURL(/\/meetings$/);
      });

      await test.step('minutes is an honest placeholder gate (no fake minutes)', async () => {
        await page.goto(`/meetings/${meetingKey}/minutes`);
        await expect(page.getByText('Minutes arrive in a later phase')).toBeVisible();
      });

      await test.step('both recipients see the publish notification', async () => {
        for (const recipient of [memberPage, chairPage]) {
          await recipient.goto('/');
          await expect(recipient.locator('.notif-badge')).toBeVisible();
        }
      });
    } finally {
      await memberCtx.close();
      await chairCtx.close();
    }
  });
});
