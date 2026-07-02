import { describe, it, expect, vi, beforeEach, afterEach, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import axe from 'axe-core';
import i18n from '../../i18n';
import { NotificationCenter } from './NotificationCenter';
import { renderWithAuth } from '../../test/render';
import type { NotificationItem, NotificationList } from '../../api/notifications';

const navigate = vi.fn();
vi.mock('react-router-dom', async (orig) => ({
  ...(await orig<typeof import('react-router-dom')>()),
  useNavigate: () => navigate,
}));

const markMutate = vi.fn();
const markAllMutate = vi.fn();
vi.mock('../../api/notifications', () => ({
  useNotifications: vi.fn(),
  useMarkNotificationRead: vi.fn(() => ({ mutate: markMutate })),
  useMarkAllNotificationsRead: vi.fn(() => ({ mutate: markAllMutate, isPending: false })),
}));
import { useNotifications } from '../../api/notifications';
const mockNotifs = useNotifications as unknown as Mock;

const ITEMS: NotificationItem[] = [
  {
    id: 'n1', titleEn: 'Agenda published', titleAr: 'تم نشر جدول الأعمال',
    bodyEn: 'The agenda for MTG-2026-019 is published.', bodyAr: 'تم نشر جدول أعمال MTG-2026-019.',
    category: 'AgendaPublished', deepLink: '/meetings/MTG-2026-019', isRead: false, createdAt: '2026-06-27T08:00:00Z',
  },
  {
    id: 'n2', titleEn: 'Meeting scheduled', titleAr: 'تم جدولة اجتماع',
    bodyEn: 'A new meeting is scheduled.', bodyAr: 'تمت جدولة اجتماع جديد.',
    category: 'MeetingScheduled', deepLink: null, isRead: true, createdAt: '2026-06-26T08:00:00Z',
  },
];

function feed(over: { data?: Partial<NotificationList>; isLoading?: boolean; isError?: boolean }) {
  const data = over.data
    ? { items: [], unreadCount: 0, total: 0, hasMore: false, ...over.data }
    : undefined;
  mockNotifs.mockReturnValue({ isLoading: false, isError: false, ...over, data });
}

describe('NotificationCenter (P6b — ACMP.dc.html L92–131)', () => {
  beforeEach(() => {
    mockNotifs.mockReset();
    navigate.mockReset();
    markMutate.mockReset();
    markAllMutate.mockReset();
  });
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('renders nothing when closed', () => {
    feed({ data: { items: ITEMS, unreadCount: 1 } });
    const { container } = renderWithAuth(<NotificationCenter open={false} onClose={vi.fn()} />);
    expect(container.firstChild).toBeNull();
  });

  it('shows the unread count pill and lists unread rows by their artifact key', () => {
    feed({ data: { items: ITEMS, unreadCount: 1 } });
    renderWithAuth(<NotificationCenter open onClose={vi.fn()} />);
    // {n} new pill in the header.
    expect(screen.getByText('1 new')).toBeTruthy();
    // Default tab = Unread → only the unread row (its derived key) shows; the read one is hidden.
    expect(screen.getByRole('button', { name: 'MTG-2026-019' })).toBeTruthy();
    expect(screen.queryByText(/A new meeting is scheduled/i)).toBeNull();
  });

  it('the All tab reveals read items too', async () => {
    feed({ data: { items: ITEMS, unreadCount: 1 } });
    const user = userEvent.setup();
    renderWithAuth(<NotificationCenter open onClose={vi.fn()} />);
    await user.click(screen.getByRole('button', { name: /All/ }));
    expect(screen.getByText('A new meeting is scheduled.')).toBeTruthy();
  });

  it('marks read, closes, and follows the deep link when the key is clicked', async () => {
    const onClose = vi.fn();
    feed({ data: { items: ITEMS, unreadCount: 1 } });
    const user = userEvent.setup();
    renderWithAuth(<NotificationCenter open onClose={onClose} />);

    await user.click(screen.getByRole('button', { name: 'MTG-2026-019' }));

    expect(markMutate).toHaveBeenCalledWith('n1');
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(navigate).toHaveBeenCalledWith('/meetings/MTG-2026-019');
  });

  it('the per-row dot marks read without navigating or closing', async () => {
    const onClose = vi.fn();
    feed({ data: { items: ITEMS, unreadCount: 1 } });
    const user = userEvent.setup();
    renderWithAuth(<NotificationCenter open onClose={onClose} />);

    await user.click(screen.getByRole('button', { name: i18n.t('notif.markRead') }));

    expect(markMutate).toHaveBeenCalledWith('n1');
    expect(navigate).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('mark-all fires the command and is disabled with zero unread', async () => {
    feed({ data: { items: ITEMS, unreadCount: 1 } });
    const user = userEvent.setup();
    const { unmount } = renderWithAuth(<NotificationCenter open onClose={vi.fn()} />);
    await user.click(screen.getByRole('button', { name: i18n.t('notif.markAll') }));
    expect(markAllMutate).toHaveBeenCalled();
    unmount();

    feed({ data: { items: ITEMS, unreadCount: 0 } });
    renderWithAuth(<NotificationCenter open onClose={vi.fn()} />);
    expect(screen.getByRole('button', { name: i18n.t('notif.markAll') })).toBeDisabled();
  });

  it('keeps the calm empty state when there are no unread items', () => {
    feed({ data: { items: [], unreadCount: 0 } });
    renderWithAuth(<NotificationCenter open onClose={vi.fn()} />);
    expect(screen.getByText(i18n.t('notif.noUnreadTitle'))).toBeTruthy();
  });

  it('a scrim click and "View all" both close; View all navigates to the page', async () => {
    const onClose = vi.fn();
    feed({ data: { items: [], unreadCount: 0 } });
    const user = userEvent.setup();
    renderWithAuth(<NotificationCenter open onClose={onClose} />);
    await user.click(screen.getByRole('button', { name: i18n.t('notif.viewAll') }));
    expect(onClose).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith('/notifications');
  });

  it('renders Arabic body content when the locale is AR', async () => {
    await i18n.changeLanguage('ar');
    feed({ data: { items: ITEMS, unreadCount: 1 } });
    renderWithAuth(<NotificationCenter open onClose={vi.fn()} />);
    expect(screen.getByText('تم نشر جدول أعمال MTG-2026-019.')).toBeTruthy();
  });

  it('has no axe (WCAG 2.2 AA) violations', async () => {
    feed({ data: { items: ITEMS, unreadCount: 1 } });
    const { container } = renderWithAuth(<NotificationCenter open onClose={vi.fn()} />);
    const results = await axe.run(container, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag22aa'] } });
    expect(results.violations).toEqual([]);
  });
});
