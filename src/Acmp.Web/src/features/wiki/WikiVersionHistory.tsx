/*
 * Wiki version history (P15e, FR-117) — wires the reading view's History button to a panel listing the
 * document's snapshots (DocumentDetailDto.Versions[], shipped by P15d) newest-first: Version, SavedAt,
 * SavedBy (resolved to a member name). Selecting a version renders THAT snapshot's Body read-only via
 * MarkdownView. Diff is deferred to P14 (Usage Map) — "viewable" satisfies FR-117.
 */
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMembers } from '../../api/members';
import type { DocumentDetail, DocumentVersion, LocalizedText } from '../../api/wiki';
import { Dialog } from '../../components/ui/Dialog';
import { Button } from '../../components/ui/Button';
import { Icon } from '../../components/icons';
import { MarkdownView } from '../../components/ui/MarkdownView';

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
              onClick={() => setSelected((cur) => (cur?.id === v.id ? null : v))}
            >
              <span className="wiki-version-num">v{v.version}</span>
              <span className="wiki-version-meta">{fmtDate(v.savedAt)} · {savedBy(v)}</span>
            </button>
          ))}
        </div>
      )}

      {selected && (
        <div className="wiki-version-preview">
          <MarkdownView markdown={pick(selected.body)} className="wiki-artbody" />
        </div>
      )}
    </Dialog>
  );
}
