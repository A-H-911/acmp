import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { ProtectedRoute, RequireRole } from './ProtectedRoute';
import { AcmpAuthContext, type AcmpAuth } from './AcmpAuthContext';
import { renderWithAuth, makeAuth } from '../test/render';

/** Render ProtectedRoute as a layout route over a protected child + a visible /login. */
function renderGuard(auth: AcmpAuth) {
  return render(
    <MemoryRouter initialEntries={['/secret']}>
      <AcmpAuthContext.Provider value={auth}>
        <Routes>
          <Route element={<ProtectedRoute />}>
            <Route path="/secret" element={<div>protected content</div>} />
          </Route>
          <Route path="/login" element={<div>login screen</div>} />
        </Routes>
      </AcmpAuthContext.Provider>
    </MemoryRouter>,
  );
}

describe('ProtectedRoute', () => {
  it('renders the protected outlet for an authenticated session', () => {
    renderGuard(makeAuth(['member']));
    expect(screen.getByText('protected content')).toBeInTheDocument();
  });

  it('redirects an unauthenticated visitor to /login', () => {
    renderGuard(makeAuth([], { isAuthenticated: false }));
    expect(screen.queryByText('protected content')).not.toBeInTheDocument();
    expect(screen.getByText('login screen')).toBeInTheDocument();
  });

  it('shows a loading state while the session resolves (no premature redirect)', () => {
    renderGuard(makeAuth([], { isLoading: true, isAuthenticated: false }));
    expect(screen.queryByText('login screen')).not.toBeInTheDocument();
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('surfaces an auth error instead of bouncing to login', () => {
    renderGuard(makeAuth([], { isAuthenticated: false, error: 'Identity provider is not configured.' }));
    expect(screen.queryByText('login screen')).not.toBeInTheDocument();
    expect(screen.getByText('Identity provider is not configured.')).toBeInTheDocument();
  });
});

describe('RequireRole', () => {
  it('renders children when the user holds a required role', () => {
    renderWithAuth(<RequireRole roles={['administrator']}>secret admin area</RequireRole>, {
      roles: ['administrator'],
    });
    expect(screen.getByText('secret admin area')).toBeInTheDocument();
  });

  it('renders a 403 state when the user lacks the role', () => {
    renderWithAuth(<RequireRole roles={['administrator']}>secret admin area</RequireRole>, {
      roles: ['member'],
    });
    expect(screen.queryByText('secret admin area')).not.toBeInTheDocument();
    expect(screen.getByText('Access denied')).toBeInTheDocument();
  });
});
