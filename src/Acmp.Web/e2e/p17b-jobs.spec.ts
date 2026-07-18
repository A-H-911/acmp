import { test, expect, type APIRequestContext } from '@playwright/test';
import { roleSession } from './apiHelpers';
import { apiCreateTopic, apiCreateAction } from './scenario';

/*
 * P17b — real-fire leg for the Hangfire reminder jobs (operator decision U3: force a genuine fire in e2e —
 * only a real fire proves Hangfire actually invokes the sweep on the composed stack).
 *   AC-054  a due-soon action → the owner receives an in-app due reminder.
 *   AC-055  an action overdue past the thresholds → Secretary + Chairman receive an escalation, and it is audited.
 *
 * The worker runs the sweep on a cron of `* * * * *` (minutely) in e2e — set by ACTION_REMINDERS_SWEEP_CRON in
 * the CI job env (and the local run); production keeps the daily default (docker-compose.yml, R4a). The sweep is
 * a no-op emitting no AuditEvent when nothing is due (SweepActionReminders.cs:122), so a fresh stack has no
 * applock contention until this spec seeds the two actions. We then wait — BOUNDED and explicit — for the fire:
 * a sleep that passes silently when the job never fires is worse than no test, so we poll for the real
 * notifications + audit and fail loudly on timeout.
 */

const DAY = 86_400_000;

/** Poll the notification center for a notification of `category` deep-linking to `actionKey`. */
async function pollNotification(
  request: APIRequestContext,
  bearer: string,
  actionKey: string,
  category: string,
  timeoutMs: number,
): Promise<boolean> {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const res = await request.get('/api/notifications', { headers: { Authorization: bearer } });
    if (res.ok()) {
      const data = (await res.json()) as { items?: { category: string; deepLink: string | null }[] };
      if ((data.items ?? []).some((n) => n.category === category && (n.deepLink ?? '').includes(actionKey))) return true;
    }
    await new Promise((r) => setTimeout(r, 5_000));
  }
  return false;
}

/**
 * Poll the audit log for the sweep's `Actions.RemindersSent` event. The event's `{ActionKey, Kinds}` payload
 * lives in the DataJson column, which the read DTO does not expose (only Before/AfterJson) — so we assert the
 * event's PRESENCE (filtered by its action). On a fresh e2e stack this test seeds the only due/overdue actions,
 * so a RemindersSent event can only come from this sweep; the escalation content itself is already proven by
 * the two escalation notifications above.
 */
async function pollRemindersAudit(request: APIRequestContext, bearer: string, timeoutMs: number): Promise<boolean> {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const res = await request.get('/api/audit?action=Actions.RemindersSent&pageSize=100', { headers: { Authorization: bearer } });
    if (res.ok()) {
      const data = (await res.json()) as { items?: unknown[] };
      if ((data.items ?? []).length > 0) return true;
    }
    await new Promise((r) => setTimeout(r, 5_000));
  }
  return false;
}

test.describe('P17b — reminder jobs real-fire (AC-054 / AC-055)', () => {
  test('the Hangfire sweep fires on the real stack: due reminder + escalation + audit', async ({ page, browser }) => {
    test.setTimeout(200_000);
    const { bearer: secBearer, member: sec } = await roleSession(page, 'secretary', 'Secretary');

    const chairCtx = await browser.newContext();
    const chairPage = await chairCtx.newPage();
    try {
      const { bearer: chairBearer } = await roleSession(chairPage, 'chairman', 'Chairman'); // active Chairman → escalation recipient
      const topic = await apiCreateTopic(page.request, secBearer, `P17b Jobs ${Date.now()}`);

      // AC-054: due in 2 days (inside the 3-day reminder window) → the owner (secretary) gets a due-soon nudge.
      const dueSoon = await apiCreateAction(page.request, secBearer, {
        title: `P17b due-soon ${Date.now()}`,
        ownerUserId: sec.keycloakUserId,
        ownerName: sec.fullName,
        sourceId: topic.id,
        dueDate: new Date(Date.now() + 2 * DAY).toISOString(),
      });
      // AC-055: overdue by 20 days (> the 14-day chairman threshold) → Secretary + Chairman escalation + audit.
      const overdue = await apiCreateAction(page.request, secBearer, {
        title: `P17b overdue ${Date.now()}`,
        ownerUserId: sec.keycloakUserId,
        ownerName: sec.fullName,
        sourceId: topic.id,
        dueDate: new Date(Date.now() - 20 * DAY).toISOString(),
      });

      // The first assertion carries the long wait (up to ~2 minutely boundaries); the rest are already produced
      // by the same fire, so they resolve quickly.
      expect(
        await pollNotification(page.request, secBearer, dueSoon.key, 'ActionDueReminder', 150_000),
        'AC-054: a due-soon reminder was delivered to the owner by a genuine sweep fire',
      ).toBe(true);
      expect(
        await pollNotification(chairPage.request, chairBearer, overdue.key, 'ActionOverdueEscalation', 30_000),
        'AC-055: the overdue escalation was delivered to the chairman',
      ).toBe(true);
      expect(
        await pollNotification(page.request, secBearer, overdue.key, 'ActionOverdueEscalation', 15_000),
        'AC-055: the overdue escalation was delivered to the secretary',
      ).toBe(true);
      expect(
        await pollRemindersAudit(page.request, secBearer, 15_000),
        'AC-055: the escalation was recorded in the audit log (Actions.RemindersSent)',
      ).toBe(true);
    } finally {
      await chairCtx.close();
    }
  });
});
