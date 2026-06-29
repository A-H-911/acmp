import { describe, it, expect, afterEach, vi } from 'vitest';
import { setAuthStatus, consumeAuthStatus } from './authStatus';

/*
 * The auth-status flag survives the Keycloak logout/expiry round-trip in
 * sessionStorage and is read exactly once by the LoginPage. Failure-first:
 * storage being unavailable must never throw into the redirect path.
 */
afterEach(() => {
  vi.restoreAllMocks();
  sessionStorage.clear();
});

/*
 * Replace the global sessionStorage with one whose accessors throw, for the
 * duration of `fn`. Spying on Storage.prototype does NOT work here — the test
 * environment's sessionStorage is not a Storage instance — so swap the object.
 */
function withBrokenStorage(fn: () => void) {
  const original = globalThis.sessionStorage;
  const broken = {
    getItem: () => {
      throw new Error('storage unavailable');
    },
    setItem: () => {
      throw new Error('storage unavailable');
    },
    removeItem: () => {},
  } as unknown as Storage;
  Object.defineProperty(globalThis, 'sessionStorage', { value: broken, configurable: true, writable: true });
  try {
    fn();
  } finally {
    Object.defineProperty(globalThis, 'sessionStorage', { value: original, configurable: true, writable: true });
  }
}

describe('authStatus', () => {
  it('round-trips signed_out and clears on read', () => {
    setAuthStatus('signed_out');
    expect(consumeAuthStatus()).toBe('signed_out');
    expect(consumeAuthStatus()).toBeNull(); // consumed once
  });

  it('round-trips session_expired', () => {
    setAuthStatus('session_expired');
    expect(consumeAuthStatus()).toBe('session_expired');
  });

  it('returns null when nothing was set', () => {
    expect(consumeAuthStatus()).toBeNull();
  });

  it('ignores a foreign/garbage value in the slot', () => {
    sessionStorage.setItem('acmp:auth-status', 'tampered');
    expect(consumeAuthStatus()).toBeNull();
    // the unrecognized value is still cleared so it can't linger
    expect(sessionStorage.getItem('acmp:auth-status')).toBeNull();
  });

  it('swallows a setItem failure instead of throwing into signout', () => {
    withBrokenStorage(() => {
      expect(() => setAuthStatus('signed_out')).not.toThrow();
    });
  });

  it('returns null when getItem throws', () => {
    withBrokenStorage(() => {
      expect(consumeAuthStatus()).toBeNull();
    });
  });
});
