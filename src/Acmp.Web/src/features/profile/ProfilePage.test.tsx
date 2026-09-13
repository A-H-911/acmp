import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import axe from 'axe-core';
import i18n from '../../i18n';
import { renderWithAuth, makeAuth } from '../../test/render';
import { ProfilePage } from './ProfilePage';

// renderWithAuth mounts a bare MemoryRouter with no Routes, so assert the navigation call itself.
const navigate = vi.hoisted(() => vi.fn());
vi.mock('react-router-dom', async (orig) => ({
  ...(await orig<typeof import('react-router-dom')>()),
  useNavigate: () => navigate,
}));

beforeEach(() => {
  localStorage.clear();
  document.documentElement.removeAttribute('data-theme');
});

afterEach(async () => {
  navigate.mockClear();
  await i18n.changeLanguage('en');
});

const auth = (over = {}) =>
  makeAuth(['member', 'chairman'], { displayName: 'Omar Hassan', initials: 'OH', email: 'omar.h@acmp.test', ...over });

describe('ProfilePage — identity', () => {
  it('shows avatar initials, name, email and the held roles in precedence order', () => {
    renderWithAuth(<ProfilePage />, { auth: auth() });

    expect(screen.getByRole('heading', { level: 1, name: 'Omar Hassan' })).toBeInTheDocument();
    expect(screen.getByText('OH')).toBeInTheDocument();
    expect(screen.getByText('omar.h@acmp.test')).toBeInTheDocument();
    // Claim order was member, chairman; precedence puts Chairman first (DEF-034).
    const tags = screen.getAllByText(/^(Chairman|Member)$/).map((el) => el.textContent);
    expect(tags).toEqual(['Chairman', 'Member']);
  });

  it('omits the email line and role tags when the session carries neither', () => {
    const { container } = renderWithAuth(<ProfilePage />, { auth: auth({ email: undefined, roles: [] }) });
    expect(container.querySelector('.prof-email')).toBeNull();
    expect(container.querySelector('.prof-tags')).toBeNull();
  });

  it('is axe-clean (WCAG 2.2 AA structure/ARIA)', async () => {
    const { container } = renderWithAuth(<ProfilePage />, { auth: auth() });
    const r = await axe.run(container, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(r.violations.map((v) => v.id)).toEqual([]);
  });
});

describe('ProfilePage — preferences', () => {
  it('switches the interface language from the Language control', async () => {
    const user = userEvent.setup();
    renderWithAuth(<ProfilePage />, { auth: auth() });
    expect(screen.getByRole('group', { name: 'Language' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'English' })).toHaveAttribute('aria-pressed', 'true');

    await user.click(screen.getByRole('button', { name: 'العربية' }));

    expect(i18n.language).toBe('ar');
    // The page re-renders in Arabic, and the control now marks Arabic as chosen.
    expect(screen.getByRole('button', { name: 'العربية' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('heading', { level: 2, name: 'التفضيلات' })).toBeInTheDocument();
  });

  it('sets the theme from the Theme control, persisting the explicit choice', async () => {
    const user = userEvent.setup();
    renderWithAuth(<ProfilePage />, { auth: auth() });
    expect(screen.getByRole('button', { name: 'Light' })).toHaveAttribute('aria-pressed', 'true');

    await user.click(screen.getByRole('button', { name: 'Dark' }));

    expect(screen.getByRole('button', { name: 'Dark' })).toHaveAttribute('aria-pressed', 'true');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    expect(localStorage.getItem('acmp-theme')).toBe('dark');
  });
});

describe('ProfilePage — account', () => {
  it('shows the SSO note and opens notification preferences', async () => {
    const user = userEvent.setup();
    renderWithAuth(<ProfilePage />, { auth: auth() });
    expect(screen.getByText('Sign-in is managed by the central identity service.')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Notification preferences' }));

    expect(navigate).toHaveBeenCalledWith('/profile/preferences');
  });

  it('logs out through the same signOut the TopBar uses', async () => {
    const user = userEvent.setup();
    const signOut = vi.fn();
    renderWithAuth(<ProfilePage />, { auth: auth({ signOut }) });

    await user.click(screen.getByRole('button', { name: 'Log out' }));

    expect(signOut).toHaveBeenCalledTimes(1);
  });
});
