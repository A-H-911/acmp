import { describe, it, expect, vi } from 'vitest';
import { screen, fireEvent } from '@testing-library/react';
import { TopBar } from './TopBar';
import { renderWithAuth, makeAuth } from '../../test/render';

describe('TopBar', () => {
  it('renders a sign-out control wired to signOut', () => {
    const signOut = vi.fn();
    renderWithAuth(<TopBar />, { auth: makeAuth(['secretary'], { signOut }) });

    const btn = screen.getByRole('button', { name: /sign out|تسجيل الخروج/i });
    fireEvent.click(btn);

    expect(signOut).toHaveBeenCalledTimes(1);
  });
});
