/*
 * Line-level diff for wiki version comparison — FR-117's third clause (DW-039 / WBS-24.3).
 *
 * NO DEPENDENCY WAS ADDED. Nothing in package.json or node_modules provides a diff, and the whole
 * algorithm is thirty lines; pulling a package in for that is the trade this codebase does not make.
 *
 * Line granularity is the right unit here: the content is markdown, and a wiki reader asking "what
 * changed?" wants the paragraph, list item or heading that moved — not a character-level ribbon
 * through a sentence.
 */

export type DiffKind = 'same' | 'added' | 'removed';

export interface DiffLine {
  kind: DiffKind;
  text: string;
}

/**
 * Above this many lines on either side the quadratic table is refused rather than built.
 * ponytail: an O(n·m) table is fine for a wiki page and hostile for a pasted log — the cap turns a
 * frozen tab into an honest message. Raise it, or move to a proper diff library, only if real
 * documents start hitting it.
 */
export const DIFF_LINE_LIMIT = 2000;

export interface DiffResult {
  lines: DiffLine[];
  /** True when the comparison was refused because a side exceeded DIFF_LINE_LIMIT. */
  tooLarge: boolean;
}

/**
 * Longest-common-subsequence diff over lines. The table holds the LCS length for every suffix pair,
 * and the walk emits `removed` before `added` at each divergence so a changed line reads as its old
 * form followed by its new one.
 */
export function diffLines(before: string, after: string): DiffResult {
  const a = before.split('\n');
  const b = after.split('\n');

  if (a.length > DIFF_LINE_LIMIT || b.length > DIFF_LINE_LIMIT) {
    return { lines: [], tooLarge: true };
  }

  const n = a.length;
  const m = b.length;
  const lcs: number[][] = Array.from({ length: n + 1 }, () => new Array<number>(m + 1).fill(0));
  for (let i = n - 1; i >= 0; i--) {
    for (let j = m - 1; j >= 0; j--) {
      lcs[i][j] = a[i] === b[j] ? lcs[i + 1][j + 1] + 1 : Math.max(lcs[i + 1][j], lcs[i][j + 1]);
    }
  }

  const lines: DiffLine[] = [];
  let i = 0;
  let j = 0;
  while (i < n && j < m) {
    if (a[i] === b[j]) {
      lines.push({ kind: 'same', text: a[i] });
      i++;
      j++;
    } else if (lcs[i + 1][j] >= lcs[i][j + 1]) {
      lines.push({ kind: 'removed', text: a[i] });
      i++;
    } else {
      lines.push({ kind: 'added', text: b[j] });
      j++;
    }
  }
  while (i < n) lines.push({ kind: 'removed', text: a[i++] });
  while (j < m) lines.push({ kind: 'added', text: b[j++] });

  return { lines, tooLarge: false };
}

/** Counts for the summary line, so a reader sees the shape of a change before reading it. */
export function diffStat(lines: DiffLine[]) {
  return {
    added: lines.filter((l) => l.kind === 'added').length,
    removed: lines.filter((l) => l.kind === 'removed').length,
  };
}
