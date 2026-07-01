/*
 * Actions register (P8b) — the filtered/sorted/paged view over Actions (W13/W14),
 * matching the "ACMP Lists & Registers.dc.html" `isActions` table. Composes the shared
 * library (Table, FilterChip, StatusChip, Button, states, Icon, Pagination); only
 * action-specific cells (owner avatar, due+overdue, progress bar) carry local CSS.
 * Wired to GET /api/actions (useActionsRegister) + the global header counts
 * (useActionsCounts). Behaviour source of truth = the planning package.
 *
 * Design↔behaviour reconciliations (visual SoT = "ACMP Lists & Registers.dc.html"; data SoT = package):
 *  - The design's "Data: live/loading/empty/error" segmented is a mock preview toggle, not a product
 *    control — real state comes from the query (dropped, like the backlog register).
 *  - Header "N actions" + "N overdue" are GLOBAL (filter-independent, as the design computes them) so
 *    they come from useActionsCounts, not the filtered page; the "Showing X of Y" line is the paged count.
 *  - Only server-backed sorts are exposed (due/progress/status — GetActionsRegister.Sort). The design's
 *    ID/Action/Owner sorts have no server sort, so those columns are not sortable.
 *  - Owner filter is rendered (design parity) but disabled this slice — it needs a verified owner
 *    directory keyed to Keycloak subjects; follow-up.
 *  - Overdue is a server filter (?overdue=true), not the design's client toggle — correct under paging.
 *  - No create button: actions are always raised from a source page (P8b2b), never standalone. Saved
 *    view is an honest disabled stub (not built).
 *  - Row navigation = a link on the Action title (accessible primary action), not a whole-row button.
 */
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useActionsRegister, useActionsCounts, type ActionSummary } from '../../api/actions';
import { Table, type Column, type SortDir } from '../../components/ui/Table';
import { Pagination } from '../../components/ui/Pagination';
import { StatusChip } from '../../components/ui/StatusChip';
import { FilterChip } from '../../components/ui/FilterChip';
import { Button } from '../../components/ui/Button';
import { ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { statusTone, progressColor, initials, ACTION_STATUSES } from './actionMeta';
import './actions.css';

// Column id → API sortBy. Only these three have a server sort (GetActionsRegister.Sort).
const SORT_PARAM: Record<string, string> = { due: 'due', progress: 'progress', status: 'status' };
const PAGE_SIZE = 25;

interface Filters {
  statuses: string[];
}

export function ActionsRegister() {
  const { t, i18n } = useTranslation();
  const [filters, setFilters] = useState<Filters>({ statuses: [] });
  const [overdueOnly, setOverdueOnly] = useState(false);
  const [sortCol, setSortCol] = useState('due');
  const [sortDir, setSortDir] = useState<SortDir>('asc');
  const [page, setPage] = useState(1);

  // Reset to page 1 on any filter/sort change.
  useEffect(() => {
    setPage(1);
  }, [filters, overdueOnly, sortCol, sortDir]);

  const { data, isLoading, isError, refetch } = useActionsRegister({
    statuses: filters.statuses.length ? filters.statuses : undefined,
    overdue: overdueOnly || undefined,
    sortBy: SORT_PARAM[sortCol] ?? 'due',
    sortDir,
    page,
    pageSize: PAGE_SIZE,
  });
  const counts = useActionsCounts();

  const clearFilters = () => {
    setFilters({ statuses: [] });
    setOverdueOnly(false);
  };
  const onSort = (col: string) => {
    if (sortCol === col) setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'));
    else {
      setSortCol(col);
      setSortDir('asc');
    }
  };

  const total = data?.total ?? 0;
  const shown = data?.items.length ?? 0;
  const hasFilters = filters.statuses.length > 0 || overdueOnly;

  return (
    <section className="page">
      <div className="act-head">
        <div>
          <h1 className="page-title">{t('actions.title')}</h1>
          <div className="act-head-sub">
            <span className="act-head-count">{t('actions.count', { count: counts.total ?? 0 })}</span>
            {!!counts.overdue && (
              <>
                <span className="act-head-sep" aria-hidden="true" />
                <span className="act-head-overdue">
                  <span className="act-dot" aria-hidden="true" />
                  {t('actions.overdueCount', { count: counts.overdue })}
                </span>
              </>
            )}
          </div>
        </div>
        {/* No create entry point here: an action is ALWAYS raised from a source artifact (a decision,
            a meeting…), never standalone — so create lives on those pages, not the register (P8b2b). */}
      </div>

      <div className="act-bar" role="search" aria-label={t('actions.filtersLabel')}>
        <button type="button" className="act-saved" disabled title={t('actions.comingSoon')}>
          <Icon name="funnel" size={14} aria-hidden /> {t('actions.savedView')}
          <Icon name="chevronDown" size={13} aria-hidden />
        </button>
        <span className="act-divider" aria-hidden="true" />
        <FilterChip
          multiple
          label={t('actions.filter.status')}
          options={ACTION_STATUSES.map((v) => ({ value: v, label: t(`actions.status.${v}`) }))}
          values={filters.statuses}
          onChange={(statuses) => setFilters({ statuses })}
          clearLabel={t('actions.clearFilters')}
        />
        <FilterChip label={t('actions.filter.owner')} anyLabel={t('actions.filter.anyOwner')} options={[]} value="" onChange={() => {}} disabled />
        <button
          type="button"
          className={`fchip ${overdueOnly ? 'active' : ''}`}
          aria-pressed={overdueOnly}
          onClick={() => setOverdueOnly((v) => !v)}
        >
          <Icon name="clock" size={14} aria-hidden /> {t('actions.filter.overdue')}
          {overdueOnly && <span className="act-toggle-on">{t('actions.on')}</span>}
        </button>
        {data && (
          <span className="act-count"><Icon name="backlog" size={13} aria-hidden /> {t('actions.showing', { shown, total })}</span>
        )}
      </div>

      {isLoading ? (
        <ActionsSkeleton />
      ) : isError ? (
        <ErrorState title={t('actions.error.title')} body={t('actions.error.body')} onRetry={() => refetch()} />
      ) : total === 0 ? (
        <div>
          <EmptyState icon="action" title={t('actions.empty.title')} body={t('actions.empty.body')} />
          {hasFilters && (
            <div className="act-empty-actions">
              <Button variant="secondary" onClick={clearFilters}>{t('actions.clearFilters')}</Button>
            </div>
          )}
        </div>
      ) : (
        <>
          <ActionsTable rows={data!.items} lang={i18n.language} sort={{ by: sortCol, dir: sortDir }} onSort={onSort} />
          <div className="act-foot">
            <Pagination
              page={page}
              pageCount={data!.totalPages}
              onPageChange={setPage}
              labels={{ nav: t('actions.pageNav'), previous: t('actions.prevPage'), next: t('actions.nextPage') }}
            />
          </div>
        </>
      )}
    </section>
  );
}

// Table-shaped loading skeleton — 8 shimmer rows over the 7-column grid (design's loading state).
function ActionsSkeleton() {
  const { t } = useTranslation();
  const rowWidths = ['62%', '80%', '54%', '72%', '48%', '66%', '58%', '76%'];
  return (
    <div className="table-wrap" role="status" aria-busy="true">
      <span className="visually-hidden">{t('common.loading')}</span>
      <div className="act-skel-head" aria-hidden="true">
        {Array.from({ length: 7 }).map((_, i) => (
          <span key={i} className="skeleton act-skel-bar" style={{ inlineSize: 54 }} />
        ))}
      </div>
      {rowWidths.map((w, i) => (
        <div key={i} className="act-skel-row" aria-hidden="true">
          <span className="skeleton act-skel-bar" style={{ inlineSize: 70 }} />
          <span className="skeleton act-skel-bar" style={{ inlineSize: w }} />
          <span className="skeleton act-skel-bar" style={{ inlineSize: 60 }} />
          <span className="skeleton act-skel-bar" style={{ inlineSize: 84 }} />
          <span className="skeleton act-skel-bar" style={{ inlineSize: 60 }} />
          <span className="skeleton act-skel-bar" style={{ inlineSize: 72 }} />
          <span className="skeleton act-skel-bar" style={{ inlineSize: 64 }} />
        </div>
      ))}
    </div>
  );
}

function Owner({ name }: { name: string }) {
  const { t } = useTranslation();
  if (!name) return <span className="act-muted">{t('actions.unassigned')}</span>;
  return (
    <span className="act-owner">
      <span className="act-avatar" aria-hidden="true">{initials(name)}</span>
      <span className="act-owner-name">{name}</span>
    </span>
  );
}

function Due({ iso, overdue, lang }: { iso: string | null; overdue: boolean; lang: string }) {
  const { t } = useTranslation();
  if (!iso) return <span className="act-muted">—</span>;
  const label = new Intl.DateTimeFormat(lang, { dateStyle: 'medium' }).format(new Date(iso));
  return (
    <span className={`act-due ${overdue ? 'overdue' : ''}`}>
      {overdue && <Icon name="clock" size={13} aria-hidden />}
      {label}
      {overdue && <span className="visually-hidden"> — {t('actions.overdue')}</span>}
    </span>
  );
}

function Progress({ pct }: { pct: number }) {
  return (
    <span className="act-progress">
      <span className="act-pbar" aria-hidden="true">
        <span className="act-pbar-fill" style={{ inlineSize: `${pct}%`, background: progressColor(pct) }} />
      </span>
      <span className="act-pct">{pct}%</span>
    </span>
  );
}

function ActionsTable({ rows, lang, sort, onSort }: { rows: ActionSummary[]; lang: string; sort: { by: string; dir: SortDir }; onSort: (id: string) => void }) {
  const { t } = useTranslation();
  const pick = (title: ActionSummary['title']) => (lang === 'ar' ? title.ar : title.en);
  const columns: Column<ActionSummary>[] = [
    { id: 'key', header: t('actions.col.key'), width: '108px', cell: (r) => <span className="act-key">{r.key}</span> },
    { id: 'action', header: t('actions.col.action'), cell: (r) => <Link className="act-title-link" to={`/actions/${r.key}`}>{pick(r.title)}</Link> },
    { id: 'linked', header: t('actions.col.linked'), width: '122px', cell: (r) => <span className="act-linked">{r.sourceKey ?? '—'}</span> },
    { id: 'owner', header: t('actions.col.owner'), width: '138px', cell: (r) => <Owner name={r.ownerName} /> },
    { id: 'due', header: t('actions.col.due'), width: '118px', sortable: true, cell: (r) => <Due iso={r.dueDate} overdue={r.isOverdue} lang={lang} /> },
    { id: 'progress', header: t('actions.col.progress'), width: '130px', sortable: true, cell: (r) => <Progress pct={r.progressPct} /> },
    { id: 'status', header: t('actions.col.status'), width: '124px', sortable: true, cell: (r) => <StatusChip tone={statusTone(r.status)} label={t(`actions.status.${r.status}`)} size="sm" /> },
  ];
  return <Table caption={t('actions.tableCaption')} columns={columns} rows={rows} getRowKey={(r) => r.id} sort={sort} onSortChange={onSort} />;
}
