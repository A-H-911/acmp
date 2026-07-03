/*
 * Action detail (P8b) — matches the "ACMP Lists & Registers.dc.html" action drill-in
 * (facts + progress + description + related). Read by key (GET /api/actions/{key}).
 *
 * GO'd blessed deviation: the design shows an in-page drawer; we route to /actions/:key
 * instead so ActionAssigned/ActionVerified notifications can deep-link. The shell owns the
 * breadcrumb (deriveBreadcrumbs), so this body starts at the header.
 *
 * Design↔behaviour reconciliations (visual SoT = "ACMP Lists & Registers.dc.html"; data SoT = package):
 *  - The other-language title subtitle is omitted (bilingual content is mirrored, so it's redundant) —
 *    same call as the decision detail.
 *  - Related links are display-only this slice, except "Raised in meeting" (a real /meetings/:key link).
 *    Cross-artifact deep-linking (decision/topic/risk/ADR by key) needs the traceability resolver → P10.
 *  - Lifecycle actions (start/block/progress/complete/verify) are not rendered — read-only slice (→ P8b2).
 */
import { useParams, Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAction, type LocalizedText } from '../../api/actions';
import { ApiError } from '../../api/apiClient';
import { StatusChip } from '../../components/ui/StatusChip';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { statusTone, progressColorDetail } from './actionMeta';
import { ActionActions } from './ActionActions';
import { TraceabilityPanel } from '../traceability/TraceabilityPanel';
import './actions.css';

export function ActionPage() {
  const { key } = useParams();
  const { t, i18n } = useTranslation();
  const { data, isLoading, isError, error, refetch } = useAction(key);

  if (isLoading) return <section className="page"><LoadingState /></section>;
  if (isError || !data) {
    const notFound = error instanceof ApiError && error.status === 404;
    return (
      <section className="page">
        {notFound ? (
          <EmptyState title={t('actions.notFoundTitle')} body={t('actions.notFoundBody')} />
        ) : (
          <ErrorState onRetry={() => refetch()} />
        )}
      </section>
    );
  }

  const act = data;
  const pick = (l: LocalizedText | null) => (l ? (i18n.language === 'ar' ? l.ar : l.en) : '');
  const fmtDate = (iso: string) => new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' }).format(new Date(iso));

  const facts = [
    { label: t('actions.fact.owner'), value: act.ownerName || t('actions.unassigned') },
    { label: t('actions.fact.due'), value: act.dueDate ? fmtDate(act.dueDate) : '—', danger: act.isOverdue },
    { label: t('actions.fact.linked'), value: act.sourceKey ?? '—' },
    { label: t('actions.fact.status'), value: t(`actions.status.${act.status}`) },
    { label: t('actions.fact.created'), value: fmtDate(act.createdAt) },
    { label: t('actions.fact.id'), value: act.key },
  ];
  const description = pick(act.description).trim();

  return (
    <section className="page act-detail">
      <div className="act-detail-grid">
        <div className="act-detail-main">
          <header className="card act-detail-head">
            <div className="act-detail-chips">
              <span className="act-key-chip">{act.key}</span>
              <StatusChip tone={statusTone(act.status)} label={t(`actions.status.${act.status}`)} size="sm" />
              {act.isOverdue && (
                <span className="act-overdue-badge">
                  <Icon name="clock" size={11} aria-hidden /> {t('actions.overdue')}
                </span>
              )}
            </div>
            <h1 className="act-detail-title">{pick(act.title)}</h1>
            <ActionActions action={act} />
          </header>

          <section className="card act-facts">
            <div className="act-facts-grid">
              {facts.map((f) => (
                <div className="act-fact" key={f.label}>
                  <span className="act-fact-label">{f.label}</span>
                  <span className={`act-fact-value ${f.danger ? 'danger' : ''}`}>{f.value}</span>
                </div>
              ))}
            </div>
            <div className="act-facts-progress">
              <div className="act-facts-progress-head">
                <span>{t('actions.progress')}</span>
                <span className="act-facts-pct">{act.progressPct}%</span>
              </div>
              <div className="act-pbar act-pbar-lg" aria-hidden="true">
                <span className="act-pbar-fill" style={{ inlineSize: `${act.progressPct}%`, background: progressColorDetail(act.progressPct) }} />
              </div>
            </div>
          </section>

          {description && (
            <section className="card act-desc">
              <div className="act-fact-label">{t('actions.description')}</div>
              <p className="act-desc-body">{description}</p>
            </section>
          )}
        </div>

        <div className="detail-aside-stack">
          <RelatedPanel sourceKey={act.sourceKey} meetingKey={act.meetingKey} />
          <TraceabilityPanel traceType="Action" depType="Action" id={act.id} artifactKey={act.key} title={pick(act.title)} />
        </div>
      </div>
    </section>
  );
}

function RelatedPanel({ sourceKey, meetingKey }: { sourceKey: string | null; meetingKey: string | null }) {
  const { t } = useTranslation();
  if (!sourceKey && !meetingKey) return null;
  return (
    <aside className="card act-related" aria-label={t('actions.related')}>
      <div className="act-related-head">
        <Icon name="deps" size={15} aria-hidden />
        <h2 className="act-related-title">{t('actions.related')}</h2>
      </div>
      <div className="act-related-body">
        {sourceKey && (
          <div className="act-related-item">
            <span className="act-related-dir up">{t('actions.relatedSourceDir')}</span>
            <span className="act-related-main">
              <span className="act-related-key">{sourceKey}</span>
              <span className="act-related-label">{t('actions.relatedSource')}</span>
            </span>
          </div>
        )}
        {meetingKey && (
          <Link className="act-related-item link" to={`/meetings/${meetingKey}`}>
            <span className="act-related-dir">{t('actions.relatedMeetingDir')}</span>
            <span className="act-related-main">
              <span className="act-related-key">{meetingKey}</span>
              <span className="act-related-label">{t('actions.relatedMeeting')}</span>
            </span>
            <Icon name="chevron" size={14} className="dir-flip" aria-hidden />
          </Link>
        )}
      </div>
    </aside>
  );
}
