import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AuthProvider } from './AuthProvider';
import { useAuth } from './AcmpAuthContext';
import { setTokenGetter } from '../api/apiClient';
import { stubFetch } from '../test/queryHarness';

/*
 * AuthProvider picks one of three backends. We mock react-oidc-context (the real
 * IdP) and toggle oidcEnabled so each branch is exercised without a live Keycloak:
 *   - configured        → OidcBridge maps claims → roles, provisions on login
 *   - unconfigured + DEV → in-memory stub (never in a prod bundle)
 *   - unconfigured + PROD→ fail closed (guardrail 4: a stub must not mint roles)
 */

// Controllable oidcEnabled via a live getter on the mocked module namespace.
let mockOidcEnabled = false;
vi.mock('./authConfig', () => ({
  get oidcEnabled() {
    return mockOidcEnabled;
  },
  oidcConfig: {},
}));

interface FakeOidc {
  isLoading: boolean;
  isAuthenticated: boolean;
  user?: { access_token?: string; profile?: unknown };
  error?: { message: string };
  events: {
    addAccessTokenExpired: (cb: () => void) => void;
    removeAccessTokenExpired: (cb: () => void) => void;
    addSilentRenewError: (cb: () => void) => void;
    removeSilentRenewError: (cb: () => void) => void;
  };
  signinRedirect: () => void;
  signoutRedirect: () => void;
}

let oidc: FakeOidc;
const expiredHandlers: Array<() => void> = [];

vi.mock('react-oidc-context', () => ({
  AuthProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useAuth: () => oidc,
}));

function Probe() {
  const a = useAuth();
  return (
    <>
      <span data-testid="auth">{String(a.isAuthenticated)}</span>
      <span data-testid="roles">{[...a.roles].sort().join(',')}</span>
      <span data-testid="name">{a.displayName}</span>
      <span data-testid="initials">{a.initials}</span>
      <span data-testid="err">{a.error ?? ''}</span>
      <button onClick={a.signOut}>sign out</button>
      {a.devSetRoles && <button onClick={() => a.devSetRoles!(['auditor'])}>become auditor</button>}
    </>
  );
}

beforeEach(() => {
  mockOidcEnabled = false;
  expiredHandlers.length = 0;
  sessionStorage.clear();
  oidc = {
    isLoading: false,
    isAuthenticated: false,
    user: undefined,
    error: undefined,
    events: {
      addAccessTokenExpired: (cb) => expiredHandlers.push(cb),
      removeAccessTokenExpired: () => {},
      addSilentRenewError: (cb) => expiredHandlers.push(cb),
      removeSilentRenewError: () => {},
    },
    signinRedirect: vi.fn(),
    signoutRedirect: vi.fn(),
  };
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.unstubAllEnvs();
  setTokenGetter(() => undefined);
});

describe('AuthProvider — fail closed (unconfigured, production)', () => {
  it('renders an unauthenticated, error-surfaced session that mints no roles', () => {
    mockOidcEnabled = false;
    vi.stubEnv('DEV', false);
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );
    expect(screen.getByTestId('auth')).toHaveTextContent('false');
    expect(screen.getByTestId('roles')).toHaveTextContent('');
    expect(screen.getByTestId('err')).toHaveTextContent('Identity provider is not configured.');
  });
});

describe('AuthProvider — DEV stub (unconfigured, development)', () => {
  it('defaults to the secretary role for a runnable shell', () => {
    mockOidcEnabled = false; // DEV is true by default under vitest
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );
    expect(screen.getByTestId('auth')).toHaveTextContent('true');
    expect(screen.getByTestId('roles')).toHaveTextContent('secretary');
  });

  it('reads previously chosen dev roles from sessionStorage', () => {
    sessionStorage.setItem('acmp-dev-roles', JSON.stringify(['chairman', 'administrator']));
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );
    expect(screen.getByTestId('roles')).toHaveTextContent('administrator,chairman');
  });

  it('persists a dev role switch to sessionStorage and updates the session', async () => {
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );
    await userEvent.click(screen.getByRole('button', { name: 'become auditor' }));
    expect(screen.getByTestId('roles')).toHaveTextContent('auditor');
    expect(JSON.parse(sessionStorage.getItem('acmp-dev-roles')!)).toEqual(['auditor']);
  });
});

