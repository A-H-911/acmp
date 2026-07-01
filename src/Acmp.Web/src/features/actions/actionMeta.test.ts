import { describe, it, expect } from 'vitest';
import { statusTone, progressColor, progressColorDetail, initials, ACTION_STATUSES } from './actionMeta';

describe('actionMeta', () => {
  it('maps every ActionStatus to a chip tone (incl. Cancelled)', () => {
    expect(statusTone('Open')).toBe('neutral');
    expect(statusTone('InProgress')).toBe('info');
    expect(statusTone('Blocked')).toBe('danger');
    expect(statusTone('Completed')).toBe('success');
    expect(statusTone('Verified')).toBe('scheduled');
    expect(statusTone('Cancelled')).toBe('neutral');
  });

  it('exposes the six statuses in lifecycle order for the filter', () => {
    expect(ACTION_STATUSES).toEqual(['Open', 'InProgress', 'Blocked', 'Completed', 'Verified', 'Cancelled']);
  });

  it('colours the register progress bar on 4 thresholds', () => {
    expect(progressColor(100)).toBe('var(--st-success-dot)');
    expect(progressColor(60)).toBe('var(--accent)');
    expect(progressColor(30)).toBe('var(--st-warn-dot)');
    expect(progressColor(10)).toBe('var(--st-danger-dot)');
  });

  it('colours the detail progress bar on 3 thresholds', () => {
    expect(progressColorDetail(100)).toBe('var(--st-success-dot)');
    expect(progressColorDetail(60)).toBe('var(--accent)');
    expect(progressColorDetail(10)).toBe('var(--st-warn-dot)');
  });

  it('derives up-to-two-letter initials with a fallback', () => {
    expect(initials('Noura Qassim')).toBe('NQ');
    expect(initials('Omar')).toBe('O');
    expect(initials('   ')).toBe('?');
  });
});
