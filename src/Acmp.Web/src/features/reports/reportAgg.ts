/*
 * Pure aggregation reducers for the Risk & Dependency reports page (P10g).
 *
 * The page is a NO-REFERENCE composition that REUSES the card renderers/tokens of
 * "ACMP Dashboards & Reports.dc.html" (the full Reports IA — view-tabs, export, other
 * domains — is deferred to P12). Every number here is composed CLIENT-SIDE from three
 * existing REST reads (risks + dependencies + topics); there is no backend aggregation
 * endpoint (ADR-0001: the FE composing three public API results is not a module reading
 * another's tables — same pattern as the P10f graph UI). Right-sized for ≤20 users.
 *
 * Design↔behaviour reconciliations (visual SoT = the .dc.html; data SoT = the package):
 *  - Matrix cells are coloured by SEVERITY (likelihood×impact: ≤2 success, 3–4 warn, ≥6
 *    danger). This maps EXACTLY onto the design's 9 authored mcell zones and merges the
 *    High+Critical exposure bands into one danger colour — a matrix is a severity heat map,
 *    not an exposure-band legend.
 *  - Risk aggregates are scoped to ACTIVE risks (exclude Closed + Accepted) so the matrix
 *    "N active" sublabel and the tallies agree.
 *  - By-stream cards resolve each risk/dependency's stream via its LINKED TOPIC's streams
 *    (only Topic carries streams — FR-095 Topic-scope; OQ-047 inherit-from-topic model,
 *    adopted for reporting aggregation only). A risk/dep on a non-Topic subject/endpoint,
 *    or on a topic with no streams, has no stream to place → excluded from the by-stream
 *    tally (flagged in-UI). A topic in N streams counts under EACH of its N streams
 *    (intended per-stream semantics), so Σbars ≥ the distinct KPI — the KPI is the distinct
 *    contributing count, never the bar sum. Stream axis/label = the raw stream CODE
 *    (a localized stream name would need a Membership streams-master seam — deferred).
 */
import type { RiskSummary, RiskStatus, RiskLevel } from '../../api/risks';
import type { DependencySummary, DependencyKind } from '../../api/dependencies';
import type { TopicSummary } from '../../api/topics';

/** Shared-kernel status tone slugs (the design's --st-<zone>-* token families).
 *  `sched` matches the --st-sched-* group (the StatusChip 'scheduled' tone). */
export type Zone = 'success' | 'warn' | 'danger' | 'neutral' | 'info' | 'sched' | 'accent';

/** RiskLevel → its 1-based ordinal (docs/12 RiskExposureScale: severity = likelihood × impact). */
const LEVEL_VALUE: Record<RiskLevel, number> = { Low: 1, Medium: 2, High: 3 };

/** "Active" = a live risk: exclude the two terminal states (Closed, Accepted). */
const ACTIVE_STATUSES: readonly RiskStatus[] = ['Open', 'Mitigating', 'Escalated'];

export function isActiveRisk(r: RiskSummary): boolean {
  return ACTIVE_STATUSES.includes(r.status);
}

/** Severity band → heat zone. Verified to reproduce the design's 9 authored matrix cells. */
export function severityZone(severity: number): Zone {
  if (severity <= 2) return 'success';
  if (severity <= 4) return 'warn';
  return 'danger';
}

// ---- risk exposure matrix (3×3, count per cell, coloured by severity zone) ----

export interface MatrixCell {
  count: number;
  zone: Zone;
}
export interface RiskMatrix {
  /** Total active risks — the card's "N active" sublabel. */
  active: number;
  /** Rows top→bottom = impact High→Med→Low; cells left→right = probability Low→Med→High. */
  rows: { impact: RiskLevel; cells: MatrixCell[] }[];
}

const IMPACT_ROWS: readonly RiskLevel[] = ['High', 'Medium', 'Low'];
const PROB_COLS: readonly RiskLevel[] = ['Low', 'Medium', 'High'];

export function riskMatrix(risks: readonly RiskSummary[]): RiskMatrix {
  const active = risks.filter(isActiveRisk);
  const rows = IMPACT_ROWS.map((impact) => ({
    impact,
    cells: PROB_COLS.map((prob) => ({
      count: active.filter((r) => r.impact === impact && r.likelihood === prob).length,
      // Zone is a property of the cell's POSITION, not its count (empty cells are still tinted).
      zone: severityZone(LEVEL_VALUE[prob] * LEVEL_VALUE[impact]),
    })),
  }));
  return { active: active.length, rows };
}

// ---- stat tiles ----

export interface StatTile {
  value: number;
  labelKey: string;
  zone?: Zone;
  /** Raw label shown instead of a translated key (e.g. a stream code with no localized name). */
  label?: string;
  /** Unit appended to the value (e.g. '%' for a rate tile); omitted for plain counts. */
  suffix?: string;
}

