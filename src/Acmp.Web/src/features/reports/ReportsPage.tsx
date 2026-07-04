/*
 * Risk & Dependency reports (P10g) — the last P10 slice.
 *
 * Replaces the /reports placeholder with a focused analytics surface that REUSES the card
 * renderers/tokens of "ACMP Dashboards & Reports.dc.html" (matrix / stat / bars). The full
 * Reports IA — the Dashboard/Reports toggle, role tabs, 6 view-tabs, Export, filters row and
 * the non-risk/dep cards — is DEFERRED to P12 Reporting (guardrail #14: this page is a
 * no-reference composition of the design's card system, not a verbatim port of the page).
 *
 * FE-only: every number is composed client-side (reportAgg) from three existing REST reads;
 * no backend aggregation endpoint (see reportAgg.ts header). The topics fetch drives the
 * by-stream join and MUST includeClosed — an active risk can sit on a closed topic, and
 * dropping those would silently undercount the stream tally.
 */
import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useRisksRegister } from '../../api/risks';
import { useDependenciesRegister } from '../../api/dependencies';
import { useBacklog } from '../../api/topics';
import { ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import {
  buildTopicStreamMap,
  riskMatrix,
  riskStats,
  depStats,
  depsByKind,
  risksByStream,
  blockedDepsByStream,
  type MatrixCell,
  type StatTile,
  type Bar,
} from './reportAgg';
import './reports.css';

// ponytail: one large page per register = the whole set at ≤20-user / committee scale (no server
// pageSize clamp on any of the three — verified). Page-loop only if a register ever exceeds this.
const ALL = 500;

export function ReportsPage() {
  const { t } = useTranslation();

  const risks = useRisksRegister({ pageSize: ALL });
  const deps = useDependenciesRegister({ pageSize: ALL });
  // includeClosed: the join map must resolve active risks/deps that hang off closed topics.
  const topics = useBacklog({ includeClosed: true, pageSize: ALL });

  const model = useMemo(() => {
    if (!risks.data || !deps.data || !topics.data) return null;
    const r = risks.data.items;
    const d = deps.data.items;
    const streams = buildTopicStreamMap(topics.data.items);
    return {
      matrix: riskMatrix(r),
      riskStats: riskStats(r),
      depStats: depStats(d),
      depsByKind: depsByKind(d),
      riskByStream: risksByStream(r, streams),
      blockedByStream: blockedDepsByStream(d, streams),
    };
  }, [risks.data, deps.data, topics.data]);

  const isLoading = risks.isLoading || deps.isLoading || topics.isLoading;
  const isError = risks.isError || deps.isError || topics.isError;
  const isEmpty = (risks.data?.total ?? 0) === 0 && (deps.data?.total ?? 0) === 0;

  const retry = () => {
    void risks.refetch();
    void deps.refetch();
    void topics.refetch();
  };

  return (
    <section className="page rpt">
      <header className="rpt-head">
        <h1 className="page-title">{t('reports.title')}</h1>
        <p className="rpt-lead">{t('reports.lead')}</p>
      </header>

      {isLoading ? (
        <ReportsSkeleton />
      ) : isError ? (
        <ErrorState title={t('reports.error.title')} body={t('reports.error.body')} onRetry={retry} />
      ) : isEmpty || !model ? (
        <EmptyState icon="reports" title={t('reports.empty.title')} body={t('reports.empty.body')} />
      ) : (
        <div className="rpt-grid">
          <ReportCard title={t('reports.card.riskExposure.title')} sub={t('reports.activeCount', { count: model.matrix.active })} to="/risks">
            <MatrixView matrix={model.matrix} />
          </ReportCard>

          <ReportCard title={t('reports.card.riskStat.title')} sub={t('reports.card.riskStat.sub')} to="/risks">
            <StatGrid stats={model.riskStats} />
          </ReportCard>

          <ReportCard title={t('reports.card.depStat.title')} sub={t('reports.card.depStat.sub')} to="/dependencies">
            <StatGrid stats={model.depStats} />
          </ReportCard>

          <ReportCard title={t('reports.card.depsByKind.title')} sub={t('reports.card.depsByKind.sub')} to="/dependencies">
            <BarsView bars={model.depsByKind} />
          </ReportCard>

          <ReportCard
            title={t('reports.card.riskByStream.title')}
            sub={t('reports.streamCount', { count: model.riskByStream.kpi })}
            to="/risks"
            note={t('reports.streamNote')}
          >
            <BarsView bars={model.riskByStream.bars} emptyKey="reports.noStreamRisks" />
          </ReportCard>

          <ReportCard
            title={t('reports.card.blockedByStream.title')}
            sub={t('reports.blockedCount', { count: model.blockedByStream.kpi })}
            to="/dependencies"
            note={t('reports.streamNote')}
          >
            <BarsView bars={model.blockedByStream.bars} emptyKey="reports.noStreamBlocked" />
          </ReportCard>
        </div>
      )}
      {!isLoading && !isError && !isEmpty && (
        <p className="rpt-scope" role="note">{t('reports.p12Note')}</p>
      )}
    </section>
  );
}

interface ReportCardProps {
  title: string;
  sub: string;
  to: string;
  note?: string;
  children: React.ReactNode;
}

function ReportCard({ title, sub, to, note, children }: ReportCardProps) {
  const { t } = useTranslation();
  return (
    <section className="rpt-card">
      <div className="rpt-card-head">
        <div>
          <h2 className="rpt-card-title">{title}</h2>
          <div className="rpt-card-sub">{sub}</div>
        </div>
        <Link className="rpt-drill" to={to} aria-label={t('reports.drill', { title })}>
          <Icon name="arrowUpRight" size={15} aria-hidden />
        </Link>
      </div>
      <div className="rpt-card-body">{children}</div>
      {note && <div className="rpt-card-note"><Icon name="infoCircle" size={12} aria-hidden /> {note}</div>}
      <Link className="rpt-card-foot" to={to}>
        {t('reports.viewDetail')} <Icon name="chevron" size={13} className="dir-flip" aria-hidden />
      </Link>
    </section>
  );
}

function MatrixView({ matrix }: { matrix: ReturnType<typeof riskMatrix> }) {
  const { t } = useTranslation();
  const cols = ['Low', 'Medium', 'High'] as const;
  return (
    <div className="rpt-matrix" role="table" aria-label={t('reports.card.riskExposure.title')}>
      <div className="rpt-matrix-row rpt-matrix-colhead" role="row">
        <span className="rpt-matrix-corner" role="columnheader" aria-hidden />
        {cols.map((c) => (
          <span key={c} className="rpt-matrix-col" role="columnheader">{t(`reports.level.${c}`)}</span>
        ))}
      </div>
      {matrix.rows.map((row) => (
        <div key={row.impact} className="rpt-matrix-row" role="row">
          <span className="rpt-matrix-rowhead" role="rowheader">{t(`reports.impactLevel.${row.impact}`)}</span>
          {row.cells.map((cell: MatrixCell, ci) => (
            <span
              key={ci}
              role="cell"
              className="rpt-cell"
              style={{ background: `var(--st-${cell.zone}-bg)`, color: `var(--st-${cell.zone}-fg)` }}
              aria-label={t('reports.cellLabel', { count: cell.count, impact: t(`reports.impactLevel.${row.impact}`), prob: t(`reports.level.${cols[ci]}`) })}
            >
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
      {stats.map((s) => (
        <div key={s.labelKey} className="rpt-stat">
          <div className="rpt-stat-v" style={s.zone ? { color: `var(--st-${s.zone}-fg)` } : undefined}>{s.value}</div>
          <div className="rpt-stat-l">{t(s.labelKey)}</div>
        </div>
      ))}
    </div>
  );
}

function BarsView({ bars, emptyKey }: { bars: Bar[]; emptyKey?: string }) {
  const { t } = useTranslation();
  if (bars.length === 0 && emptyKey) return <p className="rpt-bars-empty">{t(emptyKey)}</p>;
  return (
    <div className="rpt-bars">
      {bars.map((b) => (
        <div key={b.key} className="rpt-bar">
          <div className="rpt-bar-top">
            <span className="rpt-bar-label">{b.label ?? t(b.labelKey!)}</span>
            <span className="rpt-bar-val">{b.count}</span>
          </div>
          <div className="rpt-bar-track">
            <span className="rpt-bar-fill" style={{ inlineSize: `${b.pct}%`, background: `var(--st-${b.zone}-dot)` }} />
          </div>
        </div>
      ))}
    </div>
  );
}

// Shimmer skeletons — the design's 4-card loading state (gated behind prefers-reduced-motion in CSS).
function ReportsSkeleton() {
  return (
    <div className="rpt-grid" aria-hidden>
      {[0, 1, 2, 3].map((i) => (
        <section key={i} className="rpt-card rpt-skel">
          <div className="rpt-skel-head">
            <span className="rpt-skel-bar" style={{ inlineSize: '40%' }} />
            <span className="rpt-skel-sq" />
          </div>
          <span className="rpt-skel-bar rpt-skel-kpi" />
          <div className="rpt-skel-lines">
            {['100%', '85%', '70%', '90%'].map((w, j) => (
              <span key={j} className="rpt-skel-bar" style={{ inlineSize: w }} />
            ))}
          </div>
        </section>
      ))}
    </div>
  );
}
