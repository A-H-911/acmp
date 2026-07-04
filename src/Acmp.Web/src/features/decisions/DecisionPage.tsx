/*
 * Decision detail (P7b) — matches "ACMP Decision, Voting & ADR" (isDecision). Read by key
 * (GET /api/decisions/{key}); reachable via the DecisionIssued notification deep-link. The shell
 * owns the breadcrumb (deriveBreadcrumbs), so this body starts at the header.
 *
 * Design↔behavior reconciliations (visual SoT = design; behavior SoT = package):
 *  - From-topic link: the DTO carries the topic's Guid (ADR-0001, no cross-module key lookup), not a
 *    TOP- key, so the design's clickable "From TOP-…" is omitted until a topic-key read exists.
 *  - Superseded banner shows the reason; the successor's DECN- key isn't on the prior's DTO (only its
 *    Guid), so the successor link is omitted (flagged) — the supersede round-trip navigates to it directly.
 *  - Alternatives render as stored text (one LocalizedString), not the design's structured "Not chosen"
 *    cards (not modeled) — operator-confirmed. *  - Honest defers (no fabrication): Convert-to-ADR = disabled stub (ADR module → P9/P11); Vote result,
 *    Effective date, Decided-in-meeting, Affected systems, and the immutable-history timeline are not
 *    rendered (vote/audit-query/relationship data lands in P9/P14).
 */
import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useDecision, type LocalizedText } from '../../api/decisions';
import { ApiError } from '../../api/apiClient';
import { StatusChip } from '../../components/ui/StatusChip';
import { Button } from '../../components/ui/Button';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { useAuth, hasRole } from '../../auth/AcmpAuthContext';
import { outcomeTone, conditionTone } from './decisionMeta';
import { SupersedeDialog } from './SupersedeDialog';
import { ConvertToAdrDialog } from './ConvertToAdrDialog';
import { CreateActionDialog } from '../actions/CreateActionDialog';
import { TraceabilityPanel } from '../traceability/TraceabilityPanel';
import './decisions.css';

