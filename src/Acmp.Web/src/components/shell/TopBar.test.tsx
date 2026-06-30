import { describe, it, expect, vi, afterEach, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../i18n';
import { TopBar } from './TopBar';
import { renderWithAuth, makeAuth } from '../../test/render';

// TopBar reads the unread count for the bell badge; mock the feed (renderWithAuth has no
// QueryClientProvider). NotificationCenter's hooks are mocked too (it mounts when the panel opens).
vi.mock('../../api/notifications', () => ({
  useNotifications: vi.fn(() => ({ data: { items: [], unreadCount: 0 } })),
  useMarkNotificationRead: vi.fn(() => ({ mutate: vi.fn() })),
  useMarkAllNotificationsRead: vi.fn(() => ({ mutate: vi.fn(), isPending: false })),
}));
import { useNotifications } from '../../api/notifications';
const mockNotifs = useNotifications as unknown as Mock;

afterEach(async () => {
  await i18n.changeLanguage('en');
  mockNotifs.mockReturnValue({ data: { items: [], unreadCount: 0 } });
});

describe('TopBar notification bell', () => {
  it('shows the unread badge with the count only when there are unread notifications', () => {
    mockNotifs.mockReturnValue({ data: { items: [], unreadCount: 3 } });
    renderWithAuth(<TopBar />);
    expect(screen.getByLabelText(/3 unread/i)).toBeTruthy();
    expect(screen.getByText('3')).toBeTruthy();
  });

  it('shows no badge when the inbox is fully read', () => {
    mockNotifs.mockReturnValue({ data: { items: [], unreadCount: 0 } });
    renderWithAuth(<TopBar />);
    expect(screen.getByLabelText('Notifications')).toBeTruthy();
    expect(screen.queryByText('3')).toBeNull();
  });
});

describe('TopBar global search', () => {
  it('renders the keyboard hint and focuses search on Ctrl+K', async () => {
    const user = userEvent.setup();
    renderWithAuth(<TopBar />);
    expect(screen.getByText('Ctrl K')).toBeTruthy();
    const input = screen.getByRole('searchbox');
    expect(input).not.toHaveFocus();
    await user.keyboard('{Control>}k{/Control}');
    expect(input).toHaveFocus();
  });
});

describe('TopBar profile menu', () => {
  it('hides Log out until the menu opens, then activates sign-out by keyboard', async () => {
    const user = userEvent.setup();
    const signOut = vi.fn();
    renderWithAuth(<TopBar />, { auth: makeAuth(['secretary'], { signOut }) });

    // Collapsed by default.
    expect(screen.queryByRole('menuitem', { name: /log out/i })).toBeNull();

    // Open the profile menu.
    await user.click(screen.getByRole('button', { name: /account menu/i }));
    const logout = screen.getByRole('menuitem', { name: /log out/i });

    // Keyboard-activatable (native button): focus + Enter triggers the OIDC sign-out.
    logout.focus();
    await user.keyboard('{Enter}');
    expect(signOut).toHaveBeenCalledTimes(1);
  });

  it('renders the Arabic Log out label when the locale is AR', async () => {
    await i18n.changeLanguage('ar');
    const user = userEvent.setup();
    renderWithAuth(<TopBar />, { auth: makeAuth(['secretary']) });

    await user.click(screen.getByRole('button', { name: /قائمة الحساب/ }));
    expect(screen.getByRole('menuitem', { name: /تسجيل الخروج/ })).toBeTruthy();
  });
});
