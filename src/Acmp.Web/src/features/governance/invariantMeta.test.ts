import { describe, it, expect } from 'vitest';
import {
  statusTone, categoryDot, INVARIANT_STATUSES, INVARIANT_CATEGORIES, INVARIANT_SCOPES,
} from './invariantMeta';

describe('statusTone', () => {
  it('maps every one of the five canonical statuses to a chip tone', () => {
    expect(INVARIANT_STATUSES).toEqual(['Draft', 'Proposed', 'Active', 'Retired', 'Superseded']);
    expect(statusTone('Draft')).toBe('neutral');
    expect(statusTone('Proposed')).toBe('info');
    expect(statusTone('Active')).toBe('success');
    expect(statusTone('Retired')).toBe('neutral');
    expect(statusTone('Superseded')).toBe('neutral');
  });
});

describe('categoryDot', () => {
  it('maps every category to a distinct dot colour token', () => {
    expect(INVARIANT_CATEGORIES).toEqual(['Security', 'Performance', 'Data', 'Interoperability', 'Compliance', 'Other']);
    const dots = INVARIANT_CATEGORIES.map(categoryDot);
    expect(dots).toEqual([
      'var(--st-danger-dot)', 'var(--st-warn-dot)', 'var(--st-info-dot)',
      'var(--st-sched-dot)', 'var(--st-success-dot)', 'var(--st-neutral-dot)',
    ]);
    // Each category resolves to a non-empty CSS variable.
    dots.forEach((d) => expect(d).toMatch(/^var\(--st-.+-dot\)$/));
  });
});

describe('INVARIANT_SCOPES', () => {
  it('lists the four scopes narrowest → widest', () => {
    expect(INVARIANT_SCOPES).toEqual(['SingleStream', 'MultiStream', 'Platform', 'OrgWide']);
  });
});
