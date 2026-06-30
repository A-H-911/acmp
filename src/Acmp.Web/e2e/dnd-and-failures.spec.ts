import { test, expect, type Page } from '@playwright/test';
import { loginAs } from './login';
import { E2E_USERS } from './users';
import { captureBearer } from './apiHelpers';
import {
  apiMembers,
  apiCreateTopic,
  apiPreparedTopic,
  apiScheduleMeeting,
  apiAddAgendaItem,
  dragHtml5,
  type ApiMember,
} from './scenario';

/*
 * S6b-2 (ADR-0016 §2) — the S4-deferred native HTML5 drag paths (jsdom can't run them, so they
 * were "/* v8 ignore *​/"-ed pending E2E) + the adversarial failure-first cases the mandate names.
 *
 * Setup that isn't under test (prepared topics, meetings, agenda items) is built through the API
 * with a real captured bearer; the UI is reserved for the drag/denial being asserted.
 */

const secretaryName = `${E2E_USERS.secretary.firstName} ${E2E_USERS.secretary.lastName}`; // "E2E Secretary"

async function secretarySession(page: Page): Promise<{ bearer: string; secretary: ApiMember }> {
  await loginAs(page, 'secretary');
  const bearer = await captureBearer(page);
  // Force JIT provisioning to complete before we read the directory — the SPA's POST /members/me
  // is async on login, so the very first query can race it. The endpoint is idempotent.
  await page.request.post('/api/members/me', { headers: { Authorization: bearer } });
  const me = (await apiMembers(page.request, bearer)).find((m) => m.role === 'Secretary');
  if (!me) throw new Error('[e2e] secretary member not provisioned after login');
  return { bearer, secretary: me };
}

