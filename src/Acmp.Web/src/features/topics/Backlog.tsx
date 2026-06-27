/*
 * Backlog (P5b) — the filtered/sorted/paged view over Topics (W3), matching the
 * "ACMP Backlog & Topic" design file (backlog screen). Composes the shared library
 * (Breadcrumb, Segmented, Select, MultiSelect, Table, StatusChip, Tag, Pagination,
 * states) — only backlog-specific cells/layout carry local CSS. Wired to
 * GET /api/topics (useBacklog). Behavior source of truth = the planning package.
 *
 * Design↔behavior reconciliations (visual SoT = design; data SoT = package):
 *  - The design's "Data: live/loading/empty/error" segmented is a mock preview toggle,
 *    not a product control — real state comes from the query (dropped, like the dev role switcher).
 *  - Aging badge is driven by the DTO's `slaBreached` (real time-in-status SLA signal, AC-057),
 *    not the design's raw age-day color thresholds.
 *  - Only API-backed sorts are exposed (title/status/age/urgency); the design's Owner sort has no
 *    server sort, so that column is not sortable.
 *  - Stream/Owner filters are rendered (design parity, 5 chips) but disabled this slice — they need a
 *    verified option source (stream registry + owner directory keyed to topic owner ids); follow-up.
 *  - Kanban/Calendar/Timeline render honest "coming soon" shells (kanban → PR4; calendar/timeline need
 *    meeting/decision data → P6). Export/Saved-views are disabled affordances for the same reason.
 *  - Row navigation = a link on the title (accessible primary action) rather than a whole-row button,
 *    which doesn't nest cleanly in table grid semantics.
 */
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useBacklog, type BacklogParams, type TopicSummary } from '../../api/topics';
import { AREAS } from '../../nav/navModel';
import { Breadcrumb } from '../../components/ui/Breadcrumb';
import { Segmented } from '../../components/ui/Segmented';
import { FilterChip } from '../../components/ui/FilterChip';
import { Table, type Column, type SortDir } from '../../components/ui/Table';
import { Pagination } from '../../components/ui/Pagination';
import { StatusChip } from '../../components/ui/StatusChip';
import { Tag } from '../../components/ui/Chip';
import { Button } from '../../components/ui/Button';
import { ErrorState, EmptyState } from '../../components/states';
import { Icon, type IconName } from '../../components/icons';
import { statusTone, initials } from './topicMeta';
import { Kanban } from './Kanban';
import { Calendar } from './Calendar';
import { Timeline } from './Timeline';
import './topics.css';

const VIEWS: { id: string; icon: IconName }[] = [
  { id: 'list', icon: 'viewList' },
  { id: 'table', icon: 'viewTable' },
  { id: 'kanban', icon: 'viewKanban' },
  { id: 'calendar', icon: 'calendar' },
  { id: 'timeline', icon: 'viewTimeline' },
];
const TYPE_VALUES = ['ResearchDiscovery', 'ArchitectureDecision', 'EnhancementInnovation', 'GovernanceStandardization'];
const URGENCY_VALUES = ['Normal', 'Urgent', 'Critical'];
const STATUS_VALUES = ['Draft', 'Submitted', 'Triage', 'Accepted', 'Prepared', 'Scheduled', 'InCommittee', 'Decided', 'Deferred', 'Reopened', 'Rejected', 'Closed', 'Converted'];
// Column id → API sortBy. Only these four have a server sort (GetBacklog.Sort).
const SORT_PARAM: Record<string, string> = { topic: 'title', status: 'status', age: 'age', urgency: 'urgency' };
const PAGE_SIZE = 25;

interface Filters {
  statuses: string[];
  type: string;
  urgency: string;
}

