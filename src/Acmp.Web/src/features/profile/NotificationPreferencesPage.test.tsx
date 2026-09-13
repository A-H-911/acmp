import { describe, it, expect, afterEach, vi } from 'vitest';
import { screen, within, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import axe from 'axe-core';
import i18n from '../../i18n';
import { renderWithAuth } from '../../test/render';
import { NotificationPreferencesPage } from './NotificationPreferencesPage';
import type { NotificationPreferences } from '../../api/notificationPreferences';

/*
 * Runs the REAL hooks against a stubbed fetch (renderWithAuth supplies the QueryClient, retries off),
 * so the page is checked against the wire contract rather than a mocked hook's idea of it.
 */

const LIST: NotificationPreferences = {
  items: [
    { category: 'MeetingScheduled', group: 'meetings', inApp: true },
    { category: 'MinutesPublished', group: 'meetings', inApp: false },
    { category: 'VoteOpened', group: 'decisions', inApp: true },
    { category: 'AdrApproved', group: 'governance', inApp: true },
  ],
};

function res(status: number, body?: unknown): Response {
  return { ok: status < 300, status, headers: new Headers(), json: async () => body } as Response;
}

type Handler = (url: string, init?: RequestInit) => Promise<Response>;
function stub(handler: Handler) {
  const spy = vi.fn((input: RequestInfo | URL, init?: RequestInit) => handler(String(input), init));
  vi.stubGlobal('fetch', spy);
  return spy;
}
const isPut = (init?: RequestInit) => init?.method === 'PUT';

afterEach(async () => {
  vi.unstubAllGlobals();
  await i18n.changeLanguage('en');
});

describe('NotificationPreferencesPage — layout', () => {
  it('shows the title, reworded subtitle, In-app/Webex header with NO Email column, and the footnote', async () => {
    stub(async () => res(200, LIST));
    renderWithAuth(<NotificationPreferencesPage />);

    expect(await screen.findByRole('switch', { name: 'Meeting scheduled' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Notification preferences' })).toBeInTheDocument();
    expect(screen.getByText(/In-app delivery is active now; Webex messages to you are coming in a later phase\./)).toBeInTheDocument();
    expect(screen.getByText('Event type')).toBeInTheDocument();
    expect(screen.getByText('In-app')).toBeInTheDocument();
    expect(screen.getByText('Webex')).toBeInTheDocument();
    expect(screen.queryByText(/email/i)).toBeNull();
    expect(screen.getByText(/only in-app delivery is active now\./)).toBeInTheDocument();
    // No "Saved" until something is saved.
    expect(screen.queryByText('Saved')).toBeNull();
  });

  it('groups rows under a header per group, in the server’s order, each with a Phase 2 Webex tag and no locked toggle', async () => {
    stub(async () => res(200, LIST));
    renderWithAuth(<NotificationPreferencesPage />);
    await screen.findByRole('switch', { name: 'Meeting scheduled' });

    const groups = screen.getAllByRole('heading', { level: 2 }).map((h) => h.textContent);
    expect(groups).toEqual(['Meetings', 'Decisions', 'Governance']);

    const meetings = screen.getByRole('region', { name: 'Meetings' });
    expect(within(meetings).getAllByRole('switch').map((s) => s.getAttribute('aria-label'))).toEqual([
      'Meeting scheduled',
      'Minutes published',
    ]);

    const switches = screen.getAllByRole('switch');
    expect(switches).toHaveLength(4);
    switches.forEach((s) => expect(s).toBeEnabled());
    expect(screen.getByRole('switch', { name: 'Minutes published' })).toHaveAttribute('aria-checked', 'false');
    expect(screen.getByRole('switch', { name: 'ADR approved' })).toHaveAttribute('aria-checked', 'true');
    expect(screen.getAllByText('Phase 2')).toHaveLength(4);
  });

  it('still renders an unknown category and group from a newer server under their raw names', async () => {
    stub(async () => res(200, { items: [{ category: 'SomethingNew', group: 'future', inApp: true }] }));
    renderWithAuth(<NotificationPreferencesPage />);

    expect(await screen.findByRole('switch', { name: 'SomethingNew' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: 'future' })).toBeInTheDocument();
  });

  it('renders in Arabic from the bundle', async () => {
    await i18n.changeLanguage('ar');
    stub(async () => res(200, LIST));
    renderWithAuth(<NotificationPreferencesPage />);

    expect(await screen.findByRole('switch', { name: 'جدولة اجتماع' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'تفضيلات الإشعارات' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: 'الاجتماعات' })).toBeInTheDocument();
  });

  it('is axe-clean (WCAG 2.2 AA structure/ARIA)', async () => {
    stub(async () => res(200, LIST));
    const { container } = renderWithAuth(<NotificationPreferencesPage />);
    await screen.findByRole('switch', { name: 'Meeting scheduled' });
    const r = await axe.run(container, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(r.violations.map((v) => v.id)).toEqual([]);
  });
});

describe('NotificationPreferencesPage — loading and load failure', () => {
  it('shows the loading state, then the list', async () => {
    let release!: () => void;
    stub(() => new Promise((ok) => { release = () => ok(res(200, LIST)); }));
    renderWithAuth(<NotificationPreferencesPage />);

    expect(screen.getByRole('status', { busy: true })).toBeInTheDocument();
    release();
    expect(await screen.findByRole('switch', { name: 'Vote opened' })).toBeInTheDocument();
  });

  it('shows the error state on a failed load and retries', async () => {
    let fail = true;
    const spy = stub(async () => (fail ? res(500) : res(200, LIST)));
    const user = userEvent.setup();
    renderWithAuth(<NotificationPreferencesPage />);

    expect((await screen.findAllByText("Couldn't load your notification preferences.")).length).toBeGreaterThan(0);
    fail = false;
    await user.click(screen.getByRole('button', { name: /retry/i }));

    expect(await screen.findByRole('switch', { name: 'Vote opened' })).toBeInTheDocument();
    expect(spy).toHaveBeenCalledTimes(2);
  });
});

describe('NotificationPreferencesPage — saving a toggle', () => {
  it('PUTs that one item, shows the new state disabled while saving, then shows Saved and the server’s answer', async () => {
    let releasePut!: () => void;
    const spy = stub((_url, init) => {
      if (!isPut(init)) return Promise.resolve(res(200, LIST));
      const saved = { items: LIST.items.map((i) => (i.category === 'VoteOpened' ? { ...i, inApp: false } : i)) };
      return new Promise((ok) => { releasePut = () => ok(res(200, saved)); });
    });
    const user = userEvent.setup();
    renderWithAuth(<NotificationPreferencesPage />);
    const vote = await screen.findByRole('switch', { name: 'Vote opened' });

    await user.click(vote);

    const put = spy.mock.calls.find(([, init]) => isPut(init))!;
    expect(String(put[0])).toBe('/api/notifications/preferences');
    expect(JSON.parse(put[1]!.body as string)).toEqual({ items: [{ category: 'VoteOpened', inApp: false }] });
    // In flight: the requested state is shown and no switch can start a second save.
    expect(vote).toHaveAttribute('aria-checked', 'false');
    screen.getAllByRole('switch').forEach((s) => expect(s).toBeDisabled());

    releasePut();

    expect(await screen.findByText('Saved')).toBeInTheDocument();
    await waitFor(() => expect(vote).toBeEnabled());
    expect(vote).toHaveAttribute('aria-checked', 'false');
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('reverts the toggle and announces an error when the save fails', async () => {
    stub(async (_url, init) => (isPut(init) ? res(400, { title: 'Unknown category' }) : res(200, LIST)));
    const user = userEvent.setup();
    renderWithAuth(<NotificationPreferencesPage />);
    const minutes = await screen.findByRole('switch', { name: 'Minutes published' });
    expect(minutes).toHaveAttribute('aria-checked', 'false');

    await user.click(minutes);

    expect(await screen.findByRole('alert')).toHaveTextContent("Couldn't save your change. Try again.");
    expect(minutes).toHaveAttribute('aria-checked', 'false');
    expect(minutes).toBeEnabled();
    expect(screen.queryByText('Saved')).toBeNull();
  });
});
