/*
 * Research register (P15b) — the filtered/sorted/paged view over research missions, matching the
 * "ACMP Research & Knowledge.dc.html" isResearch mission list (gMission). Composes the shared library
 * (Table, FilterChip, StatusChip, Button, states, Icon, Pagination). Wired to GET /api/research
 * (useResearchRegister) + the global header count (useResearchCounts).
 *
 * Design↔behaviour reconciliations (visual SoT = the .dc.html; data SoT = the P15a package):
 *  - The design's "Topic" and "Sources" columns are omitted / repurposed: ResearchMissionSummaryDto has
 *    no topic key and no sources count, but it does carry Findings + Recommendation counts — so the two
 *    numeric columns show those (guardrail #14).
 *  - "New mission" is gated to Chairman/Secretary: the P15a API denies Member/Reviewer (403 — the AiO
 *    can't resolve for a non-topic-scoped mission), so a Member must not see a button that always 403s.
 *    Reads stay open to every authenticated role.
 *  - Only server-backed sorts are exposed (Mission/title, Status, Created — GetMissionsRegister.Sort).
 */
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useResearchRegister, useResearchCounts, type MissionSummary } from '../../api/research';
import { useAuth, hasRole } from '../../auth/AcmpAuthContext';
import { Table, type Column, type SortDir } from '../../components/ui/Table';
import { Pagination } from '../../components/ui/Pagination';
import { StatusChip } from '../../components/ui/StatusChip';
import { FilterChip } from '../../components/ui/FilterChip';
import { Button } from '../../components/ui/Button';
import { ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { statusTone, initials, RESEARCH_STATUSES } from './researchMeta';
import { CreateMissionDialog } from './CreateMissionDialog';
import './research.css';

// Column id → API sortBy. Only these have a server sort (GetMissionsRegister.Sort: title/status/created).
const SORT_PARAM: Record<string, string> = { mission: 'title', status: 'status', created: 'created' };
const PAGE_SIZE = 25;

export function ResearchRegister() {
  const { t, i18n } = useTranslation();
  const auth = useAuth();
  const [statuses, setStatuses] = useState<string[]>([]);
  const [sortCol, setSortCol] = useState('created');
  const [sortDir, setSortDir] = useState<SortDir>('desc');
  const [page, setPage] = useState(1);
  const [createOpen, setCreateOpen] = useState(false);
  const canCreate = hasRole(auth, 'chairman', 'secretary');

  // Reset to page 1 on any filter/sort change.
  useEffect(() => {
    setPage(1);
  }, [statuses, sortCol, sortDir]);

  const { data, isLoading, isError, refetch } = useResearchRegister({
    statuses: statuses.length ? statuses : undefined,
    sortBy: SORT_PARAM[sortCol] ?? 'created',
    sortDir,
    page,
    pageSize: PAGE_SIZE,
  });
  const counts = useResearchCounts();

  const onSort = (col: string) => {
    if (sortCol === col) setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'));
    else {
      setSortCol(col);
      setSortDir('desc');
    }
  };

  const total = data?.total ?? 0;
  const shown = data?.items.length ?? 0;
  const hasFilters = statuses.length > 0;

  return (
    <section className="page">
      <div className="rsc-head">
        <div>
          <h1 className="page-title">{t('research.title')}</h1>
          <div className="rsc-head-sub">{t('research.count', { count: counts.total ?? 0 })}</div>
        </div>
        {canCreate && (
          <Button variant="primary" onClick={() => setCreateOpen(true)}>
            <Icon name="plus" size={16} aria-hidden /> {t('research.newMission')}
          </Button>
        )}
      </div>

      <div className="rsc-bar" role="search" aria-label={t('research.filtersLabel')}>
        <FilterChip
          multiple
          label={t('research.filter.status')}
          options={RESEARCH_STATUSES.map((v) => ({ value: v, label: t(`research.status.${v}`) }))}
          values={statuses}
          onChange={setStatuses}
          clearLabel={t('research.clearFilters')}
        />
        {data && (
          <span className="rsc-count"><Icon name="backlog" size={13} aria-hidden /> {t('research.showing', { shown, total })}</span>
        )}
      </div>

      {isLoading ? (
        <ResearchSkeleton />
      ) : isError ? (
        <ErrorState title={t('research.error.title')} body={t('research.error.body')} onRetry={() => refetch()} />
      ) : total === 0 ? (
        <div>
          <EmptyState icon="research" title={t('research.empty.title')} body={t('research.empty.body')} />
          {(canCreate || hasFilters) && (
            <div className="rsc-empty-actions">
              {canCreate && (
                <Button variant="primary" onClick={() => setCreateOpen(true)}>
                  <Icon name="plus" size={16} aria-hidden /> {t('research.newMission')}
                </Button>
              )}
              {hasFilters && <Button variant="secondary" onClick={() => setStatuses([])}>{t('research.clearFilters')}</Button>}
            </div>
          )}
        </div>
      ) : (
        <>
          <MissionsTable rows={data!.items} lang={i18n.language} sort={{ by: sortCol, dir: sortDir }} onSort={onSort} />
          <div className="rsc-foot">
            <Pagination
              page={page}
              pageCount={data!.totalPages}
              onPageChange={setPage}
              labels={{ nav: t('research.pageNav'), previous: t('research.prevPage'), next: t('research.nextPage') }}
            />
          </div>
        </>
      )}

      {canCreate && <CreateMissionDialog open={createOpen} onClose={() => setCreateOpen(false)} />}
    </section>
  );
}

// Table-shaped loading skeleton — 6 shimmer rows over the 6-column gMission grid.
function ResearchSkeleton() {
  const { t } = useTranslation();
  const rowWidths = ['62%', '58%', '70%', '52%', '66%', '48%'];
  return (
    <div className="table-wrap" role="status" aria-busy="true">
      <span className="visually-hidden">{t('common.loading')}</span>
      {rowWidths.map((w, i) => (
        <div key={i} className="rsc-skel-row" aria-hidden="true">
          <span className="skeleton rsc-skel-bar" style={{ inlineSize: w }} />
          <span className="skeleton rsc-skel-bar" style={{ inlineSize: 90 }} />
          <span className="skeleton rsc-skel-bar" style={{ inlineSize: 40 }} />
          <span className="skeleton rsc-skel-bar" style={{ inlineSize: 40 }} />
          <span className="skeleton rsc-skel-bar" style={{ inlineSize: 78 }} />
          <span className="skeleton rsc-skel-bar" style={{ inlineSize: 72 }} />
        </div>
      ))}
    </div>
  );
}

function Lead({ name }: { name: string }) {
  const { t } = useTranslation();
  if (!name) return <span className="rsc-muted">{t('research.unassigned')}</span>;
  return (
    <span className="rsc-owner">
      <span className="rsc-avatar" aria-hidden="true">{initials(name)}</span>
      <span className="rsc-owner-name">{name}</span>
    </span>
  );
}

function MissionsTable({ rows, lang, sort, onSort }: { rows: MissionSummary[]; lang: string; sort: { by: string; dir: SortDir }; onSort: (id: string) => void }) {
  const { t } = useTranslation();
  const pick = (title: MissionSummary['title']) => (lang === 'ar' ? title.ar : title.en);
  const fmtDate = (iso: string) => new Intl.DateTimeFormat(lang, { dateStyle: 'medium' }).format(new Date(iso));
  const columns: Column<MissionSummary>[] = [
    {
      id: 'mission', header: t('research.col.mission'), sortable: true, cell: (r) => (
        <span className="rsc-mission-cell">
          <Link className="rsc-title-link" to={`/research/${r.key}`}>{pick(r.title)}</Link>
          <span className="rsc-key">{r.key}</span>
        </span>
      ),
    },
    { id: 'lead', header: t('research.col.lead'), width: '150px', cell: (r) => <Lead name={r.ownerName} /> },
    { id: 'findings', header: t('research.col.findings'), width: '96px', cell: (r) => <span className="rsc-num">{r.findingCount}</span> },
    { id: 'recs', header: t('research.col.recs'), width: '110px', cell: (r) => <span className="rsc-num">{r.recommendationCount}</span> },
    { id: 'status', header: t('research.col.status'), width: '124px', sortable: true, cell: (r) => <StatusChip tone={statusTone(r.status)} label={t(`research.status.${r.status}`)} size="sm" /> },
    { id: 'created', header: t('research.col.created'), width: '128px', sortable: true, cell: (r) => <span className="rsc-date">{fmtDate(r.createdAt)}</span> },
  ];
  return <Table caption={t('research.tableCaption')} columns={columns} rows={rows} getRowKey={(r) => r.id} sort={sort} onSortChange={onSort} />;
}
