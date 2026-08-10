import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { applyTheme, getStoredTheme, systemTheme } from './theme';

/** Pretend the OS prefers dark (or not) for the duration of one test. */
function stubPrefersDark(matches: boolean) {
  const spy = vi.spyOn(window, 'matchMedia').mockImplementation(
    (query: string) =>
      ({
        matches: query.includes('prefers-color-scheme: dark') ? matches : false,
        media: query,
        onchange: null,
        addEventListener: () => {},
        removeEventListener: () => {},
        addListener: () => {},
        removeListener: () => {},
        dispatchEvent: () => false,
      }) as unknown as MediaQueryList,
  );
  return spy;
}

// AC-042: theme preference persists across sessions (localStorage portion).
describe('theme persistence', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => vi.restoreAllMocks());

  /*
   * This used to assert `getStoredTheme() === 'light'` for an empty key — the default was baked into
   * the reader, so "nothing chosen" and "chose light" were the same value and the OS could never be
   * consulted. null is now a real third state.
   */
  it('reports null — not a default — when nothing is stored', () => {
    expect(getStoredTheme()).toBeNull();
  });

  it('reads the OS preference in both directions', () => {
    stubPrefersDark(true);
    expect(systemTheme()).toBe('dark');
    vi.restoreAllMocks();
    stubPrefersDark(false);
    expect(systemTheme()).toBe('light');
  });

  it('falls back to light when matchMedia is unavailable', () => {
    // The defensive path for jsdom and pre-matchMedia browsers. Worth a real test rather than a
    // coverage-shaped one: without it systemTheme() throws during render, which would surface as a
    // blank screen rather than as a missing browser API.
    const original = window.matchMedia;
    // @ts-expect-error deliberately removing the API to exercise the guard
    delete window.matchMedia;
    try {
      expect(systemTheme()).toBe('light');
    } finally {
      window.matchMedia = original;
    }
  });

  it('reflects the applied theme on <html> WITHOUT persisting by default', () => {
    // An OS-derived theme must not be written to storage; doing so fabricates a preference the user
    // never expressed, and the app could then never follow the OS again.
    applyTheme('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    expect(localStorage.getItem('acmp-theme')).toBeNull();
    expect(getStoredTheme()).toBeNull();
  });

  it('persists when the choice is explicit (AC-042)', () => {
    applyTheme('dark', true);
    expect(localStorage.getItem('acmp-theme')).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    expect(getStoredTheme()).toBe('dark');
  });

  it('an explicit choice survives and beats the OS in BOTH directions', () => {
    // The interesting case is explicit-light on a dark machine: a one-way check would pass while
    // the OS silently overrode the user.
    applyTheme('light', true);
    stubPrefersDark(true);
    expect(getStoredTheme()).toBe('light');

    localStorage.clear();
    applyTheme('dark', true);
    vi.restoreAllMocks();
    stubPrefersDark(false);
    expect(getStoredTheme()).toBe('dark');
  });
});
