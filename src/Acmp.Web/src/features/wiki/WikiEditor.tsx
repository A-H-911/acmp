/*
 * Wiki split editor (P15e, FR-116) — matches the design's isWiki editing state (lines 311-323): a left
 * Markdown source pane + a right live preview, an "Editing" badge, Cancel + Save. Only the BODY of the
 * current UI language is edited; title, category and tags are passed through unchanged (guarding against
 * replace-semantics data loss on the PUT). Save = useEditDocument → Version++ + a new snapshot. A 409
 * (the doc was archived between load and save) surfaces the ApiError title inline.
 */
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useEditDocument, type DocumentDetail, type LocalizedText } from '../../api/wiki';
import { ApiError } from '../../api/apiClient';
import { Button } from '../../components/ui/Button';
import { Icon } from '../../components/icons';
import { MarkdownEditor } from '../../components/ui/MarkdownEditor';
import { MarkdownView } from '../../components/ui/MarkdownView';

interface Props {
  document: DocumentDetail;
  onDone: () => void;
}

export function WikiEditor({ document, onDone }: Props) {
  const { t, i18n } = useTranslation();
  const edit = useEditDocument();
  const pick = (l: LocalizedText) => (i18n.language === 'ar' ? l.ar : l.en);
  const [body, setBody] = useState(pick(document.body));
  const [submitError, setSubmitError] = useState<string | null>(null);
  const loc = (v: string): LocalizedText => ({ en: v.trim(), ar: v.trim() });

  async function onSave() {
    setSubmitError(null);
    try {
      await edit.mutateAsync({
        id: document.id,
        title: document.title,
        category: document.category,
        body: loc(body),
        tags: document.tags,
      });
      onDone();
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.problem?.title ?? t('wiki.editError') : t('wiki.editError'));
    }
  }

  return (
    <div className="wiki-editor">
      <div className="wiki-editor-head">
        <div className="wiki-editor-head-main">
          <span className="wiki-editing-badge">
            <Icon name="pencil" size={12} aria-hidden /> {t('wiki.editingBadge')}
          </span>
          <span className="wiki-editor-title">{pick(document.title)}</span>
        </div>
        <div className="wiki-editor-actions">
          <Button variant="secondary" size="sm" onClick={onDone}>{t('common.cancel')}</Button>
          <Button variant="primary" size="sm" loading={edit.isPending} onClick={() => void onSave()}>
            <Icon name="check" size={14} aria-hidden /> {t('wiki.saveChanges')}
          </Button>
        </div>
      </div>

      {submitError && (
        <p className="field-error wiki-submit-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{submitError}</p>
      )}

      <div className="wiki-editor-split">
        <div className="wiki-editor-pane">
          <div className="wiki-editor-pane-label">{t('wiki.markdown')}</div>
          <MarkdownEditor value={body} onChange={setBody} ariaLabel={t('wiki.markdown')} rows={20} />
        </div>
        <div className="wiki-preview">
          <div className="wiki-preview-label">{t('wiki.preview')}</div>
          <div className="wiki-preview-body">
            <h1 className="wiki-title">{pick(document.title)}</h1>
            <MarkdownView markdown={body} className="wiki-artbody" />
          </div>
        </div>
      </div>
    </div>
  );
}
