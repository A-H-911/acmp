import { describe, it, expect, vi, afterEach } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../i18n';
import { TopBar } from './TopBar';
import { renderWithAuth, makeAuth } from '../../test/render';

afterEach(async () => { await i18n.changeLanguage('en'); });

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