export function DecisionPage() {
  const { key } = useParams();
  const { t, i18n } = useTranslation();
  const auth = useAuth();
  const { data, isLoading, isError, error, refetch } = useDecision(key);
  const [supersedeOpen, setSupersedeOpen] = useState(false);
  const [createActionOpen, setCreateActionOpen] = useState(false);
  const [convertOpen, setConvertOpen] = useState(false);

  if (isLoading) return <section className="page"><LoadingState /></section>;
  if (isError || !data) {
    const notFound = error instanceof ApiError && error.status === 404;
    return (
      <section className="page">
        {notFound ? (
          <EmptyState title={t('decisions.notFoundTitle')} body={t('decisions.notFoundBody')} />
        ) : (
          <ErrorState onRetry={() => refetch()} />
        )}
      </section>
    );
  }

  const decn = data;
  // Content is mirrored to both columns (en === ar), so a straight per-language pick is enough.
  const pick = (l: LocalizedText) => (i18n.language === 'ar' ? l.ar : l.en);
  const fmtDate = (iso: string) => new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso));
  const superseded = decn.status === 'Superseded';
  const isActive = decn.status === 'Issued';
  const canSupersede = isActive && hasRole(auth, 'chairman');
  // FR-068: only the Chairman promotes an issued decision to an ADR; the API re-checks (Adr.Promote).
  const canConvert = isActive && hasRole(auth, 'chairman');
  // W13: a follow-up action is raised from an active decision. Chairman/Secretary/Member may create; the
  // API re-checks (Member is allow-if-owner). Owner assignment happens in the dialog.
  const canCreateAction = isActive && hasRole(auth, 'chairman', 'secretary', 'member');

  const meta = [
    { label: t('decisions.meta.outcome'), value: t(`decisions.outcome.${decn.outcome}`) },
    { label: t('decisions.meta.approvedBy'), value: decn.chairApprovedByName ?? '—' },
    { label: t('decisions.meta.issuedAt'), value: decn.issuedAt ? fmtDate(decn.issuedAt) : '—' },
  ];

  return (
    <section className="page dec-detail">
      <header className="dec-head">
        <div className="dec-head-main">
          <div className="dec-head-chips">
            <span className="dec-key-chip">{decn.key}</span>
            <StatusChip tone={outcomeTone(decn.outcome)} label={t(`decisions.outcome.${decn.outcome}`)} />
            {superseded && <StatusChip tone="neutral" label={t('decisions.supersededBadge')} />}
          </div>
          <h1 className="dec-title">{pick(decn.title)}</h1>
        </div>
        {isActive && (
          <div className="dec-head-actions">
            {canCreateAction && (
              <Button variant="primary" onClick={() => setCreateActionOpen(true)}>
                <Icon name="action" size={15} aria-hidden />
                {t('actions.create.action')}
              </Button>
            )}
            {canConvert && (
              <Button variant="secondary" onClick={() => setConvertOpen(true)}>
                <Icon name="adr" size={15} aria-hidden />
                {t('decisions.convertAdr')}
              </Button>
            )}
            {canSupersede && (
              <Button variant="secondary" onClick={() => setSupersedeOpen(true)}>
                <Icon name="refresh" size={15} aria-hidden />
                {t('decisions.supersede.action')}
              </Button>
            )}
          </div>
        )}
      </header>

      {superseded && (
        <div className="dec-superseded-banner" role="status">
          <Icon name="refresh" size={18} aria-hidden />
          <div>
            <span className="dec-superseded-lead">{t('decisions.supersededLead')}</span>
            {decn.supersessionReason && <span className="dec-superseded-reason"> · {pick(decn.supersessionReason)}</span>}
          </div>
        </div>
      )}

      <div className="dec-grid">
        <article className={`dec-body${superseded ? ' dec-body-muted' : ''}`}>
          <section className="dec-section">
            <h2 className="dec-section-h">{t('decisions.statement')}</h2>
            <p className="dec-prose dec-statement">{pick(decn.statement)}</p>

            <h2 className="dec-section-h">{t('decisions.rationale')}</h2>
            <p className="dec-prose">{pick(decn.rationale)}</p>
          </section>

          {decn.conditions.length > 0 && (
            <section className="dec-section">
              <h2 className="dec-section-h">{t('decisions.conditions')}</h2>
              <ul className="dec-conditions-list">
                {decn.conditions.map((c) => (
                  <li key={c.id} className="dec-condition">
                    <span className={`dec-condition-mark ${conditionTone(c.status)}`} aria-hidden="true">
                      <Icon name="check" size={12} />
                    </span>
                    <span className="dec-condition-text">{pick(c.text)}</span>
                    <StatusChip size="sm" tone={conditionTone(c.status)} label={t(`decisions.conditionStatus.${c.status}`)} />
                  </li>
                ))}
              </ul>
            </section>
          )}

          {decn.alternatives && pick(decn.alternatives).trim() && (
            <section className="dec-section">
              <h2 className="dec-section-h">{t('decisions.alternatives')}</h2>
              <p className="dec-prose">{pick(decn.alternatives)}</p>
            </section>
          )}
        </article>

        <div className="dec-side detail-aside-stack">
          <section className="dec-card">
            <div className="dec-card-h">{t('decisions.recordDetail')}</div>
            <div className="dec-card-body">
              {meta.map((m) => (
                <div className="dec-meta-row" key={m.label}>
                  <div className="dec-meta-label">{m.label}</div>
                  <div className="dec-meta-value">{m.value}</div>
                </div>
              ))}
            </div>
          </section>
          <TraceabilityPanel traceType="Decision" depType="Decision" id={decn.id} artifactKey={decn.key} title={pick(decn.title)} />
        </div>
      </div>

      {data.id && (
        <SupersedeDialog
          open={supersedeOpen}
          onClose={() => setSupersedeOpen(false)}
          priorKey={decn.key}
          priorDecisionId={decn.id}
          cacheKey={key}
        />
      )}
      {data.id && (
        <CreateActionDialog
          open={createActionOpen}
          onClose={() => setCreateActionOpen(false)}
          source={{ sourceType: 'Decision', sourceId: decn.id, sourceKey: decn.key }}
        />
      )}
      {convertOpen && (
        <ConvertToAdrDialog
          open
          onClose={() => setConvertOpen(false)}
          decisionId={decn.id}
          decisionKey={decn.key}
        />
      )}
    </section>
  );
}
