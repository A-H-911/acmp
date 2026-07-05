/*
 * Reports view assembly (P12-PR3) — the full Reports IA over the six view-tabs
 * (executive / committee / stream / decisions / actions / audit).
 *
 * Every card is composed CLIENT-SIDE from existing REST reads (ADR-0022: no server
 * read-model, no columnstore, no chart library — CSS primitives). This module is the
 * pure, directly-tested layer; the page just renders the ReportCard[] it returns.
 *
 * Two honest-empty categories, flagged in-UI and in the progress log (guardrail #11):
 *  - `trend` — per-week / per-quarter time series (throughput, decisions-per-quarter,
 *    created-vs-closed, overdue trend, audit-event volume). The app keeps no time series
 *    (ADR-0022 defers it to PH-3); a snapshot read can't reconstruct one.
 *  - `seam` — data not on the summary DTOs (meeting attendance is not on MeetingSummary;
 *    per-ballot vote attribution is not on VoteSummary). Needs a read that doesn't exist.
 */
import type { TopicSummary } from '../../api/topics';
import type { ActionSummary } from '../../api/actions';
import type { DecisionSummary, DecisionOutcome } from '../../api/decisions';
import type { RiskSummary } from '../../api/risks';
import type { DependencySummary } from '../../api/dependencies';
import { backlogByBucket } from '../dashboard/dashboardAgg';
import {
  type Zone, type Bar, type StatTile, type RiskMatrix,
  riskMatrix, risksByStream, blockedDepsByStream, buildTopicStreamMap, isActiveRisk,
} from './reportAgg';

export type ReportView = 'executive' | 'committee' | 'stream' | 'decisions' | 'actions' | 'audit';
export const REPORT_VIEWS: readonly ReportView[] = ['executive', 'committee', 'stream', 'decisions', 'actions', 'audit'];

export interface Column { key: string; label: string; value: number; pct: number; zone: Zone; }
export interface Segment { key: string; labelKey: string; value: number; pct: number; zone: Zone; }

interface CardMeta {
  key: string;
  titleKey: string;
  subKey: string;
  subVars?: Record<string, number>;
  kpi?: string;
  kpiSubKey?: string;
  to?: string; // drill-down target
}
export type ReportCard = CardMeta & (
  | { kind: 'bars'; bars: Bar[] }
  | { kind: 'columns'; cols: Column[] }
  | { kind: 'stack'; segments: Segment[] }
  | { kind: 'stat'; stats: StatTile[] }
  | { kind: 'matrix'; matrix: RiskMatrix }
  | { kind: 'empty'; reason: 'trend' | 'seam' }
);

export interface ReportData {
  activeTopics: TopicSummary[];
  allTopics: TopicSummary[];
  decisions: DecisionSummary[];
  actions: ActionSummary[];
  risks: RiskSummary[];
  deps: DependencySummary[];
}

// ---- primitives ----

const pctOf = (v: number, max: number) => Math.round((v / Math.max(1, max)) * 100);

/** Per-rank colour cycle for stream bars (matches the design's multi-stream palette). */
const STREAM_PALETTE: readonly Zone[] = ['info', 'sched', 'success', 'warn', 'danger'];

/** Group a topic set by each topic's stream codes (a topic in N streams counts N times),
 *  optionally filtered to a predicate; returns count bars, largest first, colour-cycled per rank. */
function streamBars(topics: readonly TopicSummary[], keep: (t: TopicSummary) => boolean): Bar[] {
  const per = new Map<string, number>();
  for (const t of topics) {
    if (!keep(t)) continue;
    for (const code of t.streams ?? []) per.set(code, (per.get(code) ?? 0) + 1);
  }
  const entries = [...per.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]));
  const max = Math.max(1, ...entries.map(([, v]) => v));
  return entries.map(([code, v], i) => ({ key: code, label: code, count: v, pct: pctOf(v, max), zone: STREAM_PALETTE[i % STREAM_PALETTE.length] }));
}

/** Stream filter — narrows every set to the selected stream, resolving decisions/actions/risks/deps
 *  through their linked Topic. A record whose topic isn't in the stream (or that has no topic) drops
 *  out, so the whole view is stream-scoped — no card silently stays committee-wide. */
export function applyStreamFilter(d: ReportData, stream: string): ReportData {
  if (stream === 'all') return d;
  const inStream = (tp: TopicSummary) => (tp.streams ?? []).includes(stream);
  const topicIds = new Set(d.allTopics.filter(inStream).map((tp) => tp.id));
  return {
    activeTopics: d.activeTopics.filter(inStream),
    allTopics: d.allTopics.filter(inStream),
    decisions: d.decisions.filter((dec) => topicIds.has(dec.topicId)),
    actions: d.actions.filter((a) => a.sourceType === 'Topic' && topicIds.has(a.sourceId)),
    risks: d.risks.filter((r) => r.subjectType === 'Topic' && topicIds.has(r.subjectId)),
    deps: d.deps.filter((x) => (x.fromType === 'Topic' && topicIds.has(x.fromId)) || (x.toType === 'Topic' && topicIds.has(x.toId))),
  };
}

