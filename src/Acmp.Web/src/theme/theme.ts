export type Theme = 'light' | 'dark';

const STORAGE_KEY = 'acmp-theme';

// Theme preference persists across sessions (AC-042, frontend portion) via localStorage.
export function getStoredTheme(): Theme {
  return localStorage.getItem(STORAGE_KEY) === 'dark' ? 'dark' : 'light';
}

export function applyTheme(theme: Theme): void {
  document.documentElement.setAttribute('data-theme', theme);
  localStorage.setItem(STORAGE_KEY, theme);
}
