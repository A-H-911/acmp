export type Theme = 'light' | 'dark';

const STORAGE_KEY = 'acmp-theme';

/*
 * Theme preference persists across sessions (AC-042, frontend portion) via localStorage — an
 * explicit choice must survive a logout, and it must beat the operating system in BOTH directions.
 *
 * null means "the user has never chosen", which is a THIRD state this module used to destroy.
 * getStoredTheme() returned 'light' for an empty key and applyTheme() wrote to storage on every
 * call, so the first paint of a first visit persisted an explicit-looking `light` — after which no
 * code could tell an actual preference from a default. Following the OS requires that distinction,
 * so storage is now written only when the user acts.
 */
export function getStoredTheme(): Theme | null {
  const raw = localStorage.getItem(STORAGE_KEY);
  return raw === 'dark' || raw === 'light' ? raw : null;
}

/** The OS preference, used only while getStoredTheme() is null. */
export function systemTheme(): Theme {
  // matchMedia is missing in jsdom and in very old browsers; treat absence as light rather than
  // throwing during render.
  return typeof window !== 'undefined' && typeof window.matchMedia === 'function'
    ? window.matchMedia('(prefers-color-scheme: dark)').matches
      ? 'dark'
      : 'light'
    : 'light';
}

/*
 * Reflect a theme in the DOM. `persist` is opt-in BY DESIGN: applying a theme because the OS says so
 * must not look like a decision the user made.
 *
 * tokens.css already answers prefers-color-scheme on its own, so a visitor with no stored preference
 * is themed correctly before this — or any script — runs. Setting the attribute here keeps the DOM
 * and React in agreement and makes the resolved theme observable to tests.
 */
export function applyTheme(theme: Theme, persist = false): void {
  document.documentElement.setAttribute('data-theme', theme);
  if (persist) localStorage.setItem(STORAGE_KEY, theme);
}
