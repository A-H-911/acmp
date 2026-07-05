/*
 * Reports (P12-PR3) — the full Reports IA over six view-tabs, composed CLIENT-SIDE from
 * existing REST reads (ADR-0022: no server read-model, no chart library — CSS primitives).
 * Card assembly + CSV live in the pure, tested `reportViews`; this file is the shell:
 * view-tabs, the (real) Stream filter, CSV export, data-states, and the renderers.
 *
 * Reconciliations vs "ACMP Dashboards & Reports.dc.html" (guardrail #11/#14, flagged in the
 * progress log): the design's "DATA: Live/Loading/Empty" state tabs are a PREVIEW affordance —
 * not built (real state = query status). The "Period: This quarter" filter is dishonest without
 * a time series — omitted; only the Stream filter (which does real work) ships. Trend cards
 * (per-week/quarter series the app doesn't keep) and seam cards (attendance / per-ballot vote
 * attribution — not on the summary DTOs) render an honest "Phase 3" empty state.
 */
import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useBacklog, type TopicSummary } from '../../api/topics';
import { useDecisionsRegister } from '../../api/decisions';
import { useActionsRegister } from '../../api/actions';
import { useRisksRegister } from '../../api/risks';
import { useDependenciesRegister } from '../../api/dependencies';
import { ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import type { Bar, StatTile, RiskMatrix, MatrixCell } from './reportAgg';
import {
  REPORT_VIEWS, type ReportView, type ReportCard, type ReportData, type Column, type Segment,
  buildView, viewToCsv,
} from './reportViews';
import './reports.css';

const ALL = 500;

export function ReportsPage() {
  const { t } = useTranslation();
  const [view, setView] = useState<ReportView>('executive');
  const [stream, setStream] = useState('all');

  const activeTopics = useBacklog({ pageSize: ALL });
  const allTopics = useBacklog({ includeClosed: true, pageSize: ALL });
  const decisions = useDecisionsRegister({});
  const actions = useActionsRegister({ pageSize: ALL });
  const risks = useRisksRegister({ pageSize: ALL });
  const deps = useDependenciesRegister({ pageSize: ALL });

  const reads = [activeTopics, allTopics, decisions, actions, risks, deps];
  const isLoading = reads.some((r) => r.isLoading);
  const isError = reads.some((r) => r.isError);
  const ready = activeTopics.data && allTopics.data && decisions.data && actions.data && risks.data && deps.data;

  const streamOptions = useMemo(() => {
    const set = new Set<string>();
    (allTopics.data?.items ?? []).forEach((tp) => (tp.streams ?? []).forEach((s) => set.add(s)));
    return [...set].sort();
  }, [allTopics.data]);

  const cards = useMemo<ReportCard[] | null>(() => {
    if (!ready) return null;
    const base: ReportData = {
      activeTopics: activeTopics.data!.items,
      allTopics: allTopics.data!.items,
      decisions: decisions.data!,
      actions: actions.data!.items,
      risks: risks.data!.items,
      deps: deps.data!.items,
    };
    return buildView(view, applyStreamFilter(base, stream));
  }, [ready, view, stream, activeTopics.data, allTopics.data, decisions.data, actions.data, risks.data, deps.data]);

  const retry = () => reads.forEach((r) => void r.refetch());

  const onExport = () => {
    if (!cards) return;
    const csv = viewToCsv(cards, t);
    const blob = new Blob([`﻿${csv}`], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `acmp-report-${view}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const isEmpty = ready && (activeTopics.data!.total ?? 0) === 0 && decisions.data!.length === 0
    && (risks.data!.total ?? 0) === 0 && (deps.data!.total ?? 0) === 0 && actions.data!.total === 0;

  return (
    <section className="page rpt">
      <header className="rpt-toolbar">
        <div className="rpt-head">
          <h1 className="page-title">{t('reports.title')}</h1>
          <p className="rpt-lead">{t(`reports.viewSub.${view}`)}</p>
        </div>
        <button type="button" className="rpt-export" onClick={onExport} disabled={!cards}>
          <Icon name="download" size={15} aria-hidden />
          {t('reports.export')}
        </button>
      </header>

      <div className="rpt-tabs" role="tablist" aria-label={t('reports.viewsLabel')}>
        {REPORT_VIEWS.map((v) => (
          <button
            key={v}
            type="button"
            role="tab"
            aria-selected={view === v}
            className={`rpt-tab${view === v ? ' active' : ''}`}
            onClick={() => setView(v)}
          >
            {t(`reports.view.${v}`)}
          </button>
        ))}
      </div>

      {streamOptions.length > 0 && (
        <div className="rpt-filters">
          <label className="rpt-filter">
            <span className="rpt-filter-l">{t('reports.filter.stream')}</span>
            <select value={stream} onChange={(e) => setStream(e.target.value)}>
              <option value="all">{t('reports.filter.allStreams')}</option>
              {streamOptions.map((s) => <option key={s} value={s}>{s}</option>)}
            </select>
          </label>
        </div>
      )}

      {isLoading ? (
        <ReportsSkeleton />
      ) : isError ? (
        <ErrorState title={t('reports.error.title')} body={t('reports.error.body')} onRetry={retry} />
      ) : isEmpty || !cards ? (
        <EmptyState icon="reports" title={t('reports.empty.title')} body={t('reports.empty.body')} />
      ) : (
        <div className="rpt-grid">
          {cards.map((c) => <ReportCardView key={c.key} card={c} />)}
        </div>
      )}
    </section>
  );
}

/** Stream filter — narrows the topic sets, and the risk/dep sets via their linked Topic.
 *  Decisions/actions carry no stream on their summary DTOs, so they stay committee-wide (flagged). */
function applyStreamFilter(d: ReportData, stream: string): ReportData {
  if (stream === 'all') return d;
  const inStream = (tp: TopicSummary) => (tp.streams ?? []).includes(stream);
  const topicIds = new Set(d.allTopics.filter(inStream).map((tp) => tp.id));
  return {
    ...d,
    activeTopics: d.activeTopics.filter(inStream),
    allTopics: d.allTopics.filter(inStream),
    risks: d.risks.filter((r) => r.subjectType === 'Topic' && topicIds.has(r.subjectId)),
    deps: d.deps.filter((x) => (x.fromType === 'Topic' && topicIds.has(x.fromId)) || (x.toType === 'Topic' && topicIds.has(x.toId))),
  };
}

function ReportCardView({ card }: { card: ReportCard }) {
  const { t } = useTranslation();
  return (
    <section className="rpt-card">
      <div className="rpt-card-head">
        <div>
          <h2 className="rpt-card-title">{t(card.titleKey)}</h2>
          <div className="rpt-card-sub">{t(card.subKey, card.subVars)}</div>
        </div>
        {card.to && (
          <Link className="rpt-drill" to={card.to} aria-label={t('reports.drill', { title: t(card.titleKey) })}>
            <Icon name="arrowUpRight" size={15} aria-hidden />
          </Link>
        )}
      </div>

      {card.kpi != null && (
        <div className="rpt-kpi">
          <span className="rpt-kpi-v">{card.kpi}</span>
          {card.kpiSubKey && <span className="rpt-kpi-l">{t(card.kpiSubKey)}</span>}
        </div>
      )}

      <div className="rpt-card-body">
        {card.kind === 'bars' && <BarsView bars={card.bars} />}
        {card.kind === 'columns' && <ColumnsView cols={card.cols} />}
        {card.kind === 'stack' && <StackView segments={card.segments} />}
        {card.kind === 'stat' && <StatGrid stats={card.stats} />}
        {card.kind === 'matrix' && <MatrixView matrix={card.matrix} />}
        {card.kind === 'empty' && <EmptyCard reason={card.reason} />}
      </div>

      {card.to && (
        <Link className="rpt-card-foot" to={card.to}>
          {t('reports.viewDetail')} <Icon name="chevron" size={13} className="dir-flip" aria-hidden />
        </Link>
      )}
    </section>
  );
}

function MatrixView({ matrix }: { matrix: RiskMatrix }) {
  const { t } = useTranslation();
  const cols = ['Low', 'Medium', 'High'] as const;
  return (
    <div className="rpt-matrix" role="table" aria-label={t('reports.card.riskExposure.title')}>
      <div className="rpt-matrix-row rpt-matrix-colhead" role="row">
        <span className="rpt-matrix-corner" role="columnheader" aria-hidden />
        {cols.map((c) => <span key={c} className="rpt-matrix-col" role="columnheader">{t(`reports.level.${c}`)}</span>)}
      </div>
      {matrix.rows.map((row) => (
        <div key={row.impact} className="rpt-matrix-row" role="row">
          <span className="rpt-matrix-rowhead" role="rowheader">{t(`reports.impactLevel.${row.impact}`)}</span>
          {row.cells.map((cell: MatrixCell, ci) => (
            <span key={ci} role="cell" className="rpt-cell" style={{ background: `var(--st-${cell.zone}-bg)`, color: `var(--st-${cell.zone}-fg)` }}
              aria-label={t('reports.cellLabel', { count: cell.count, impact: t(`reports.impactLevel.${row.impact}`), prob: t(`reports.level.${cols[ci]}`) })}>
              {cell.count}
            </span>
          ))}
        </div>
      ))}
      <div className="rpt-matrix-cap">{t('reports.probability')}</div>
    </div>
  );
}

function StatGrid({ stats }: { stats: StatTile[] }) {
  const { t } = useTranslation();
  return (
    <div className="rpt-stats">
      {stats.map((s, i) => (
        <div key={s.label ?? (s.labelKey || String(i))} className="rpt-stat">
          <div className="rpt-stat-v" style={s.zone ? { color: `var(--st-${s.zone}-fg)` } : undefined}>{s.value}{s.suffix ?? ''}</div>
          <div className="rpt-stat-l">{s.label ?? t(s.labelKey)}</div>
        </div>
      ))}
    </div>
  );
}

function BarsView({ bars }: { bars: Bar[] }) {
  const { t } = useTranslation();
  if (bars.length === 0) return <p className="rpt-bars-empty">{t('reports.noData')}</p>;
  return (
    <div className="rpt-bars">
      {bars.map((b) => (
        <div key={b.key} className="rpt-bar">
          <div className="rpt-bar-top">
            <span className="rpt-bar-label">{b.label ?? t(b.labelKey!)}</span>
            <span className="rpt-bar-val">{b.count}</span>
          </div>
          <div className="rpt-bar-track"><span className="rpt-bar-fill" style={{ inlineSize: `${b.pct}%`, background: `var(--st-${b.zone}-dot)` }} /></div>
        </div>
      ))}
    </div>
  );
}

function ColumnsView({ cols }: { cols: Column[] }) {
  return (
    <div className="rpt-cols">
      <div className="rpt-cols-bars">
        {cols.map((c) => (
          <div key={c.key} className="rpt-col">
            <span className="rpt-col-v">{c.value}</span>
            <span className="rpt-col-bar" style={{ blockSize: `${c.pct}%`, background: `var(--st-${c.zone}-dot)` }} />
          </div>
        ))}
      </div>
      <div className="rpt-cols-labels">{cols.map((c) => <span key={c.key}>{c.label}</span>)}</div>
    </div>
  );
}

function StackView({ segments }: { segments: Segment[] }) {
  const { t } = useTranslation();
  const total = segments.reduce((s, x) => s + x.value, 0);
  return (
    <div>
      {/* Decorative — the legend below is the accessible source of the same counts. */}
      <div className="rpt-stack" aria-hidden="true">
        {total > 0 && segments.filter((s) => s.value > 0).map((s) => (
          <span key={s.key} style={{ inlineSize: `${s.pct}%`, background: `var(--st-${s.zone}-dot)` }} />
        ))}
      </div>
      <div className="rpt-stack-legend">
        {segments.map((s) => (
          <span key={s.key}>
            <span className="rpt-stack-dot" style={{ background: `var(--st-${s.zone}-dot)` }} />
            <b>{s.value}</b> {t(s.labelKey)}
          </span>
        ))}
      </div>
    </div>
  );
}

function EmptyCard({ reason }: { reason: 'trend' | 'seam' }) {
  const { t } = useTranslation();
  return (
    <div className="rpt-empty-card">
      <Icon name={reason === 'trend' ? 'activity' : 'database'} size={22} aria-hidden />
      <p>{t(`reports.reason.${reason}`)}</p>
    </div>
  );
}

function ReportsSkeleton() {
  return (
    <div className="rpt-grid" aria-hidden>
      {[0, 1, 2, 3].map((i) => (
        <section key={i} className="rpt-card rpt-skel">
          <div className="rpt-skel-head"><span className="rpt-skel-bar" style={{ inlineSize: '40%' }} /><span className="rpt-skel-sq" /></div>
          <span className="rpt-skel-bar rpt-skel-kpi" />
          <div className="rpt-skel-lines">{['100%', '85%', '70%', '90%'].map((w, j) => <span key={j} className="rpt-skel-bar" style={{ inlineSize: w }} />)}</div>
        </section>
      ))}
    </div>
  );
}
