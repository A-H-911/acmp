import { describe, it, expect, vi, afterEach, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../i18n';
import { TopBar } from './TopBar';
import { renderWithAuth, makeAuth } from '../../test/render';

// renderWithAuth mounts a bare MemoryRouter with no Routes, so a navigation has nothing to render
// into and cannot be observed from the DOM. Spy on the hook instead and assert the URL itself —
// which is the contract that matters here anyway: /search reads its query from `q`.
const navigate = vi.hoisted(() => vi.fn());
vi.mock('react-router-dom', async (orig) => ({
  ...(await orig<typeof import('react-router-dom')>()),
  useNavigate: () => navigate,
}));

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
  navigate.mockClear();
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

/*
 * Every control in the top bar except the profile menu and Ctrl+K was wired to a handler no test
 * had ever invoked — search, the language toggle, the bell, and the two dismiss paths. This is the
 * app's persistent chrome: it is on every one of the 52 routes, so a dead control here is dead
 * everywhere at once.
 *
 * ⚠ It is worth recording that this file was carried for two sessions as "NOT a handler fix — the
 * DevRoleSwitcher coverage exclusion charges TopBar for a branch it hides". That was measured false
 * (PE-614): the lazy() declaration is covered and the {DevRoleSwitcher && …} call site has no
 * statement starting on it, so it never enters the line metric at all.
 */
describe('TopBar controls', () => {
  it('submits a search to /search, url-encoding the query', async () => {
    const user = userEvent.setup();
    renderWithAuth(<TopBar />);
    const input = screen.getByRole('searchbox');

    await user.type(input, 'auth & identity{Enter}');

    // The VALUE is asserted too: onChange is a separate handler from submit, and a box that never
    // updates would submit an empty query while looking fine.
    expect(input).toHaveValue('auth & identity');
    expect(navigate).toHaveBeenCalledWith('/search?q=auth%20%26%20identity');
  });

  it('does not navigate when the query is only whitespace', async () => {
    const user = userEvent.setup();
    renderWithAuth(<TopBar />);

    await user.type(screen.getByRole('searchbox'), '   {Enter}');

    // The guard is the point: a blank search must not push a route with an empty q.
    expect(navigate).not.toHaveBeenCalled();
  });

  it('switches the interface language from the chip', async () => {
    const user = userEvent.setup();
    renderWithAuth(<TopBar />);
    expect(i18n.language).toBe('en');

    // ⚠ The label interpolates the TARGET language's own endonym — "Switch to العربية", not
    // "Switch to Arabic". Matching the English word would pass only until someone read the string.
    await user.click(screen.getByRole('button', { name: /Switch to العربية/ }));

    expect(i18n.language).toBe('ar');
  });

  it('opens the notification panel from the bell and closes it with Escape', async () => {
    const user = userEvent.setup();
    renderWithAuth(<TopBar />);
    const bell = screen.getByRole('button', { name: /notification/i });
    expect(bell).toHaveAttribute('aria-expanded', 'false');

    await user.click(bell);
    expect(bell).toHaveAttribute('aria-expanded', 'true');
    const panel = screen.getByRole('dialog');

    // Dismissing is the panel's own onClose reaching back into TopBar's state — a separate handler
    // from the toggle. Escape is asserted because it is the keyboard route to the same onClose the
    // scrim uses, and it needs no test-only DOM query to reach.
    await user.keyboard('{Escape}');

    expect(panel).not.toBeInTheDocument();
    expect(bell).toHaveAttribute('aria-expanded', 'false');
  });

  it('closes the profile menu by clicking the backdrop', async () => {
    const user = userEvent.setup();
    const { container } = renderWithAuth(<TopBar />);

    await user.click(screen.getByRole('button', { name: /account|profile/i }));
    expect(screen.getByRole('menu')).toBeInTheDocument();

    const backdrop = container.querySelector('.profile-backdrop');
    expect(backdrop).not.toBeNull();
    await user.click(backdrop!);

    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });
});
