import { describe, it, expect } from 'vitest';
import { kindColor, kindPointsUp, statusTone, DEP_KINDS, DEP_STATUSES } from './depMeta';

describe('depMeta', () => {
  it('colours the relation by kind (design relMeta.c)', () => {
    expect(kindColor('DependsOn')).toBe('var(--st-info-fg)');
    expect(kindColor('BlockedBy')).toBe('var(--st-danger-fg)');
    expect(kindColor('Blocks')).toBe('var(--st-danger-fg)');
    expect(kindColor('RelatesTo')).toBe('var(--st-neutral-fg)');
  });

  it('points the arrow up only for the upstream kinds (design relMeta.up)', () => {
    expect(kindPointsUp('DependsOn')).toBe(true);
    expect(kindPointsUp('BlockedBy')).toBe(true);
    expect(kindPointsUp('Blocks')).toBe(false);
    expect(kindPointsUp('RelatesTo')).toBe(false);
  });

  it('maps status → tone (active→info, resolved→success, removed→neutral)', () => {
    expect(statusTone('Open')).toBe('info');
    expect(statusTone('Resolved')).toBe('success');
    expect(statusTone('Removed')).toBe('neutral');
  });

  it('exposes the full kind set and the user-filterable status set', () => {
    expect(DEP_KINDS).toEqual(['DependsOn', 'BlockedBy', 'Blocks', 'RelatesTo']);
    expect(DEP_STATUSES).toEqual(['Open', 'Resolved']); // Removed is soft-deleted, not a filter option
  });
});
