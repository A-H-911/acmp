import { useEffect, useState } from 'react';
import { applyTheme, getStoredTheme, systemTheme, type Theme } from './theme';

/**
 * Theme state + persistence (AC-042), defaulting to the operating system.
 *
 * Two pieces of state, not one. `explicit` is the user's choice or null; `theme` is what is actually
 * rendered. Collapsing them would lose the ability to tell "I chose light" from "the OS is light",
 * and the OS subscription below needs exactly that distinction.
 *
 * AC-042 is unaffected: it grades that a chosen dark theme is still active after logging in again.
 * An explicit choice is still persisted and still beats the OS.
 *
 * WBS-40.3: the hook has more than one consumer (TopBar's toggle and the Profile page's Light/Dark
 * control are mounted together), and each call holds its own state. A choice made in one is fanned
 * out to every mounted instance, or TopBar's icon would go stale and its next click would re-apply
 * the theme already showing.
 * ponytail: a module-level listener set, not a context — upgrade if a third kind of consumer appears.
 */
const listeners = new Set<(t: Theme) => void>();

export function useTheme(): { theme: Theme; toggle: () => void; setTheme: (t: Theme) => void } {
  const [explicit, setExplicit] = useState<Theme | null>(getStoredTheme);
  const [system, setSystem] = useState<Theme>(systemTheme);
  const theme = explicit ?? system;

  useEffect(() => {
    listeners.add(setExplicit);
    return () => {
      listeners.delete(setExplicit);
    };
  }, []);

  // Follow the OS live, but ONLY while no explicit choice exists — otherwise changing the system
  // theme would silently discard the user's decision.
  useEffect(() => {
    if (explicit !== null) return;
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return;
    const mq = window.matchMedia('(prefers-color-scheme: dark)');
    const onChange = (e: MediaQueryListEvent) => setSystem(e.matches ? 'dark' : 'light');
    mq.addEventListener('change', onChange);
    return () => mq.removeEventListener('change', onChange);
  }, [explicit]);

  // Persist only what the user chose. Mirroring an OS-derived theme into storage would fabricate a
  // preference nobody expressed, and the app could never follow the OS again.
  useEffect(() => applyTheme(theme, explicit !== null), [theme, explicit]);

  const setTheme = (t: Theme) => listeners.forEach((set) => set(t));
  const toggle = () => setTheme(theme === 'dark' ? 'light' : 'dark');
  return { theme, toggle, setTheme };
}
