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
vi.mock('../../api/notifications', () => ({
  useNotifications: vi.fn(),
  useMarkNotificationRead: vi.fn(() => ({ mutate: markMutate })),
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
  // The popover only reads items + unreadCount; fill the paging fields so the mock satisfies the
  // NotificationList type without every call site repeating total/hasMore.
  const data = over.data
    ? { items: [], unreadCount: 0, total: 0, hasMore: false, ...over.data }
    : undefined;
  mockNotifs.mockReturnValue({ isLoading: false, isError: false, ...over, data });
}

describe('NotificationCenter (P6e)', () => {
  beforeEach(() => {
    mockNotifs.mockReset();
    navigate.mockReset();
    markMutate.mockReset();
  });
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('renders nothing when closed', () => {
    feed({ data: { items: ITEMS, unreadCount: 1 } });
    const { container } = renderWithAuth(<NotificationCenter open={false} onClose={vi.fn()} />);
    expect(container.firstChild).toBeNull();
  });

  it('lists the live feed with the unread count and marks unread items visually', () => {
    feed({ data: { items: ITEMS, unreadCount: 1 } });
    renderWithAuth(<NotificationCenter open onClose={vi.fn()} />);
    expect(screen.getByText('Agenda published')).toBeTruthy();
    expect(screen.getByText('1 unread')).toBeTruthy();
    // The unread item carries the `unread` class; the read one does not.
    const unread = screen.getByRole('button', { name: /agenda published/i });
    expect(unread.className).toContain('unread');
    const read = screen.getByRole('button', { name: /meeting scheduled/i });
    expect(read.className).not.toContain('unread');
  });

  it('marks an unread item read, closes, and follows its deep link on click', async () => {
    const onClose = vi.fn();
    feed({ data: { items: ITEMS, unreadCount: 1 } });
    const user = userEvent.setup();
    renderWithAuth(<NotificationCenter open onClose={onClose} />);

    await user.click(screen.getByRole('button', { name: /agenda published/i }));

    expect(markMutate).toHaveBeenCalledWith('n1');
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(navigate).toHaveBeenCalledWith('/meetings/MTG-2026-019');
  });

  it('does not re-mark an already-read item and skips navigation when there is no deep link', async () => {
    const onClose = vi.fn();
    feed({ data: { items: ITEMS, unreadCount: 0 } });
    const user = userEvent.setup();
    renderWithAuth(<NotificationCenter open onClose={onClose} />);

    await user.click(screen.getByRole('button', { name: /meeting scheduled/i }));

    expect(markMutate).not.toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('keeps the calm empty state when the inbox is empty', () => {
    feed({ data: { items: [], unreadCount: 0 } });
    renderWithAuth(<NotificationCenter open onClose={vi.fn()} />);
    expect(screen.getByText(/all caught up/i)).toBeTruthy();
  });

  it('renders Arabic content when the locale is AR', async () => {
    await i18n.changeLanguage('ar');
    feed({ data: { items: ITEMS, unreadCount: 1 } });
    renderWithAuth(<NotificationCenter open onClose={vi.fn()} />);
    expect(screen.getByText('تم نشر جدول الأعمال')).toBeTruthy();
  });

  it('has no axe (WCAG 2.2 AA) violations', async () => {
    feed({ data: { items: ITEMS, unreadCount: 1 } });
    const { container } = renderWithAuth(<NotificationCenter open onClose={vi.fn()} />);
    const results = await axe.run(container, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag22aa'] } });
    expect(results.violations).toEqual([]);
  });
});
