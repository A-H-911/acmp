import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../i18n';
import { NotificationCenter } from './NotificationCenter';
import { renderWithAuth } from '../../test/render';

const navigate = vi.fn();
vi.mock('react-router-dom', async (orig) => ({
  ...(await orig<typeof import('react-router-dom')>()),
  useNavigate: () => navigate,
}));

vi.mock('../../api/notifications', () => ({
  useNotifications: vi.fn(),
  useMarkNotificationRead: vi.fn(() => ({ mutate: vi.fn() })),
}));
import { useNotifications } from '../../api/notifications';
const mockNotifs = useNotifications as unknown as Mock;

// Covers the loading / error states and the "see all" navigation the main suite skips.
describe('NotificationCenter states + see-all', () => {
  beforeEach(() => {
    mockNotifs.mockReset();
    navigate.mockReset();
  });

  it('shows the loading status while the feed is fetching', () => {
    mockNotifs.mockReturnValue({ isLoading: true, isError: false, data: undefined });
    renderWithAuth(<NotificationCenter open onClose={vi.fn()} />);
    expect(screen.getByText(i18n.t('notif.loading'))).toBeInTheDocument();
  });

  it('shows the error status when the feed fails', () => {
    mockNotifs.mockReturnValue({ isLoading: false, isError: true, data: undefined });
    renderWithAuth(<NotificationCenter open onClose={vi.fn()} />);
    expect(screen.getByText(i18n.t('notif.error'))).toBeInTheDocument();
  });

  it('closes the panel and navigates to the full feed on "see all"', async () => {
    mockNotifs.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items: [], unreadCount: 0, total: 0, hasMore: false },
    });
    const onClose = vi.fn();
    renderWithAuth(<NotificationCenter open onClose={onClose} />);
    await userEvent.click(screen.getByRole('button', { name: i18n.t('notif.seeAll') }));
    expect(onClose).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith('/notifications');
  });
});