test.describe('S6b-2 — native drag paths + failure-first', () => {
  test('Kanban: dragging a Triage card to Accepted opens the accept dialog', async ({ page, request }) => {
    const { bearer } = await secretarySession(page);
    const topic = await apiCreateTopic(request, bearer, `S6b2 Kanban drag ${Date.now()}`);

    await page.goto('/backlog');
    await page.getByRole('button', { name: 'Kanban' }).click();
    const card = page.locator('.kb-card', { hasText: topic.key });
    const acceptedCol = page.locator('.kb-col').filter({ hasText: 'Accepted' });
    await dragHtml5(card, acceptedCol);

    // The triage→accepted move needs an owner, so the drop opens the AcceptDialog.
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText(topic.key);
  });

  test('Kanban: dragging a Triage card to Scheduled is rejected with an announced reason', async ({ page, request }) => {
    const { bearer } = await secretarySession(page);
    const topic = await apiCreateTopic(request, bearer, `S6b2 Kanban illegal ${Date.now()}`);

    await page.goto('/backlog');
    await page.getByRole('button', { name: 'Kanban' }).click();
    const card = page.locator('.kb-card', { hasText: topic.key });
    const scheduledCol = page.locator('.kb-col').filter({ hasText: 'Scheduled' });
    await dragHtml5(card, scheduledCol);

    // No P5 endpoint for →Scheduled: announced rejection, not a silent no-op (and no dialog).
    await expect(page.locator('[aria-live="assertive"]')).toContainText('Scheduled');
    await expect(page.getByRole('dialog')).toHaveCount(0);
  });

  test('Agenda: dragging a pool topic onto the agenda adds it', async ({ page, request }) => {
    const { bearer, secretary } = await secretarySession(page);
    const topic = await apiPreparedTopic(request, bearer, `S6b2 pool drag ${Date.now()}`, secretary);
    const meeting = await apiScheduleMeeting(request, bearer, `S6b2 Meeting ${Date.now()}`, secretary);

    await page.goto(`/meetings/${meeting.key}`);
    const poolCard = page.locator('.mt-pool-card', { hasText: topic.key });
    await expect(poolCard).toBeVisible();
    const agenda = page.getByRole('region', { name: 'Agenda items' });
    await dragHtml5(poolCard, agenda);

    await expect(page.locator('.mt-agenda-list')).toContainText(topic.key);
  });

  test('Agenda: dragging the second item onto the first reorders them', async ({ page, request }) => {
    const { bearer, secretary } = await secretarySession(page);
    const stamp = Date.now();
    const topicA = await apiPreparedTopic(request, bearer, `S6b2 reorder A ${stamp}`, secretary);
    const topicB = await apiPreparedTopic(request, bearer, `S6b2 reorder B ${stamp}`, secretary);
    const meeting = await apiScheduleMeeting(request, bearer, `S6b2 Reorder Mtg ${stamp}`, secretary);
    await apiAddAgendaItem(request, bearer, meeting.id, topicA, secretary);
    await apiAddAgendaItem(request, bearer, meeting.id, topicB, secretary);

    await page.goto(`/meetings/${meeting.key}`);
    const list = page.locator('.mt-agenda-list');
    await expect(list.locator('.mt-item').first()).toContainText(topicA.key);

    await dragHtml5(list.locator('.mt-item', { hasText: topicB.key }), list.locator('.mt-item', { hasText: topicA.key }));

    // Reorder is a single ±1 step: B nudges above A.
    await expect(list.locator('.mt-item').first()).toContainText(topicB.key);
  });

  test('Schedule: a member is denied scheduling a meeting (403)', async ({ page }) => {
    await loginAs(page, 'member');
    await page.goto('/meetings/new');
    await page.getByRole('textbox', { name: 'Title', exact: true }).fill(`S6b2 Denied ${Date.now()}`);
    // Pick the member themselves as chair so the form is client-valid and the POST actually fires.
    await page.getByRole('button', { name: 'Chair' }).click();
    await page.getByRole('option', { name: `${E2E_USERS.member.firstName} ${E2E_USERS.member.lastName}`, exact: true }).click();
    await page.getByRole('button', { name: 'Date', exact: true }).click();
    await page.locator('.datepicker-day.is-today').click();
    await page.getByLabel('Start time').fill('14:00');
    await page.getByLabel('End time').fill('15:00');

    const [res] = await Promise.all([
      page.waitForResponse((r) => r.url().endsWith('/api/meetings') && r.request().method() === 'POST'),
      page.getByRole('button', { name: 'Schedule', exact: true }).click(),
    ]);
    expect(res.status()).toBe(403);
    await expect(page.getByText("Couldn't schedule the meeting. Please try again.")).toBeVisible();
  });

  test('Schedule: the form blocks empty and inverted-window submissions', async ({ page }) => {
    await secretarySession(page); // log in as secretary so the chair picker is populated
    await page.goto('/meetings/new');

    // Empty submit → required-field errors, no request.
    await page.getByRole('button', { name: 'Schedule', exact: true }).click();
    await expect(page.getByText('A meeting title is required.')).toBeVisible();
    await expect(page.getByText('A meeting date is required.')).toBeVisible();

    // Fill everything but invert the window → window error.
    await page.getByRole('textbox', { name: 'Title', exact: true }).fill(`S6b2 Window ${Date.now()}`);
    await page.getByRole('button', { name: 'Chair' }).click();
    await page.getByRole('option', { name: secretaryName, exact: true }).click();
    await page.getByRole('button', { name: 'Date', exact: true }).click();
    await page.locator('.datepicker-day.is-today').click();
    await page.getByLabel('Start time').fill('15:00');
    await page.getByLabel('End time').fill('14:00');
    await page.getByRole('button', { name: 'Schedule', exact: true }).click();
    await expect(page.getByText('The end time must be after the start time.')).toBeVisible();
  });

  test('Meeting: publish is disabled and start is blocked until the agenda is built and published', async ({ page, request }) => {
    const { bearer, secretary } = await secretarySession(page);
    const meeting = await apiScheduleMeeting(request, bearer, `S6b2 Empty Mtg ${Date.now()}`, secretary);

    await page.goto(`/meetings/${meeting.key}`);
    // Agenda tab (default): empty agenda → Publish disabled.
    await expect(page.getByRole('button', { name: 'Publish & notify' })).toBeDisabled();

    // Meeting tab: not published → "Not started yet" gate, no Start control.
    await page.getByRole('tab', { name: 'Meeting' }).click();
    await expect(page.getByText('Not started yet')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Start meeting' })).toHaveCount(0);
  });
});
