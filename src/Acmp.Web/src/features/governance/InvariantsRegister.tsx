/*
 * Invariant register (P11d) — the filtered/sorted/paged view over Governance Invariants (W18), matching
 * the "ACMP Lists & Registers.dc.html" isInvTab tab (gInv, 6 columns). Composes the shared library (Table,
 * FilterChip, StatusChip, Button, states, Icon, Pagination); only Invariant-specific chrome (the tab bar,
 * the category dot) carries local CSS. Wired to GET /api/invariants (useInvariantsRegister) + header total.
 *
 * Design↔behaviour reconciliations (visual SoT = "ACMP Lists & Registers.dc.html"; data SoT = package):
 *  - The header "N invariants" total is filter-INDEPENDENT (the design computes it over the whole set) so
 *    it comes from useInvariantsCount, not the filtered page; the "Showing X of Y" line is the paged count.
 *  - The design's 6th column is "Viol." (violation count); it is OMITTED entirely (operator DEFER — no
 *    violations surface in v1). The register is 5 columns: ID · Statement · Category · Scope · Status.
 *  - Only server-backed sorts are exposed (ID/key, Statement, Category, Status — the register query's
 *    sortBy set). Scope is a plain column (matches the design). Default is created-desc (no column carries
 *    it, so no header shows an initial sort indicator).
 *  - Category filter is rendered (design parity) but disabled — the register query has no category FILTER
 *    param (category is a sort key only); enabling it would touch the backend.
 *  - "New invariant" opens CreateInvariantDialog; the API enforces Invariant.Create.
 */
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useInvariantsRegister, useInvariantsCount, type InvariantSummary } from '../../api/invariants';
import { useAdrsCount } from '../../api/adrs';
import { Table, type Column, type SortDir } from '../../components/ui/Table';
import { Pagination } from '../../components/ui/Pagination';
import { StatusChip } from '../../components/ui/StatusChip';
import { FilterChip } from '../../components/ui/FilterChip';
import { Button } from '../../components/ui/Button';
import { ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { statusTone, categoryDot, INVARIANT_STATUSES } from './invariantMeta';
import { CreateInvariantDialog } from './CreateInvariantDialog';
import { GovernanceTabs } from './GovernanceTabs';
import './governance.css';

// Column id → API sortBy. Only these have a server sort (register query: key/statement/category/status).
const SORT_PARAM: Record<string, string> = { key: 'key', statement: 'statement', category: 'category', status: 'status' };
const PAGE_SIZE = 25;

export function InvariantsRegister() {
  const { t, i18n } = useTranslation();
  const [statuses, setStatuses] = useState<string[]>([]);
  const [sortCol, setSortCol] = useState('created');
  const [sortDir, setSortDir] = useState<SortDir>('desc');
  const [page, setPage] = useState(1);
  const [createOpen, setCreateOpen] = useState(false);

  // Reset to page 1 on any filter/sort change.
  useEffect(() => {
    setPage(1);
  }, [statuses, sortCol, sortDir]);

  const { data, isLoading, isError, refetch } = useInvariantsRegister({
    statuses: statuses.length ? statuses : undefined,
    sortBy: SORT_PARAM[sortCol] ?? 'created',
    sortDir,
    page,
    pageSize: PAGE_SIZE,
  });
  const total = useInvariantsCount();
  const adrTotal = useAdrsCount();

  const clearFilters = () => setStatuses([]);
  const onSort = (col: string) => {
    if (sortCol === col) setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'));
    else {
      setSortCol(col);
      setSortDir('desc');
    }
  };

  const pageTotal = data?.total ?? 0;

  return (
    <section className="page">
      <div className="adr-head">
        <div>
          <h1 className="page-title">{t('adrs.title')}</h1>
          <div className="adr-head-sub">{t('invariants.count', { count: total.data ?? 0 })}</div>
        </div>
        <Button variant="primary" onClick={() => setCreateOpen(true)}>
          <Icon name="plus" size={16} aria-hidden /> {t('invariants.newInvariant')}
        </Button>
      </div>

      <GovernanceTabs active="invariants" adrCount={adrTotal.data ?? 0} invCount={total.data ?? 0} />

      <div className="adr-bar" role="search" aria-label={t('invariants.filtersLabel')}>
        <FilterChip
          multiple
          label={t('invariants.filter.status')}
          options={INVARIANT_STATUSES.map((v) => ({ value: v, label: t(`invariants.status.${v}`) }))}
          values={statuses}
          onChange={setStatuses}
          clearLabel={t('invariants.clearFilters')}
        />
        <FilterChip label={t('invariants.filter.category')} anyLabel={t('invariants.filter.anyCategory')} options={[]} value="" onChange={() => {}} disabled />
        {data && (
          <span className="adr-count"><Icon name="filterLines" size={13} aria-hidden /> {t('invariants.showing', { count: pageTotal })}</span>
        )}
      </div>

      {isLoading ? (
        <InvariantsSkeleton />
      ) : isError ? (
        <ErrorState title={t('invariants.error.title')} body={t('invariants.error.body')} onRetry={() => refetch()} />
      ) : pageTotal === 0 ? (
        <div>
          <EmptyState icon="checklist" title={t('invariants.empty.title')} body={t('invariants.empty.body')} />
          <div className="adr-empty-actions">
            <Button variant="secondary" onClick={clearFilters}>{t('invariants.clearFilters')}</Button>
            <Button variant="primary" onClick={() => setCreateOpen(true)}>
              <Icon name="plus" size={16} aria-hidden /> {t('invariants.newInvariant')}
            </Button>
          </div>
        </div>
      ) : (
        <>
          <InvariantsTable rows={data!.items} lang={i18n.language} sort={{ by: sortCol, dir: sortDir }} onSort={onSort} />
          <div className="adr-foot">
            <Pagination
              page={page}
              pageCount={data!.totalPages}
              onPageChange={setPage}
              labels={{ nav: t('invariants.pageNav'), previous: t('invariants.prevPage'), next: t('invariants.nextPage') }}
            />
          </div>
        </>
      )}

      <CreateInvariantDialog open={createOpen} onClose={() => setCreateOpen(false)} />
    </section>
  );
}

// Table-shaped loading skeleton — 8 shimmer rows over the 5-column gInv grid (design's loading state).
function InvariantsSkeleton() {
  const { t } = useTranslation();
  const rowWidths = ['84%', '60%', '68%', '72%', '52%', '78%', '58%', '66%'];
  return (
    <div className="table-wrap" role="status" aria-busy="true">
      <span className="visually-hidden">{t('common.loading')}</span>
      <div className="inv-skel-head" aria-hidden="true">
        {Array.from({ length: 5 }).map((_, i) => (
          <span key={i} className="skeleton adr-skel-bar" style={{ inlineSize: 54 }} />
        ))}
      </div>
      {rowWidths.map((w, i) => (
        <div key={i} className="inv-skel-row" aria-hidden="true">
          <span className="skeleton adr-skel-bar" style={{ inlineSize: 92 }} />
          <span className="skeleton adr-skel-bar" style={{ inlineSize: w }} />
          <span className="skeleton adr-skel-bar" style={{ inlineSize: 70 }} />
          <span className="skeleton adr-skel-bar" style={{ inlineSize: 84 }} />
          <span className="skeleton adr-skel-bar" style={{ inlineSize: 78 }} />
        </div>
      ))}
    </div>
  );
}

function Category({ row }: { row: InvariantSummary }) {
  const { t } = useTranslation();
  return (
    <span className="inv-cat">
      <span className="inv-cat-dot" style={{ background: categoryDot(row.category) }} aria-hidden />
      {t(`invariants.category.${row.category}`)}
    </span>
  );
}

function InvariantsTable({ rows, lang, sort, onSort }: { rows: InvariantSummary[]; lang: string; sort: { by: string; dir: SortDir }; onSort: (id: string) => void }) {
  const { t } = useTranslation();
  const pick = (s: InvariantSummary['statement']) => (lang === 'ar' ? s.ar : s.en);
  const columns: Column<InvariantSummary>[] = [
    { id: 'key', header: t('invariants.col.key'), width: '132px', sortable: true, cell: (r) => <span className="adr-key">{r.key}</span> },
    { id: 'statement', header: t('invariants.col.statement'), sortable: true, cell: (r) => <Link className="inv-stmt-link" to={`/invariants/${r.key}`}>{pick(r.statement)}</Link> },
    { id: 'category', header: t('invariants.col.category'), width: '150px', sortable: true, cell: (r) => <Category row={r} /> },
    { id: 'scope', header: t('invariants.col.scope'), width: '140px', cell: (r) => <span className="adr-muted">{t(`invariants.scope.${r.scope}`)}</span> },
    { id: 'status', header: t('invariants.col.status'), width: '128px', sortable: true, cell: (r) => <StatusChip tone={statusTone(r.status)} label={t(`invariants.status.${r.status}`)} size="sm" /> },
  ];
  return <Table caption={t('invariants.tableCaption')} columns={columns} rows={rows} getRowKey={(r) => r.id} sort={sort} onSortChange={onSort} />;
}
