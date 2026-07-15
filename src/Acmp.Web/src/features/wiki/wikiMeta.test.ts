import { describe, it, expect } from 'vitest';
import type { TFunction } from 'i18next';
import { statusTone, DOCUMENT_STATUSES, WIKI_CATEGORIES, readTime, initials, categoryLabel } from './wikiMeta';

// Identity stub: known categories resolve through the key, unknown ones bypass it.
const tKey = ((k: string) => k) as unknown as TFunction;

describe('wikiMeta', () => {
  it('maps each DocumentStatus to its tone', () => {
    expect(statusTone('Draft')).toBe('neutral');
    expect(statusTone('Published')).toBe('success');
    expect(statusTone('Archived')).toBe('warn');
  });

  it('falls back to neutral for an unknown status', () => {
    expect(statusTone('Weird' as never)).toBe('neutral');
  });

  it('exposes the lifecycle status + category option arrays', () => {
    expect(DOCUMENT_STATUSES).toEqual(['Draft', 'Published', 'Archived']);
    expect(WIKI_CATEGORIES).toContain('Governance');
    expect(WIKI_CATEGORIES).toContain('General');
  });

  it('estimates read time as words ÷ 200, at least one minute', () => {
    expect(readTime('')).toBe(1);
    expect(readTime('one two three')).toBe(1);
    expect(readTime(Array.from({ length: 400 }, () => 'w').join(' '))).toBe(2);
    expect(readTime(Array.from({ length: 201 }, () => 'w').join(' '))).toBe(2);
  });

  it('localizes known categories via the wiki.category.* key and falls back to raw for legacy ones', () => {
    expect(categoryLabel('Governance', tKey)).toBe('wiki.category.Governance');
    expect(categoryLabel('LegacySpace', tKey)).toBe('LegacySpace');
  });

  it('derives two-letter initials with a fallback', () => {
    expect(initials('Khalid Ahmed')).toBe('KA');
    expect(initials('Solo')).toBe('S');
    expect(initials('')).toBe('?');
  });
});
