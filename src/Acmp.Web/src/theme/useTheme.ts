import { useEffect, useState } from 'react';
import { applyTheme, getStoredTheme, type Theme } from './theme';

/** Theme state + persistence (AC-042). Applies data-theme on every change. */
export function useTheme(): { theme: Theme; toggle: () => void } {
  const [theme, setTheme] = useState<Theme>(getStoredTheme);
  useEffect(() => applyTheme(theme), [theme]);
  const toggle = () => setTheme((t) => (t === 'dark' ? 'light' : 'dark'));
  return { theme, toggle };
}