// ---- aging histogram (columns) — REAL: a histogram of CURRENT ages, not a time series ----

const AGING_BUCKETS: { key: string; label: string; test: (d: number) => boolean; zone: Zone }[] = [
  { key: '0-7', label: '0–7', test: (d) => d <= 7, zone: 'success' },
  { key: '8-14', label: '8–14', test: (d) => d > 7 && d <= 14, zone: 'info' },
  { key: '15-30', label: '15–30', test: (d) => d > 14 && d <= 30, zone: 'warn' },
  { key: '30+', label: '30+', test: (d) => d > 30, zone: 'danger' },
];

export function agingColumns(topics: readonly TopicSummary[]): Column[] {
  const counts = AGING_BUCKETS.map((b) => topics.filter((t) => b.test(t.ageDays)).length);
  const max = Math.max(1, ...counts);
  return AGING_BUCKETS.map((b, i) => ({ key: b.key, label: b.label, value: counts[i], pct: pctOf(counts[i], max), zone: b.zone }));
}

// ---- backlog by status (bars) ----

const STATUS_BAR_ZONE: Record<string, Zone> = { triage: 'neutral', accepted: 'info', scheduled: 'sched', returned: 'warn', done: 'success' };

export function backlogStatusBars(topics: readonly TopicSummary[]): Bar[] {
  const { segments } = backlogByBucket(topics);
  const max = Math.max(1, ...segments.map((s) => s.count));
  return segments.map((s) => ({ key: s.bucket, labelKey: `reports.status.${s.bucket}`, count: s.count, pct: pctOf(s.count, max), zone: STATUS_BAR_ZONE[s.bucket] }));
}

// ---- decision outcome mix (stack) ----

const APPROVED: DecisionOutcome = 'Approved';
const CONDITIONAL: DecisionOutcome = 'ConditionallyApproved';
const REJECTED: DecisionOutcome = 'Rejected';

export function decisionOutcomeStack(decisions: readonly DecisionSummary[]): Segment[] {
  const issued = decisions.filter((d) => d.status !== 'Draft');
  const approved = issued.filter((d) => d.outcome === APPROVED).length;
  const conditional = issued.filter((d) => d.outcome === CONDITIONAL).length;
  const rejected = issued.filter((d) => d.outcome === REJECTED).length;
  const other = issued.length - approved - conditional - rejected;
  const total = issued.length;
  const raw: [string, number, Zone][] = [
    // Approved + Conditional are one "approved" family → both green (design SoT).
    ['approved', approved, 'success'], ['conditional', conditional, 'success'],
    ['rejected', rejected, 'danger'], ['other', other, 'neutral'],
  ];
  return raw.map(([key, value, zone]) => ({ key, labelKey: `reports.outcome.${key}`, value, pct: pctOf(value, total), zone }));
}

// ---- stat sets ----

const stat = (value: number, labelKey: string, zone?: Zone): StatTile => ({ value, labelKey, ...(zone ? { zone } : {}) });

export function openItemsStats(d: ReportData): StatTile[] {
  return [
    stat(d.activeTopics.length, 'reports.open.backlog'),
    stat(d.decisions.filter((x) => x.status === 'Draft').length, 'reports.open.pendingDecisions', 'warn'),
    stat(d.actions.filter((a) => a.status === 'Open' || a.status === 'InProgress' || a.status === 'Blocked').length, 'reports.open.openActions'),
    stat(d.risks.filter(isActiveRisk).length, 'reports.open.activeRisks', 'danger'),
  ];
}

export function throughputByStreamStats(allTopics: readonly TopicSummary[]): StatTile[] {
  const per = new Map<string, number>();
  for (const t of allTopics) {
    if (t.status !== 'Decided' && t.status !== 'Closed') continue;
    for (const code of t.streams ?? []) per.set(code, (per.get(code) ?? 0) + 1);
  }
  return [...per.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0])).slice(0, 4)
    .map(([code, v]) => ({ value: v, labelKey: '', label: code }));
}

export function supersedeStats(decisions: readonly DecisionSummary[]): StatTile[] {
  const superseded = decisions.filter((d) => d.status === 'Superseded').length;
  const issued = decisions.filter((d) => d.status === 'Issued').length;
  return [
    stat(superseded, 'reports.supersede.superseded', 'warn'),
    stat(issued, 'reports.supersede.active', 'success'),
    stat(0, 'reports.supersede.disputed'),
    stat(decisions.length, 'reports.supersede.tracked', 'success'),
  ];
}