describe('AuthProvider — OidcBridge (configured Keycloak)', () => {
  it('maps Keycloak claims to canonical roles and a display name', () => {
    mockOidcEnabled = true;
    stubFetch(() => ({ jsonBody: {} }));
    oidc.isAuthenticated = true;
    oidc.user = {
      access_token: 'tok',
      profile: { name: 'Dr Sara Noor', realm_access: { roles: ['acmp-chairman', 'member'] } },
    };
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );
    expect(screen.getByTestId('auth')).toHaveTextContent('true');
    expect(screen.getByTestId('roles')).toHaveTextContent('chairman,member');
    expect(screen.getByTestId('name')).toHaveTextContent('Dr Sara Noor');
    expect(screen.getByTestId('initials')).toHaveTextContent('DN');
  });

  it('provisions the local profile once on login, carrying the bearer token (post-login never 401)', async () => {
    mockOidcEnabled = true;
    const spy = stubFetch(() => ({ jsonBody: {} }));
    oidc.isAuthenticated = true;
    oidc.user = { access_token: 'tok', profile: { name: 'A' } };
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );
    await waitFor(() => expect(spy).toHaveBeenCalledTimes(1));
    const [url, init] = spy.mock.calls.at(-1)!;
    expect(url).toBe('/api/members/me');
    expect((init as RequestInit).method).toBe('POST');
    // The token getter is wired SYNCHRONOUSLY in render (AuthProvider.tsx:31) so the very first
    // child fetch already carries the token. If that wiring regresses to an effect, this fails
    // even though line coverage stays 100% — the whole point of the synchronous wiring.
    const headers = (init as RequestInit).headers as Record<string, string>;
    expect(headers.Authorization).toBe('Bearer tok');
  });

  it('retries provisioning on the next render after a failure (resets the guard)', async () => {
    mockOidcEnabled = true;
    const spy = stubFetch(() => ({ status: 500, jsonBody: { title: 'down' } }));
    oidc.isAuthenticated = true;
    oidc.user = { access_token: 'tok', profile: { name: 'A' } };
    const { rerender } = render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );
    await waitFor(() => expect(spy).toHaveBeenCalledTimes(1));
    // New user identity → effect deps change. A second call only happens if the
    // failure reset provisioned.current (the early-return guard would block it otherwise).
    oidc.user = { access_token: 'tok2', profile: { name: 'A' } };
    rerender(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );
    await waitFor(() => expect(spy).toHaveBeenCalledTimes(2));
  });

  it('does not provision when the session is not authenticated', async () => {
    mockOidcEnabled = true;
    const spy = stubFetch(() => ({ jsonBody: {} }));
    oidc.isAuthenticated = false;
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );
    await new Promise((r) => setTimeout(r, 0));
    expect(spy).not.toHaveBeenCalled();
  });

  it('flags a signed-out status on sign-out and triggers the Keycloak redirect', async () => {
    mockOidcEnabled = true;
    stubFetch(() => ({ jsonBody: {} }));
    oidc.isAuthenticated = true;
    oidc.user = { access_token: 'tok', profile: { name: 'A' } };
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );
    await userEvent.click(screen.getByRole('button', { name: 'sign out' }));
    expect(oidc.signoutRedirect).toHaveBeenCalled();
    expect(sessionStorage.getItem('acmp:auth-status')).toBe('signed_out');
  });

  it('flags session_expired when the access token expires', () => {
    mockOidcEnabled = true;
    stubFetch(() => ({ jsonBody: {} }));
    oidc.isAuthenticated = true;
    oidc.user = { access_token: 'tok', profile: { name: 'A' } };
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );
    expect(expiredHandlers.length).toBeGreaterThan(0);
    act(() => expiredHandlers.forEach((h) => h()));
    expect(sessionStorage.getItem('acmp:auth-status')).toBe('session_expired');
  });

  it('surfaces a token-exchange error from the IdP', () => {
    mockOidcEnabled = true;
    stubFetch(() => ({ jsonBody: {} }));
    oidc.isAuthenticated = false;
    oidc.error = { message: 'invalid_grant' };
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );
    expect(screen.getByTestId('err')).toHaveTextContent('invalid_grant');
  });
});
