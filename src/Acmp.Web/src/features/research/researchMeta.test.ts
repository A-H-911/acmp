import { describe, it, expect } from 'vitest';
import {
  statusTone, findingTone, recStatusTone, initials,
  RESEARCH_STATUSES, CONFIDENCES, PRIORITIES, REC_STATUSES,
} from './researchMeta';

describe('researchMeta', () => {
  it('maps every mission status to a tone (Active=info, Completed=success, no-ref Proposed/Cancelled)', () => {
    expect(statusTone('Proposed')).toBe('neutral');
    expect(statusTone('Active')).toBe('info');
    expect(statusTone('Completed')).toBe('success');
    expect(statusTone('Cancelled')).toBe('danger');
    // Defensive fallback for an unexpected wire value.
    expect(statusTone('Unknown' as never)).toBe('neutral');
  });

  it('derives the finding tone from verification', () => {
    expect(findingTone(true)).toBe('success');
    expect(findingTone(false)).toBe('neutral');
  });

  it('maps every recommendation status to a tone', () => {
    expect(recStatusTone('Proposed')).toBe('info');
    expect(recStatusTone('Accepted')).toBe('success');
    expect(recStatusTone('Rejected')).toBe('danger');
    expect(recStatusTone('Unknown' as never)).toBe('neutral');
  });

  it('builds two-letter initials, falling back to ? for a blank name', () => {
    expect(initials('Noura Public')).toBe('NP');
    expect(initials('Omar')).toBe('O');
    expect(initials('   ')).toBe('?');
  });

  it('exposes the full enum-value arrays (both locales rely on these)', () => {
    expect(RESEARCH_STATUSES).toEqual(['Proposed', 'Active', 'Completed', 'Cancelled']);
    expect(CONFIDENCES).toEqual(['Low', 'Medium', 'High']);
    expect(PRIORITIES).toEqual(['Low', 'Medium', 'High']);
    expect(REC_STATUSES).toEqual(['Proposed', 'Accepted', 'Rejected']);
  });
});
