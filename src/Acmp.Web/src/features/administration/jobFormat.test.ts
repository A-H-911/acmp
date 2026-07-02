import { describe, it, expect } from 'vitest';
import { formatRelative, formatDuration } from './jobFormat';

const NOW = Date.parse('2026-07-02T12:00:00Z');
const at = (deltaSec: number) => new Date(NOW + deltaSec * 1000).toISOString();

describe('formatRelative', () => {
  it('picks the largest fitting unit in the past', () => {
    expect(formatRelative(at(-30), 'en', NOW)).toMatch(/30 seconds ago/);
    expect(formatRelative(at(-120), 'en', NOW)).toMatch(/2 minutes ago/);
    expect(formatRelative(at(-7200), 'en', NOW)).toMatch(/2 hours ago/);
    expect(formatRelative(at(-172800), 'en', NOW)).toMatch(/2 days ago/);
  });

  it('formats a future instant', () => {
    expect(formatRelative(at(180), 'en', NOW)).toMatch(/in 3 minutes/);
  });

  it('says "now" at the current instant (numeric: auto)', () => {
    expect(formatRelative(at(0), 'en', NOW)).toMatch(/now/);
  });

  it('localizes to Arabic', () => {
    const ar = formatRelative(at(-120), 'ar', NOW);
    expect(ar).not.toBe(formatRelative(at(-120), 'en', NOW));
    expect(ar).toMatch(/[؀-ۿ]/); // contains Arabic script
  });
});

describe('formatDuration', () => {
  it('sub-second → milliseconds', () => {
    expect(formatDuration(820)).toEqual({ n: '820', unit: 'ms' });
  });

  it('whole seconds drop the trailing decimal', () => {
    expect(formatDuration(12000)).toEqual({ n: '12', unit: 's' });
  });

  it('fractional seconds keep one decimal', () => {
    expect(formatDuration(1200)).toEqual({ n: '1.2', unit: 's' });
  });
});
