import { describe, it, expect } from 'vitest';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import { numberLocale, formatNumber, formatPercent } from './numberFmt';

/*
 * AC-167 (DEC-192 r2): ONE shared decision for digits - every Intl formatter takes its locale from numberLocale(),
 * which gives Latin digits in Arabic. DEF-175 was twenty formatters passing the bare `ar` tag, whose digits are an
 * ICU default, beside four that pinned Arabic-Indic. This reads the source, so a new formatter that bypasses the
 * shared tag fails here instead of on a screen.
 */
const SRC = join(__dirname, '..');

function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const p = join(dir, name);
    if (statSync(p).isDirectory()) return sourceFiles(p);
    return /\.(ts|tsx)$/.test(name) && !/\.test\.(ts|tsx)$/.test(name) ? [p] : [];
  });
}

const ARABIC_INDIC = /[٠-٩۰-۹]/;

describe('AC-167: one digit rule for every screen', () => {
  it('every Intl formatter takes its locale from numberLocale() (or a fixed English tag)', () => {
    const offenders: string[] = [];
    for (const file of sourceFiles(SRC)) {
      const text = readFileSync(file, 'utf8');
      for (const m of text.matchAll(/new Intl\.(DateTimeFormat|NumberFormat|RelativeTimeFormat|PluralRules)\(([^,)]*)/g)) {
        const arg = m[2].trim();
        const viaVariable = /^\w+$/.test(arg) && new RegExp(`\\b${arg}\\s*=\\s*numberLocale\\(`).test(text);
        const ok = arg.startsWith('numberLocale(') || arg === "'en-GB'" || arg === "'en'"
          || arg.startsWith("lang === 'ar' ? numberLocale(") || viaVariable;
        if (!ok) offenders.push(`${relative(SRC, file)}: new Intl.${m[1]}(${arg}`);
      }
    }
    expect(offenders).toEqual([]);
  });

  it('nothing pins Arabic-Indic digits any more', () => {
    const pinned = sourceFiles(SRC).filter((f) => readFileSync(f, 'utf8').includes('nu-arab')).map((f) => relative(SRC, f));
    expect(pinned).toEqual([]);
  });

  it('the Arabic translations contain no Arabic-Indic digit', () => {
    const ar = readFileSync(join(SRC, 'i18n', 'locales', 'ar.json'), 'utf8');
    expect(ar.match(ARABIC_INDIC)).toBeNull();
  });

  it('Arabic dates, times, numbers and percentages render with Latin digits', () => {
    const tag = numberLocale('ar');
    const when = new Date('2026-09-14T10:18:03Z');
    const samples = [
      new Intl.DateTimeFormat(tag, { dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC' }).format(when),
      new Intl.DateTimeFormat(tag, { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(when),
      new Intl.RelativeTimeFormat(tag, { numeric: 'auto' }).format(-3, 'minute'),
      formatNumber(1234.5, 'ar'),
      formatPercent(42, 'ar'),
    ];
    for (const s of samples) expect(s).not.toMatch(ARABIC_INDIC);
    expect(samples[0]).toContain('14');
    expect(samples[3]).toBe('1,234.5');
  });
});
