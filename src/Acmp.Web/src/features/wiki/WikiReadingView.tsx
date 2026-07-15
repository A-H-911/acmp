/*
 * Wiki reading view (P15e, FR-116/117) — the article surface of "ACMP Research & Knowledge.dc.html"
 * (isWiki reading state, lines 324-365): an in-content breadcrumb, a manager-only action row, a serif
 * title, an author/updated/read-time/tags meta line, the markdown body, and a Linked-artifacts footer.
 *
 * Design↔behaviour reconciliations (INV-014 no-reference additions, flagged): the design's reading view
 * has no lifecycle affordances — this adds a status badge + Publish/Archive + Edit (Chairman/Secretary
 * only, the API re-checks Document.Manage) plus a History button available to every reader. Publish shows
 * on Draft only; Archive on any non-Archived doc; Edit is hidden on Archived (edits 409). Cross-links use
 * the bespoke read-only WikiLinkedArtifacts card (design lines 356-363) — WK10/ADR-0029 reverses the
 * earlier TraceabilityPanel substitution, so wiki edge CREATION now lives only on routable detail pages.
 */
import { useTranslation } from 'react-i18next';
import { useMembers } from '../../api/members';
import { usePublishDocument, useArchiveDocument, type DocumentDetail, type LocalizedText } from '../../api/wiki';
import { StatusChip } from '../../components/ui/StatusChip';
import { Button } from '../../components/ui/Button';
import { Icon } from '../../components/icons';
import { MarkdownView } from '../../components/ui/MarkdownView';
import { WikiLinkedArtifacts } from './WikiLinkedArtifacts';
import { statusTone, initials, readTime, categoryLabel } from './wikiMeta';
import { formatDmy } from '../../lib/p15Date';

interface Props {
  document: DocumentDetail;
  canManage: boolean;
  onEdit: () => void;
  onHistory: () => void;
}

export function WikiReadingView({ document, canManage, onEdit, onHistory }: Props) {
  const { t, i18n } = useTranslation();
  const members = useMembers();
  const publish = usePublishDocument();
  const archive = useArchiveDocument();

  const pick = (l: LocalizedText) => (i18n.language === 'ar' ? l.ar : l.en);
  const fmtDate = (iso: string) => formatDmy(iso, i18n.language);
  // Read-time minutes localized so the Arabic locale renders Arabic-Indic digits (design "قراءة ٤ دقائق").
  const fmtCount = (n: number) => new Intl.NumberFormat(i18n.language === 'ar' ? 'ar-u-nu-arab' : 'en').format(n);
  const author = members.data?.find((m) => m.keycloakUserId === document.ownerUserId)?.fullName ?? document.ownerUserId;
  const body = pick(document.body);
  const canPublish = canManage && document.status === 'Draft';
  const canArchive = canManage && document.status !== 'Archived';
  const canEdit = canManage && document.status !== 'Archived';

  return (
    <article className="wiki-article">
      <div className="wiki-art-head">
        <nav className="wiki-crumb" aria-label={t('common.breadcrumb')}>
          <span>{t('wiki.crumbRoot')}</span>
          <Icon name="chevron" size={13} className="dir-flip" aria-hidden />
          <span>{categoryLabel(document.category, t)}</span>
          <Icon name="chevron" size={13} className="dir-flip" aria-hidden />
          <span className="wiki-crumb-current">{pick(document.title)}</span>
        </nav>
        {/* History is a read affordance — available to every reader; the lifecycle actions stay manager-only. */}
        <div className="wiki-art-actions">
          {canManage && <StatusChip tone={statusTone(document.status)} label={t(`wiki.status.${document.status}`)} size="sm" />}
          <Button variant="secondary" size="sm" onClick={onHistory}>
            <Icon name="history" size={14} aria-hidden /> {t('wiki.history')}
            <span className="wiki-history-ver">v{document.version}</span>
          </Button>
          {canEdit && (
            <Button variant="secondary" size="sm" onClick={onEdit}>
              <Icon name="pencil" size={14} aria-hidden /> {t('wiki.edit')}
            </Button>
          )}
          {canPublish && (
            <Button variant="primary" size="sm" loading={publish.isPending} onClick={() => void publish.mutateAsync(document.id)}>
              <Icon name="send" size={14} aria-hidden /> {t('wiki.publish')}
            </Button>
          )}
          {canArchive && (
            <Button variant="secondary" size="sm" loading={archive.isPending} onClick={() => void archive.mutateAsync(document.id)}>
              <Icon name="archive" size={14} aria-hidden /> {t('wiki.archive')}
            </Button>
          )}
        </div>
      </div>

      <h1 className="wiki-title">{pick(document.title)}</h1>
      <div className="wiki-meta">
        <span className="wiki-meta-author">
          <span className="wiki-meta-avatar" aria-hidden="true">{initials(author)}</span>
          {author}
        </span>
        <span className="wiki-meta-dot" aria-hidden="true" />
        <span>{t('wiki.updated')} {fmtDate(document.updatedAt ?? document.createdAt)}</span>
        <span className="wiki-meta-dot" aria-hidden="true" />
        <span className="wiki-meta-time">
          <Icon name="clock" size={13} aria-hidden /> {t('wiki.readtime', { minutes: fmtCount(readTime(body)) })}
        </span>
        {document.tags.length > 0 && (
          <span className="wiki-tags">
            {document.tags.map((tag) => (
              <span className="wiki-tag" key={tag}>{tag}</span>
            ))}
          </span>
        )}
      </div>

      <MarkdownView markdown={body} className="wiki-artbody" />

      <WikiLinkedArtifacts documentId={document.id} />
    </article>
  );
}
