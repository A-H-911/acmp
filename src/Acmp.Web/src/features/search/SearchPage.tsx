/*
 * Global search results (P15g, FR-143/144/145 · AC-060/AC-061). The top-bar search box
 * navigates here with ?q=…; this page reads the term, calls GET /api/search, and renders
 * the hits grouped by artifact type. No-reference composition (INV-014 — there is no
 * dedicated search .dc.html): built from the shared design system (page chrome, states,
 * StatusChip, Icon) and the IA. Each hit is a deep link to the artifact.
 *
 * Reconciliation: each hit's status is localized through its artifact type's status namespace
 * (searchStatusLabel), with a raw-enum fallback for any type/value without an i18n key — completeness is
 * bounded by each provider's status vocabulary (m22).
 */
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router-dom';
import { useSearch, type LocalizedText } from '../../api/search';
import { StatusChip } from '../../components/ui/StatusChip';
import { EmptyState, ErrorState, LoadingState } from '../../components/states';
import { searchStatusLabel } from './searchMeta';
import './search.css';

const pick = (l: LocalizedText, lang: string) => (lang === 'ar' ? l.ar : l.en);

export function SearchPage() {
  const { t, i18n } = useTranslation();
  const [params] = useSearchParams();
  const q = (params.get('q') ?? '').trim();
  const { data, isLoading, isError, refetch } = useSearch(q);

  const groups = (data ?? []).filter((g) => g.items.length > 0);
  const totalHits = groups.reduce((n, g) => n + g.items.length, 0);

  return (
    <section className="page">
      <div className="search-head">
        <h1 className="page-title">{t('search.title')}</h1>
        {q.length > 0 && !isLoading && !isError && (
          <p className="search-sub">{t('search.resultsFor', { query: q, count: totalHits })}</p>
        )}
      </div>

      {q.length === 0 ? (
        <EmptyState icon="search" title={t('search.prompt.title')} body={t('search.prompt.body')} />
      ) : isLoading ? (
        <LoadingState label={t('search.loading')} />
      ) : isError ? (
        <ErrorState title={t('search.error.title')} body={t('search.error.body')} onRetry={() => refetch()} />
      ) : totalHits === 0 ? (
        <EmptyState icon="search" title={t('search.empty.title')} body={t('search.empty.body', { query: q })} />
      ) : (
        <div className="search-groups">
          {groups.map((group) => (
            <section key={group.type} className="search-group" aria-label={t(`search.groups.${group.type}`, group.type)}>
              <h2 className="search-group-title">
                {t(`search.groups.${group.type}`, group.type)}
                <span className="search-group-count">{group.items.length}</span>
              </h2>
              <ul className="search-hits">
                {group.items.map((hit) => (
                  <li key={hit.id} className="search-hit">
                    <div className="search-hit-main">
                      <Link to={hit.deepLink} className="search-hit-link">
                        <span className="search-hit-key">{hit.key}</span>
                        <span className="search-hit-title">{pick(hit.title, i18n.language)}</span>
                      </Link>
                      <StatusChip tone="neutral" label={searchStatusLabel(hit.type, hit.status, t)} size="sm" />
                    </div>
                    {hit.excerpt && <p className="search-hit-excerpt">{hit.excerpt}</p>}
                  </li>
                ))}
              </ul>
            </section>
          ))}
        </div>
      )}
    </section>
  );
}