export function actionStatusBars(actions: readonly ActionSummary[]): Bar[] {
  const rows: [string, (a: ActionSummary) => boolean, Zone][] = [
    ['Open', (a) => a.status === 'Open', 'neutral'],
    ['InProgress', (a) => a.status === 'InProgress', 'accent'],
    ['Blocked', (a) => a.status === 'Blocked', 'danger'],
    ['Verified', (a) => a.status === 'Verified', 'success'],
  ];
  const counts = rows.map(([, f]) => actions.filter(f).length);
  const max = Math.max(1, ...counts);
  return rows.map(([k, , zone], i) => ({ key: k, labelKey: `reports.action.${k}`, count: counts[i], pct: pctOf(counts[i], max), zone }));
}

export function verificationStats(actions: readonly ActionSummary[]): StatTile[] {
  const verified = actions.filter((a) => a.status === 'Verified').length;
  const completed = actions.filter((a) => a.status === 'Completed').length;
  const closed = verified + completed;
  return [
    { value: closed === 0 ? 0 : Math.round((verified / closed) * 100), labelKey: 'reports.verify.rate', zone: 'success', suffix: '%' },
    stat(closed, 'reports.verify.closed'),
    stat(completed, 'reports.verify.awaiting', 'warn'),
    stat(actions.filter((a) => a.isOverdue).length, 'reports.verify.overdue', 'danger'),
  ];
}

export function approvalCoverageStats(decisions: readonly DecisionSummary[]): StatTile[] {
  const issued = decisions.filter((d) => d.status === 'Issued' || d.status === 'Superseded').length;
  return [
    stat(issued, 'reports.audit.approved', 'success'),
    stat(0, 'reports.audit.missing'),
    stat(decisions.filter((d) => d.outcome === 'Escalated').length, 'reports.audit.overrides'),
    stat(decisions.length, 'reports.audit.attributed', 'success'),
  ];
}

export function immutableRecordsStats(d: ReportData): StatTile[] {
  return [
    stat(d.decisions.filter((x) => x.status !== 'Draft').length, 'reports.audit.decisions'),
    stat(d.decisions.filter((x) => x.status === 'Superseded').length, 'reports.audit.superseded', 'warn'),
    stat(d.risks.filter((r) => r.status === 'Closed').length, 'reports.audit.risksClosed'),
    stat(d.deps.filter((x) => x.status === 'Resolved').length, 'reports.audit.depsResolved'),
  ];
}

// ---- view assembly ----

const trend = (key: string, titleKey: string, subKey: string): ReportCard => ({ key, titleKey, subKey, kind: 'empty', reason: 'trend' });
const seam = (key: string, titleKey: string, subKey: string): ReportCard => ({ key, titleKey, subKey, kind: 'empty', reason: 'seam' });

