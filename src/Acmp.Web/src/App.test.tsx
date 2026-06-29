import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { RouterProvider, createMemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { appRoutes } from './App';
import { AcmpAuthContext, type AcmpAuth } from './auth/AcmpAuthContext';
import { makeAuth } from './test/render';
import { stubFetch } from './test/queryHarness';
import i18n from './i18n';

/*
 * Route-tree integration: the guards wired in App.tsx must actually redirect and
 * gate. Render the REAL appRoutes through a memory router. Role gating hides UI
 * only — the server is the real authority — but a non-admin must still not see the
 * admin screen here. fetch is stubbed so shell data hooks resolve to empty states.
 */
afterEach(() => vi.unstubAllGlobals());

function emptyApi() {
  return stubFetch((url) => {
    if (url.includes('/notifications')) {
      return { jsonBody: { items: [], unreadCount: 0, total: 0, hasMore: false } };
    }
    if (url.includes('/members')) return { jsonBody: [] };
    return { jsonBody: {} };
  });
}

function renderAt(path: string, auth: AcmpAuth) {
  emptyApi();
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(appRoutes, { initialEntries: [path] });
  const utils = render(
    <QueryClientProvider client={client}>
      <AcmpAuthContext.Provider value={auth}>
        <RouterProvider router={router} />
      </AcmpAuthContext.Provider>
    </QueryClientProvider>,
  );
  return { ...utils, router };
}

describe('appRoutes guards', () => {
  // Note: the unauthenticated → /login redirect is asserted in ProtectedRoute.test.tsx
  // (declarative <Routes>, where <Navigate> works). Triggering it through a data router
  // here hits a jsdom+undici AbortSignal brand-check bug on client-side navigation, so
  // the App-level integration sticks to non-redirecting outcomes.

  it('renders a protected route inside the shell for an authenticated member', async () => {
    renderAt('/dashboard', makeAuth(['member']));
    expect(await screen.findByText(i18n.t('dashboard.title'))).toBeInTheDocument();
    expect(screen.getByRole('navigation')).toBeInTheDocument();
  });

  it('denies the admin area to a non-admin (role gate)', async () => {
    renderAt('/admin', makeAuth(['member']));
    expect(await screen.findByText(i18n.t('state.deniedTitle'))).toBeInTheDocument();
  });

  it('lets an administrator into the admin area', async () => {
    renderAt('/admin', makeAuth(['administrator']));
    // role gate did NOT block — the actual admin screen (its page heading) rendered in the shell.
    // ("Administration" also appears as a nav link, so query the heading specifically.)
    expect(await screen.findByRole('heading', { name: i18n.t('admin.title') })).toBeInTheDocument();
    expect(screen.queryByText(i18n.t('state.deniedTitle'))).not.toBeInTheDocument();
  });

  it('shows the 404 page for an unknown route inside the shell', async () => {
    renderAt('/totally-unknown', makeAuth(['member']));
    expect(await screen.findByText('404')).toBeInTheDocument();
  });
});
