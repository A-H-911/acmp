/*
 * ADR register (P11b) — the filtered/sorted/paged view over Governance ADRs (W17), matching the
 * "ACMP Lists & Registers.dc.html" isAdrs tab (gAdr, 4 columns). Composes the shared library (Table,
 * FilterChip, StatusChip, Button, states, Icon, Pagination); only ADR-specific chrome (the tab bar,
 * supersede marker) carries local CSS. Wired to GET /api/adrs (useAdrsRegister) + the header total.
 *
 * Design↔behaviour reconciliations (visual SoT = "ACMP Lists & Registers.dc.html"; data SoT = package):
 *  - The header "N records" total is filter-INDEPENDENT (the design computes it over the whole set) so it
 *    comes from useAdrsCount, not the filtered page; the "Showing X of Y" line is the paged count.
 *  - Only server-backed sorts are exposed (ID/key, Title, Status — GetAdrsRegister.Sort). The default is
 *    created-desc (newest first), which no column carries, so no header shows an initial sort indicator.
 *  - The "Supersedes / superseded-by" column shows a superseded MARKER only: the lean AdrSummary carries
 *    IsSuperseded (bool), not the peer keys/direction (that lives on the detail, where GetAdrByKey resolves
 *    them). Enriching the register with directional peer keys is a later, backend-touching follow-up.
 *  - Category filter is rendered (design parity) but disabled — there is no server-side ADR category yet.
 *  - Tabs: ADRs is live; Invariants is present-but-disabled (P11d); Violations is omitted entirely
 *    (operator DEFER decision — no violations surface in v1).
 *  - "New ADR" opens CreateAdrDialog; the API enforces Adr.Create (Chairman/Secretary, or allow-if-owner).
 */
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAdrsRegister, useAdrsCount, type AdrSummary } from '../../api/adrs';
import { Table, type Column, type SortDir } from '../../components/ui/Table';
import { Pagination } from '../../components/ui/Pagination';
import { StatusChip } from '../../components/ui/StatusChip';
import { FilterChip } from '../../components/ui/FilterChip';
import { Button } from '../../components/ui/Button';
import { ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { statusTone, ADR_STATUSES } from './adrMeta';
import { CreateAdrDialog } from './CreateAdrDialog';
import './governance.css';

// Column id → API sortBy. Only these three have a server sort (GetAdrsRegister.Sort: key/title/status).
const SORT_PARAM: Record<string, string> = { key: 'key', title: 'title', status: 'status' };
const PAGE_SIZE = 25;

export function AdrsRegister() {
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

  const { data, isLoading, isError, refetch } = useAdrsRegister({
    statuses: statuses.length ? statuses : undefined,
    sortBy: SORT_PARAM[sortCol] ?? 'created',
    sortDir,
    page,
    pageSize: PAGE_SIZE,
  });
  const total = useAdrsCount();

  const clearFilters = () => setStatuses([]);
  const onSort = (col: string) => {
    if (sortCol === col) setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'));
    else {
      setSortCol(col);
      setSortDir('desc');
    }
  };

  const pageTotal = data?.total ?? 0;
  const shown = data?.items.length ?? 0;
  const hasFilters = statuses.length > 0;

  return (
    <section className="page">
      <div className="adr-head">
        <div>
          <h1 className="page-title">{t('adrs.title')}</h1>
          <div className="adr-head-sub">{t('adrs.count', { count: total.data ?? 0 })}</div>
        </div>
        <Button variant="primary" onClick={() => setCreateOpen(true)}>
          <Icon name="plus" size={16} aria-hidden /> {t('adrs.newAdr')}
        </Button>
      </div>

      <div className="adr-tabs" role="tablist" aria-label={t('adrs.tabsLabel')}>
        <span className="adr-tab is-active" role="tab" aria-selected="true">
          <Icon name="adr" size={16} aria-hidden /> {t('adrs.tab.adrs')}
          <span className="adr-tab-count">{total.data ?? 0}</span>
        </span>
        <span className="adr-tab is-disabled" role="tab" aria-disabled="true" title={t('adrs.tab.soon')}>
          <Icon name="shieldUser" size={16} aria-hidden /> {t('adrs.tab.invariants')}
          <span className="adr-tab-soon">{t('adrs.tab.soon')}</span>
        </span>
      </div>

      <div className="adr-bar" role="search" aria-label={t('adrs.filtersLabel')}>
        <FilterChip
          multiple
          label={t('adrs.filter.status')}
          options={ADR_STATUSES.map((v) => ({ value: v, label: t(`adrs.status.${v}`) }))}
          values={statuses}
          onChange={setStatuses}
          clearLabel={t('adrs.clearFilters')}
        />
        <FilterChip label={t('adrs.filter.category')} anyLabel={t('adrs.filter.anyCategory')} options={[]} value="" onChange={() => {}} disabled />
        {data && (
          <span className="adr-count"><Icon name="viewList" size={13} aria-hidden /> {t('adrs.showing', { shown, total: pageTotal })}</span>
        )}
      </div>

      {isLoading ? (
        <AdrsSkeleton />
      ) : isError ? (
        <ErrorState title={t('adrs.error.title')} body={t('adrs.error.body')} onRetry={() => refetch()} />
      ) : pageTotal === 0 ? (
        <div>
          <EmptyState icon="adr" title={t('adrs.empty.title')} body={t('adrs.empty.body')} />
          <div className="adr-empty-actions">
            <Button variant="primary" onClick={() => setCreateOpen(true)}>
              <Icon name="plus" size={16} aria-hidden /> {t('adrs.newAdr')}
            </Button>
            {hasFilters && <Button variant="secondary" onClick={clearFilters}>{t('adrs.clearFilters')}</Button>}
          </div>
        </div>
      ) : (
        <>
          <AdrsTable rows={data!.items} lang={i18n.language} sort={{ by: sortCol, dir: sortDir }} onSort={onSort} />
          <div className="adr-foot">
            <Pagination
              page={page}
              pageCount={data!.totalPages}
              onPageChange={setPage}
              labels={{ nav: t('adrs.pageNav'), previous: t('adrs.prevPage'), next: t('adrs.nextPage') }}
            />
          </div>
        </>
      )}

      <CreateAdrDialog open={createOpen} onClose={() => setCreateOpen(false)} />
    </section>
  );
}

// Table-shaped loading skeleton — 8 shimmer rows over the 4-column gAdr grid (design's loading state).
function AdrsSkeleton() {
  const { t } = useTranslation();
  const rowWidths = ['82%', '58%', '64%', '70%', '48%', '76%', '54%', '68%'];
  return (
    <div className="table-wrap" role="status" aria-busy="true">
      <span className="visually-hidden">{t('common.loading')}</span>
      <div className="adr-skel-head" aria-hidden="true">
        {Array.from({ length: 4 }).map((_, i) => (
          <span key={i} className="skeleton adr-skel-bar" style={{ inlineSize: 54 }} />
        ))}
      </div>
      {rowWidths.map((w, i) => (
        <div key={i} className="adr-skel-row" aria-hidden="true">
          <span className="skeleton adr-skel-bar" style={{ inlineSize: 92 }} />
          <span className="skeleton adr-skel-bar" style={{ inlineSize: w }} />
          <span className="skeleton adr-skel-bar" style={{ inlineSize: 78 }} />
          <span className="skeleton adr-skel-bar" style={{ inlineSize: 96 }} />
        </div>
      ))}
    </div>
  );
}

function Supersede({ row }: { row: AdrSummary }) {
  const { t } = useTranslation();
  if (!row.isSuperseded) return <span className="adr-muted">—</span>;
  return <StatusChip tone="neutral" label={t('adrs.status.Superseded')} size="sm" />;
}

function AdrsTable({ rows, lang, sort, onSort }: { rows: AdrSummary[]; lang: string; sort: { by: string; dir: SortDir }; onSort: (id: string) => void }) {
  const { t } = useTranslation();
  const pick = (title: AdrSummary['title']) => (lang === 'ar' ? title.ar : title.en);
  const columns: Column<AdrSummary>[] = [
    { id: 'key', header: t('adrs.col.key'), width: '132px', sortable: true, cell: (r) => <span className="adr-key">{r.key}</span> },
    { id: 'title', header: t('adrs.col.title'), sortable: true, cell: (r) => <Link className="adr-title-link" to={`/adrs/${r.key}`}>{pick(r.title)}</Link> },
    { id: 'status', header: t('adrs.col.status'), width: '128px', sortable: true, cell: (r) => <StatusChip tone={statusTone(r.status)} label={t(`adrs.status.${r.status}`)} size="sm" /> },
    { id: 'supersede', header: t('adrs.col.supersede'), width: '150px', cell: (r) => <Supersede row={r} /> },
  ];
  return <Table caption={t('adrs.tableCaption')} columns={columns} rows={rows} getRowKey={(r) => r.id} sort={sort} onSortChange={onSort} />;
}
