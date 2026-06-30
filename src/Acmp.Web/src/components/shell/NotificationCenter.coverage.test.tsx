import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
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
  useMarkAllNotificationsRead: vi.fn(() => ({ mutate: vi.fn(), isPending: false })),
}));
import { useNotifications } from '../../api/notifications';
const mockNotifs = useNotifications as unknown as Mock;

// Covers the loading skeleton and error states the main suite skips.
describe('NotificationCenter loading + error states', () => {
  beforeEach(() => {
    mockNotifs.mockReset();
    navigate.mockReset();
  });

  it('shows the shimmer skeleton while the feed is fetching', () => {
    mockNotifs.mockReturnValue({ isLoading: true, isError: false, data: undefined });
    const { container } = renderWithAuth(<NotificationCenter open onClose={vi.fn()} />);
    expect(container.querySelectorAll('.notif-skeleton-row').length).toBeGreaterThan(0);
  });

  it('shows the error status when the feed fails', () => {
    mockNotifs.mockReturnValue({ isLoading: false, isError: true, data: undefined });
    renderWithAuth(<NotificationCenter open onClose={vi.fn()} />);
    expect(screen.getByText(i18n.t('notif.error'))).toBeInTheDocument();
  });
});
