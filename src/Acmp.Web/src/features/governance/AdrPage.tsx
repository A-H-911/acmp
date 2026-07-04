/*
 * ADR (MADR-lite) detail — matches the "ACMP Decision, Voting & ADR.dc.html" isAdr screen: header
 * (key + status chip + Export .md), the supersede-links banner, the MADR body (Context · Drivers ·
 * Considered options · Decision · Consequences), a metadata aside, and Traceability. Read by key
 * (GET /api/adrs/{key}). The shell owns the breadcrumb, so this body starts at the header.
 *
 * Design↔behaviour reconciliations (visual SoT = the design; data SoT = package):
 *  - The design's status state-tabs (proposed/accepted/superseded) are a PREVIEW toggle, not a user
 *    control — the real status is fixed by the ADR's lifecycle, so we render a status chip, not tabs.
 *  - Read-only: no propose/approve/supersede/deprecate buttons. Those endpoints exist (P11a) but their UI
 *    is a later lifecycle slice (mirrors Risks P10b). The only action here is Export .md (FR-104).
 *  - Considered options: the model knows chosen vs not-chosen; a non-chosen option renders as a neutral
 *    "Alternative" (the design's "Rejected" tag is a stronger claim the data doesn't carry).
 *  - Metadata aside carries Status / Date / Author / Approved-by from the DTO; the design's "Tags" row is
 *    omitted (not modelled). Traceability is the real relationship panel (design had static links).
 */
import type { ReactNode } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAdr, type AdrDetail, type AdrOption, type LocalizedText } from '../../api/adrs';
import { ApiError } from '../../api/apiClient';
import { StatusChip } from '../../components/ui/StatusChip';
import { Button } from '../../components/ui/Button';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { Icon, type IconName } from '../../components/icons';
import { TraceabilityPanel } from '../traceability/TraceabilityPanel';
import { statusTone, exportMarkdown, downloadMarkdown } from './adrMeta';
import './governance.css';

export function AdrPage() {
  const { key } = useParams();
  const { t } = useTranslation();
  const { data, isLoading, isError, error, refetch } = useAdr(key);

  if (isLoading) return <section className="page"><LoadingState /></section>;
  if (isError || !data) {
    const notFound = error instanceof ApiError && error.status === 404;
    return (
      <section className="page">
        {notFound ? (
          <EmptyState title={t('adrs.notFoundTitle')} body={t('adrs.notFoundBody')} />
        ) : (
          <ErrorState onRetry={() => refetch()} />
        )}
      </section>
    );
  }

  return <AdrDetailView adr={data} />;
}

