import { describe, it, expect } from 'vitest';
import {
  agingColumns, backlogStatusBars, decisionOutcomeStack, openItemsStats,
  throughputByStreamStats, supersedeStats, actionStatusBars, verificationStats,
  buildView, viewToCsv, applyStreamFilter, REPORT_VIEWS, type ReportData,
} from './reportViews';
import type { TopicSummary } from '../../api/topics';
import type { DecisionSummary } from '../../api/decisions';
import type { ActionSummary } from '../../api/actions';
import type { RiskSummary } from '../../api/risks';
import type { DependencySummary } from '../../api/dependencies';

const topic = (p: Partial<TopicSummary>): TopicSummary => ({
  id: 'i', key: 'TOP-1', title: 't', type: 'ArchitectureDecision', status: 'Triage', urgency: 'Normal',
  scope: 'SingleStream', streams: ['identity'], ownerId: null, ownerName: null, priority: 0,
  timesDeferred: 0, ageDays: 1, slaBreached: false, createdAt: '2026-01-01', ...p,
});
const decision = (p: Partial<DecisionSummary>): DecisionSummary => ({
  id: 'i', key: 'DECN-1', topicId: 't', meetingId: null, outcome: 'Approved', status: 'Issued',
  title: { en: 'd', ar: 'd' }, issuedAt: '2026-06-01', ...p,
});
const action = (p: Partial<ActionSummary>): ActionSummary => ({
  id: 'i', key: 'ACT-1', title: { en: 'a', ar: 'a' }, status: 'Open', priority: 'Normal', ownerUserId: 'u',
  ownerName: 'o', dueDate: null, isOverdue: false, progressPct: 0, sourceType: 'Topic', sourceId: 's',
  sourceKey: null, meetingKey: null, ...p,
});
const risk = (p: Partial<RiskSummary>): RiskSummary => ({
  id: 'i', key: 'RSK-1', title: { en: 'r', ar: 'r' }, status: 'Open', likelihood: 'High', impact: 'High',
  severity: 9, exposure: 'Critical', ownerUserId: '', ownerName: '', subjectType: 'Topic', subjectId: 't1', subjectKey: 'TOP-1', ...p,
});
const dep = (p: Partial<DependencySummary>): DependencySummary => ({
  id: 'i', key: 'DPN-1', fromType: 'Topic', fromId: 't1', fromKey: 'TOP-1', fromTitle: 'a', toType: 'Topic',
  toId: 't2', toKey: 'TOP-2', toTitle: 'b', kind: 'Blocks', status: 'Open', isBlocker: true, ...p,
});

describe('agingColumns', () => {
  it('bins topics by current age into 0-7 / 8-14 / 15-30 / 30+', () => {
    const cols = agingColumns([topic({ ageDays: 3 }), topic({ ageDays: 10 }), topic({ ageDays: 20 }), topic({ ageDays: 45 }), topic({ ageDays: 60 })]);
    expect(cols.map((c) => [c.label, c.value])).toEqual([['0–7', 1], ['8–14', 1], ['15–30', 1], ['30+', 2]]); // en-dash labels (design)
    expect(cols[3].zone).toBe('danger');
  });
});

describe('backlogStatusBars', () => {
  it('maps kanban buckets to bars with the scheduled tone slug', () => {
    const bars = backlogStatusBars([topic({ status: 'Scheduled' }), topic({ status: 'Triage' })]);
    const sched = bars.find((b) => b.key === 'scheduled')!;
    expect(sched.zone).toBe('sched');
    expect(bars.find((b) => b.key === 'triage')!.count).toBe(1);
  });
});

describe('decisionOutcomeStack', () => {
  it('buckets outcomes into approved/conditional/rejected/other over issued decisions', () => {
    const segs = decisionOutcomeStack([
      decision({ outcome: 'Approved' }), decision({ outcome: 'ConditionallyApproved' }),
      decision({ outcome: 'Rejected' }), decision({ outcome: 'Deferred' }),
      decision({ status: 'Draft', outcome: 'Approved' }), // drafts excluded
    ]);
    expect(segs.map((s) => [s.key, s.value])).toEqual([['approved', 1], ['conditional', 1], ['rejected', 1], ['other', 1]]);
    // Approved + Conditional share the green "approved" family (design SoT).
    expect(segs.find((s) => s.key === 'conditional')!.zone).toBe('success');
  });
});

