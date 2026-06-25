import { describe, it, expect, vi } from 'vitest';
import { readFileSync } from 'node:fs';
import { screen, fireEvent } from '@testing-library/react';
import { LoginPage } from './LoginPage';
import { renderWithAuth, makeAuth } from '../test/render';

describe('LoginPage', () => {
  it('renders unauthenticated; the Log in button starts the OIDC sign-in redirect', () => {
    const signIn = vi.fn();
    renderWithAuth(<LoginPage />, { auth: makeAuth([], { isAuthenticated: false, signIn }) });

    const btn = screen.getByRole('button', { name: /log in|تسجيل الدخول/i });
    fireEvent.click(btn);
    expect(signIn).toHaveBeenCalledTimes(1);
  });

  it('ships the ACMP logo/favicon asset and references it from index.html', () => {
    expect(() => readFileSync('public/favicon.svg')).not.toThrow();
    expect(readFileSync('index.html', 'utf8')).toMatch(/favicon\.svg/);
  });
});
