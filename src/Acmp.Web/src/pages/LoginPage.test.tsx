import { describe, it, expect, afterEach, vi } from 'vitest';
import { readFileSync } from 'node:fs';
import { screen, fireEvent } from '@testing-library/react';
import { LoginPage } from './LoginPage';
import { setAuthStatus } from '../auth/authStatus';
import { renderWithAuth, makeAuth } from '../test/render';
import i18n from '../i18n';

describe('LoginPage', () => {
  afterEach(() => {
    sessionStorage.clear();
    void i18n.changeLanguage('en');
  });

  it('renders unauthenticated; the Log in button starts the OIDC sign-in redirect', () => {
    const signIn = vi.fn();
    renderWithAuth(<LoginPage />, { auth: makeAuth([], { isAuthenticated: false, signIn }) });

    const btn = screen.getByRole('button', { name: /log in|تسجيل الدخول/i });
    fireEvent.click(btn);
    expect(signIn).toHaveBeenCalledTimes(1);
  });

  it('shows an info banner after a deliberate sign-out', () => {
    setAuthStatus('signed_out');
    renderWithAuth(<LoginPage />, { auth: makeAuth([], { isAuthenticated: false }) });
    expect(screen.getByText(i18n.t('auth.signedOut'))).toBeInTheDocument();
    expect(document.querySelector('.login-status-info')).toBeInTheDocument();
  });

  it('shows a warning banner after the session expired', () => {
    setAuthStatus('session_expired');
    renderWithAuth(<LoginPage />, { auth: makeAuth([], { isAuthenticated: false }) });
    expect(screen.getByText(i18n.t('auth.sessionExpired'))).toBeInTheDocument();
    expect(document.querySelector('.login-status-warn')).toBeInTheDocument();
  });

  it('shows no status banner on a first, clean visit', () => {
    renderWithAuth(<LoginPage />, { auth: makeAuth([], { isAuthenticated: false }) });
    expect(document.querySelector('.login-status')).not.toBeInTheDocument();
  });

  it('ships the ACMP logo/favicon asset and references it from index.html', () => {
    expect(() => readFileSync('public/favicon.svg')).not.toThrow();
    expect(readFileSync('index.html', 'utf8')).toMatch(/favicon\.svg/);
  });
});
