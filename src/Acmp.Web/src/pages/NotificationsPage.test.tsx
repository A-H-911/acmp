import { describe, it, expect, vi, beforeEach, afterEach, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import axe from 'axe-core';
import i18n from '../i18n';
import NotificationsPage from './NotificationsPage';
import { renderWithAuth } from '../test/render';
import type { NotificationItem, NotificationList } from '../api/notifications';

const navigate = vi.fn();
vi.mock('react-router-dom', async (orig) => ({
  ...(await orig<typeof import('react-router-dom')>()),
  useNavigate: () => navigate,
}));

const markReadMutate = vi.fn();
const markAllMutate = vi.fn();
const fetchNextPage = vi.fn();
vi.mock('../api/notifications', () => ({
  useInfiniteNotifications: vi.fn(),
  useMarkNotificationRead: vi.fn(() => ({ mutate: markReadMutate })),
  useMarkAllNotificationsRead: vi.fn(() => ({ mutate: markAllMutate, isPending: false })),
}));
import { useInfiniteNotifications } from '../api/notifications';
const mockInfinite = useInfiniteNotifications as unknown as Mock;

const ITEMS: NotificationItem[] = [
  {
    id: 'n1', titleEn: 'Agenda published', titleAr: 'تم نشر جدول الأعمال',
    bodyEn: 'The agenda for MTG-2026-019 is published.', bodyAr: 'النص',
    category: 'AgendaPublished', deepLink: '/meetings/MTG-2026-019', isRead: false, createdAt: '2026-06-27T08:00:00Z',
  },
  {
    id: 'n2', titleEn: 'Meeting scheduled', titleAr: 'تم جدولة اجتماع',
    bodyEn: 'A new meeting is scheduled.', bodyAr: 'النص', category: 'MeetingScheduled',
    deepLink: null, isRead: true, createdAt: '2026-06-26T08:00:00Z',
  },
];

function page(over: Partial<NotificationList> = {}, q: Record<string, unknown> = {}) {
  const list: NotificationList = { items: ITEMS, unreadCount: 1, total: ITEMS.length, hasMore: false, ...over };
  mockInfinite.mockReturnValue({
    data: { pages: [list] },
    isLoading: false, isError: false,
    hasNextPage: false, isFetchingNextPage: false,
    fetchNextPage, refetch: vi.fn(), ...q,
  });
}

describe('NotificationsPage (#79 — ACMP.dc.html L706–739)', () => {
  beforeEach(() => {
    mockInfinite.mockReset();
    [navigate, markReadMutate, markAllMutate, fetchNextPage].forEach((m) => m.mockReset());
  });
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('renders the loading state while the first page is fetching', () => {
    mockInfinite.mockReturnValue({ data: undefined, isLoading: true, isError: false, refetch: vi.fn() });
    const { container } = renderWithAuth(<NotificationsPage />);
    expect(container.querySelector('.skeleton, .spinner, [role="status"]')).toBeTruthy();
  });

  it('renders the error state with a retry that refetches', async () => {
    const refetch = vi.fn();
    mockInfinite.mockReturnValue({ data: undefined, isLoading: false, isError: true, refetch });
    const user = userEvent.setup();
    renderWithAuth(<NotificationsPage />);
    await user.click(screen.getByRole('button', { name: /retry/i }));
    expect(refetch).toHaveBeenCalled();
  });

  it('renders Arabic body content when the locale is AR', async () => {
    await i18n.changeLanguage('ar');
    page({ items: [{ ...ITEMS[0], bodyAr: 'تم نشر جدول الأعمال للاجتماع.' }] });
    renderWithAuth(<NotificationsPage />);
    expect(screen.getByText('تم نشر جدول الأعمال للاجتماع.')).toBeInTheDocument();
  });

  it('shows the heading, the channel line, and the Unread tab count; lists the unread body', () => {
    page();
    renderWithAuth(<NotificationsPage />);
    expect(screen.getByRole('heading', { name: 'Notifications' })).toBeInTheDocument();
    expect(screen.getByText('In-app')).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /Unread/ })).toHaveTextContent('1');
    // Default = Unread → the unread body shows; the read item is hidden.
    expect(screen.getByText('The agenda for MTG-2026-019 is published.')).toBeInTheDocument();
    expect(screen.queryByText('A new meeting is scheduled.')).not.toBeInTheDocument();
  });

  it('marks an unread item read and follows its deep link when the key is clicked', async () => {
    page();
    const user = userEvent.setup();
    renderWithAuth(<NotificationsPage />);
    await user.click(screen.getByRole('button', { name: 'MTG-2026-019' }));
    expect(markReadMutate).toHaveBeenCalledWith('n1');
    expect(navigate).toHaveBeenCalledWith('/meetings/MTG-2026-019');
  });

  it('mark-all is disabled with zero unread and fires the command otherwise', async () => {
    page({ unreadCount: 0, items: [{ ...ITEMS[1] }] });
    const { unmount } = renderWithAuth(<NotificationsPage />);
    expect(screen.getByRole('button', { name: /Mark all read/ })).toBeDisabled();
    unmount();

    page();
    const user = userEvent.setup();
    renderWithAuth(<NotificationsPage />);
    await user.click(screen.getByRole('button', { name: /Mark all read/ }));
    expect(markAllMutate).toHaveBeenCalled();
  });

  it('the All tab reveals read items and shows the total count', async () => {
    page();
    const user = userEvent.setup();
    renderWithAuth(<NotificationsPage />);
    expect(screen.getByRole('tab', { name: /All/ })).toHaveTextContent('2');
    await user.click(screen.getByRole('tab', { name: /All/ }));
    expect(screen.getByText('A new meeting is scheduled.')).toBeInTheDocument();
  });

  it('shows Load more under All when more pages exist and fetches the next page', async () => {
    page({ hasMore: true }, { hasNextPage: true });
    const user = userEvent.setup();
    renderWithAuth(<NotificationsPage />);
    await user.click(screen.getByRole('tab', { name: /All/ }));
    await user.click(screen.getByRole('button', { name: 'Load more' }));
    expect(fetchNextPage).toHaveBeenCalled();
  });

  it('shows the no-unread empty state by default and the all-caught-up card under All', async () => {
    page({ items: [], unreadCount: 0, total: 0 });
    const user = userEvent.setup();
    renderWithAuth(<NotificationsPage />);
    expect(screen.getByText(/No unread notifications/i)).toBeInTheDocument();
    await user.click(screen.getByRole('tab', { name: /All/ }));
    expect(screen.getByText(/all caught up/i)).toBeInTheDocument();
  });

  it('is axe-clean (WCAG 2.2 AA)', async () => {
    page();
    const { container } = renderWithAuth(<NotificationsPage />);
    const results = await axe.run(container, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });
});
