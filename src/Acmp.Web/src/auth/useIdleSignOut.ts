import { useEffect, useRef } from 'react';

/*
 * AC-004 / OQ-076 — the idle timeout the realm setting could not deliver.
 *
 * WHY THIS EXISTS AT ALL, because "Keycloak already has ssoSessionIdleTimeout" is the obvious
 * objection and it is wrong. The realm does set it (1800s, read from the live production realm), but
 * oidc-client-ts runs `automaticSilentRenew`, and every renew is a request to Keycloak that RESETS
 * the SSO session's idle clock. So a user who leaves ACMP open and walks away is never idle from
 * Keycloak's point of view: the tab keeps the session alive on their behalf. The 30-minute timeout
 * can only fire once the tab is CLOSED — at which point there is no session to redirect and no
 * protected action to attempt. For an open tab the only ceiling was ssoSessionMaxLifespan at TEN
 * HOURS, which fires identically whether the person was working or absent.
 *
 * ⚠ THE SUPPRESSION IS THE LOAD-BEARING HALF, NOT THE SIGN-OUT. Signing out without stopping the
 * renew leaves the next renew tick free to resurrect the session — the timer is keyed to token
 * expiry and knows nothing about our intent. `onIdle` must therefore stop the renew FIRST; this hook
 * only decides WHEN, and the caller wires WHAT.
 *
 * ponytail: pointerdown + keydown + a visible-again visibilitychange, and nothing else. No scroll,
 * no mousemove — a page that scrolls itself or a nudged desk should not count as presence, and
 * anything a human does to actually use the app produces one of these three first.
 */

/** 30 minutes, matching the realm's own ssoSessionIdleTimeout (1800s) rather than inventing a number. */
export const IDLE_TIMEOUT_MS = 30 * 60 * 1000;

/**
 * Calls `onIdle` once, after `idleMs` with no user activity. Any activity restarts the clock.
 * Inert while `enabled` is false, so an unauthenticated visitor is never signed out of nothing.
 */
export function useIdleSignOut(enabled: boolean, onIdle: () => void, idleMs: number = IDLE_TIMEOUT_MS): void {
  // The callback is read through a ref so that a caller passing an inline arrow — which every render
  // recreates — does not reset the idle clock on every render. That would make the timeout
  // unreachable in exactly the app that renders most often, and it would look like it worked.
  const onIdleRef = useRef(onIdle);
  onIdleRef.current = onIdle;

  useEffect(() => {
    if (!enabled) return;

    let timer: ReturnType<typeof setTimeout> | undefined;
    // Fire at most once. signoutRedirect navigates away, but the timer could otherwise re-arm during
    // the redirect and call it twice.
    let fired = false;

    const arm = () => {
      if (fired) return;
      if (timer !== undefined) clearTimeout(timer);
      timer = setTimeout(() => {
        fired = true;
        onIdleRef.current();
      }, idleMs);
    };

    const onActivity = () => arm();
    // Returning to a tab is activity; leaving it is not, and must NOT restart the clock — an
    // abandoned machine is the case this whole hook exists for.
    const onVisibility = () => {
      if (document.visibilityState === 'visible') arm();
    };

    window.addEventListener('pointerdown', onActivity, { passive: true });
    window.addEventListener('keydown', onActivity, { passive: true });
    document.addEventListener('visibilitychange', onVisibility);
    arm();

    return () => {
      if (timer !== undefined) clearTimeout(timer);
      window.removeEventListener('pointerdown', onActivity);
      window.removeEventListener('keydown', onActivity);
      document.removeEventListener('visibilitychange', onVisibility);
    };
  }, [enabled, idleMs]);
}
