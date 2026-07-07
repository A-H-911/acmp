import { describe, it, expect, afterEach, vi } from 'vitest';
import { readFileSync } from 'node:fs';
import { render, screen, fireEvent } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { LoginPage } from './LoginPage';
import { setAuthStatus } from '../auth/authStatus';
import { AcmpAuthContext } from '../auth/AcmpAuthContext';
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

  it('forwards the deep-link path (router state.from) to signIn so login returns there', () => {
    const signIn = vi.fn();
    render(
      <MemoryRouter initialEntries={[{ pathname: '/login', state: { from: '/meetings/new' } }]}>
        <AcmpAuthContext.Provider value={makeAuth([], { isAuthenticated: false, signIn })}>
          <LoginPage />
        </AcmpAuthContext.Provider>
      </MemoryRouter>,
    );

    fireEvent.click(screen.getByRole('button', { name: /log in|تسجيل الدخول/i }));
    expect(signIn).toHaveBeenCalledWith('/meetings/new');
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
