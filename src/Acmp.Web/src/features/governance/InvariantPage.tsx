/*
 * Invariant detail (P11d) — NO-REFERENCE composition (guardrail #14): there is no dedicated `.dc.html` for
 * the Invariant detail. It is composed from the shared design system + docs/14, taking its cue from the
 * register's detail-drawer facts panel in "ACMP Lists & Registers.dc.html" (Category / Scope / Status /
 * Owner / Invariant-ID), and mirroring the read-only shape of the ADR detail (AdrPage). Read by key
 * (GET /api/invariants/{key}). The shell owns the breadcrumb, so this body starts at the header.
 *
 * Read-only: no propose / activate / retire / supersede buttons. Those endpoints exist (P11c) but their UI
 * is a later governance-lifecycle slice, shared with ADRs. There is no markdown export (an Invariant is a
 * standing rule, not a MADR document). The register's "Violations" fact is omitted (operator DEFER).
 */
import type { ReactNode } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useInvariant, type InvariantDetail, type LocalizedText } from '../../api/invariants';
import { ApiError } from '../../api/apiClient';
import { StatusChip } from '../../components/ui/StatusChip';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { Icon, type IconName } from '../../components/icons';
import { TraceabilityPanel } from '../traceability/TraceabilityPanel';
import { statusTone } from './invariantMeta';
import './governance.css';

export function InvariantPage() {
  const { key } = useParams();
  const { t } = useTranslation();
  const { data, isLoading, isError, error, refetch } = useInvariant(key);

  if (isLoading) return <section className="page"><LoadingState /></section>;
  if (isError || !data) {
    const notFound = error instanceof ApiError && error.status === 404;
    return (
      <section className="page">
        {notFound ? (
          <EmptyState title={t('invariants.notFoundTitle')} body={t('invariants.notFoundBody')} />
        ) : (
          <ErrorState onRetry={() => refetch()} />
        )}
      </section>
    );
  }

  return <InvariantDetailView inv={data} />;
}

function InvariantDetailView({ inv }: { inv: InvariantDetail }) {
  const { t, i18n } = useTranslation();
  const lang = i18n.language;
  const pick = (l: LocalizedText | null) => (l ? (lang === 'ar' ? l.ar : l.en) : '');
  const fmtDate = (iso: string | null) => (iso ? new Intl.DateTimeFormat(lang, { dateStyle: 'medium' }).format(new Date(iso)) : '—');

  const meta = [
    { label: t('invariants.meta.status'), value: t(`invariants.status.${inv.status}`) },
    { label: t('invariants.meta.category'), value: t(`invariants.category.${inv.category}`) },
    { label: t('invariants.meta.scope'), value: t(`invariants.scope.${inv.scope}`) },
    { label: t('invariants.meta.owner'), value: inv.ownerName || t('invariants.unassigned') },
    { label: t('invariants.meta.date'), value: fmtDate(inv.activatedAt ?? inv.createdAt) },
  ];
  // Superseded/Retired invariants read as stood-down — the body dims (mirrors the ADR detail treatment).
  const isRetired = inv.status === 'Superseded' || inv.status === 'Retired';

  return (
    <section className="page adr-detail">
      <div className="adr-detail-top">
        <div className="adr-detail-chips">
          <span className="adr-key-chip">{inv.key}</span>
          <StatusChip tone={statusTone(inv.status)} label={t(`invariants.status.${inv.status}`)} />
        </div>
      </div>
      <span className="adr-detail-eyebrow">{t('invariants.recordLabel')}</span>
      <h1 className="adr-detail-title">{pick(inv.statement)}</h1>

      <SupersedeBanner inv={inv} />

      <div className="adr-detail-grid">
        <article className={`card adr-body${isRetired ? ' adr-body-muted' : ''}`}>
          <Section heading={t('invariants.section.rationale')} icon="infoCircle">
            <p className="adr-prose">{pick(inv.rationale)}</p>
          </Section>

          {inv.exceptionsPolicy && (
            <Section heading={t('invariants.section.exceptions')} icon="warnTriangle">
              <p className="adr-prose">{pick(inv.exceptionsPolicy)}</p>
            </Section>
          )}
        </article>

        <div className="detail-aside-stack">
          <aside className="card adr-meta" aria-label={t('invariants.meta.title')}>
            <div className="adr-meta-head">{t('invariants.meta.title')}</div>
            <dl className="adr-meta-list">
              {meta.map((m) => (
                <div className="adr-meta-row" key={m.label}>
                  <dt>{m.label}</dt>
                  <dd>{m.value}</dd>
                </div>
              ))}
            </dl>
          </aside>
          <TraceabilityPanel traceType="Invariant" id={inv.id} artifactKey={inv.key} title={pick(inv.statement)} />
        </div>
      </div>
    </section>
  );
}

function Section({ heading, icon, children }: { heading: string; icon: IconName; children: ReactNode }) {
  return (
    <section className="adr-section">
      <h2 className="adr-section-head"><Icon name={icon} size={16} aria-hidden /> {heading}</h2>
      {children}
    </section>
  );
}

/**
 * The supersede-links banner — shows the prior Invariant this one supersedes and/or the successor that
 * superseded it, each a peer key resolved in-module. Rendered only when at least one link exists.
 */
function SupersedeBanner({ inv }: { inv: InvariantDetail }) {
  const { t } = useTranslation();
  if (!inv.supersedesInvariantKey && !inv.supersededByInvariantKey) return null;
  return (
    <div className="adr-supersede-banner">
      {inv.supersedesInvariantKey && (
        <div className="adr-supersede-link">
          <span className="adr-supersede-icon"><Icon name="arrowLeft" size={14} aria-hidden /></span>
          <div>
            <span className="adr-supersede-label">{t('invariants.supersedes')}</span>
            <span className="adr-supersede-key">{inv.supersedesInvariantKey}</span>
          </div>
        </div>
      )}
      {inv.supersededByInvariantKey && (
        <div className="adr-supersede-link is-by">
          <span className="adr-supersede-icon"><Icon name="arrowRight" size={14} aria-hidden /></span>
          <div>
            <span className="adr-supersede-label">{t('invariants.supersededBy')}</span>
            <span className="adr-supersede-key">{inv.supersededByInvariantKey}</span>
          </div>
        </div>
      )}
    </div>
  );
}
