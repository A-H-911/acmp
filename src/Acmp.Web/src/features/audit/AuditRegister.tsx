/*
 * Audit register (PR4) — the read-only Auditor view over the immutable, hash-chained
 * AuditEvent log, matching the "ACMP Lists & Registers.dc.html" `isAudit` trail (gAudit,
 * 5 columns: Timestamp · Actor · Action · Artifact · Detail). Composes the shared library
 * (Table, FilterChip, StatusChip, states, Icon, Pagination). Wired to GET /api/audit
 * (useAuditRegister); the API enforces Policies.AuditRead (Auditor/Chairman/Secretary).
 *
 * Read-only BY CONSTRUCTION: rows are not links (no drill-in — the record is append-only,
 * immutable), there is no create/edit affordance, and a read-only banner states it.
 *
 * Design↔behaviour reconciliations (see auditMeta for the data-shape rationale):
 *  - The mock's "Export log" button is dropped (no export endpoint this slice; a real CSV
 *    export is a follow-up, like the register chrome the risks slice dropped).
 *  - Of the mock's four filters, only "Artifact type" (→ entityType) is wired; Actor / Action
 *    / Date range render as inert parity chips (they need an actor directory / action catalog /
 *    date-range picker — follow-up, same call as the risks register's disabled Owner chip).
 *  - Detail shows the localized Outcome; the mock's narrative sentences + human artifact keys
 *    are not reconstructable from the structured row without a cross-module key lookup (owed).
 */
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuditRegister, type AuditEvent } from '../../api/audit';
import { Table, type Column } from '../../components/ui/Table';
import { Pagination } from '../../components/ui/Pagination';
import { StatusChip } from '../../components/ui/StatusChip';
import { FilterChip } from '../../components/ui/FilterChip';
import { ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { auditTone, outcomeTone, actorInitials, formatTimestamp, AUDIT_ENTITY_TYPES } from './auditMeta';
import './audit.css';

const PAGE_SIZE = 25;

export function AuditRegister() {
  const { t, i18n } = useTranslation();
  const [entityType, setEntityType] = useState('');
  const [page, setPage] = useState(1);

  // Reset to page 1 on any filter change.
  useEffect(() => {
    setPage(1);
  }, [entityType]);

  const { data, isLoading, isError, refetch } = useAuditRegister({
    entityType: entityType || undefined,
    page,
    pageSize: PAGE_SIZE,
  });

  const total = data?.total ?? 0;
  const shown = data?.items.length ?? 0;

  const readOnly = (
    <span className="aud-readonly">
      <Icon name="lock" size={13} aria-hidden /> {t('audit.readonly')}
    </span>
  );

  return (
    <section className="page">
      <div className="aud-head">
        <div>
          <h1 className="page-title">{t('audit.title')}</h1>
          <div className="aud-head-sub">
            <span className="aud-head-count">{t('audit.count', { count: total })}</span>
            <span className="aud-head-sep" aria-hidden="true" />
            {readOnly}
          </div>
        </div>
      </div>

      <div className="aud-bar" role="search" aria-label={t('audit.filtersLabel')}>
        <FilterChip
          label={t('audit.filter.type')}
          anyLabel={t('audit.filter.anyType')}
          options={AUDIT_ENTITY_TYPES.map((v) => ({ value: v, label: t(`audit.entity.${v}`) }))}
          value={entityType}
          onChange={setEntityType}
        />
        <FilterChip label={t('audit.filter.actor')} anyLabel={t('audit.filter.anyActor')} options={[]} value="" onChange={() => {}} disabled />
        <FilterChip label={t('audit.filter.action')} anyLabel={t('audit.filter.anyAction')} options={[]} value="" onChange={() => {}} disabled />
        <FilterChip label={t('audit.filter.date')} anyLabel={t('audit.filter.anyDate')} options={[]} value="" onChange={() => {}} disabled />
        {data && (
          <span className="aud-count"><Icon name="backlog" size={13} aria-hidden /> {t('audit.showing', { shown, total })}</span>
        )}
      </div>

      {isLoading ? (
        <AuditSkeleton />
      ) : isError ? (
        <ErrorState title={t('audit.error.title')} body={t('audit.error.body')} onRetry={() => refetch()} />
      ) : total === 0 ? (
        <EmptyState icon="audit" title={t('audit.empty.title')} body={t('audit.empty.body')} />
      ) : (
        <>
          {/* Table + banner share one card: the banner is the design's in-card footer strip
              (border-top only), not a detached second box. .aud-card neutralizes the shared
              table-wrap's own bottom radius/border so there is no double-border seam. */}
          <div className="aud-card">
            <AuditTable rows={data!.items} lang={i18n.language} t={t} />
            <div className="aud-banner">{readOnly}</div>
          </div>
          <div className="aud-foot">
            <Pagination
              page={page}
              pageCount={data!.totalPages}
              onPageChange={setPage}
              labels={{ nav: t('audit.pageNav'), previous: t('audit.prevPage'), next: t('audit.nextPage') }}
            />
          </div>
        </>
      )}
    </section>
  );
}

// Table-shaped loading skeleton — 8 shimmer rows over the 5-column gAudit grid.
function AuditSkeleton() {
  const { t } = useTranslation();
  const rowWidths = ['70%', '58%', '46%', '52%', '80%'];
  return (
    <div className="table-wrap" role="status" aria-busy="true">
      <span className="visually-hidden">{t('common.loading')}</span>
      <div className="aud-skel-head" aria-hidden="true">
        {Array.from({ length: 5 }).map((_, i) => (
          <span key={i} className="skeleton aud-skel-bar" style={{ inlineSize: 54 }} />
        ))}
      </div>
      {Array.from({ length: 8 }).map((_, i) => (
        <div key={i} className="aud-skel-row" aria-hidden="true">
          {rowWidths.map((w, c) => (
            <span key={c} className="skeleton aud-skel-bar" style={{ inlineSize: w }} />
          ))}
        </div>
      ))}
    </div>
  );
}

function Actor({ row }: { row: AuditEvent }) {
  const { t } = useTranslation();
  if (!row.actor) {
    return (
      <span className="aud-actor">
        <span className="aud-avatar aud-avatar-sys" aria-hidden="true">
          <Icon name="cog" size={12} />
        </span>
        <span className="aud-actor-name">{t('audit.system')}</span>
      </span>
    );
  }
  return (
    <span className="aud-actor">
      <span className="aud-avatar" aria-hidden="true">{actorInitials(row.actor)}</span>
      <span className="aud-actor-lines">
        <span className="aud-actor-name">{row.actor}</span>
        {row.actorRole && <span className="aud-actor-role">{row.actorRole}</span>}
      </span>
    </span>
  );
}

function Artifact({ row }: { row: AuditEvent; }) {
  const { t } = useTranslation();
  if (!row.subjectType) return <span className="aud-muted">—</span>;
  // Localize the known CLR type names; fall back to the raw string for any not in the map.
  const label = t(`audit.entity.${row.subjectType}`, { defaultValue: row.subjectType });
  return (
    <span className="aud-artifact">
      <span className="aud-artifact-type">{label}</span>
      {row.subjectId && <span className="aud-artifact-id" dir="ltr">{row.subjectId.slice(0, 8)}</span>}
    </span>
  );
}

type TFn = ReturnType<typeof useTranslation>['t'];

function AuditTable({ rows, lang, t }: { rows: AuditEvent[]; lang: string; t: TFn }) {
  const columns: Column<AuditEvent>[] = [
    { id: 'when', header: t('audit.col.when'), width: '176px', cell: (r) => <span className="aud-ts" dir="ltr">{formatTimestamp(r.occurredAt, lang)}</span> },
    { id: 'actor', header: t('audit.col.actor'), width: '190px', cell: (r) => <Actor row={r} /> },
    { id: 'action', header: t('audit.col.action'), width: '150px', cell: (r) => <StatusChip tone={auditTone(r.action, r.outcome)} label={r.action} size="sm" /> },
    { id: 'artifact', header: t('audit.col.artifact'), width: '150px', cell: (r) => <Artifact row={r} /> },
    {
      id: 'detail', header: t('audit.col.detail'),
      cell: (r) => (r.outcome
        ? <StatusChip tone={outcomeTone(r.outcome)} label={t(`audit.outcome.${r.outcome}`, { defaultValue: r.outcome })} size="sm" />
        : <span className="aud-muted">—</span>),
    },
  ];
  return <Table caption={t('audit.tableCaption')} columns={columns} rows={rows} getRowKey={(r) => String(r.sequence)} />;
}