export function buildView(view: ReportView, d: ReportData): ReportCard[] {
  const streamMap = buildTopicStreamMap(d.allTopics);
  switch (view) {
    case 'executive': {
      const outcomes = decisionOutcomeStack(d.decisions);
      const issued = outcomes.reduce((s, x) => s + x.value, 0);
      const approvedFamily = outcomes.filter((s) => s.key === 'approved' || s.key === 'conditional').reduce((s, x) => s + x.value, 0);
      const exposure = riskMatrix(d.risks);
      const highSev = d.risks.filter((r) => isActiveRisk(r) && (r.exposure === 'High' || r.exposure === 'Critical')).length;
      return [
        { key: 'outcomes', titleKey: 'reports.card.outcomes', subKey: 'reports.sub.outcomes', kpi: issued > 0 ? `${pctOf(approvedFamily, issued)}%` : '—', kpiSubKey: 'reports.kpi.approvedRate', kind: 'stack', segments: outcomes, to: '/decisions' },
        trend('throughput', 'reports.card.throughput', 'reports.sub.throughput'),
        { key: 'exposure', titleKey: 'reports.card.riskExposure.title', subKey: 'reports.activeCount', subVars: { count: exposure.active }, kpi: String(highSev), kpiSubKey: 'reports.kpi.highSeverity', kind: 'matrix', matrix: exposure, to: '/risks' },
        { key: 'open', titleKey: 'reports.card.openItems', subKey: 'reports.sub.openItems', kind: 'stat', stats: openItemsStats(d) },
      ];
    }
    case 'committee':
      return [
        { key: 'byStatus', titleKey: 'reports.card.backlogStatus', subKey: 'reports.activeTopics', subVars: { count: d.activeTopics.length }, kind: 'bars', bars: backlogStatusBars(d.activeTopics), to: '/backlog' },
        { key: 'aging', titleKey: 'reports.card.aging', subKey: 'reports.sub.aging', kpi: String(d.activeTopics.filter((t) => t.ageDays > 30).length), kpiSubKey: 'reports.aging.over30', kind: 'columns', cols: agingColumns(d.activeTopics), to: '/backlog' },
        seam('attendance', 'reports.card.attendance', 'reports.sub.attendance'),
        { key: 'outcomes', titleKey: 'reports.card.outcomes', subKey: 'reports.sub.outcomes', kind: 'stack', segments: decisionOutcomeStack(d.decisions), to: '/decisions' },
      ];
    case 'stream':
      return [
        { key: 'backlogStream', titleKey: 'reports.card.backlogStream', subKey: 'reports.sub.backlogStream', kind: 'bars', bars: streamBars(d.activeTopics, () => true), to: '/backlog' },
        { key: 'riskStream', titleKey: 'reports.card.riskByStream.title', subKey: 'reports.streamCount', subVars: { count: risksByStream(d.risks, streamMap).kpi }, kind: 'bars', bars: risksByStream(d.risks, streamMap).bars, to: '/risks' },
        { key: 'depStream', titleKey: 'reports.card.blockedByStream.title', subKey: 'reports.blockedCount', subVars: { count: blockedDepsByStream(d.deps, streamMap).kpi }, kind: 'bars', bars: blockedDepsByStream(d.deps, streamMap).bars, to: '/dependencies' },
        { key: 'throughputStream', titleKey: 'reports.card.throughputStream', subKey: 'reports.sub.throughputStream', kind: 'stat', stats: throughputByStreamStats(d.allTopics), to: '/backlog' },
      ];
    case 'decisions':
      return [
        trend('perQuarter', 'reports.card.perQuarter', 'reports.sub.perQuarter'),
        { key: 'outcomes', titleKey: 'reports.card.outcomeMix', subKey: 'reports.sub.outcomes', kind: 'stack', segments: decisionOutcomeStack(d.decisions), to: '/decisions' },
        trend('turnaround', 'reports.card.turnaround', 'reports.sub.turnaround'),
        { key: 'supersede', titleKey: 'reports.card.supersede', subKey: 'reports.sub.supersede', kind: 'stat', stats: supersedeStats(d.decisions), to: '/decisions' },
      ];
    case 'actions':
      return [
        trend('createdClosed', 'reports.card.createdClosed', 'reports.sub.createdClosed'),
        { key: 'byStatus', titleKey: 'reports.card.actionStatus', subKey: 'reports.sub.actionStatus', kind: 'bars', bars: actionStatusBars(d.actions), to: '/actions' },
        trend('overdueTrend', 'reports.card.overdueTrend', 'reports.sub.overdueTrend'),
        { key: 'verify', titleKey: 'reports.card.verification', subKey: 'reports.sub.verification', kind: 'stat', stats: verificationStats(d.actions), to: '/actions' },
      ];
    case 'audit':
      return [
        { key: 'coverage', titleKey: 'reports.card.coverage', subKey: 'reports.sub.coverage', kind: 'stat', stats: approvalCoverageStats(d.decisions), to: '/decisions' },
        seam('voteAttribution', 'reports.card.voteAttribution', 'reports.sub.voteAttribution'),
        trend('eventVolume', 'reports.card.eventVolume', 'reports.sub.eventVolume'),
        { key: 'immutable', titleKey: 'reports.card.immutable', subKey: 'reports.sub.immutable', kind: 'stat', stats: immutableRecordsStats(d), to: '/decisions' },
      ];
  }
}

// ---- CSV export (current view → flat rows) ----

/** Flatten the current view's cards to CSV. Trend/seam cards contribute a single note row so the
 *  export self-documents what wasn't available, rather than silently dropping the card. */
export function viewToCsv(cards: ReportCard[], t: (k: string, v?: Record<string, number>) => string): string {
  const rows: string[][] = [['Card', 'Metric', 'Value']];
  for (const c of cards) {
    const title = t(c.titleKey);
    if (c.kind === 'bars') c.bars.forEach((b) => rows.push([title, b.label ?? t(b.labelKey!), String(b.count)]));
    else if (c.kind === 'columns') c.cols.forEach((col) => rows.push([title, col.label, String(col.value)]));
    else if (c.kind === 'stack') c.segments.forEach((s) => rows.push([title, t(s.labelKey), String(s.value)]));
    else if (c.kind === 'stat') c.stats.forEach((s) => rows.push([title, (s as StatTile & { label?: string }).label ?? t(s.labelKey), String(s.value)]));
    else if (c.kind === 'matrix') c.matrix.rows.forEach((r) => r.cells.forEach((cell, i) => rows.push([title, `${r.impact}×${['Low', 'Med', 'High'][i]}`, String(cell.count)])));
    else rows.push([title, t(`reports.reason.${c.reason}`), '—']);
  }
  return rows.map((r) => r.map((cell) => (/[",\n]/.test(cell) ? `"${cell.replace(/"/g, '""')}"` : cell)).join(',')).join('\n');
}
