/*
 * Wiki "Linked artifacts" card (P15e, WK10) — the read-only cross-link card from the design's isWiki
 * reading view (lines 356-363): a labelled card listing each linked artifact as a key chip + title, with a
 * chevron on the routable ones. It reuses the traceability read-hook (the same edges the panel read), so
 * the list is complete; it deliberately DROPS the reading view's "Add relationship" affordance — see
 * ADR-0029. Non-routable targets (ADR/Document/…) render as plain rows, never a dead link (honest).
 *
 * ponytail: the card is hidden when there are no links — a reading view shouldn't show an empty labelled
 * shell. Edge creation for wiki pages now lives only where a routable detail page exposes the panel.
 */
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useArtifactRelationships } from '../../api/traceability';
import { hrefFor } from '../traceability/traceMeta';
import { Icon } from '../../components/icons';

export function WikiLinkedArtifacts({ documentId }: { documentId: string }) {
  const { t } = useTranslation();
  const rels = useArtifactRelationships('Document', documentId);
  const edges = [...(rels.data?.outgoing ?? []), ...(rels.data?.incoming ?? [])];
  if (edges.length === 0) return null;

  return (
    <div className="card wiki-linked-card">
      <div className="wiki-linked-label">
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="var(--accent)" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          <path d="M10 13a5 5 0 007 0l2-2a5 5 0 00-7-7l-1 1M14 11a5 5 0 00-7 0l-2 2a5 5 0 007 7l1-1" />
        </svg>
        {t('wiki.linkedArtifacts')}
      </div>
      <div className="wiki-linked-list">
        {edges.map((e) => {
          const href = hrefFor(e.otherType, e.otherKey);
          const inner = (
            <>
              <span className="wiki-link-key">{e.otherKey}</span>
              <span className="wiki-link-title">{e.otherTitle}</span>
              {href && <Icon name="chevron" size={14} className="dir-flip wiki-link-chev" aria-hidden />}
            </>
          );
          return href ? (
            <Link key={e.id} to={href} className="wiki-linked-row">{inner}</Link>
          ) : (
            <div key={e.id} className="wiki-linked-row wiki-linked-row-static">{inner}</div>
          );
        })}
      </div>
    </div>
  );
}
