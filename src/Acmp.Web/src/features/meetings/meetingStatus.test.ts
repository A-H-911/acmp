import { describe, it, expect } from 'vitest';
import { meetingTone, isConcluded } from './meetingStatus';

// Both views (list rows + calendar pills) read a meeting's colour/section from these maps,
// so every status arm must be pinned — a wrong tone or section is a real visual regression.
describe('meetingTone', () => {
  it('maps concluded states to success', () => {
    expect(meetingTone('Held')).toBe('success');
    expect(meetingTone('Closed')).toBe('success');
  });

  it('maps Cancelled to danger and InProgress to warn', () => {
    expect(meetingTone('Cancelled')).toBe('danger');
    expect(meetingTone('InProgress')).toBe('warn');
  });

  it('falls back to scheduled for Scheduled/draft/unknown/undefined', () => {
    expect(meetingTone('Scheduled')).toBe('scheduled');
    expect(meetingTone('Draft')).toBe('scheduled');
    expect(meetingTone(undefined)).toBe('scheduled');
  });
});

describe('isConcluded', () => {
  it('is true only for Held/Closed/Cancelled', () => {
    expect(isConcluded('Held')).toBe(true);
    expect(isConcluded('Closed')).toBe(true);
    expect(isConcluded('Cancelled')).toBe(true);
  });

  it('is false for upcoming/in-progress states (status-based, not clock-based)', () => {
    expect(isConcluded('Scheduled')).toBe(false);
    expect(isConcluded('InProgress')).toBe(false);
  });
});
