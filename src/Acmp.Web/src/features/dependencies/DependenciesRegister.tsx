/*
 * Dependencies register (P10e) — the filtered/sorted/paged view over dependency edges, matching the
 * "ACMP Lists & Registers.dc.html" `isDeps` table (From · Relation · To · Blocked · Status). Composes
 * the shared library (Table, FilterChip, StatusChip, Button, states, Icon, Pagination); only relation
 * colour/arrow carries local CSS. Wired to GET /api/dependencies + the global header counts.
 *
 * Design↔behaviour reconciliations (visual SoT = the .dc.html; data SoT = package):
 *  - The Cross-stream column AND its filter are OMITTED (not rendered as an all-"—" column): IsCrossStream
 *    is not on the wire and its cross-module derivation (FR-095) is deferred (ASM-016). Flagged.
 *  - Only server-backed sorts are exposed (key/ID, Status — GetDependenciesRegister). Relation, From, To
 *    have no server sort, so those columns are not sortable.
 *  - Header "N links" + "N blocked" are GLOBAL (filter-independent) so they come from useDependenciesCounts,
 *    not the paged list; the "Showing X of Y" line is the paged count.
 *  - "New dependency" (Chairman/Secretary only) opens the create dialog; in this register/blank mode only
 *    Topic/Action are pickable (no FE list source for Decision/System) — flagged. "Open graph" is a
 *    disabled stub → the SVG impact graph ships in P10f.
 */
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth, hasRole } from '../../auth/AcmpAuthContext';
import { useDependenciesRegister, useDependenciesCounts, type DependencySummary } from '../../api/dependencies';
import { Table, type Column, type SortDir } from '../../components/ui/Table';
import { Pagination } from '../../components/ui/Pagination';
import { StatusChip } from '../../components/ui/StatusChip';
import { FilterChip } from '../../components/ui/FilterChip';
import { Button } from '../../components/ui/Button';
import { ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { statusTone, kindColor, kindPointsUp, DEP_KINDS } from './depMeta';
import { CreateDependencyDialog } from './CreateDependencyDialog';
import './dependencies.css';

// Column id → API sortBy. Only key + status have a server sort (GetDependenciesRegister).
const SORT_PARAM: Record<string, string> = { from: 'key', status: 'status' };
const PAGE_SIZE = 25;

interface Filters {
  kind: string;
  blockedOnly: boolean;
}

export function DependenciesRegister() {
  const { t } = useTranslation();
  const auth = useAuth();
  const canCreate = hasRole(auth, 'chairman', 'secretary');
  const [filters, setFilters] = useState<Filters>({ kind: '', blockedOnly: false });
  const [sortCol, setSortCol] = useState('from');
  const [sortDir, setSortDir] = useState<SortDir>('asc');
  const [page, setPage] = useState(1);
  const [createOpen, setCreateOpen] = useState(false);

  useEffect(() => {
    setPage(1);
  }, [filters, sortCol, sortDir]);

  const { data, isLoading, isError, refetch } = useDependenciesRegister({
    kind: filters.kind || undefined,
    blockedOnly: filters.blockedOnly || undefined,
    sortBy: SORT_PARAM[sortCol] ?? 'key',
    sortDir,
    page,
    pageSize: PAGE_SIZE,
  });
  const counts = useDependenciesCounts();

  const clearFilters = () => setFilters({ kind: '', blockedOnly: false });
  const onSort = (col: string) => {
    if (sortCol === col) setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'));
    else {
      setSortCol(col);
      setSortDir('asc');
    }
  };

  const total = data?.total ?? 0;
  const shown = data?.items.length ?? 0;
  const hasFilters = filters.kind !== '' || filters.blockedOnly;

  return (
    <section className="page">
      <div className="dep-head">
        <div>
          <h1 className="page-title">{t('deps.title')}</h1>
          <div className="dep-head-sub">
            <span className="dep-head-count">{t('deps.count', { count: counts.total ?? 0 })}</span>
            {!!counts.blocked && (
              <>
                <span className="dep-head-sep" aria-hidden="true" />
                <span className="dep-head-blocked">
                  <span className="dep-dot" aria-hidden="true" />
                  {t('deps.blockedCount', { count: counts.blocked })}
                </span>
              </>
            )}
          </div>
        </div>
        <div className="dep-head-actions">
          {canCreate && (
            <Button variant="primary" onClick={() => setCreateOpen(true)}>
              <Icon name="plus" size={15} aria-hidden /> {t('deps.newDependency')}
            </Button>
          )}
          {/* P10f: the SVG impact graph. Disabled stub this slice (guardrail #14, honest). */}
          <Button variant="secondary" disabled title={t('deps.graphSoon')}>
            <Icon name="deps" size={15} aria-hidden /> {t('deps.openGraph')}
          </Button>
        </div>
      </div>

      <div className="dep-bar" role="search" aria-label={t('deps.filtersLabel')}>
        <FilterChip
          label={t('deps.filter.relation')}
          anyLabel={t('deps.filter.anyRelation')}
          options={DEP_KINDS.map((v) => ({ value: v, label: t(`deps.kind.${v}`) }))}
          value={filters.kind}
          onChange={(kind) => setFilters((f) => ({ ...f, kind }))}
        />
        <button
          type="button"
          className={`dep-blocked-toggle${filters.blockedOnly ? ' on' : ''}`}
          aria-pressed={filters.blockedOnly}
          onClick={() => setFilters((f) => ({ ...f, blockedOnly: !f.blockedOnly }))}
        >
          <Icon name="ban" size={13} aria-hidden /> {t('deps.filter.blockedWork')}
        </button>
        {data && (
          <span className="dep-showing"><Icon name="backlog" size={13} aria-hidden /> {t('deps.showing', { shown, total })}</span>
        )}
      </div>

      {isLoading ? (
        <DepsSkeleton />
      ) : isError ? (
        <ErrorState title={t('deps.error.title')} body={t('deps.error.body')} onRetry={() => refetch()} />
      ) : total === 0 ? (
        <div>
          <EmptyState icon="deps" title={t('deps.empty.title')} body={t('deps.empty.body')} />
          <div className="dep-empty-actions">
            {canCreate && (
              <Button variant="primary" onClick={() => setCreateOpen(true)}>
                <Icon name="plus" size={16} aria-hidden /> {t('deps.newDependency')}
              </Button>
            )}
            {hasFilters && <Button variant="secondary" onClick={clearFilters}>{t('deps.clearFilters')}</Button>}
          </div>
        </div>
      ) : (
        <>
          <DepsTable rows={data!.items} sort={{ by: sortCol, dir: sortDir }} onSort={onSort} />
          <div className="dep-foot">
            <Pagination
              page={page}
              pageCount={data!.totalPages}
              onPageChange={setPage}
              labels={{ nav: t('deps.pageNav'), previous: t('deps.prevPage'), next: t('deps.nextPage') }}
            />
          </div>
        </>
      )}

      <CreateDependencyDialog open={createOpen} onClose={() => setCreateOpen(false)} />
    </section>
  );
}

function DepsSkeleton() {
  const { t } = useTranslation();
  const widths = ['70%', '54%', '72%', '40%', '58%'];
  return (
    <div className="table-wrap" role="status" aria-busy="true">
      <span className="visually-hidden">{t('common.loading')}</span>
      <div className="dep-skel-head" aria-hidden="true">
        {Array.from({ length: 5 }).map((_, i) => (
          <span key={i} className="skeleton dep-skel-bar" style={{ inlineSize: 50 }} />
        ))}
      </div>
      {widths.map((w, i) => (
        <div key={i} className="dep-skel-row" aria-hidden="true">
          <span className="skeleton dep-skel-bar" style={{ inlineSize: w }} />
          <span className="skeleton dep-skel-bar" style={{ inlineSize: 90 }} />
          <span className="skeleton dep-skel-bar" style={{ inlineSize: '66%' }} />
          <span className="skeleton dep-skel-bar" style={{ inlineSize: 40 }} />
          <span className="skeleton dep-skel-bar" style={{ inlineSize: 72 }} />
        </div>
      ))}
    </div>
  );
}

/** Relation cell: localized kind label, coloured, with a direction arrow (up = points to the From end). */
function Relation({ row }: { row: DependencySummary }) {
  const { t } = useTranslation();
  return (
    <span className="dep-rel" style={{ color: kindColor(row.kind) }}>
      <Icon name="arrowRight" size={13} className={kindPointsUp(row.kind) ? 'dep-rel-back' : 'dir-flip'} aria-hidden />
      {t(`deps.kind.${row.kind}`)}
    </span>
  );
}

function DepsTable({ rows, sort, onSort }: { rows: DependencySummary[]; sort: { by: string; dir: SortDir }; onSort: (id: string) => void }) {
  const { t } = useTranslation();
  const columns: Column<DependencySummary>[] = [
    { id: 'from', header: t('deps.col.from'), sortable: true, cell: (r) => <Link className="dep-endpoint-link" to={`/dependencies/${r.key}`}>{r.fromKey}</Link> },
    { id: 'relation', header: t('deps.col.relation'), width: '132px', cell: (r) => <Relation row={r} /> },
    { id: 'to', header: t('deps.col.to'), cell: (r) => <span className="dep-endpoint">{r.toKey}</span> },
    { id: 'blocked', header: t('deps.col.blocked'), width: '96px', cell: (r) => (r.isBlocker ? <span className="dep-blocked-cell"><Icon name="ban" size={11} aria-hidden />{t('deps.blocked')}</span> : null) },
    { id: 'status', header: t('deps.col.status'), width: '116px', sortable: true, cell: (r) => <StatusChip tone={statusTone(r.status)} label={t(`deps.status.${r.status}`)} size="sm" /> },
  ];
  return <Table caption={t('deps.tableCaption')} columns={columns} rows={rows} getRowKey={(r) => r.id} sort={sort} onSortChange={onSort} />;
}