export function Backlog() {
  const { t } = useTranslation();
  const [view, setView] = useState('table');
  const [search, setSearch] = useState('');
  const [searchParam, setSearchParam] = useState('');
  const [filters, setFilters] = useState<Filters>({ statuses: [], type: '', urgency: '' });
  const [sortCol, setSortCol] = useState('age');
  const [sortDir, setSortDir] = useState<SortDir>('desc');
  const [page, setPage] = useState(1);

  // Debounce the search box into a query param; reset to page 1 on any filter change.
  useEffect(() => {
    const id = setTimeout(() => setSearchParam(search.trim()), 300);
    return () => clearTimeout(id);
  }, [search]);
  useEffect(() => {
    setPage(1);
  }, [searchParam, filters, sortCol, sortDir]);

  const params: BacklogParams = {
    statuses: filters.statuses.length ? filters.statuses : undefined,
    type: filters.type || undefined,
    urgency: filters.urgency || undefined,
    search: searchParam || undefined,
    sortBy: SORT_PARAM[sortCol] ?? 'age',
    sortDir,
    page,
    pageSize: PAGE_SIZE,
  };
  const { data, isLoading, isError, refetch } = useBacklog(params);

  const patch = (p: Partial<Filters>) => setFilters((f) => ({ ...f, ...p }));
  const clearFilters = () => {
    setFilters({ statuses: [], type: '', urgency: '' });
    setSearch('');
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

  return (
    <section className="page">
      <Breadcrumb
        ariaLabel={t('topics.backlog')}
        items={[{ label: t('topics.home'), href: AREAS.home.path }, { label: t('topics.backlog'), current: true }]}
      />

      <div className="bk-head">
        <div>
          <h1 className="page-title">{t('topics.backlog')}</h1>
          <div className="bk-head-sub">{t('topics.count', { total })}</div>
        </div>
        <div className="bk-head-actions">
          <Button variant="secondary" disabled title={t('topics.comingSoon')}>
            <Icon name="download" size={15} aria-hidden /> {t('topics.export')}
          </Button>
          <Link className="btn btn-primary" to={AREAS.submit.path}>
            <Icon name="plus" size={15} aria-hidden /> {t('topics.newTopic')}
          </Link>
        </div>
      </div>

      <div className="bk-bar">
        <Segmented
          ariaLabel={t('topics.viewLabel')}
          value={view}
          onValueChange={setView}
          items={VIEWS.map((v) => ({
            id: v.id,
            label: (
              <span className="bk-view-item">
                <Icon name={v.icon} size={15} aria-hidden /> {t(`topics.view.${v.id}`)}
              </span>
            ),
          }))}
        />
        <button type="button" className="bk-saved" disabled title={t('topics.comingSoon')}>
          <Icon name="funnel" size={14} aria-hidden /> {t('topics.savedView')}
          <Icon name="chevronDown" size={13} aria-hidden />
        </button>
      </div>

      <div className="bk-filters" role="search" aria-label={t('topics.filtersLabel')}>
        <span className="bk-search">
          <Icon name="search" size={15} aria-hidden />
          <input
            type="search"
            aria-label={t('topics.searchLabel')}
            placeholder={t('topics.searchPlaceholder')}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </span>
        <span className="bk-divider" aria-hidden="true" />
        <FilterChip
          multiple
          label={t('topics.filter.status')}
          options={STATUS_VALUES.map((v) => ({ value: v, label: t(`topics.status.${v}`) }))}
          values={filters.statuses}
          onChange={(statuses) => patch({ statuses })}
          clearLabel={t('topics.clearFilters')}
        />
        <FilterChip
          label={t('topics.filter.type')}
          anyLabel={t('topics.filter.anyType')}
          options={TYPE_VALUES.map((v) => ({ value: v, label: t(`topics.type.${v}`) }))}
          value={filters.type}
          onChange={(type) => patch({ type })}
        />
        <FilterChip label={t('topics.filter.stream')} anyLabel={t('topics.filter.anyStream')} options={[]} value="" onChange={() => {}} disabled />
        <FilterChip label={t('topics.filter.owner')} anyLabel={t('topics.filter.anyOwner')} options={[]} value="" onChange={() => {}} disabled />
        <FilterChip
          label={t('topics.filter.urgency')}
          anyLabel={t('topics.filter.anyUrgency')}
          options={URGENCY_VALUES.map((v) => ({ value: v, label: t(`topics.urgency.${v}`) }))}
          value={filters.urgency}
          onChange={(urgency) => patch({ urgency })}
        />
        {data && (
          <span className="bk-count"><Icon name="backlog" size={13} aria-hidden /> {t('topics.showing', { shown, total })}</span>
        )}
      </div>

      {isLoading ? (
        <BacklogSkeleton />
      ) : isError ? (
        <ErrorState title={t('topics.error.title')} body={t('topics.error.body')} onRetry={() => refetch()} />
      ) : view === 'calendar' ? (
        <Calendar />
      ) : view === 'timeline' ? (
        <Timeline rows={data?.items ?? []} />
      ) : total === 0 ? (
        <div>
          <EmptyState icon="search" title={t('topics.empty.title')} body={t('topics.empty.body')} />
          <div className="bk-empty-actions">
            <Button variant="secondary" onClick={clearFilters}>{t('topics.clearFilters')}</Button>
            <Link className="btn btn-primary" to={AREAS.submit.path}>{t('topics.newTopic')}</Link>
          </div>
        </div>
      ) : (
        <>
          {view === 'table' ? (
            <TopicsTable rows={data!.items} sort={{ by: sortCol, dir: sortDir }} onSort={onSort} />
          ) : view === 'list' ? (
            <TopicsList rows={data!.items} />
          ) : (
            <Kanban rows={data!.items} />
          )}
          {view !== 'kanban' && (
            <div className="bk-foot">
              <Pagination
                page={page}
                pageCount={data!.totalPages}
                onPageChange={setPage}
                labels={{ nav: t('topics.pageNav'), previous: t('topics.prevPage'), next: t('topics.nextPage') }}
              />
            </div>
          )}
        </>
      )}
    </section>
  );
}

// Table-shaped loading skeleton (matches the design's skeleton header + shimmer rows on the grid).
function BacklogSkeleton() {
  const { t } = useTranslation();
  const rowWidths = ['62%', '80%', '54%', '72%', '48%', '66%', '58%', '76%'];
  return (
    <div className="table-wrap" role="status" aria-busy="true">
      <span className="visually-hidden">{t('common.loading')}</span>
      <div className="bk-skel-head" aria-hidden="true">
        {Array.from({ length: 6 }).map((_, i) => (
          <span key={i} className="skeleton bk-skel-bar" style={{ inlineSize: 54 }} />
        ))}
      </div>
      {rowWidths.map((w, i) => (
        <div key={i} className="bk-skel-row" aria-hidden="true">
          <span className="skeleton bk-skel-bar" style={{ inlineSize: 70 }} />
          <span className="skeleton bk-skel-bar" style={{ inlineSize: w }} />
          <span className="skeleton bk-skel-bar" style={{ inlineSize: 60 }} />
          <span className="skeleton bk-skel-bar" style={{ inlineSize: 50 }} />
          <span className="skeleton bk-skel-bar" style={{ inlineSize: 64 }} />
          <span className="skeleton bk-skel-bar" style={{ inlineSize: 64 }} />
        </div>
      ))}
    </div>
  );
}

function UrgentMark() {
  const { t } = useTranslation();
  return (
    <span className="bk-urgent-ic">
      <Icon name="warnTriangle" size={13} aria-hidden />
      <span className="visually-hidden">{t('topics.urgent')}</span>
    </span>
  );
}

function Owner({ id, name }: { id: string | null; name: string | null }) {
  const { t } = useTranslation();
  if (!id || !name) return <span className="bk-muted">{t('topics.unassigned')}</span>;
  return (
    <span className="bk-owner">
      <span className="bk-avatar" aria-hidden="true">{initials(name)}</span>
      <span className="bk-owner-name">{name}</span>
    </span>
  );
}

function Age({ days, breached }: { days: number; breached: boolean }) {
  const { t } = useTranslation();
  return (
    <span className={`bk-age ${breached ? 'breached' : ''}`}>
      {breached && <Icon name="warnTriangle" size={12} aria-hidden />}
      {t('topics.age', { days })}
      {breached && <span className="visually-hidden"> — {t('topics.slaBreached')}</span>}
    </span>
  );
}

function TopicsTable({ rows, sort, onSort }: { rows: TopicSummary[]; sort: { by: string; dir: SortDir }; onSort: (id: string) => void }) {
  const { t } = useTranslation();
  const columns: Column<TopicSummary>[] = [
    { id: 'key', header: t('topics.col.key'), width: '112px', cell: (r) => <span className="bk-key">{r.key}</span> },
    {
      id: 'topic',
      header: t('topics.col.topic'),
      sortable: true,
      cell: (r) => (
        <span className="bk-topic">
          {r.urgency !== 'Normal' && <UrgentMark />}
          <Link className="bk-topic-link" to={`/topics/${r.key}`}>{r.title}</Link>
        </span>
      ),
    },
    { id: 'type', header: t('topics.col.type'), width: '124px', cell: (r) => <span className="bk-type">{t(`topics.type.${r.type}`)}</span> },
    {
      id: 'streams',
      header: t('topics.col.streams'),
      width: '150px',
      cell: (r) => <span className="bk-streams">{r.streams.map((s) => <Tag key={s}>{s}</Tag>)}</span>,
    },
    { id: 'owner', header: t('topics.col.owner'), width: '140px', cell: (r) => <Owner id={r.ownerId} name={r.ownerName} /> },
    { id: 'status', header: t('topics.col.status'), width: '104px', sortable: true, cell: (r) => <StatusChip tone={statusTone(r.status)} label={t(`topics.status.${r.status}`)} size="sm" /> },
    { id: 'age', header: t('topics.col.age'), width: '96px', sortable: true, cell: (r) => <Age days={r.ageDays} breached={r.slaBreached} /> },
    {
      id: 'urgency',
      header: t('topics.col.urgency'),
      width: '84px',
      sortable: true,
      cell: (r) => <span className={`bk-urg ${r.urgency.toLowerCase()}`}>{t(`topics.urgency.${r.urgency}`)}</span>,
    },
  ];
  return <Table caption={t('topics.tableCaption')} columns={columns} rows={rows} getRowKey={(r) => r.id} sort={sort} onSortChange={onSort} />;
}

function TopicsList({ rows }: { rows: TopicSummary[] }) {
  const { t } = useTranslation();
  return (
    <div className="bk-list">
      {rows.map((r) => {
        const tone = statusTone(r.status);
        return (
          <Link key={r.id} className="bk-list-item" to={`/topics/${r.key}`}>
            <span className={`bk-list-accent tone-${tone}`} aria-hidden="true" />
            <span className="bk-list-main">
              <span className="bk-list-id">
                <span className="bk-key">{r.key}</span>
                {r.urgency !== 'Normal' && (
                  <span className="bk-urgent-pill">
                    <Icon name="warnTriangle" size={10} aria-hidden /> {t('topics.urgent')}
                  </span>
                )}
              </span>
              <span className="bk-list-title">{r.title}</span>
              <span className="bk-list-meta">
                <span>{t(`topics.type.${r.type}`)}</span>
                <Owner id={r.ownerId} name={r.ownerName} />
                {r.streams.map((s) => <Tag key={s}>{s}</Tag>)}
              </span>
            </span>
            <span className="bk-list-side">
              <StatusChip tone={tone} label={t(`topics.status.${r.status}`)} />
              <Age days={r.ageDays} breached={r.slaBreached} />
            </span>
          </Link>
        );
      })}
    </div>
  );
}
