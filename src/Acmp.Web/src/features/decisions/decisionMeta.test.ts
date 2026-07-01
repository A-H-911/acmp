import { describe, it, expect } from 'vitest';
import { outcomeTone, conditionTone, DECISION_OUTCOMES } from './decisionMeta';

describe('decisionMeta', () => {
  it('maps every outcome to a defined status tone', () => {
    for (const o of DECISION_OUTCOMES) {
      expect(outcomeTone(o)).toBeDefined();
    }
    expect(outcomeTone('Approved')).toBe('success');
    expect(outcomeTone('Rejected')).toBe('danger');
    expect(outcomeTone('ConditionallyApproved')).toBe('warn');
  });

  it('lists all 11 README §E outcomes', () => {
    expect(DECISION_OUTCOMES).toHaveLength(11);
  });

  it('maps condition status to a tone', () => {
    expect(conditionTone('Open')).toBe('warn');
    expect(conditionTone('Met')).toBe('success');
    expect(conditionTone('Waived')).toBe('neutral');
  });
});
