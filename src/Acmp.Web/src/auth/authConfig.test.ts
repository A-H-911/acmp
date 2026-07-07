import { describe, it, expect, afterEach, vi } from 'vitest';

/*
 * authConfig reads VITE_OIDC_* at module load, so each case stubs the env and
 * re-imports the module. Security-relevant: PKCE/auth-code only, no client secret
 * in a public SPA, and tokens in sessionStorage (smaller XSS window).
 */
afterEach(() => {
  vi.unstubAllEnvs();
  vi.resetModules();
  vi.restoreAllMocks();
});

async function loadConfig() {
  vi.resetModules();
  return import('./authConfig');
}

/** AuthProviderProps is a union; in tests we read its UserManager settings as a flat record. */
type ConfigView = Record<string, unknown> & { onSigninCallback?: (user: unknown) => void };
async function loadConfigView() {
  const { oidcConfig } = await loadConfig();
  return oidcConfig as unknown as ConfigView;
}

describe('oidcEnabled', () => {
  it('is true only when both authority and clientId are configured', async () => {
    vi.stubEnv('VITE_OIDC_AUTHORITY', 'https://kc.example/realms/acmp');
    vi.stubEnv('VITE_OIDC_CLIENT_ID', 'acmp-web');
    const { oidcEnabled } = await loadConfig();
    expect(oidcEnabled).toBe(true);
  });

  it('is false when the authority is missing (fail closed, not half-configured)', async () => {
    vi.stubEnv('VITE_OIDC_AUTHORITY', '');
    vi.stubEnv('VITE_OIDC_CLIENT_ID', 'acmp-web');
    const { oidcEnabled } = await loadConfig();
    expect(oidcEnabled).toBe(false);
  });

  it('is false when the clientId is missing', async () => {
    vi.stubEnv('VITE_OIDC_AUTHORITY', 'https://kc.example/realms/acmp');
    vi.stubEnv('VITE_OIDC_CLIENT_ID', '');
    const { oidcEnabled } = await loadConfig();
    expect(oidcEnabled).toBe(false);
  });
});

describe('oidcConfig', () => {
  it('uses authorization-code + PKCE with no client secret', async () => {
    vi.stubEnv('VITE_OIDC_AUTHORITY', 'https://kc.example/realms/acmp');
    vi.stubEnv('VITE_OIDC_CLIENT_ID', 'acmp-web');
    const cfg = await loadConfigView();
    expect(cfg.response_type).toBe('code'); // PKCE is on by default for code flow
    expect(cfg).not.toHaveProperty('client_secret');
    expect(cfg.authority).toBe('https://kc.example/realms/acmp');
    expect(cfg.client_id).toBe('acmp-web');
    expect(cfg.automaticSilentRenew).toBe(true);
    expect(cfg.redirect_uri).toBe(`${window.location.origin}/auth/callback`);
  });

  it('defaults the scope to openid profile email when none is configured', async () => {
    vi.stubEnv('VITE_OIDC_AUTHORITY', 'https://kc.example/realms/acmp');
    vi.stubEnv('VITE_OIDC_CLIENT_ID', 'acmp-web');
    // VITE_OIDC_SCOPE deliberately left unset → the ?? fallback applies.
    const cfg = await loadConfigView();
    expect(cfg.scope).toBe('openid profile email');
  });

  it('honours a custom scope when provided', async () => {
    vi.stubEnv('VITE_OIDC_AUTHORITY', 'https://kc.example/realms/acmp');
    vi.stubEnv('VITE_OIDC_CLIENT_ID', 'acmp-web');
    vi.stubEnv('VITE_OIDC_SCOPE', 'openid roles');
    const cfg = await loadConfigView();
    expect(cfg.scope).toBe('openid roles');
  });

  it('strips the OIDC code/state from the URL after a successful sign-in', async () => {
    vi.stubEnv('VITE_OIDC_AUTHORITY', 'https://kc.example/realms/acmp');
    vi.stubEnv('VITE_OIDC_CLIENT_ID', 'acmp-web');
    const cfg = await loadConfigView();
    const replace = vi.spyOn(window.history, 'replaceState');
    cfg.onSigninCallback?.(undefined);
    expect(replace).toHaveBeenCalledWith({}, document.title, '/');
  });

  it('restores a stored deep-link path into the URL after sign-in', async () => {
    vi.stubEnv('VITE_OIDC_AUTHORITY', 'https://kc.example/realms/acmp');
    vi.stubEnv('VITE_OIDC_CLIENT_ID', 'acmp-web');
    const { oidcConfig, RETURN_KEY } = await loadConfig();
    sessionStorage.setItem(RETURN_KEY, '/meetings/new');
    const replace = vi.spyOn(window.history, 'replaceState');
    (oidcConfig as unknown as ConfigView).onSigninCallback?.(undefined);
    expect(replace).toHaveBeenCalledWith({}, document.title, '/meetings/new');
    sessionStorage.clear();
  });
});

describe('returnPathToStore', () => {
  it('keeps a real deep-link path', async () => {
    const { returnPathToStore } = await loadConfig();
    expect(returnPathToStore('/meetings/new')).toBe('/meetings/new');
    expect(returnPathToStore('/decisions/DEC-2026-001?tab=votes')).toBe('/decisions/DEC-2026-001?tab=votes');
  });

  it('clears (returns null) for no path, home, and the auth routes — no stale target on a plain sign-in', async () => {
    const { returnPathToStore } = await loadConfig();
    expect(returnPathToStore(undefined)).toBeNull();
    expect(returnPathToStore('/')).toBeNull();
    expect(returnPathToStore('/login')).toBeNull();
    expect(returnPathToStore('/auth/callback')).toBeNull();
  });
});
