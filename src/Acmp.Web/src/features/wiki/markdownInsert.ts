/*
 * Pure markdown edit transforms for the wiki editor's full-width formatting toolbar (design isWiki editing
 * state — Bold/Italic/Heading/List/Quote/Link/Cross-link). Each takes the textarea value + selection range
 * and returns the new text plus the caret/selection to restore. Kept pure (no DOM) so they unit-test
 * without a textarea; the wiki toolbar applies them to its own <textarea> ref.
 *
 * ponytail: the shared MarkdownEditor keeps its own inline copies deliberately — its 5-tool inline bar is a
 * different UI used by 6 other surfaces, and touching it here would widen the blast radius (plan-locked).
 */
export interface EditResult {
  text: string;
  start: number;
  end: number;
}

/** Wrap the selection in a mark (e.g. ** for bold), keeping the original text selected inside the marks. */
export function surround(v: string, s: number, e: number, mark: string): EditResult {
  const sel = v.slice(s, e);
  return {
    text: v.slice(0, s) + mark + sel + mark + v.slice(e),
    start: s + mark.length,
    end: s + mark.length + sel.length,
  };
}

/** Insert a line prefix (e.g. "# ", "- ", "> ") at the start of the selection's line. */
export function linePrefix(v: string, s: number, e: number, prefix: string): EditResult {
  const lineStart = v.lastIndexOf('\n', s - 1) + 1;
  return {
    text: v.slice(0, lineStart) + prefix + v.slice(lineStart),
    start: s + prefix.length,
    end: e + prefix.length,
  };
}

/** Insert a `[text](url)` link, selecting the link text so it can be typed over. */
export function insertLink(v: string, s: number, e: number): EditResult {
  const sel = v.slice(s, e) || 'text';
  const ins = `[${sel}](url)`;
  return { text: v.slice(0, s) + ins + v.slice(e), start: s + 1, end: s + 1 + sel.length };
}

/** Insert a `[[KEY]]` cross-link (artifact reference), selecting the key so it can be typed over. */
export function insertCrossLink(v: string, s: number, e: number): EditResult {
  const sel = v.slice(s, e) || 'KEY';
  const ins = `[[${sel}]]`;
  return { text: v.slice(0, s) + ins + v.slice(e), start: s + 2, end: s + 2 + sel.length };
}
