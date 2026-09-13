import { describe, it, expect, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useTheme } from './useTheme';

beforeEach(() => {
  localStorage.clear();
  document.documentElement.removeAttribute('data-theme');
});

/*
 * WBS-40.3: TopBar and the Profile page each call useTheme. A choice made through one instance must
 * reach the other, or the TopBar icon goes stale and its next click re-applies the current theme.
 */
describe('useTheme across instances', () => {
  it('a setTheme in one instance updates every mounted instance and persists', () => {
    const a = renderHook(() => useTheme());
    const b = renderHook(() => useTheme());
    expect(a.result.current.theme).toBe('light');

    act(() => b.result.current.setTheme('dark'));

    expect(a.result.current.theme).toBe('dark');
    expect(b.result.current.theme).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    expect(localStorage.getItem('acmp-theme')).toBe('dark');
  });

  it('toggle flips from the resolved theme and is seen by the other instance', () => {
    const a = renderHook(() => useTheme());
    const b = renderHook(() => useTheme());

    act(() => a.result.current.toggle());
    expect(b.result.current.theme).toBe('dark');

    act(() => b.result.current.toggle());
    expect(a.result.current.theme).toBe('light');
  });

  it('follows the OS live while no explicit choice exists', () => {
    let onChange: ((e: MediaQueryListEvent) => void) | undefined;
    const original = window.matchMedia;
    window.matchMedia = ((q: string) => ({
      matches: false, media: q,
      addEventListener: (_: string, cb: (e: MediaQueryListEvent) => void) => { onChange = cb; },
      removeEventListener: () => {},
    })) as unknown as typeof window.matchMedia;
    try {
      const a = renderHook(() => useTheme());
      act(() => onChange!({ matches: true } as MediaQueryListEvent));
      expect(a.result.current.theme).toBe('dark');
      act(() => onChange!({ matches: false } as MediaQueryListEvent));
      expect(a.result.current.theme).toBe('light');
      // Following the OS is not a choice: nothing is persisted.
      expect(localStorage.getItem('acmp-theme')).toBeNull();
    } finally {
      window.matchMedia = original;
    }
  });

  it('renders light without throwing when matchMedia is unavailable', () => {
    const original = window.matchMedia;
    // @ts-expect-error — simulating a browser without matchMedia
    delete window.matchMedia;
    try {
      expect(renderHook(() => useTheme()).result.current.theme).toBe('light');
    } finally {
      window.matchMedia = original;
    }
  });

  it('an unmounted instance stops listening', () => {
    const a = renderHook(() => useTheme());
    const b = renderHook(() => useTheme());
    b.unmount();

    // Would warn/throw on a state update to an unmounted hook if the listener leaked.
    act(() => a.result.current.setTheme('dark'));
    expect(a.result.current.theme).toBe('dark');
  });
});
