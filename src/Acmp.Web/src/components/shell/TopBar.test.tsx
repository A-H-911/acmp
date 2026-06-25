import { describe, it, expect, vi } from 'vitest';
import { screen, fireEvent } from '@testing-library/react';
import { TopBar } from './TopBar';
import { renderWithAuth, makeAuth } from '../../test/render';

describe('TopBar', () => {
  it('hides Sign out until the account menu is opened, then calls signOut', () => {
    const signOut = vi.fn();
    renderWithAuth(<TopBar />, { auth: makeAuth(['secretary'], { signOut }) });

    // Collapsed by default — no menu item visible.
    expect(screen.queryByRole('menuitem', { name: /sign out|تسجيل الخروج/i })).toBeNull();

    // Open the identity dropdown.
    fireEvent.click(screen.getByRole('button', { name: /account menu|قائمة الحساب/i }));

    const signOutItem = screen.getByRole('menuitem', { name: /sign out|تسجيل الخروج/i });
    fireEvent.click(signOutItem);
    expect(signOut).toHaveBeenCalledTimes(1);
  });
});