describe('applyStreamFilter', () => {
  it('scopes topics, decisions, actions, risks and deps to the stream via each linked topic', () => {
    const idT = topic({ id: 'idT', streams: ['identity'] });
    const payT = topic({ id: 'payT', streams: ['payments'] });
    const d: ReportData = {
      activeTopics: [idT, payT], allTopics: [idT, payT],
      decisions: [decision({ topicId: 'idT' }), decision({ topicId: 'payT' })],
      actions: [action({ sourceType: 'Topic', sourceId: 'idT' }), action({ sourceType: 'Topic', sourceId: 'payT' }), action({ sourceType: 'Meeting', sourceId: 'idT' })],
      risks: [risk({ subjectType: 'Topic', subjectId: 'idT' }), risk({ subjectType: 'Topic', subjectId: 'payT' })],
      deps: [dep({ fromType: 'Topic', fromId: 'idT', toType: 'Topic', toId: 'x' }), dep({ fromType: 'Topic', fromId: 'payT', toType: 'Topic', toId: 'y' })],
    };
    const out = applyStreamFilter(d, 'identity');
    expect(out.activeTopics.map((tp) => tp.id)).toEqual(['idT']);
    expect(out.decisions).toHaveLength(1); // the payments-linked decision drops
    expect(out.actions).toHaveLength(1); // payments action + the non-Topic (Meeting) action both drop
    expect(out.risks).toHaveLength(1);
    expect(out.deps).toHaveLength(1);
  });
  it('returns the same object untouched for "all"', () => {
    const d: ReportData = { activeTopics: [], allTopics: [], decisions: [], actions: [], risks: [], deps: [] };
    expect(applyStreamFilter(d, 'all')).toBe(d);
  });
});

describe('openItemsStats', () => {
  it('counts backlog, pending decisions, open actions and active risks', () => {
    const d: ReportData = {
      activeTopics: [topic({}), topic({})], allTopics: [], decisions: [decision({ status: 'Draft' })],
      actions: [action({ status: 'Open' }), action({ status: 'Completed' })], risks: [risk({ status: 'Open' }), risk({ status: 'Closed' })], deps: [],
    };
    const s = openItemsStats(d);
    expect(s.map((x) => x.value)).toEqual([2, 1, 1, 1]); // backlog, pending, open actions, active risks
  });
});

describe('throughputByStreamStats', () => {
  it('counts Decided/Closed topics per stream, top 4', () => {
    const s = throughputByStreamStats([
      topic({ status: 'Decided', streams: ['identity'] }), topic({ status: 'Closed', streams: ['identity', 'payments'] }), topic({ status: 'Triage', streams: ['identity'] }),
    ]);
    expect(s.find((x) => x.label === 'identity')!.value).toBe(2);
    expect(s.find((x) => x.label === 'payments')!.value).toBe(1);
  });
});

describe('supersedeStats / actionStatusBars / verificationStats', () => {
  it('supersede counts superseded vs issued and total traceable', () => {
    const s = supersedeStats([decision({ status: 'Superseded' }), decision({ status: 'Issued' }), decision({ status: 'Issued' })]);
    expect(s.map((x) => x.value)).toEqual([1, 2, 0, 3]);
  });
  it('actionStatusBars counts Open/InProgress/Blocked/Verified', () => {
    const b = actionStatusBars([action({ status: 'Open' }), action({ status: 'InProgress' }), action({ status: 'Verified' })]);
    expect(Object.fromEntries(b.map((x) => [x.key, x.count]))).toEqual({ Open: 1, InProgress: 1, Blocked: 0, Verified: 1 });
  });
  it('verificationStats computes the verified rate as a percentage tile', () => {
    const s = verificationStats([action({ status: 'Verified' }), action({ status: 'Verified' }), action({ status: 'Completed' })]);
    expect(s[0].value).toBe(67); // 2 verified / 3 closed
    expect(s[0].suffix).toBe('%');
  });
});

describe('buildView', () => {
  const data: ReportData = {
    activeTopics: [topic({})], allTopics: [topic({})], decisions: [decision({})],
    actions: [action({})], risks: [risk({})], deps: [dep({})],
  };
  it('returns four cards for every view', () => {
    for (const v of REPORT_VIEWS) expect(buildView(v, data)).toHaveLength(4);
  });
  it('marks the time-series cards as honest-empty trend', () => {
    const exec = buildView('executive', data);
    expect(exec.find((c) => c.key === 'throughput')).toMatchObject({ kind: 'empty', reason: 'trend' });
    const committee = buildView('committee', data);
    expect(committee.find((c) => c.key === 'attendance')).toMatchObject({ kind: 'empty', reason: 'seam' });
  });
});

describe('viewToCsv', () => {
  it('flattens cards to rows and self-documents empty cards', () => {
    const t = (k: string) => k;
    const csv = viewToCsv(buildView('executive', {
      ...{ activeTopics: [topic({})], allTopics: [], decisions: [decision({ outcome: 'Approved' })], actions: [action({})], risks: [risk({})], deps: [] },
    }), t);
    expect(csv.split('\n')[0]).toBe('Card,Metric,Value');
    expect(csv).toContain('reports.reason.trend'); // the throughput trend card documents itself
  });
});
