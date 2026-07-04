/*
 * Risks register (P10b) — the filtered/sorted/paged view over Risks (W15), matching the
 * "ACMP Lists & Registers.dc.html" `isRisks` table (gRsk, 8 columns). Composes the shared
 * library (Table, FilterChip, StatusChip, Button, states, Icon, Pagination); only
 * risk-specific cells (heat grid, exposure chip, level colours, owner avatar) carry local CSS.
 * Wired to GET /api/risks (useRisksRegister) + the global header counts (useRisksCounts).
 *
 * Design↔behaviour reconciliations (visual SoT = "ACMP Lists & Registers.dc.html"; data SoT = package):
 *  - Header "N risks" + "N critical" are GLOBAL (filter-independent, as the design computes them) so
 *    they come from useRisksCounts, not the filtered page; the "Showing X of Y" line is the paged count.
 *  - Only server-backed sorts are exposed (ID/key, Exposure, Status — GetRisksRegister.Sort). Prob,
 *    Impact, Risk-title, Owner and Linked have no server sort, so those columns are not sortable.
 *  - Owner filter is rendered (design parity) but disabled this slice — it needs a verified owner
 *    directory keyed to Keycloak subjects; follow-up (same call as the actions register).
 *  - The "Open risks by exposure" saved view is a non-functional design chrome — dropped (like the
 *    actions register), the default exposure-desc sort already realises it.
 *  - "New risk" raises a risk against a linked topic (CreateRiskDialog); the API enforces Risk.Manage.
 */
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useRisksRegister, useRisksCounts, type RiskSummary } from '../../api/risks';
import { Table, type Column, type SortDir } from '../../components/ui/Table';
import { Pagination } from '../../components/ui/Pagination';
import { StatusChip } from '../../components/ui/StatusChip';
import { FilterChip } from '../../components/ui/FilterChip';
import { Button } from '../../components/ui/Button';
import { ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { statusTone, exposureTone, levelColor, heatCells, initials, RISK_STATUSES, RISK_EXPOSURES } from './riskMeta';
import { CreateRiskDialog } from './CreateRiskDialog';
import './risks.css';

// Column id → API sortBy. Only these three have a server sort (GetRisksRegister.Sort: key/status/exposure).
const SORT_PARAM: Record<string, string> = { key: 'key', exposure: 'exposure', status: 'status' };
const PAGE_SIZE = 25;

interface Filters {
  statuses: string[];
  exposures: string[];
}

export function RisksRegister() {
  const { t, i18n } = useTranslation();
  const [filters, setFilters] = useState<Filters>({ statuses: [], exposures: [] });
  const [sortCol, setSortCol] = useState('exposure');
  const [sortDir, setSortDir] = useState<SortDir>('desc');
  const [page, setPage] = useState(1);
  const [createOpen, setCreateOpen] = useState(false);

  // Reset to page 1 on any filter/sort change.
  useEffect(() => {
    setPage(1);
  }, [filters, sortCol, sortDir]);

  const { data, isLoading, isError, refetch } = useRisksRegister({
    statuses: filters.statuses.length ? filters.statuses : undefined,
    exposures: filters.exposures.length ? filters.exposures : undefined,
    sortBy: SORT_PARAM[sortCol] ?? 'exposure',
    sortDir,
    page,
    pageSize: PAGE_SIZE,
  });
  const counts = useRisksCounts();

  const clearFilters = () => setFilters({ statuses: [], exposures: [] });
  const onSort = (col: string) => {
    if (sortCol === col) setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'));
    else {
      setSortCol(col);
      setSortDir('desc');
    }
  };

  const total = data?.total ?? 0;
  const shown = data?.items.length ?? 0;
  const hasFilters = filters.statuses.length > 0 || filters.exposures.length > 0;

  return (
    <section className="page">
      <div className="rsk-head">
        <div>
          <h1 className="page-title">{t('risks.title')}</h1>
          <div className="rsk-head-sub">
            <span className="rsk-head-count">{t('risks.count', { count: counts.total ?? 0 })}</span>
            {!!counts.critical && (
              <>
                <span className="rsk-head-sep" aria-hidden="true" />
                <span className="rsk-head-critical">
                  <span className="rsk-dot" aria-hidden="true" />
                  {t('risks.criticalCount', { count: counts.critical })}
                </span>
              </>
            )}
          </div>
        </div>
        <Button variant="primary" onClick={() => setCreateOpen(true)}>
          <Icon name="plus" size={16} aria-hidden /> {t('risks.newRisk')}
        </Button>
      </div>

      <div className="rsk-bar" role="search" aria-label={t('risks.filtersLabel')}>
        <FilterChip
          multiple
          label={t('risks.filter.status')}
          options={RISK_STATUSES.map((v) => ({ value: v, label: t(`risks.status.${v}`) }))}
          values={filters.statuses}
          onChange={(statuses) => setFilters((f) => ({ ...f, statuses }))}
          clearLabel={t('risks.clearFilters')}
        />
        <FilterChip label={t('risks.filter.owner')} anyLabel={t('risks.filter.anyOwner')} options={[]} value="" onChange={() => {}} disabled />
        <FilterChip
          multiple
          label={t('risks.filter.exposure')}
          options={RISK_EXPOSURES.map((v) => ({ value: v, label: t(`risks.exposure.${v}`) }))}
          values={filters.exposures}
          onChange={(exposures) => setFilters((f) => ({ ...f, exposures }))}
          clearLabel={t('risks.clearFilters')}
        />
        {data && (
          <span className="rsk-count"><Icon name="backlog" size={13} aria-hidden /> {t('risks.showing', { shown, total })}</span>
        )}
      </div>

      {isLoading ? (
        <RisksSkeleton />
      ) : isError ? (
        <ErrorState title={t('risks.error.title')} body={t('risks.error.body')} onRetry={() => refetch()} />
      ) : total === 0 ? (
        <div>
          <EmptyState icon="risk" title={t('risks.empty.title')} body={t('risks.empty.body')} />
          <div className="rsk-empty-actions">
            <Button variant="primary" onClick={() => setCreateOpen(true)}>
              <Icon name="plus" size={16} aria-hidden /> {t('risks.newRisk')}
            </Button>
            {hasFilters && <Button variant="secondary" onClick={clearFilters}>{t('risks.clearFilters')}</Button>}
          </div>
        </div>
      ) : (
        <>
          <RisksTable rows={data!.items} lang={i18n.language} sort={{ by: sortCol, dir: sortDir }} onSort={onSort} />
          <div className="rsk-foot">
            <Pagination
              page={page}
              pageCount={data!.totalPages}
              onPageChange={setPage}
              labels={{ nav: t('risks.pageNav'), previous: t('risks.prevPage'), next: t('risks.nextPage') }}
            />
          </div>
        </>
      )}

      <CreateRiskDialog open={createOpen} onClose={() => setCreateOpen(false)} />
    </section>
  );
}

// Table-shaped loading skeleton — 8 shimmer rows over the 8-column gRsk grid (design's loading state).
function RisksSkeleton() {
  const { t } = useTranslation();
  const rowWidths = ['58%', '82%', '50%', '70%', '46%', '64%', '56%', '74%'];
  return (
    <div className="table-wrap" role="status" aria-busy="true">
      <span className="visually-hidden">{t('common.loading')}</span>
      <div className="rsk-skel-head" aria-hidden="true">
        {Array.from({ length: 8 }).map((_, i) => (
          <span key={i} className="skeleton rsk-skel-bar" style={{ inlineSize: 54 }} />
        ))}
      </div>
      {rowWidths.map((w, i) => (
        <div key={i} className="rsk-skel-row" aria-hidden="true">
          <span className="skeleton rsk-skel-bar" style={{ inlineSize: 66 }} />
          <span className="skeleton rsk-skel-bar" style={{ inlineSize: w }} />
          <span className="skeleton rsk-skel-bar" style={{ inlineSize: 44 }} />
          <span className="skeleton rsk-skel-bar" style={{ inlineSize: 44 }} />
          <span className="skeleton rsk-skel-bar" style={{ inlineSize: 78 }} />
          <span className="skeleton rsk-skel-bar" style={{ inlineSize: 72 }} />
          <span className="skeleton rsk-skel-bar" style={{ inlineSize: 64 }} />
          <span className="skeleton rsk-skel-bar" style={{ inlineSize: 56 }} />
        </div>
      ))}
    </div>
  );
}

function Owner({ name }: { name: string }) {
  const { t } = useTranslation();
  if (!name) return <span className="rsk-muted">{t('risks.unassigned')}</span>;
  return (
    <span className="rsk-owner">
      <span className="rsk-avatar" aria-hidden="true">{initials(name)}</span>
      <span className="rsk-owner-name">{name}</span>
    </span>
  );
}

function Heat({ row }: { row: RiskSummary }) {
  const { t } = useTranslation();
  const cells = heatCells(row.likelihood, row.impact, row.exposure);
  return (
    <span className="rsk-exposure">
      <span className="rsk-heat" aria-hidden="true">
        {cells.map((bg, i) => (
          <span key={i} className="rsk-hc" style={{ background: bg }} />
        ))}
      </span>
      <StatusChip tone={exposureTone(row.exposure)} label={t(`risks.exposure.${row.exposure}`)} size="sm" />
    </span>
  );
}

function RisksTable({ rows, lang, sort, onSort }: { rows: RiskSummary[]; lang: string; sort: { by: string; dir: SortDir }; onSort: (id: string) => void }) {
  const { t } = useTranslation();
  const pick = (title: RiskSummary['title']) => (lang === 'ar' ? title.ar : title.en);
  const columns: Column<RiskSummary>[] = [
    { id: 'key', header: t('risks.col.key'), width: '104px', sortable: true, cell: (r) => <span className="rsk-key">{r.key}</span> },
    { id: 'risk', header: t('risks.col.risk'), cell: (r) => <Link className="rsk-title-link" to={`/risks/${r.key}`}>{pick(r.title)}</Link> },
    { id: 'prob', header: t('risks.col.prob'), width: '80px', cell: (r) => <span className="rsk-level" style={{ color: levelColor(r.likelihood) }}>{t(`risks.level.${r.likelihood}`)}</span> },
    { id: 'impact', header: t('risks.col.impact'), width: '78px', cell: (r) => <span className="rsk-level" style={{ color: levelColor(r.impact) }}>{t(`risks.level.${r.impact}`)}</span> },
    { id: 'exposure', header: t('risks.col.exposure'), width: '120px', sortable: true, cell: (r) => <Heat row={r} /> },
    { id: 'owner', header: t('risks.col.owner'), width: '130px', cell: (r) => <Owner name={r.ownerName} /> },
    { id: 'status', header: t('risks.col.status'), width: '112px', sortable: true, cell: (r) => <StatusChip tone={statusTone(r.status)} label={t(`risks.status.${r.status}`)} size="sm" /> },
    { id: 'linked', header: t('risks.col.linked'), width: '96px', cell: (r) => <span className="rsk-linked">{r.subjectKey ?? '—'}</span> },
  ];
  return <Table caption={t('risks.tableCaption')} columns={columns} rows={rows} getRowKey={(r) => r.id} sort={sort} onSortChange={onSort} />;
}
