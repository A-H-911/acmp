import { describe, it, expect } from 'vitest';
import {
  backlogByBucket,
  backlogByUrgency,
  nextScheduledMeeting,
  actionStatusCounts,
  daysOverdue,
  overdueBeyondThreshold,
  deferredAtLeastTwice,
  slaBreached,
  ESCALATION_THRESHOLD_DAYS,
} from './dashboardAgg';
import type { TopicSummary } from '../../api/topics';
import type { ActionSummary } from '../../api/actions';
import type { MeetingSummary } from '../../api/meetings';

const NOW = new Date('2026-06-24T09:00:00Z');

function topic(p: Partial<TopicSummary>): TopicSummary {
  return {
    id: 'id', key: 'TOP-2026-001', title: 't', type: 'ArchitectureDecision', status: 'Triage',
    urgency: 'Normal', scope: 'SingleStream', streams: ['identity'], ownerId: null, ownerName: null,
    priority: 0, timesDeferred: 0, ageDays: 1, slaBreached: false, createdAt: NOW.toISOString(), ...p,
  };
}
function action(p: Partial<ActionSummary>): ActionSummary {
  return {
    id: 'id', key: 'ACT-2026-001', title: { en: 'a', ar: 'a' }, status: 'Open', priority: 'Normal',
    ownerUserId: 'kc-1', ownerName: 'O', dueDate: null, isOverdue: false, progressPct: 0,
    sourceType: 'Topic', sourceId: 's', sourceKey: null, meetingKey: null, ...p,
  };
}
function meeting(p: Partial<MeetingSummary>): MeetingSummary {
  return {
    id: 'id', key: 'MTG-2026-001', title: 'm', scheduledStart: NOW.toISOString(), scheduledEnd: NOW.toISOString(),
    status: 'Scheduled', type: 'Regular', mode: 'InPerson', chairName: 'C', itemCount: 0, agendaStatus: 'Draft', ...p,
  };
}
const daysFromNow = (n: number) => new Date(NOW.getTime() + n * 86_400_000).toISOString();

describe('backlogByBucket (AC-064)', () => {
  it('folds canonical statuses into kanban buckets and totals', () => {
    const r = backlogByBucket([
      topic({ status: 'Triage' }), topic({ status: 'Submitted' }), // → triage x2
      topic({ status: 'Accepted' }),                                 // → accepted
      topic({ status: 'Scheduled' }),                                // → scheduled
      topic({ status: 'Rejected' }),                                 // → returned
      topic({ status: 'Decided' }),                                  // → done
    ]);
    expect(r.total).toBe(6);
    const by = Object.fromEntries(r.segments.map((s) => [s.bucket, s.count]));
    expect(by).toEqual({ triage: 2, accepted: 1, scheduled: 1, returned: 1, done: 1 });
  });

  it('returns zeroed segments for an empty backlog', () => {
    const r = backlogByBucket([]);
    expect(r.total).toBe(0);
    expect(r.segments.every((s) => s.count === 0)).toBe(true);
  });
});

describe('backlogByUrgency (AC-064)', () => {
  it('orders known urgencies most-severe first and counts each', () => {
    const r = backlogByUrgency([
      topic({ urgency: 'Normal' }), topic({ urgency: 'Critical' }), topic({ urgency: 'Normal' }), topic({ urgency: 'Urgent' }),
    ]);
    expect(r).toEqual([
      { urgency: 'Critical', count: 1 },
      { urgency: 'Urgent', count: 1 },
      { urgency: 'Normal', count: 2 },
    ]);
  });

  it('appends unknown urgency values instead of dropping them', () => {
    const r = backlogByUrgency([topic({ urgency: 'Low' })]);
    expect(r).toEqual([{ urgency: 'Low', count: 1 }]);
  });
});

describe('nextScheduledMeeting (AC-064)', () => {
  it('picks the soonest upcoming meeting, not list order', () => {
    const m = nextScheduledMeeting([
      meeting({ key: 'MTG-later', scheduledStart: daysFromNow(10) }),
      meeting({ key: 'MTG-soon', scheduledStart: daysFromNow(2) }),
    ], NOW);
    expect(m?.key).toBe('MTG-soon');
  });

  it('excludes past, completed and cancelled meetings; null when none upcoming', () => {
    expect(nextScheduledMeeting([meeting({ scheduledStart: daysFromNow(-1) })], NOW)).toBeNull();
    expect(nextScheduledMeeting([meeting({ status: 'Completed', scheduledStart: daysFromNow(3) })], NOW)).toBeNull();
    expect(nextScheduledMeeting([meeting({ status: 'Cancelled', scheduledStart: daysFromNow(3) })], NOW)).toBeNull();
    expect(nextScheduledMeeting([], NOW)).toBeNull();
  });
});

describe('actionStatusCounts (AC-064)', () => {
  it('buckets Open/InProgress/Blocked and counts overdue orthogonally', () => {
    const r = actionStatusCounts([
      action({ status: 'Open' }),
      action({ status: 'InProgress', isOverdue: true }),
      action({ status: 'Blocked' }),
      action({ status: 'Completed', isOverdue: true }), // overdue counts even when not open
    ]);
    expect(r).toEqual({ open: 1, inProgress: 1, blocked: 1, overdue: 2 });
  });
});

describe('daysOverdue / overdueBeyondThreshold (AC-065 + AC-066)', () => {
  it('computes whole days past due, zero when not overdue or undated', () => {
    expect(daysOverdue(daysFromNow(-5), true, NOW)).toBe(5);
    expect(daysOverdue(daysFromNow(-5), false, NOW)).toBe(0); // server says not overdue
    expect(daysOverdue(null, true, NOW)).toBe(0);
  });

  it('keeps only actions past the threshold, most-overdue first', () => {
    const list = overdueBeyondThreshold([
      action({ key: 'ACT-2', dueDate: daysFromNow(-(ESCALATION_THRESHOLD_DAYS + 1)), isOverdue: true }),
      action({ key: 'ACT-edge', dueDate: daysFromNow(-ESCALATION_THRESHOLD_DAYS), isOverdue: true }), // exactly at → excluded
      action({ key: 'ACT-1', dueDate: daysFromNow(-(ESCALATION_THRESHOLD_DAYS + 9)), isOverdue: true }),
    ], NOW);
    expect(list.map((a) => a.key)).toEqual(['ACT-1', 'ACT-2']);
  });
});

describe('deferredAtLeastTwice (AC-066)', () => {
  it('keeps topics deferred ≥2 times, most-deferred first', () => {
    const r = deferredAtLeastTwice([
      topic({ key: 'TOP-1', timesDeferred: 1 }),
      topic({ key: 'TOP-2', timesDeferred: 2 }),
      topic({ key: 'TOP-3', timesDeferred: 4 }),
    ]);
    expect(r.map((t) => t.key)).toEqual(['TOP-3', 'TOP-2']);
  });
});

describe('slaBreached (AC-065)', () => {
  it('keeps SLA-breached topics, worst-aged first', () => {
    const r = slaBreached([
      topic({ key: 'TOP-1', slaBreached: true, ageDays: 12 }),
      topic({ key: 'TOP-2', slaBreached: false, ageDays: 99 }),
      topic({ key: 'TOP-3', slaBreached: true, ageDays: 40 }),
    ]);
    expect(r.map((t) => t.key)).toEqual(['TOP-3', 'TOP-1']);
  });
});
