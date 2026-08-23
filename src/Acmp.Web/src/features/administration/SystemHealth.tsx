/*
 * Administration → System Health (NR-08; mirrors the "ACMP Administration" `health` section).
 * Wired to GET /api/admin/health. The screen renders a FIXED catalog of the six core services so it
 * matches the design's tile list; it overlays the real status of whichever checks the server
 * actually registers (api, SQL Server, object storage, Hangfire and Seq) and shows the rest as
 * "monitoring not configured" —
 * never a fabricated status. Per-tile latency is real (HealthReportEntry.Duration); uptime% / p95 are
 * not collected on-prem in v1 and are intentionally omitted (recorded design deviation).
 */
import { useTranslation } from 'react-i18next';
import { useSystemHealth, type HealthEntry } from '../../api/systemHealth';
import { StatusChip, type StatusTone } from '../../components/ui/StatusChip';
import { LoadingState, ErrorState } from '../../components/states';
import { Icon, type IconName } from '../../components/icons';

/*
 * The six services the design lists, in order. `check` matches the server's health-check name when one
 * exists; a service with no registered check renders as "monitoring not configured" — never a
 * fabricated status.
 *
 * ⚠ THREE OF THESE WERE PERMANENTLY UNMONITORED FOR A REASON THAT NO LONGER HOLDS (DEF-039). The
 * object store, Seq and Hangfire tiles carried no `check` because, when this screen was written, the
 * only registered checks were `api` and `sqlserver`. The server has registered all three since — and
 * DEF-078/DEF-082 then found that two of them had never actually passed, which is why wiring them
 * earlier would have surfaced a real 503 rather than a green tile.
 *
 * ⚠ THE OBJECT-STORE TILE IS DELIBERATELY NOT CALLED "MinIO" ANY MORE. It used to be, and that was the
 * display half of DEF-039: the cloud stack runs S3, so a tile labelled MinIO was wrong on the
 * environment it was most likely to be read on. The check behind it (`objectstore`) is environment-
 * aware by construction — one SDK bucket probe that works against MinIO and S3 alike — so the label
 * follows suit rather than branching on environment.
 *
 * `webex` keeps no check on purpose: Webex is Phase 2 and disabled in every deployed environment, so
 * "monitoring not configured" is the honest answer rather than a gap.
 */
const SERVICES: { key: string; check?: string; icon: IconName }[] = [
  { key: 'application', check: 'api', icon: 'server' },
  { key: 'sqlServer', check: 'sqlserver', icon: 'database' },
  { key: 'objectStore', check: 'objectstore', icon: 'box' },
  { key: 'seq', check: 'seq', icon: 'viewList' },
  { key: 'hangfire', check: 'hangfire', icon: 'cog' },
  { key: 'webex', icon: 'video' },
];

type Health = 'operational' | 'degraded' | 'down' | 'unmonitored';

const TONE: Record<Health, StatusTone> = {
  operational: 'success',
  degraded: 'warn',
  down: 'danger',
  unmonitored: 'neutral',
};

function toHealth(entry: HealthEntry | undefined): Health {
  if (!entry) return 'unmonitored';
  if (entry.status === 'Healthy') return 'operational';
  if (entry.status === 'Degraded') return 'degraded';
  return 'down';
}

export function SystemHealth() {
  const { t } = useTranslation();
  const { data, isLoading, isError, refetch, isFetching } = useSystemHealth();

  if (isLoading) return <LoadingState />;
  if (isError || !data) return <ErrorState onRetry={() => refetch()} />;

  const byCheck = new Map(data.entries.map((e) => [e.name, e]));
  const tiles = SERVICES.map((s) => {
    const entry = s.check ? byCheck.get(s.check) : undefined;
    return { ...s, entry, health: toHealth(entry) };
  });

  // Overall banner reflects only what is monitored. Unhealthy outranks degraded outranks healthy.
  const monitored = tiles.filter((tl) => tl.health !== 'unmonitored');
  const anyDown = monitored.some((tl) => tl.health === 'down');
  const anyDegraded = monitored.some((tl) => tl.health === 'degraded');
  const overallTone: StatusTone = anyDown ? 'danger' : anyDegraded ? 'warn' : 'success';
  const overallTitle = anyDown
    ? t('admin.health.overall.down')
    : anyDegraded
      ? t('admin.health.overall.degraded')
      : t('admin.health.overall.ok');

  return (
    <>
      <div className={`adm-health-overall ${overallTone}`}>
        <div className="adm-health-overall-main">
          <span className="adm-health-overall-icon" aria-hidden="true">
            <Icon name="activity" size={19} />
          </span>
          <div>
            <div className="adm-health-overall-title">{overallTitle}</div>
            <div className="adm-health-overall-sub">{t('admin.health.lastRefresh')}</div>
          </div>
        </div>
        <button type="button" className="adm-refresh" onClick={() => refetch()} disabled={isFetching}>
          <Icon name="refresh" size={14} aria-hidden />
          {t('admin.health.refresh')}
        </button>
      </div>

      <div className="adm-health-grid">
        {tiles.map((tl) => (
          <div key={tl.key} className={`adm-tile${tl.health === 'unmonitored' ? ' adm-tile-muted' : ''}`}>
            <div className="adm-tile-head">
              <span className="adm-tile-title">
                <span className="adm-tile-icon" aria-hidden="true">
                  <Icon name={tl.icon} size={17} />
                </span>
                {t(`admin.health.service.${tl.key}`)}
              </span>
              <StatusChip tone={TONE[tl.health]} label={t(`admin.health.status.${tl.health}`)} size="sm" />
            </div>
            <div className="adm-tile-metric">
              {tl.health === 'unmonitored'
                ? t('admin.health.notMonitored')
                : `${tl.entry?.durationMs ?? 0} ${t('admin.health.ms')}${tl.entry?.description ? ` · ${tl.entry.description}` : ''}`}
            </div>
            <div className="adm-tile-foot">
              <Icon name="clock" size={12} aria-hidden />
              {tl.health === 'unmonitored' ? '—' : t('admin.health.checkedNow')}
            </div>
          </div>
        ))}
      </div>
    </>
  );
}
