import { describe, it, expect } from 'vitest';
import { surround, linePrefix, insertLink, insertCrossLink } from './markdownInsert';

describe('markdownInsert', () => {
  it('surrounds the selection with a mark and keeps it selected', () => {
    const r = surround('hello world', 6, 11, '**');
    expect(r.text).toBe('hello **world**');
    expect(r.text.slice(r.start, r.end)).toBe('world');
  });

  it('prefixes the line containing the selection', () => {
    const r = linePrefix('a\nsecond line', 2, 2, '# ');
    expect(r.text).toBe('a\n# second line');
    expect(r.start).toBe(4); // caret shifted past the prefix
  });

  it('inserts a link with placeholder text when nothing is selected', () => {
    const r = insertLink('', 0, 0);
    expect(r.text).toBe('[text](url)');
    expect(r.text.slice(r.start, r.end)).toBe('text');
  });

  it('inserts a link around the selection when text is selected', () => {
    const r = insertLink('see docs', 4, 8);
    expect(r.text).toBe('see [docs](url)');
    expect(r.text.slice(r.start, r.end)).toBe('docs');
  });

  it('inserts a [[KEY]] cross-link with the key selected', () => {
    const r = insertCrossLink('', 0, 0);
    expect(r.text).toBe('[[KEY]]');
    expect(r.text.slice(r.start, r.end)).toBe('KEY');
  });
});
