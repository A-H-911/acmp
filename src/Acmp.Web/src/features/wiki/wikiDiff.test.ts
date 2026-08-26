import { describe, it, expect } from 'vitest';
import { diffLines, diffStat, DIFF_LINE_LIMIT } from './wikiDiff';

/**
 * The assertions describe the RESULT of a diff rather than re-implementing the walk. A test that
 * recomputes the LCS alongside the implementation agrees with it by construction and would still
 * pass if both were wrong.
 */
const kinds = (before: string, after: string) => diffLines(before, after).lines.map((l) => `${l.kind[0]}:${l.text}`);

describe('diffLines', () => {
  it('marks every line same when nothing changed', () => {
    expect(kinds('a\nb\nc', 'a\nb\nc')).toEqual(['s:a', 's:b', 's:c']);
  });

  it('reports an inserted line without disturbing its neighbours', () => {
    expect(kinds('a\nc', 'a\nb\nc')).toEqual(['s:a', 'a:b', 's:c']);
  });

  it('reports a deleted line without disturbing its neighbours', () => {
    expect(kinds('a\nb\nc', 'a\nc')).toEqual(['s:a', 'r:b', 's:c']);
  });

  it('renders a changed line as the old form followed by the new one', () => {
    expect(kinds('a\nold\nc', 'a\nnew\nc')).toEqual(['s:a', 'r:old', 'a:new', 's:c']);
  });

  it('keeps unmoved lines SAME when a block is inserted in the middle', () => {
    // The point of an LCS diff rather than a positional compare: everything after the insertion
    // must stay `same`, not shift into a wall of add/remove pairs.
    const out = kinds('intro\ntail', 'intro\none\ntwo\nthree\ntail');
    expect(out).toEqual(['s:intro', 'a:one', 'a:two', 'a:three', 's:tail']);
  });

  it('handles an empty starting document as all additions', () => {
    expect(kinds('', 'a\nb')).toEqual(['r:', 'a:a', 'a:b']);
  });

  it('handles everything being deleted', () => {
    expect(kinds('a\nb', '')).toEqual(['r:a', 'r:b', 'a:']);
  });

  it('treats two empty documents as a single unchanged empty line', () => {
    expect(kinds('', '')).toEqual(['s:']);
  });

  it('refuses rather than building a quadratic table for an enormous document', () => {
    const huge = Array.from({ length: DIFF_LINE_LIMIT + 1 }, (_, i) => `line ${i}`).join('\n');
    const result = diffLines(huge, 'a');
    expect(result.tooLarge).toBe(true);
    expect(result.lines).toEqual([]);
  });

  it('refuses when the AFTER side is the enormous one', () => {
    const huge = Array.from({ length: DIFF_LINE_LIMIT + 1 }, (_, i) => `line ${i}`).join('\n');
    expect(diffLines('a', huge).tooLarge).toBe(true);
  });

  it('accepts a document exactly at the limit, so the bound is inclusive as written', () => {
    const atLimit = Array.from({ length: DIFF_LINE_LIMIT }, (_, i) => `line ${i}`).join('\n');
    expect(diffLines(atLimit, atLimit).tooLarge).toBe(false);
  });
});

describe('diffStat', () => {
  it('counts additions and removals and ignores unchanged lines', () => {
    const { lines } = diffLines('a\nold\nc', 'a\nnew\nc\nextra');
    expect(diffStat(lines)).toEqual({ added: 2, removed: 1 });
  });

  it('reports zeroes when nothing changed', () => {
    const { lines } = diffLines('a\nb', 'a\nb');
    expect(diffStat(lines)).toEqual({ added: 0, removed: 0 });
  });
});