function AdrDetailView({ adr }: { adr: AdrDetail }) {
  const { t, i18n } = useTranslation();
  const lang = i18n.language;
  const pick = (l: LocalizedText | null) => (l ? (lang === 'ar' ? l.ar : l.en) : '');
  const fmtDate = (iso: string | null) => (iso ? new Intl.DateTimeFormat(lang, { dateStyle: 'medium' }).format(new Date(iso)) : '—');

  const onExport = () => {
    const md = exportMarkdown(adr, lang, {
      status: t('adrs.meta.status'),
      context: t('adrs.section.context'),
      drivers: t('adrs.section.drivers'),
      options: t('adrs.section.options'),
      chosen: t('adrs.option.chosen'),
      decision: t('adrs.section.decision'),
      consequences: t('adrs.section.consequences'),
      positive: t('adrs.consequence.positive'),
      negative: t('adrs.consequence.negative'),
    });
    downloadMarkdown(`${adr.key}.md`, md);
  };

  const meta = [
    { label: t('adrs.meta.status'), value: t(`adrs.status.${adr.status}`) },
    { label: t('adrs.meta.date'), value: fmtDate(adr.approvedAt ?? adr.createdAt) },
    { label: t('adrs.meta.author'), value: adr.authorName },
    { label: t('adrs.meta.approvedBy'), value: adr.approvedByName ?? '—' },
  ];
  // Superseded/Deprecated records read as retired — the body dims (the design's reduced-opacity treatment).
  const isRetired = adr.status === 'Superseded' || adr.status === 'Deprecated';

  return (
    <section className="page adr-detail">
      <div className="adr-detail-top">
        <div className="adr-detail-chips">
          <span className="adr-key-chip">{adr.key}</span>
          <StatusChip tone={statusTone(adr.status)} label={t(`adrs.status.${adr.status}`)} />
        </div>
        <Button variant="secondary" onClick={onExport}>
          <Icon name="download" size={15} aria-hidden /> {t('adrs.exportMd')}
        </Button>
      </div>
      <span className="adr-detail-eyebrow">{t('adrs.recordLabel')}</span>
      <h1 className="adr-detail-title">{pick(adr.title)}</h1>

      <SupersedeBanner adr={adr} />

      <div className="adr-detail-grid">
        <article className={`card adr-body${isRetired ? ' adr-body-muted' : ''}`}>
          <Section heading={t('adrs.section.context')} icon="infoCircle">
            <p className="adr-prose">{pick(adr.context)}</p>
          </Section>

          {adr.decisionDrivers && (
            <Section heading={t('adrs.section.drivers')} icon="activity">
              <p className="adr-prose">{pick(adr.decisionDrivers)}</p>
            </Section>
          )}

          {adr.options.length > 0 && (
            <Section heading={t('adrs.section.options')} icon="checkCircle">
              <div className="adr-options">
                {adr.options.map((o) => (
                  <OptionCard key={o.id} option={o} pick={pick} />
                ))}
              </div>
            </Section>
          )}

          <Section heading={t('adrs.section.decision')} icon="decision">
            <p className="adr-prose">{pick(adr.decisionText)}</p>
          </Section>

          {(adr.consequencesPositive || adr.consequencesNegative) && (
            <Section heading={t('adrs.section.consequences')} icon="clipboardCheck">
              <div className="adr-consequences">
                {adr.consequencesPositive && (
                  <div className="adr-conseq adr-conseq-pos">
                    <span className="adr-conseq-label"><Icon name="check" size={14} aria-hidden /> {t('adrs.consequence.positive')}</span>
                    <p className="adr-prose">{pick(adr.consequencesPositive)}</p>
                  </div>
                )}
                {adr.consequencesNegative && (
                  <div className="adr-conseq adr-conseq-neg">
                    <span className="adr-conseq-label"><Icon name="warnTriangle" size={14} aria-hidden /> {t('adrs.consequence.negative')}</span>
                    <p className="adr-prose">{pick(adr.consequencesNegative)}</p>
                  </div>
                )}
              </div>
            </Section>
          )}
        </article>

        <div className="detail-aside-stack">
          <aside className="card adr-meta" aria-label={t('adrs.meta.title')}>
            <div className="adr-meta-head">{t('adrs.meta.title')}</div>
            <dl className="adr-meta-list">
              {meta.map((m) => (
                <div className="adr-meta-row" key={m.label}>
                  <dt>{m.label}</dt>
                  <dd>{m.value}</dd>
                </div>
              ))}
            </dl>
          </aside>
          <TraceabilityPanel traceType="Adr" id={adr.id} artifactKey={adr.key} title={pick(adr.title)} />
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

function OptionCard({ option, pick }: { option: AdrOption; pick: (l: LocalizedText | null) => string }) {
  const { t } = useTranslation();
  return (
    <div className={`adr-option${option.isChosen ? ' is-chosen' : ''}`}>
      <div className="adr-option-head">
        <Icon name={option.isChosen ? 'checkCircle' : 'minus'} size={15} aria-hidden />
        <span className="adr-option-name">{pick(option.name)}</span>
        <span className={`adr-option-tag${option.isChosen ? ' is-chosen' : ''}`}>
          {option.isChosen ? t('adrs.option.chosen') : t('adrs.option.alternative')}
        </span>
      </div>
      {option.body && <div className="adr-option-body">{pick(option.body)}</div>}
    </div>
  );
}

/**
 * The supersede-links banner — shows the prior ADR this one supersedes and/or the successor that
 * superseded it, each a peer key resolved in-module. Rendered only when at least one link exists.
 * "Supersedes" points back (left arrow); "Superseded by" points forward (right arrow); both mirror in RTL.
 */
function SupersedeBanner({ adr }: { adr: AdrDetail }) {
  const { t } = useTranslation();
  if (!adr.supersedesAdrKey && !adr.supersededByAdrKey) return null;
  return (
    <div className="adr-supersede-banner">
      {adr.supersedesAdrKey && (
        <div className="adr-supersede-link">
          <span className="adr-supersede-icon"><Icon name="arrowLeft" size={14} aria-hidden /></span>
          <div>
            <span className="adr-supersede-label">{t('adrs.supersedes')}</span>
            <span className="adr-supersede-key">{adr.supersedesAdrKey}</span>
          </div>
        </div>
      )}
      {adr.supersededByAdrKey && (
        <div className="adr-supersede-link is-by">
          <span className="adr-supersede-icon"><Icon name="arrowRight" size={14} aria-hidden /></span>
          <div>
            <span className="adr-supersede-label">{t('adrs.supersededBy')}</span>
            <span className="adr-supersede-key">{adr.supersededByAdrKey}</span>
          </div>
        </div>
      )}
    </div>
  );
}
