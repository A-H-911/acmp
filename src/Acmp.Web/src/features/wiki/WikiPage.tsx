/*
 * Knowledge wiki shell (P15e, FR-116/117) — the isWiki surface of "ACMP Research & Knowledge.dc.html":
 * a 260px category tree ("spaces", grouped client-side from GET /api/knowledge/documents) + a main pane
 * that switches between the loading skeleton, a search-empty state, a no-documents-yet empty state, a
 * "select a page" hint, the reading view, and the split editor. Reads are committee-wide.
 *
 * Tree visibility (design draws no distinction; backend returns every status to every reader — flagged):
 * Published pages show to everyone; Draft pages show only to managers (Chairman/Secretary), dot-marked;
 * Archived pages are excluded from the tree entirely. New-page + the edit/publish/archive affordances are
 * manager-only (the API is the real gate).
 */
import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useWikiDocuments, useDocument, type DocumentSummary, type LocalizedText } from '../../api/wiki';
import { ApiError } from '../../api/apiClient';
import { useAuth, hasRole } from '../../auth/AcmpAuthContext';
import { Button } from '../../components/ui/Button';
import { ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { WikiReadingView } from './WikiReadingView';
import { WikiEditor } from './WikiEditor';
import { WikiVersionHistory } from './WikiVersionHistory';
import { CreateDocumentDialog } from './CreateDocumentDialog';
import { WIKI_CATEGORIES } from './wikiMeta';
import './wiki.css';

interface Space { space: string; pages: DocumentSummary[]; }

export function WikiPage() {
  const { key } = useParams();
  const { t, i18n } = useTranslation();
  const auth = useAuth();
  const canManage = hasRole(auth, 'chairman', 'secretary');
  const { data, isLoading, isError, refetch } = useWikiDocuments();
  const [search, setSearch] = useState('');
  const [createOpen, setCreateOpen] = useState(false);

  const pick = (l: LocalizedText) => (i18n.language === 'ar' ? l.ar : l.en);
  const categoryLabel = (c: string) => (WIKI_CATEGORIES.includes(c) ? t(`wiki.category.${c}`) : c);

  const docs = data?.items ?? [];
  // Archived is excluded from the tree; Draft only for managers, marked.
  const visible = docs.filter((d) => d.status === 'Published' || (d.status === 'Draft' && canManage));
  const q = search.trim().toLowerCase();
  const filtered = q ? visible.filter((d) => pick(d.title).toLowerCase().includes(q)) : visible;

  const spaces: Space[] = [...groupByCategory(filtered).entries()]
    .sort((a, b) => a[0].localeCompare(b[0]))
    .map(([space, pages]) => ({ space, pages: pages.sort((x, y) => pick(x.title).localeCompare(pick(y.title))) }));

  return (
    <section className="page">
      <div className="wiki-shell">
        <div className="wiki-tree">
          <div className="wiki-tree-search">
            <span className="wiki-search-icon"><Icon name="search" size={15} aria-hidden /></span>
            <input
              type="search"
              aria-label={t('wiki.searchLabel')}
              placeholder={t('wiki.searchLabel')}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          {canManage && (
            <div style={{ padding: '0 14px 8px' }}>
              <Button variant="secondary" size="sm" onClick={() => setCreateOpen(true)}>
                <Icon name="plus" size={14} aria-hidden /> {t('wiki.newPage')}
              </Button>
            </div>
          )}
          <nav className="wiki-tree-scroll" aria-label={t('wiki.treeLabel')}>
            {spaces.map((sp) => (
              <div className="wiki-space" key={sp.space}>
                <div className="wiki-space-head">
                  <Icon name="folder" size={13} aria-hidden /> {categoryLabel(sp.space)}
                </div>
                {sp.pages.map((pg) => {
                  const active = pg.key === key;
                  return (
                    <Link
                      key={pg.id}
                      to={`/wiki/${pg.key}`}
                      className={`tnode${active ? ' tnode-active' : ''}`}
                      aria-current={active ? 'page' : undefined}
                    >
                      <Icon name="file" size={14} className="tnode-icon" aria-hidden />
                      <span className="tnode-label">{pick(pg.title)}</span>
                      {pg.status === 'Draft' && (
                        <>
                          <span className="tnode-draft-dot" aria-hidden="true" title={t('wiki.status.Draft')} />
                          <span className="visually-hidden">{t('wiki.status.Draft')}</span>
                        </>
                      )}
                    </Link>
                  );
                })}
              </div>
            ))}
          </nav>
        </div>

        <div className="wiki-main">
          <WikiMain
            selectedKey={key}
            canManage={canManage}
            isLoading={isLoading}
            isError={isError}
            hasDocs={docs.length > 0}
            searching={q.length > 0}
            noResults={filtered.length === 0}
            onRetry={() => refetch()}
            onClearSearch={() => setSearch('')}
            onCreate={() => setCreateOpen(true)}
          />
        </div>
      </div>

      {canManage && <CreateDocumentDialog open={createOpen} onClose={() => setCreateOpen(false)} />}
    </section>
  );
}

function groupByCategory(docs: DocumentSummary[]): Map<string, DocumentSummary[]> {
  const map = new Map<string, DocumentSummary[]>();
  for (const d of docs) {
    const arr = map.get(d.category) ?? [];
    arr.push(d);
    map.set(d.category, arr);
  }
  return map;
}

interface MainProps {
  selectedKey: string | undefined;
  canManage: boolean;
  isLoading: boolean;
  isError: boolean;
  hasDocs: boolean;
  searching: boolean;
  noResults: boolean;
  onRetry: () => void;
  onClearSearch: () => void;
  onCreate: () => void;
}

function WikiMain({ selectedKey, canManage, isLoading, isError, hasDocs, searching, noResults, onRetry, onClearSearch, onCreate }: MainProps) {
  const { t } = useTranslation();

  if (isLoading) return <WikiSkeleton />;
  if (isError) return <div className="wiki-empty"><ErrorState title={t('wiki.error.title')} body={t('wiki.error.body')} onRetry={onRetry} /></div>;
  // A selected page always wins over the empty/search states (it fetches by key regardless of the tree filter).
  if (selectedKey) return <WikiArticlePane key={selectedKey} docKey={selectedKey} canManage={canManage} />;
  if (!hasDocs) {
    return (
      <div className="wiki-empty">
        <EmptyState icon="wiki" title={t('wiki.empty.title')} body={t('wiki.empty.body')} />
        {canManage && (
          <Button variant="primary" onClick={onCreate}>
            <Icon name="plus" size={16} aria-hidden /> {t('wiki.newPage')}
          </Button>
        )}
      </div>
    );
  }
  if (searching && noResults) {
    return (
      <div className="wiki-empty">
        <EmptyState icon="search" title={t('wiki.searchEmpty.title')} body={t('wiki.searchEmpty.body')} />
        <Button variant="secondary" onClick={onClearSearch}>{t('wiki.clearSearch')}</Button>
      </div>
    );
  }
  return <div className="wiki-empty"><EmptyState icon="file" title={t('wiki.select.title')} body={t('wiki.select.body')} /></div>;
}

/** Keyed by docKey so navigating to another page remounts fresh (resets edit mode). */
function WikiArticlePane({ docKey, canManage }: { docKey: string; canManage: boolean }) {
  const { t } = useTranslation();
  const { data, isLoading, isError, error, refetch } = useDocument(docKey);
  const [editing, setEditing] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);

  if (isLoading) return <WikiSkeleton />;
  if (isError || !data) {
    const notFound = error instanceof ApiError && error.status === 404;
    return (
      <div className="wiki-empty">
        {notFound
          ? <EmptyState icon="wiki" title={t('wiki.notFoundTitle')} body={t('wiki.notFoundBody')} />
          : <ErrorState title={t('wiki.error.title')} body={t('wiki.error.body')} onRetry={() => refetch()} />}
      </div>
    );
  }

  if (editing) return <WikiEditor document={data} onDone={() => setEditing(false)} />;
  return (
    <>
      <WikiReadingView document={data} canManage={canManage} onEdit={() => setEditing(true)} onHistory={() => setHistoryOpen(true)} />
      <WikiVersionHistory open={historyOpen} onClose={() => setHistoryOpen(false)} document={data} />
    </>
  );
}

function WikiSkeleton() {
  const { t } = useTranslation();
  const bars = ['200px', '70%', '48%', '100%', '96%', '90%', '40%', '98%', '88%'];
  return (
    <div className="wiki-skeleton" role="status" aria-busy="true">
      <span className="visually-hidden">{t('common.loading')}</span>
      {bars.map((w, i) => (
        <div key={i} className="skeleton" style={{ inlineSize: w }} aria-hidden="true" />
      ))}
    </div>
  );
}
