import { describe, it, expect, beforeEach } from 'vitest';
import { applyTheme, getStoredTheme } from './theme';

// AC-042: theme preference persists across sessions (localStorage portion).
describe('theme persistence', () => {
  beforeEach(() => localStorage.clear());

  it('defaults to light when nothing is stored', () => {
    expect(getStoredTheme()).toBe('light');
  });

  it('persists and reflects the applied theme on <html>', () => {
    applyTheme('dark');
    expect(localStorage.getItem('acmp-theme')).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    expect(getStoredTheme()).toBe('dark');
  });
});
