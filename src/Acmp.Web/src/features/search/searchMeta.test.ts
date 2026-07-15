import { describe, it, expect } from 'vitest';
import type { TFunction } from 'i18next';
import { searchStatusLabel } from './searchMeta';

// Stub t: known keys resolve; missing keys return the defaultValue (i18next's raw-fallback contract).
const KNOWN: Record<string, string> = { 'wiki.status.Published': 'Published', 'topics.status.Decided': 'Decided' };
const t = ((k: string, o?: { defaultValue?: string }) => KNOWN[k] ?? o?.defaultValue ?? k) as unknown as TFunction;

describe('searchStatusLabel', () => {
  it('localizes a mapped type + status through its namespace', () => {
    expect(searchStatusLabel('Documents', 'Published', t)).toBe('Published');
    expect(searchStatusLabel('Topics', 'Decided', t)).toBe('Decided');
  });

  it('falls back to the raw status for an unmapped type (Decisions)', () => {
    expect(searchStatusLabel('Decisions', 'Issued', t)).toBe('Issued');
  });

  it('falls back to the raw status when the type is mapped but the value has no key', () => {
    expect(searchStatusLabel('Topics', 'Backlog', t)).toBe('Backlog');
  });
});
