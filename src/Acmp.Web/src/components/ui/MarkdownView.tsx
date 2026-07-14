/*
 * MarkdownView — renders trusted-authored wiki/template Markdown as safe HTML (P15e, FR-116). The app had no
 * markdown renderer (MarkdownEditor only stores raw text), so this is the one genuine new build of the slice.
 *
 * Pipeline: preprocess [[KEY]] cross-links into .xref chips → marked.parse (sync) → DOMPurify.sanitize with an
 * ALLOWLIST of markdown-output tags (react/security.md: allowlist, not denylist; sanitize at the same call site)
 * → sanitized dangerouslySetInnerHTML. The default DOMPurify profile permits <style>/<form>/<iframe>-adjacent
 * tags a wiki should not render, so we constrain ALLOWED_TAGS to the markdown subset. Links are hardened with
 * rel="noopener noreferrer" (afterSanitizeAttributes hook). Even though authors are Chair/Secretary (trusted),
 * a stored-XSS payload rendered to every committee member is a real vector — hence sanitize, never trust.
 */
import { useMemo } from 'react';
import DOMPurify from 'dompurify';
import { marked } from 'marked';

// The tags marked emits for our authored subset — headings, prose, lists, quotes, code, tables, links, and the
// <span class="xref"> cross-link chip. No <img>, <style>, <form>, <iframe>, <script>, etc.
const ALLOWED_TAGS = [
  'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'p', 'ul', 'ol', 'li', 'blockquote', 'pre', 'code',
  'strong', 'em', 'del', 'a', 'hr', 'br', 'span', 'table', 'thead', 'tbody', 'tr', 'th', 'td',
];
const ALLOWED_ATTR = ['href', 'title', 'class'];

// Harden every anchor once (module-level: DOMPurify hooks are global). javascript:/data: schemes are already
// stripped by the sanitizer; this adds the tab-nabbing guard for real links.
DOMPurify.addHook('afterSanitizeAttributes', (node) => {
  if (node.tagName === 'A') {
    node.setAttribute('rel', 'noopener noreferrer');
    node.setAttribute('target', '_blank');
  }
});

/** [[TOP-2026-014]] → an inline .xref chip (matches the design's inline cross-link styling). */
function linkifyXrefs(markdown: string): string {
  return markdown.replace(/\[\[([A-Z]{2,}-\d{4}-\d+)\]\]/g, '<span class="xref">$1</span>');
}

export function renderMarkdown(markdown: string): string {
  const html = marked.parse(linkifyXrefs(markdown ?? ''), { async: false }) as string;
  return DOMPurify.sanitize(html, { ALLOWED_TAGS, ALLOWED_ATTR });
}

interface MarkdownViewProps {
  markdown: string;
  className?: string;
}

export function MarkdownView({ markdown, className }: MarkdownViewProps) {
  const html = useMemo(() => renderMarkdown(markdown), [markdown]);
  // eslint-disable-next-line react/no-danger -- html is DOMPurify-sanitized at this call site (allowlist).
  return <div className={`markdown-body${className ? ` ${className}` : ''}`} dangerouslySetInnerHTML={{ __html: html }} />;
}
