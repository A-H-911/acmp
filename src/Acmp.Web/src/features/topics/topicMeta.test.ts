import { describe, it, expect } from 'vitest';
import { bucketOf, moveAction, statusTone, initials } from './topicMeta';

describe('topicMeta', () => {
  it('maps each canonical status to its kanban bucket', () => {
    expect(bucketOf('Submitted')).toBe('triage');
    expect(bucketOf('Triage')).toBe('triage');
    expect(bucketOf('Accepted')).toBe('accepted');
    expect(bucketOf('Prepared')).toBe('accepted');
    expect(bucketOf('Scheduled')).toBe('scheduled');
    expect(bucketOf('Deferred')).toBe('returned');
    expect(bucketOf('Rejected')).toBe('returned');
    expect(bucketOf('Decided')).toBe('done');
  });

  it('classifies kanban moves to the only P5-legal transitions', () => {
    expect(moveAction('triage', 'accepted')).toBe('accept'); // accept (needs owner)
    expect(moveAction('triage', 'returned')).toBe('return'); // reject/defer (needs reason)
    expect(moveAction('accepted', 'returned')).toBe('return');
    expect(moveAction('accepted', 'accepted')).toBe('none');
    expect(moveAction('triage', 'scheduled')).toBe('illegal'); // scheduling needs a meeting (P6)
    expect(moveAction('triage', 'done')).toBe('illegal'); // no decide/close endpoint in P5
    expect(moveAction('accepted', 'triage')).toBe('illegal'); // no un-accept endpoint
  });

  it('maps status to a chip tone and builds initials', () => {
    expect(statusTone('Rejected')).toBe('danger');
    expect(statusTone('Scheduled')).toBe('scheduled');
    expect(initials('Omar Hassan')).toBe('OH');
  });
});
