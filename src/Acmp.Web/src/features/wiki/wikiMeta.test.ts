import { describe, it, expect } from 'vitest';
import { statusTone, DOCUMENT_STATUSES, WIKI_CATEGORIES, readTime, initials } from './wikiMeta';

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

  it('derives two-letter initials with a fallback', () => {
    expect(initials('Khalid Ahmed')).toBe('KA');
    expect(initials('Solo')).toBe('S');
    expect(initials('')).toBe('?');
  });
});
