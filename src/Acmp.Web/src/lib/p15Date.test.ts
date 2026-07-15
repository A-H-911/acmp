import { describe, it, expect } from 'vitest';
import { formatDmy } from './p15Date';

describe('formatDmy', () => {
  it('renders Latin day-first for non-Arabic locales', () => {
    expect(formatDmy('2026-06-14T09:00:00Z', 'en')).toBe('14 Jun 2026');
  });

  it('renders Arabic-Indic day-first for the Arabic locale', () => {
    // Day-first, Arabic-Indic digits — assert the shape, not the exact ICU month spelling.
    const ar = formatDmy('2026-06-14T09:00:00Z', 'ar');
    expect(ar.startsWith('١٤')).toBe(true); // day-first (١٤), not the month
    expect(ar).toContain('٢٠٢٦'); // Arabic-Indic year
    expect(ar).not.toBe(formatDmy('2026-06-14T09:00:00Z', 'en'));
  });
});
