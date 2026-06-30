/*
 * Shared markdown editor (DV-04). One editor for every long-form field — Submit-topic description,
 * meeting discussion notes, and (P7) minutes. A plain <textarea> + a small real formatting toolbar
 * that inserts markdown marks around the selection; the body is STORED AS MARKDOWN TEXT (no rich-text
 * framework, no HTML, no sanitization surface — right-sized for a ≤20-user on-prem tool). Rendering of
 * the stored markdown on read is the consumer's concern. RTL-safe (logical CSS, no caret math beyond
 * the selection range). Was three divergent implementations (inert Submit toolbar, functional meeting
 * markdown, deferred minutes) — unified here per the DV-04 decision.
 */
import { useRef } from 'react';
import { useTranslation } from 'react-i18next';

type EditorTool = {
  id: 'bold' | 'italic' | 'bulletList' | 'numberedList' | 'link';
  kind: 'text' | 'icon';
  glyph?: string;
  weight?: number;
  italic?: boolean;
  path?: string;
  wrap?: string;
  prefix?: string;
  link?: boolean;
};

const EDITOR_TOOLS: EditorTool[] = [
  { id: 'bold', kind: 'text', glyph: 'B', weight: 700, wrap: '**' },
  { id: 'italic', kind: 'text', glyph: 'I', italic: true, wrap: '*' },
  { id: 'bulletList', kind: 'icon', path: 'M9 6h11M9 12h11M9 18h11M4 6h.01M4 12h.01M4 18h.01', prefix: '- ' },
  { id: 'numberedList', kind: 'icon', path: 'M10 6h10M10 12h10M10 18h10M4 5l1 2M4 11l1 2', prefix: '1. ' },
  { id: 'link', kind: 'icon', path: 'M10 13a5 5 0 007 0l2-2a5 5 0 00-7-7l-1 1M14 11a5 5 0 00-7 0l-2 2a5 5 0 007 7l1-1', link: true },
];

type Props = {
  value: string;
  onChange: (next: string) => void;
  placeholder?: string;
  rows?: number;
  onBlur?: () => void;
  /** Standalone label when the editor is not wrapped in a <Field> (e.g. meeting notes). */
  ariaLabel?: string;
  /** Field-provided control props (id + aria-*) when wrapped in a labelled <Field>. */
  id?: string;
  'aria-invalid'?: boolean;
  'aria-describedby'?: string;
};

export function MarkdownEditor({
  value,
  onChange,
  placeholder,
  rows = 4,
  onBlur,
  ariaLabel,
  id,
  'aria-invalid': ariaInvalid,
  'aria-describedby': ariaDescribedBy,
}: Props) {
  const { t } = useTranslation();
  const ref = useRef<HTMLTextAreaElement>(null);

  // Apply a text transform around the current selection, then restore focus + caret.
  const apply = (fn: (v: string, s: number, e: number) => { text: string; start: number; end: number }) => {
    const ta = ref.current;
    if (!ta) return;
    const { text, start, end } = fn(ta.value, ta.selectionStart, ta.selectionEnd);
    onChange(text);
    requestAnimationFrame(() => {
      ta.focus();
      ta.setSelectionRange(start, end);
    });
  };
  const surround = (mark: string) =>
    apply((v, s, e) => {
      const sel = v.slice(s, e);
      return { text: v.slice(0, s) + mark + sel + mark + v.slice(e), start: s + mark.length, end: s + mark.length + sel.length };
    });
  const linePrefix = (prefix: string) =>
    apply((v, s, e) => {
      const lineStart = v.lastIndexOf('\n', s - 1) + 1;
      return { text: v.slice(0, lineStart) + prefix + v.slice(lineStart), start: s + prefix.length, end: e + prefix.length };
    });
  const insertLink = () =>
    apply((v, s, e) => {
      const sel = v.slice(s, e) || 'text';
      const ins = `[${sel}](url)`;
      return { text: v.slice(0, s) + ins + v.slice(e), start: s + 1, end: s + 1 + sel.length };
    });

  const onTool = (tool: EditorTool) => {
    if (tool.link) return insertLink();
    if (tool.prefix) return linePrefix(tool.prefix);
    if (tool.wrap) return surround(tool.wrap);
  };

  return (
    <div className="md-editor">
      <div className="md-editor-toolbar" role="toolbar" aria-label={t('editor.toolbar')}>
        {EDITOR_TOOLS.map((tool) => (
          <button
            key={tool.id}
            type="button"
            className="md-editor-tool"
            aria-label={t(`editor.${tool.id}`)}
            style={tool.kind === 'text' ? { fontWeight: tool.weight ?? 400, fontStyle: tool.italic ? 'italic' : 'normal' } : undefined}
            onMouseDown={(ev) => ev.preventDefault()}
            onClick={() => onTool(tool)}
          >
            {tool.kind === 'text' ? (
              tool.glyph
            ) : (
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <path d={tool.path} />
              </svg>
            )}
          </button>
        ))}
      </div>
      <textarea
        ref={ref}
        id={id}
        className="md-editor-input"
        aria-label={ariaLabel}
        aria-invalid={ariaInvalid}
        aria-describedby={ariaDescribedBy}
        placeholder={placeholder}
        rows={rows}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        onBlur={onBlur}
      />
    </div>
  );
}
