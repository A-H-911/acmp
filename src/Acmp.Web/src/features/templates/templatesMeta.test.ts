import { describe, it, expect } from 'vitest';
import { statusTone, TEMPLATE_STATUSES, TARGET_TYPES } from './templatesMeta';

describe('templatesMeta', () => {
  it('maps each TemplateStatus to its tone', () => {
    expect(statusTone('Active')).toBe('success');
    expect(statusTone('Deprecated')).toBe('neutral');
  });

  it('falls back to neutral for an unknown status', () => {
    expect(statusTone('Retired' as never)).toBe('neutral');
  });

  it('exposes the status + target-type option arrays', () => {
    expect(TEMPLATE_STATUSES).toEqual(['Active', 'Deprecated']);
    expect(TARGET_TYPES).toEqual(['Topic', 'Adr', 'MinutesOfMeeting', 'ResearchMission']);
  });
});
