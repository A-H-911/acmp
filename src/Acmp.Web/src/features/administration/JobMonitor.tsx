/*
 * Administration → Job Monitor (AC-056; mirrors the "ACMP Administration" `jobs` section). Wired to
 * GET /api/admin/jobs: five stat tiles + a recent-runs table with a Retry (re-queue) button on failed
 * rows. Honest-sparse — it shows only the jobs that actually run (the action-reminder sweep), never the
 * mock's aspirational catalog; and when Hangfire isn't wired it says "monitoring not configured" rather
 * than rendering fabricated zeros. Composes the shared Table / StatusChip / Button / states, so it
 * mirrors cleanly under RTL with no per-direction overrides.
 */
import { useTranslation } from 'react-i18next';
import { useAdminJobs, useRequeueJob, type JobRow, type JobStatus } from '../../api/jobs';
import { Table, type Column } from '../../components/ui/Table';
import { StatusChip, type StatusTone } from '../../components/ui/StatusChip';
import { Button } from '../../components/ui/Button';
import { EmptyState, LoadingState, ErrorState } from '../../components/states';
import { Icon } from '../../components/icons';
import { formatRelative, formatDuration } from './jobFormat';

// The five tiles, in design order. Keys match the JobCounts fields.
const STAT_ORDER = ['succeeded', 'processing', 'scheduled', 'enqueued', 'failed'] as const;

// Status → chip tone. A closed map over the five server statuses (design colours).
const TONE: Record<JobStatus, StatusTone> = {
  Succeeded: 'success',
  Processing: 'info',
  Scheduled: 'scheduled',
  Enqueued: 'neutral',
  Failed: 'danger',
};

export function JobMonitor() {
  const { t, i18n } = useTranslation();
  const { data, isLoading, isError, refetch } = useAdminJobs();
  const requeue = useRequeueJob();

  if (isLoading) return <LoadingState />;
  if (isError || !data) return <ErrorState onRetry={() => refetch()} />;
  if (!data.configured) {
    return (
      <EmptyState
        icon="cog"
        title={t('admin.jobs.notConfiguredTitle')}
        body={t('admin.jobs.notConfiguredBody')}
      />
    );
  }

  const nowMs = Date.now();
  const renderWhen = (row: JobRow) =>
    row.timestamp ? formatRelative(row.timestamp, i18n.language, nowMs) : t('admin.jobs.none');
  const renderDuration = (row: JobRow) => {
    if (row.durationMs == null) return t('admin.jobs.none');
    const { n, unit } = formatDuration(row.durationMs);
    return t(`admin.jobs.unit.${unit}`, { n });
  };

  const columns: Column<JobRow>[] = [
    { id: 'name', header: t('admin.jobs.col.job'), cell: (r) => <span className="adm-job-name">{r.name}</span> },
    { id: 'queue', header: t('admin.jobs.col.queue'), cell: (r) => <span className="adm-job-queue">{r.queue}</span> },
    {
      id: 'status',
      header: t('admin.jobs.col.status'),
      cell: (r) => <StatusChip tone={TONE[r.status]} label={t(`admin.jobs.status.${r.status}`, r.status)} size="sm" />,
    },
    { id: 'when', header: t('admin.jobs.col.when'), cell: renderWhen },
    { id: 'duration', header: t('admin.jobs.col.duration'), align: 'end', cell: renderDuration },
    {
      id: 'retries',
      header: t('admin.jobs.col.retries'),
      align: 'end',
      cell: (r) =>
        r.canRetry ? (
          <Button
            variant="danger"
            size="sm"
            loading={requeue.isPending && requeue.variables === r.id}
            onClick={() => requeue.mutate(r.id)}
          >
            <Icon name="refresh" size={12} aria-hidden />
            {t('admin.jobs.retry')}
          </Button>
        ) : (
          <span className="adm-job-none">{t('admin.jobs.none')}</span>
        ),
    },
  ];

  return (
    <>
      <div className="adm-jobs-stats">
        {STAT_ORDER.map((key) => (
          <div key={key} className={`adm-jobs-stat ${key}`}>
            <div className="adm-jobs-stat-value">{data.counts[key]}</div>
            <div className="adm-jobs-stat-label">{t(`admin.jobs.stat.${key}`)}</div>
          </div>
        ))}
      </div>

      {data.jobs.length === 0 ? (
        <EmptyState icon="cog" title={t('admin.jobs.emptyTitle')} body={t('admin.jobs.emptyBody')} />
      ) : (
        <Table caption={t('admin.tabs.jobs')} columns={columns} rows={data.jobs} getRowKey={(r) => r.id} />
      )}

      {requeue.isError && (
        <p className="adm-jobs-error" role="alert">
          {t('admin.jobs.retryError')}
        </p>
      )}
    </>
  );
}
