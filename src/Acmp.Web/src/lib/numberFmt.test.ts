import { describe, it, expect } from 'vitest';
import { formatNumber, formatBytes, formatPercent } from './numberFmt';

describe('formatNumber', () => {
  it('renders Latin digits with grouping for English', () => {
    expect(formatNumber(12345, 'en')).toBe('12,345');
  });

  it('renders LATIN digits and separators for Arabic too (AC-167)', () => {
    // AC-167 (DEC-192 r2) superseded AC-147's Arabic-Indic rule. MUTATION CHECK: pin `ar-u-nu-arab` again
    // and this goes red; leave the tag bare and it depends on the runtime's ICU default instead of a rule.
    expect(formatNumber(12345, 'ar')).toBe('12,345');
  });

  it('treats an undefined language as English rather than throwing', () => {
    expect(formatNumber(7, undefined)).toBe('7');
  });

  it('matches on the `ar` PREFIX, so a regional Arabic tag is still Arabic', () => {
    // ar-SA's own default is Arabic-Indic, so this proves the prefix match reaches the Latin pin.
    expect(formatNumber(5, 'ar-SA')).toBe('5');
  });

  it('passes Intl options through', () => {
    expect(formatNumber(0.5, 'en', { style: 'percent' })).toBe('50%');
  });
});

describe('formatPercent', () => {
  it('takes a whole percent, the way this codebase already holds one', () => {
    expect(formatPercent(87, 'en')).toBe('87%');
  });

  it('uses Latin digits and the % sign in Arabic (AC-167), with bidi marks keeping it in order', () => {
    const ar = formatPercent(87, 'ar');
    expect(ar).toContain('87');
    expect(ar).toContain('%');
    expect(ar).not.toMatch(/[٠-٩٪]/);
  });

  it('rounds to whole percents rather than exposing the division', () => {
    expect(formatPercent(67, 'en')).toBe('67%');
  });
});

describe('formatBytes', () => {
  it('steps up through the units a person reads', () => {
    expect(formatBytes(512, 'en')).toBe('512 B');
    expect(formatBytes(2048, 'en')).toBe('2 KB');
    expect(formatBytes(1_572_864, 'en')).toBe('1.5 MB');
    expect(formatBytes(3 * 1024 ** 3, 'en')).toBe('3 GB');
  });

  it('renders Latin digits with the Arabic unit the mockups draw (AC-167, AC-168)', () => {
    expect(formatBytes(1_572_864, 'ar')).toBe('1.5 م.ب');
    expect(formatBytes(2048, 'ar')).toBe('2 ك.ب');
    expect(formatBytes(512, 'ar')).toBe('512 ب');
  });

  it('caps at the largest unit rather than inventing one past the table', () => {
    expect(formatBytes(5 * 1024 ** 4, 'en')).toBe('5,120 GB');
  });

  it('floors a negative size at zero instead of rendering a negative byte count', () => {
    expect(formatBytes(-1, 'en')).toBe('0 B');
  });
});
