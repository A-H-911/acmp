/*
 * Wiki split editor (P15e, FR-116) — matches the design's isWiki editing state (lines 311-323): an
 * "Editing" badge + a "Draft autosaved" indicator, Cancel + Save, a full-width formatting toolbar above a
 * left Markdown source pane + a right live preview. Only the BODY of the current UI language is edited;
 * title, category and tags are passed through unchanged (guarding against replace-semantics data loss on
 * the PUT). Save = useEditDocument → Version++ + a new snapshot. A 409 (the doc was archived between load
 * and save) surfaces the ApiError title inline.
 *
 * WK8: the body is debounced to a per-doc/per-language localStorage draft, restored on reopen, and cleared
 * on save or cancel. ponytail: last-write-wins across tabs — fine for a ≤20-user single-editor tool; add a
 * storage-event merge only if concurrent editing ever becomes real.
 */
import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useEditDocument, type DocumentDetail, type LocalizedText } from '../../api/wiki';
import { ApiError } from '../../api/apiClient';
import { Button } from '../../components/ui/Button';
import { Icon } from '../../components/icons';
import { MarkdownView } from '../../components/ui/MarkdownView';
import { surround, linePrefix, insertLink, insertCrossLink, type EditResult } from './markdownInsert';

interface Props {
  document: DocumentDetail;
  onDone: () => void;
}

type Insert = (v: string, s: number, e: number) => EditResult;

// The design's 7-tool wiki toolbar (glyph paths from the mockup) + the markdown transform each applies.
const TOOLS: { id: string; path: string; run: Insert }[] = [
  { id: 'bold', path: 'M6 4h8a4 4 0 010 8H6zM6 12h9a4 4 0 010 8H6z', run: (v, s, e) => surround(v, s, e, '**') },
  { id: 'italic', path: 'M19 4h-9M14 20H5M15 4L9 20', run: (v, s, e) => surround(v, s, e, '*') },
  { id: 'heading', path: 'M6 4v16M18 4v16M6 12h12', run: (v, s, e) => linePrefix(v, s, e, '# ') },
  { id: 'list', path: 'M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01', run: (v, s, e) => linePrefix(v, s, e, '- ') },
  { id: 'quote', path: 'M3 21c3 0 7-1 7-8V5H3v7h4M14 21c3 0 7-1 7-8V5h-7v7h4', run: (v, s, e) => linePrefix(v, s, e, '> ') },
  { id: 'link', path: 'M10 13a5 5 0 007 0l2-2a5 5 0 00-7-7l-1 1M14 11a5 5 0 00-7 0l-2 2a5 5 0 007 7l1-1', run: insertLink },
  { id: 'crosslink', path: 'M9 17H7A5 5 0 017 7h2M15 7h2a5 5 0 010 10h-2M8 12h8', run: insertCrossLink },
];

const DRAFT_DEBOUNCE_MS = 500;

function draftKeyFor(id: string, lang: string): string {
  return `acmp:wiki-draft:${id}:${lang}`;
}
function readDraft(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

export function WikiEditor({ document, onDone }: Props) {
  const { t, i18n } = useTranslation();
  const edit = useEditDocument();
  const ref = useRef<HTMLTextAreaElement>(null);
  const pick = (l: LocalizedText) => (i18n.language === 'ar' ? l.ar : l.en);
  const original = pick(document.body);
  const draftKey = draftKeyFor(document.id, i18n.language);

  const [body, setBody] = useState(() => readDraft(draftKey) ?? original);
  const [draftSaved, setDraftSaved] = useState(() => readDraft(draftKey) !== null);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const loc = (v: string): LocalizedText => ({ en: v.trim(), ar: v.trim() });

  // Debounced draft persistence: write after typing settles; a body back at the original clears the draft.
  useEffect(() => {
    if (body === original) {
      try {
        localStorage.removeItem(draftKey);
      } catch {
        /* storage unavailable — nothing to clear */
      }
      setDraftSaved(false);
      return;
    }
    const timer = setTimeout(() => {
      try {
        localStorage.setItem(draftKey, body);
        setDraftSaved(true);
      } catch {
        /* storage unavailable — skip the draft, editing still works */
      }
    }, DRAFT_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [body, original, draftKey]);

  const clearDraft = () => {
    try {
      localStorage.removeItem(draftKey);
    } catch {
      /* storage unavailable */
    }
  };

  const applyTool = (run: Insert) => {
    const ta = ref.current;
    if (!ta) return;
    const { text, start, end } = run(ta.value, ta.selectionStart, ta.selectionEnd);
    setBody(text);
    requestAnimationFrame(() => {
      ta.focus();
      ta.setSelectionRange(start, end);
    });
  };

  async function onSave() {
    setSubmitError(null);
    try {
      await edit.mutateAsync({ id: document.id, title: document.title, category: document.category, body: loc(body), tags: document.tags });
      clearDraft();
      onDone();
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.problem?.title ?? t('wiki.editError') : t('wiki.editError'));
    }
  }

  const onCancel = () => {
    clearDraft();
    onDone();
  };

  return (
    <div className="wiki-editor">
      <div className="wiki-editor-head">
        <div className="wiki-editor-head-main">
          <span className="wiki-editing-badge">
            <Icon name="pencil" size={12} aria-hidden /> {t('wiki.editingBadge')}
          </span>
          <span className="wiki-editor-title">{pick(document.title)}</span>
          {draftSaved && (
            <span className="wiki-draft-saved" role="status">
              <Icon name="check" size={12} aria-hidden /> {t('wiki.draftSaved')}
            </span>
          )}
        </div>
        <div className="wiki-editor-actions">
          <Button variant="secondary" size="sm" onClick={onCancel}>{t('common.cancel')}</Button>
          <Button variant="primary" size="sm" loading={edit.isPending} onClick={() => void onSave()}>
            <Icon name="check" size={14} aria-hidden /> {t('wiki.saveChanges')}
          </Button>
        </div>
      </div>

      <div className="wiki-editor-toolbar" role="toolbar" aria-label={t('wiki.toolbarLabel')}>
        {TOOLS.map((tool) => (
          <button
            key={tool.id}
            type="button"
            className="wiki-tool"
            aria-label={t(`wiki.tool.${tool.id}`)}
            onMouseDown={(ev) => ev.preventDefault()}
            onClick={() => applyTool(tool.run)}
          >
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <path d={tool.path} />
            </svg>
          </button>
        ))}
      </div>

      {submitError && (
        <p className="field-error wiki-submit-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{submitError}</p>
      )}

      <div className="wiki-editor-split">
        <div className="wiki-editor-pane">
          <div className="wiki-editor-pane-label">{t('wiki.markdown')}</div>
          <textarea
            ref={ref}
            className="wiki-editor-textarea"
            aria-label={t('wiki.markdown')}
            rows={20}
            value={body}
            onChange={(e) => setBody(e.target.value)}
          />
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
