import { type ReactElement } from 'react';
import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AcmpAuthContext, type AcmpAuth } from '../auth/AcmpAuthContext';
import type { CommitteeRole } from '../auth/roles';

/** Build a fake authenticated session for tests (bypasses OIDC). */
export function makeAuth(roles: CommitteeRole[], over: Partial<AcmpAuth> = {}): AcmpAuth {
  return {
    isLoading: false,
    isAuthenticated: true,
    roles,
    displayName: 'Test User',
    initials: 'TU',
    signIn: () => {},
    signOut: () => {},
    ...over,
  };
}

interface Options {
  roles?: CommitteeRole[];
  route?: string;
  auth?: AcmpAuth;
}

/** Render under a router + a fake auth context. */
export function renderWithAuth(ui: ReactElement, { roles = ['secretary'], route = '/', auth }: Options = {}) {
  return render(
    <MemoryRouter initialEntries={[route]}>
      <AcmpAuthContext.Provider value={auth ?? makeAuth(roles)}>{ui}</AcmpAuthContext.Provider>
    </MemoryRouter>,
  );
}
