import { type ReactElement } from 'react';
import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
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

/**
 * Render under a router + a fake auth context + a query client.
 *
 * ⚠ THE QueryClientProvider IS NOT OPTIONAL SCAFFOLDING. Screen tests mock their data hooks away, so
 * for a long time nothing here touched react-query and the omission was invisible. WBS-24.7 added a
 * mutation (`useCreateStream`) inside a component a screen test renders, and the missing provider
 * surfaced as a test that PASSED ALONE and FAILED IN THE FULL SUITE — the same shape `Backlog.test`
 * hit in WBS-24.2. Providing it here fixes the class rather than the one call site: a client that
 * nothing queries is inert, so tests that mock their hooks are unaffected.
 *
 * Retries are off so a failure-path test resolves immediately instead of waiting out a backoff.
 */
export function renderWithAuth(ui: ReactElement, { roles = ['secretary'], route = '/', auth }: Options = {}) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[route]}>
        <AcmpAuthContext.Provider value={auth ?? makeAuth(roles)}>{ui}</AcmpAuthContext.Provider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}
