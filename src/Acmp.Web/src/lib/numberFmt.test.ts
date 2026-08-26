import { describe, it, expect } from 'vitest';
import { formatNumber, formatBytes } from './numberFmt';

describe('formatNumber', () => {
  it('renders Latin digits with grouping for English', () => {
    expect(formatNumber(12345, 'en')).toBe('12,345');
  });

  it('renders Arabic-Indic digits for Arabic', () => {
    // MUTATION CHECK: drop the `ar-u-nu-arab` pin and this goes red under Node, whose ICU resolves
    // bare `ar` to Latin digits while a browser resolves it to Arabic-Indic. Asserting the digits is
    // the only assertion that can tell the two runtimes apart.
    expect(formatNumber(12345, 'ar')).toBe('١٢٬٣٤٥');
  });

  it('treats an undefined language as English rather than throwing', () => {
    expect(formatNumber(7, undefined)).toBe('7');
  });

  it('matches on the `ar` PREFIX, so a regional Arabic tag is still Arabic', () => {
    expect(formatNumber(5, 'ar-SA')).toBe('٥');
  });

  it('passes Intl options through', () => {
    expect(formatNumber(0.5, 'en', { style: 'percent' })).toBe('50%');
  });
});

describe('formatBytes', () => {
  it('steps up through the units a person reads', () => {
    expect(formatBytes(512, 'en')).toBe('512 B');
    expect(formatBytes(2048, 'en')).toBe('2 KB');
    expect(formatBytes(1_572_864, 'en')).toBe('1.5 MB');
    expect(formatBytes(3 * 1024 ** 3, 'en')).toBe('3 GB');
  });

  it('renders the numeral in Arabic-Indic while the unit symbol stays as the mockups draw it', () => {
    // ١٫٥ — Arabic decimal separator, not a full stop. The unit suffix is deliberately unchanged.
    expect(formatBytes(1_572_864, 'ar')).toBe('١٫٥ MB');
  });

  it('caps at the largest unit rather than inventing one past the table', () => {
    expect(formatBytes(5 * 1024 ** 4, 'en')).toBe('5,120 GB');
  });

  it('floors a negative size at zero instead of rendering a negative byte count', () => {
    expect(formatBytes(-1, 'en')).toBe('0 B');
  });
});
