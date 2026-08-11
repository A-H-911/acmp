import { test, expect, type Page } from '@playwright/test';
import { loginAs } from './login';
import { E2E_USERS } from './users';
import { captureBearer, meMember } from './apiHelpers';
import {
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
  // meMember still forces JIT provisioning first (idempotent), so the original guard against racing
  // the SPA's async login-time provision is preserved — but it matches the caller's OWN row by
  // publicId instead of by role, which is not unique on an environment that is never reset.
  const me = await meMember(page, bearer);
  return { bearer, secretary: me };
}

/**
 * Open the kanban narrowed to ONE topic.
 *
 * A fresh stack shows every topic, so `.kb-card` for a just-created topic was always present. A
 * long-lived environment is different: GetBacklog pages at 25 and the kanban sorts (Priority,
 * CreatedAt, Key) ascending, so the newest priority-0 card sorts LAST and falls past the fold —
 * `dragHtml5` then waits for a card that is never rendered and the test times out. Worse, the suite
 * ratchets: every run creates more topics, so these tests passed earlier today and stopped passing
 * a few runs later with no code change at all. Search matches Title OR Key, so the key is exact.
 */
async function kanbanFilteredTo(page: Page, key: string): Promise<void> {
  await page.goto('/backlog');
  await page.getByRole('button', { name: 'Kanban' }).click();
  await page.locator('.bk-filters').getByRole('searchbox').fill(key); // 300 ms debounce → refetch at page 1
  await expect(page.locator('.kb-card', { hasText: key })).toBeVisible();
}

test.describe('S6b-2 — native drag paths + failure-first', () => {
  test('Kanban: dragging a Triage card to Accepted opens the accept dialog', async ({ page, request }) => {
    const { bearer } = await secretarySession(page);
    const topic = await apiCreateTopic(request, bearer, `S6b2 Kanban drag ${Date.now()}`);

    await kanbanFilteredTo(page, topic.key);
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

    await kanbanFilteredTo(page, topic.key);
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

    await page.goto(`/meetings/${meeting.key}/agenda`);
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

    await page.goto(`/meetings/${meeting.key}/agenda`);
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

    // Agenda route: empty agenda → Publish disabled.
    await page.goto(`/meetings/${meeting.key}/agenda`);
    await expect(page.getByRole('button', { name: 'Publish & notify' })).toBeDisabled();

    // Conduct route (Notes): not published → "Not started yet" gate, and the shell's lifecycle
    // action is "Build agenda" (notReady), never "Start meeting" until the agenda is published.
    await page.goto(`/meetings/${meeting.key}/notes`);
    await expect(page.getByText('Not started yet')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Start meeting' })).toHaveCount(0);
  });
});
