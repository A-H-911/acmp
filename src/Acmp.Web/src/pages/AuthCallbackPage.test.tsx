import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { AuthCallbackPage } from './AuthCallbackPage';
import { AcmpAuthContext, type AcmpAuth } from '../auth/AcmpAuthContext';
import { makeAuth } from '../test/render';

/** Mount the callback at /auth/callback with visible /dashboard and /login targets. */
function renderCallback(auth: AcmpAuth) {
  return render(
    <MemoryRouter initialEntries={['/auth/callback']}>
      <AcmpAuthContext.Provider value={auth}>
        <Routes>
          <Route path="/auth/callback" element={<AuthCallbackPage />} />
          <Route path="/" element={<div>home</div>} />
          <Route path="/login" element={<div>login</div>} />
        </Routes>
      </AcmpAuthContext.Provider>
    </MemoryRouter>,
  );
}

describe('AuthCallbackPage', () => {
  it('shows a loading state while the code exchange is in flight', () => {
    renderCallback(makeAuth([], { isLoading: true, isAuthenticated: false }));
    expect(screen.getByRole('status')).toBeInTheDocument();
    expect(screen.queryByText('home')).not.toBeInTheDocument();
  });

  it('routes a resolved session onward to home', () => {
    renderCallback(makeAuth(['member']));
    expect(screen.getByText('home')).toBeInTheDocument();
  });

  it('sends a failed/unauthenticated callback back to login', () => {
    renderCallback(makeAuth([], { isAuthenticated: false }));
    expect(screen.getByText('login')).toBeInTheDocument();
  });

  it('surfaces a token-exchange error instead of redirecting', () => {
    renderCallback(makeAuth([], { isAuthenticated: false, error: 'invalid_grant' }));
    expect(screen.getByText('invalid_grant')).toBeInTheDocument();
    expect(screen.queryByText('login')).not.toBeInTheDocument();
    expect(screen.queryByText('dashboard')).not.toBeInTheDocument();
  });
});
