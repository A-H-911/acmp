import { describe, it, expect } from 'vitest';
import { formatDmy } from './p15Date';

describe('formatDmy', () => {
  it('renders Latin day-first for non-Arabic locales', () => {
    expect(formatDmy('2026-06-14T09:00:00Z', 'en')).toBe('14 Jun 2026');
  });

  it('renders day-first with Latin digits for the Arabic locale (AC-167)', () => {
    // Day-first, Latin digits, Arabic month name - assert the shape, not the exact ICU month spelling.
    const ar = formatDmy('2026-06-14T09:00:00Z', 'ar');
    expect(ar.startsWith('14')).toBe(true); // day-first, not the month
    expect(ar).toContain('2026');
    expect(ar).not.toMatch(/[٠-٩]/);
    expect(ar).not.toBe(formatDmy('2026-06-14T09:00:00Z', 'en'));
  });
});
