/*
 * Risk detail (P10b) — matches the "ACMP Lists & Registers.dc.html" risk drill-in
 * (kind==='risks': header + facts + large exposure matrix + Mitigation-plan card + Related panel).
 * Read by key (GET /api/risks/{key}).
 *
 * GO'd blessed deviation: the design shows an in-page drawer; we route to /risks/:key instead so
 * RiskAssigned / RiskEscalated notifications can deep-link (same call as the action + decision details).
 * The shell owns the breadcrumb (deriveBreadcrumbs), so this body starts at the header.
 *
 * Design↔behaviour reconciliations (visual SoT = "ACMP Lists & Registers.dc.html"; data SoT = package):
 *  - The "Mitigation plan" card renders the risk's mitigation description(s) as prose (the design has no
 *    mitigations table — a single prose block, relabelled from the action description card). A structured
 *    mitigations list is a later, no-reference extension, not shipped here.
 *  - Related / Traceability shows the linked subject key display-only (mirrors ActionPage's sourceKey).
 *    Clickable TYPED edges + the impact-graph link are deferred to the traceability slices (P10c/e).
 *  - Lifecycle actions (begin-mitigation/close/escalate/accept + mitigation edits) are not rendered —
 *    read-only slice (a later slice, like P8b → P8b2).
 */
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useRisk, type LocalizedText } from '../../api/risks';
import { ApiError } from '../../api/apiClient';
import { StatusChip } from '../../components/ui/StatusChip';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { statusTone, exposureTone, exposureMatrix } from './riskMeta';
import { TraceabilityPanel } from '../traceability/TraceabilityPanel';
import './risks.css';

export function RiskPage() {
  const { key } = useParams();
  const { t, i18n } = useTranslation();
  const { data, isLoading, isError, error, refetch } = useRisk(key);

  if (isLoading) return <section className="page"><LoadingState /></section>;
  if (isError || !data) {
    const notFound = error instanceof ApiError && error.status === 404;
    return (
      <section className="page">
        {notFound ? (
          <EmptyState title={t('risks.notFoundTitle')} body={t('risks.notFoundBody')} />
        ) : (
          <ErrorState onRetry={() => refetch()} />
        )}
      </section>
    );
  }

  const risk = data;
  const pick = (l: LocalizedText | null) => (l ? (i18n.language === 'ar' ? l.ar : l.en) : '');
  const expFg = `var(--st-${exposureTone(risk.exposure)}-fg)`;

  const facts = [
    { label: t('risks.fact.prob'), value: t(`risks.level.${risk.likelihood}`) },
    { label: t('risks.fact.impact'), value: t(`risks.level.${risk.impact}`) },
    { label: t('risks.fact.exposure'), value: t(`risks.exposure.${risk.exposure}`), color: expFg },
    { label: t('risks.fact.owner'), value: risk.ownerName || t('risks.unassigned') },
    { label: t('risks.fact.status'), value: t(`risks.status.${risk.status}`) },
    { label: t('risks.fact.linked'), value: risk.subjectKey ?? '—' },
  ];
  const matrix = exposureMatrix(risk.likelihood, risk.impact, risk.exposure);
  const plans = risk.mitigations.map((m) => pick(m.description)).filter((p) => p.trim().length > 0);

  return (
    <section className="page rsk-detail">
      <div className="rsk-detail-grid">
        <div className="rsk-detail-main">
          <header className="card rsk-detail-head">
            <div className="rsk-detail-chips">
              <span className="rsk-key-chip">{risk.key}</span>
              <StatusChip tone={statusTone(risk.status)} label={t(`risks.status.${risk.status}`)} size="sm" />
            </div>
            <h1 className="rsk-detail-title">{pick(risk.title)}</h1>
          </header>

          <section className="card rsk-facts">
            <div className="rsk-facts-grid">
              {facts.map((f) => (
                <div className="rsk-fact" key={f.label}>
                  <span className="rsk-fact-label">{f.label}</span>
                  <span className="rsk-fact-value" style={f.color ? { color: f.color } : undefined}>{f.value}</span>
                </div>
              ))}
            </div>
            <div className="rsk-matrix-wrap">
              <div>
                <div className="rsk-fact-label rsk-matrix-label">{t('risks.exposureMatrix')}</div>
                <div className="rsk-matrix" role="img" aria-label={t('risks.matrixAria', {
                  prob: t(`risks.level.${risk.likelihood}`),
                  impact: t(`risks.level.${risk.impact}`),
                  exposure: t(`risks.exposure.${risk.exposure}`),
                })}>
                  {matrix.map((mr, r) => (
                    <div className="rsk-matrix-row" key={r}>
                      {mr.map((mc, c) => (
                        <span key={c} className="rsk-matrix-cell" style={{ background: mc.bg, borderColor: mc.bd }} />
                      ))}
                    </div>
                  ))}
                </div>
              </div>
              <dl className="rsk-legend">
                <div><dt>{t('risks.fact.prob')}</dt><dd>{t(`risks.level.${risk.likelihood}`)}</dd></div>
                <div><dt>{t('risks.fact.impact')}</dt><dd>{t(`risks.level.${risk.impact}`)}</dd></div>
                <div><dt>{t('risks.fact.exposure')}</dt><dd style={{ color: expFg }}>{t(`risks.exposure.${risk.exposure}`)}</dd></div>
              </dl>
            </div>
          </section>

          {plans.length > 0 && (
            <section className="card rsk-plan">
              <div className="rsk-fact-label">{t('risks.mitigationPlan')}</div>
              {plans.map((p, i) => (
                <p className="rsk-plan-body" key={i}>{p}</p>
              ))}
            </section>
          )}
        </div>

        <div className="detail-aside-stack">
          <RelatedPanel subjectKey={risk.subjectKey} />
          <TraceabilityPanel traceType="Risk" id={risk.id} artifactKey={risk.key} title={pick(risk.title)} />
        </div>
      </div>
    </section>
  );
}

/**
 * Related / Traceability — honest for P10b: it lists the linked subject key (display-only). Clickable
 * typed edges + the impact-graph button arrive with the traceability backend (P10c/e); we do NOT fake
 * a graph here. If the risk has no subject key there is nothing to relate yet, so the panel is omitted.
 */
function RelatedPanel({ subjectKey }: { subjectKey: string | null }) {
  const { t } = useTranslation();
  if (!subjectKey) return null;
  return (
    <aside className="card rsk-related" aria-label={t('risks.related')}>
      <div className="rsk-related-head">
        <Icon name="deps" size={15} aria-hidden />
        <div className="rsk-related-heading">
          <h2 className="rsk-related-title">{t('risks.related')}</h2>
          <span className="rsk-related-sub">{t('risks.relatedSub')}</span>
        </div>
      </div>
      <div className="rsk-related-body">
        {subjectKey && (
          <div className="rsk-related-item">
            <span className="rsk-related-dir rel">{t('risks.relatedSubjectDir')}</span>
            <span className="rsk-related-main">
              <span className="rsk-related-key">{subjectKey}</span>
              <span className="rsk-related-label">{t('risks.relatedSubject')}</span>
            </span>
          </div>
        )}
        <p className="rsk-related-note">{t('risks.tracePending')}</p>
      </div>
    </aside>
  );
}