export function riskStats(risks: readonly RiskSummary[]): StatTile[] {
  const n = (f: (r: RiskSummary) => boolean) => risks.filter(f).length;
  return [
    { value: n((r) => r.status === 'Open'), labelKey: 'reports.risk.open' },
    { value: n((r) => r.status === 'Mitigating'), labelKey: 'reports.risk.mitigating', zone: 'warn' },
    {
      value: n((r) => isActiveRisk(r) && (r.exposure === 'High' || r.exposure === 'Critical')),
      labelKey: 'reports.risk.highSeverity',
      zone: 'danger',
    },
    { value: n((r) => isActiveRisk(r) && r.exposure === 'Critical'), labelKey: 'reports.risk.critical', zone: 'danger' },
  ];
}

export function depStats(deps: readonly DependencySummary[]): StatTile[] {
  // The deps register excludes Removed by default, so `deps` is the non-removed set.
  const n = (f: (d: DependencySummary) => boolean) => deps.filter(f).length;
  return [
    { value: deps.length, labelKey: 'reports.dep.total' },
    { value: n((d) => d.status === 'Open'), labelKey: 'reports.dep.open' },
    { value: n((d) => d.isBlocker), labelKey: 'reports.dep.blocked', zone: 'danger' },
    { value: n((d) => d.status === 'Resolved'), labelKey: 'reports.dep.resolved', zone: 'success' },
  ];
}

// ---- bars ----

export interface Bar {
  key: string;
  /** i18n key (kind bars) — mutually exclusive with `label`. */
  labelKey?: string;
  /** Literal label (stream-code bars, shown raw — no localized stream name on the wire). */
  label?: string;
  count: number;
  /** 0–100, normalized to the largest bar in the set. */
  pct: number;
  zone: Zone;
}

const KIND_ORDER: readonly DependencyKind[] = ['DependsOn', 'BlockedBy', 'Blocks', 'RelatesTo'];

export function depsByKind(deps: readonly DependencySummary[]): Bar[] {
  const counts = KIND_ORDER.map((k) => deps.filter((d) => d.kind === k).length);
  const max = Math.max(1, ...counts);
  return KIND_ORDER.map((k, i) => ({
    key: k,
    labelKey: `deps.kind.${k}`,
    count: counts[i],
    pct: Math.round((counts[i] / max) * 100),
    zone: k === 'BlockedBy' || k === 'Blocks' ? 'danger' : 'info',
  }));
}

// ---- by-stream (three-way join over the linked Topic's streams) ----

/** topicId (GUID) → its stream codes. Built once from an includeClosed topics fetch. */
export function buildTopicStreamMap(topics: readonly TopicSummary[]): Map<string, string[]> {
  const map = new Map<string, string[]>();
  for (const t of topics) map.set(t.id, t.streams ?? []);
  return map;
}

export interface StreamBars {
  /** Distinct contributing risks/deps (resolved to ≥1 stream). Bars may sum higher (multi-stream). */
  kpi: number;
  bars: Bar[];
}

function toBars(perStream: Map<string, { count: number; zone: Zone }>): Bar[] {
  const entries = [...perStream.entries()].sort((a, b) => b[1].count - a[1].count || a[0].localeCompare(b[0]));
  const max = Math.max(1, ...entries.map(([, v]) => v.count));
  return entries.map(([code, v]) => ({
    key: code,
    label: code,
    count: v.count,
    pct: Math.round((v.count / max) * 100),
    zone: v.zone,
  }));
}

/** Active risks grouped by their linked Topic's streams; bar colour = max severity in that stream. */
export function risksByStream(risks: readonly RiskSummary[], topicStreams: Map<string, string[]>): StreamBars {
  const perStream = new Map<string, { count: number; maxSev: number }>();
  let contributing = 0;
  for (const r of risks) {
    if (!isActiveRisk(r) || r.subjectType !== 'Topic') continue;
    const streams = topicStreams.get(r.subjectId);
    if (!streams || streams.length === 0) continue;
    contributing++;
    const sev = LEVEL_VALUE[r.likelihood] * LEVEL_VALUE[r.impact];
    for (const code of streams) {
      const cur = perStream.get(code) ?? { count: 0, maxSev: 0 };
      perStream.set(code, { count: cur.count + 1, maxSev: Math.max(cur.maxSev, sev) });
    }
  }
  const zoned = new Map([...perStream].map(([k, v]) => [k, { count: v.count, zone: severityZone(v.maxSev) }]));
  return { kpi: contributing, bars: toBars(zoned) };
}

/** Blocker dependencies grouped by their Topic endpoints' streams; bar colour by count magnitude. */
export function blockedDepsByStream(deps: readonly DependencySummary[], topicStreams: Map<string, string[]>): StreamBars {
  const perStream = new Map<string, number>();
  let contributing = 0;
  for (const d of deps) {
    if (!d.isBlocker) continue;
    const codes = new Set<string>();
    if (d.fromType === 'Topic') (topicStreams.get(d.fromId) ?? []).forEach((c) => codes.add(c));
    if (d.toType === 'Topic') (topicStreams.get(d.toId) ?? []).forEach((c) => codes.add(c));
    if (codes.size === 0) continue;
    contributing++;
    for (const c of codes) perStream.set(c, (perStream.get(c) ?? 0) + 1);
  }
  const zoned = new Map(
    [...perStream].map(([k, count]) => [k, { count, zone: (count >= 2 ? 'danger' : 'warn') as Zone }]),
  );
  return { kpi: contributing, bars: toBars(zoned) };
}
