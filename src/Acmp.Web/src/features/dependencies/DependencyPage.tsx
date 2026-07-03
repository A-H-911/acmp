/*
 * Dependency edge detail (P10e) — routed /dependencies/:key, matching the Lists&Registers
 * `buildDetail` deps drill-in (From · Relation · To · Blocked · Status + a Notes block). Read by key
 * (GET /api/dependencies/{key}). Routed (not a drawer) so the register row + future notifications
 * deep-link (same call as the risk/action/decision details).
 *
 * A dependency is an EDGE, not an artifact — so it carries NO traceability panel (architect ruling).
 * Its two endpoints are shown in a small Related block, navigable when the target type has a route.
 * Cross-stream is omitted (FR-095 deferred); the impact graph link is a P10f stub.
 */
import { useParams, Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useDependency } from '../../api/dependencies';
import { ApiError } from '../../api/apiClient';
import { StatusChip } from '../../components/ui/StatusChip';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { statusTone, kindColor } from './depMeta';
import { hrefFor } from '../traceability/traceMeta';
import './dependencies.css';

export function DependencyPage() {
  const { key } = useParams();
  const { t } = useTranslation();
  const { data, isLoading, isError, error, refetch } = useDependency(key);

  if (isLoading) return <section className="page"><LoadingState /></section>;
  if (isError || !data) {
    const notFound = error instanceof ApiError && error.status === 404;
    return (
      <section className="page">
        {notFound ? (
          <EmptyState title={t('deps.notFoundTitle')} body={t('deps.notFoundBody')} />
        ) : (
          <ErrorState onRetry={() => refetch()} />
        )}
      </section>
    );
  }

  const dep = data;
  const facts = [
    { label: t('deps.fact.from'), value: dep.fromKey },
    { label: t('deps.fact.relation'), value: t(`deps.kind.${dep.kind}`), color: kindColor(dep.kind) },
    { label: t('deps.fact.to'), value: dep.toKey },
    { label: t('deps.fact.blocked'), value: dep.isBlocker ? t('deps.blocked') : t('deps.no'), color: dep.isBlocker ? 'var(--st-danger-fg)' : undefined },
    { label: t('deps.fact.status'), value: t(`deps.status.${dep.status}`) },
  ];
  const fromHref = hrefFor(dep.fromType, dep.fromKey);
  const toHref = hrefFor(dep.toType, dep.toKey);

  return (
    <section className="page dep-detail">
      <div className="dep-detail-grid">
        <div className="dep-detail-main">
          <header className="card dep-detail-head">
            <div className="dep-detail-chips">
              <span className="dep-key-chip">{dep.key}</span>
              <StatusChip tone={statusTone(dep.status)} label={t(`deps.status.${dep.status}`)} size="sm" />
              {dep.isBlocker && <span className="dep-blocked-cell">{t('deps.blocked')}</span>}
            </div>
            <h1 className="dep-detail-title">
              <span className="dep-detail-endpoint">{dep.fromKey}</span>
              <span className="dep-detail-rel" style={{ color: kindColor(dep.kind) }}>{t(`deps.kind.${dep.kind}`)}</span>
              <span className="dep-detail-endpoint">{dep.toKey}</span>
            </h1>
          </header>

          <section className="card dep-facts">
            <div className="dep-facts-grid">
              {facts.map((f) => (
                <div className="dep-fact" key={f.label}>
                  <span className="dep-fact-label">{f.label}</span>
                  <span className="dep-fact-value" style={f.color ? { color: f.color } : undefined}>{f.value}</span>
                </div>
              ))}
            </div>
          </section>

          {dep.note && (
            <section className="card dep-note">
              <div className="dep-fact-label">{t('deps.fact.notes')}</div>
              <p className="dep-note-body">{dep.note}</p>
            </section>
          )}
        </div>

        <aside className="card dep-related" aria-label={t('deps.related')}>
          <div className="dep-related-head">
            <Icon name="deps" size={15} aria-hidden />
            <h2 className="dep-related-title">{t('deps.related')}</h2>
          </div>
          <div className="dep-related-body">
            <RelatedEnd dir={t('deps.fact.from')} keyText={dep.fromKey} title={dep.fromTitle} href={fromHref} />
            <RelatedEnd dir={t('deps.fact.to')} keyText={dep.toKey} title={dep.toTitle} href={toHref} />
          </div>
        </aside>
      </div>
    </section>
  );
}

function RelatedEnd({ dir, keyText, title, href }: { dir: string; keyText: string; title: string; href: string | null }) {
  const inner = (
    <>
      <span className="dep-related-dir">{dir}</span>
      <span className="dep-related-main">
        <span className="dep-related-key">{keyText}</span>
        <span className="dep-related-label">{title}</span>
      </span>
    </>
  );
  return href ? (
    <Link className="dep-related-item link" to={href}>
      {inner}
      <Icon name="chevron" size={14} className="dir-flip" aria-hidden />
    </Link>
  ) : (
    <div className="dep-related-item">{inner}</div>
  );
}
