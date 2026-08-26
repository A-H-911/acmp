/*
 * NFR-037's number half, at the seam that matters: a number handed to t() must come back in the
 * reader's digits. This drives the REAL i18n instance rather than a formatter in isolation, because
 * the wiring is the fragile part — `interpolation.format` looks like the hook and is silently
 * overwritten by i18next's own Formatter, and `alwaysFormat` is what makes the module fire at all.
 *
 * MUTATION CHECK (both halves confirmed to compile and to turn these red):
 *   - drop `.use(numberFormatter)`  -> "١٢ من ٢٠ حاضرون" reverts to "12 من 20 حاضرون"
 *   - drop `alwaysFormat: true`     -> same reversion; the module is registered but never called
 */
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import i18n from './index';

const AR_DIGITS = /[٠-٩]/; // ٠-٩
const LATIN_DIGITS = /[0-9]/;

describe('number interpolation through the live i18n instance', () => {
  const original = i18n.language;
  beforeEach(async () => {
    await i18n.changeLanguage('ar');
  });
  afterEach(async () => {
    await i18n.changeLanguage(original);
  });

  it('renders a number handed to t() in Arabic-Indic digits', () => {
    const out = i18n.t('meetings.attendanceSummary', { present: 12, total: 20, needed: 7 });
    expect(out).toMatch(AR_DIGITS);
    expect(out).not.toMatch(LATIN_DIGITS);
  });

  it('leaves a STRING placeholder alone — an entity key is not a quantity', () => {
    // TOP-042 must survive verbatim: keying the formatter off the runtime type is the whole guard.
    expect(i18n.t('kanban.accept.title', { key: 'TOP-042' })).toContain('TOP-042');
  });

  it('groups thousands in English, which is the same requirement seen from the other locale', async () => {
    await i18n.changeLanguage('en');
    expect(i18n.t('audit.count', { count: 12345 })).toContain('12,345');
  });

  /*
   * The escape hatch the module's own comment promises. Replacing i18next's Formatter costs the
   * built-in named formats, and the comment says to register what you need with
   * `i18n.services.formatter.add(...)` — a claim about a code path nothing else exercises, which is
   * the kind of sentence that is true when written and quietly stops being true. These run it.
   */
  describe('named formats registered through the formatter service', () => {
    it('honours a format added with add()', async () => {
      await i18n.changeLanguage('en');
      i18n.services.formatter?.add('shout', (value) => String(value).toUpperCase());
      i18n.addResource('en', 'translation', 'test.shout', '{{v, shout}}');
      expect(i18n.t('test.shout', { v: 'loud' })).toBe('LOUD');
    });

    it('honours a format added with addCached()', async () => {
      await i18n.changeLanguage('en');
      i18n.services.formatter?.addCached('bracket', () => (value) => `[${String(value)}]`);
      i18n.addResource('en', 'translation', 'test.bracket', '{{v, bracket}}');
      expect(i18n.t('test.bracket', { v: 'x' })).toBe('[x]');
    });

    it('passes a value through untouched when the named format is unknown', async () => {
      await i18n.changeLanguage('en');
      i18n.addResource('en', 'translation', 'test.unknown', '{{v, nosuchformat}}');
      expect(i18n.t('test.unknown', { v: 'x' })).toBe('x');
    });
  });

  it('formats every numeric placeholder in a multi-placeholder string, not just the first', () => {
    const out = i18n.t('topics.showing', { shown: 25, total: 1300 });
    expect(out).not.toMatch(LATIN_DIGITS);
    // 1300 must be grouped in Arabic-Indic too — proves options reach the formatter, not just digits.
    expect(out).toContain('١٬٣٠٠');
  });
});
