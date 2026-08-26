/*
 * Wiki version history (P15e, FR-117) — wires the reading view's History button to a panel listing the
 * document's snapshots (DocumentDetailDto.Versions[], shipped by P15d) newest-first: Version, SavedAt,
 * SavedBy (resolved to a member name). Selecting a version renders THAT snapshot's Body read-only via
 * MarkdownView, or a line-level DIFF against the preceding version.
 *
 * ⚠ THIS HEADER USED TO SAY: "Diff is deferred to P14 (Usage Map) — 'viewable' satisfies FR-117."
 * DW-039 exists because of that sentence, and every part of it was a problem. It was a judgement
 * about REQUIREMENT SATISFACTION made in a source comment, where no register view can see it. It was
 * not true as written: FR-117 says versions are "viewable AND diffable", so viewable satisfies half.
 * And P14 was deferred INDEFINITELY by DEC-028, so pointing at it converted a deferral-with-a-trigger
 * into an abandonment — for a markdown diff that never depended on Tarseem or diagramming at all.
 * WBS-24.3 builds the missing clause; AC-146 records the verdict where the register can see it.
 */
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMembers } from '../../api/members';
import type { DocumentDetail, DocumentVersion, LocalizedText } from '../../api/wiki';
import { Dialog } from '../../components/ui/Dialog';
import { Button } from '../../components/ui/Button';
import { Icon } from '../../components/icons';
import { MarkdownView } from '../../components/ui/MarkdownView';
import { diffLines, diffStat } from './wikiDiff';

/** U+2212 MINUS SIGN, not a hyphen: it is the same width as `+` so the gutter stays a column. */
const MINUS = '−';
/** A non-breaking space keeps an unchanged/blank line at full height instead of collapsing it. */
const NBSP = ' ';

interface Props {
  open: boolean;
  onClose: () => void;
  document: DocumentDetail;
}

export function WikiVersionHistory({ open, onClose, document }: Props) {
  const { t, i18n } = useTranslation();
  const members = useMembers();
  const pick = (l: LocalizedText) => (i18n.language === 'ar' ? l.ar : l.en);
  const versions = [...document.versions].sort((a, b) => b.version - a.version);
  const [selected, setSelected] = useState<DocumentVersion | null>(null);
  const [comparing, setComparing] = useState(false);

  // Versions are sorted newest-first, so a version's PREDECESSOR is the next element along.
  const predecessorOf = (v: DocumentVersion) => versions[versions.indexOf(v) + 1] ?? null;

  const fmtDate = (iso: string) => new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso));
  const savedBy = (v: DocumentVersion) => members.data?.find((m) => m.keycloakUserId === v.savedByUserId)?.fullName ?? v.savedByUserId;

  return (
    <Dialog
      open={open}
      onClose={onClose}
      tone="default"
      icon={<Icon name="history" size={20} aria-hidden />}
      title={t('wiki.versions.title')}
      description={t('wiki.versions.subtitle')}
      footer={<Button variant="secondary" onClick={onClose}>{t('common.close')}</Button>}
    >
      {versions.length === 0 ? (
        <p className="wiki-version-meta">{t('wiki.versions.empty')}</p>
      ) : (
        <div className="wiki-versions">
          {versions.map((v) => (
            <button
              key={v.id}
              type="button"
              className={`wiki-version-row${selected?.id === v.id ? ' wiki-version-row-active' : ''}`}
              aria-pressed={selected?.id === v.id}
              onClick={() => {
                setSelected((cur) => (cur?.id === v.id ? null : v));
                setComparing(false); // a fresh selection starts on View, never on a stale compare
              }}
            >
              <span className="wiki-version-num">v{v.version}</span>
              <span className="wiki-version-meta">{fmtDate(v.savedAt)} · {savedBy(v)}</span>
            </button>
          ))}
        </div>
      )}

      {selected && (
        <div className="wiki-version-preview">
          <VersionPane
            selected={selected}
            previous={predecessorOf(selected)}
            comparing={comparing}
            onCompare={setComparing}
            pick={pick}
          />
        </div>
      )}
    </Dialog>
  );
}

/**
 * The two ways to read a snapshot: the version itself, or what changed since the one before it.
 * Split out so each branch is small enough to read, and so the diff can be tested without the dialog.
 */
function VersionPane({
  selected,
  previous,
  comparing,
  onCompare,
  pick,
}: {
  selected: DocumentVersion;
  previous: DocumentVersion | null;
  comparing: boolean;
  onCompare: (on: boolean) => void;
  pick: (l: LocalizedText) => string;
}) {
  const { t } = useTranslation();
  const result = previous ? diffLines(pick(previous.body), pick(selected.body)) : null;
  const stat = result ? diffStat(result.lines) : null;
  const unchanged = stat !== null && stat.added === 0 && stat.removed === 0;

  return (
    <>
      <div className="wiki-version-tabs">
        <button
          type="button"
          className={`wiki-version-tab${comparing ? '' : ' is-active'}`}
          aria-pressed={!comparing}
          onClick={() => onCompare(false)}
        >
          {t('wiki.versions.view')}
        </button>
        <button
          type="button"
          className={`wiki-version-tab${comparing ? ' is-active' : ''}`}
          aria-pressed={comparing}
          disabled={!previous}
          title={previous ? undefined : t('wiki.versions.oldest')}
          onClick={() => onCompare(true)}
        >
          {previous ? t('wiki.versions.compare', { version: previous.version }) : t('wiki.versions.compareNone')}
        </button>
      </div>

      {!comparing || !result ? (
        <MarkdownView markdown={pick(selected.body)} className="wiki-artbody" />
      ) : result.tooLarge ? (
        <p className="wiki-version-meta">{t('wiki.versions.tooLarge')}</p>
      ) : unchanged ? (
        <p className="wiki-version-meta">{t('wiki.versions.identical')}</p>
      ) : (
        <>
          {/* Bare numerals with visually-hidden labels: this codebase has no plural keys, and
              count-noun agreement is a six-form problem in Arabic (DEC-032). */}
          <p className="wiki-diff-stat">
            <span className="wiki-diff-added">
              <span className="visually-hidden">{t('wiki.versions.linesAdded')} </span>+{stat!.added}
            </span>
            <span className="wiki-diff-removed">
              <span className="visually-hidden">{t('wiki.versions.linesRemoved')} </span>{MINUS}{stat!.removed}
            </span>
          </p>
          {/* The +/- prefix is real text, so the change is never signalled by colour alone. */}
          <ul className="wiki-diff" aria-label={t('wiki.versions.diffLabel')}>
            {result.lines.map((line, idx) => (
              <li key={idx} className={`wiki-diff-line wiki-diff-${line.kind}`}>
                <span className="wiki-diff-marker" aria-hidden="true">
                  {line.kind === 'added' ? '+' : line.kind === 'removed' ? MINUS : NBSP}
                </span>
                <span className="wiki-diff-text">{line.text || NBSP}</span>
              </li>
            ))}
          </ul>
        </>
      )}
    </>
  );
}
