/*
 * NFR-037's NUMBER half (DW-068 / WBS-24.4). The date half was already locale-aware; every number a
 * user reads was not, so an Arabic screen rendered ١٤ يونيو ٢٠٢٦ beside a Latin "12" in the same row.
 *
 * AC-167 (DEC-192 r2, superseding AC-147): LATIN digits in BOTH languages - dates, times and every
 * quantity. The numbering system is PINNED (`ar-u-nu-latn`) rather than left to the bare `ar` tag, because
 * what bare `ar` resolves to is an ICU/CLDR default that has changed between versions (ICU 78 gives Latin;
 * this file used to assert a browser gives Arabic-Indic) - a pin renders the same everywhere. Every `Intl`
 * formatter in the SPA takes its locale from numberLocale(); numberLocale.guard.test.ts enforces it.
 */
import { useTranslation } from 'react-i18next';

/**
 * The BCP-47 tag to format with. Arabic pins Latin digits (AC-167); everything else takes `en`.
 * Exported because ANY `Intl` formatter that emits digits needs it — `RelativeTimeFormat` does.
 */
export function numberLocale(lang: string | undefined): string {
  return lang?.startsWith('ar') ? 'ar-u-nu-latn' : 'en';
}

/** Locale-appropriate digits and separators for any number a user reads. */
export function formatNumber(value: number, lang: string | undefined, opts?: Intl.NumberFormatOptions): string {
  return new Intl.NumberFormat(numberLocale(lang), opts).format(value);
}

/**
 * A percentage given the way this codebase already holds one — 0..100, not 0..1.
 *
 * ⚠ THE SIGN IS PART OF THE FORMAT, NOT A SUFFIX. The mockups draw `٤٠٪` in Arabic, with U+066A, and
 * the app was rendering an ASCII `%` glued on after the number (INV-014). `style: 'percent'` makes
 * Intl choose the right sign per locale, which is also why this takes 0..100 and divides: the data
 * carries whole percents everywhere, and converting at the boundary beats changing every producer.
 */
export function formatPercent(value: number, lang: string | undefined): string {
  return new Intl.NumberFormat(numberLocale(lang), { style: 'percent', maximumFractionDigits: 0 }).format(value / 100);
}

/**
 * Bytes rounded to the unit a person reads. Replaces three separate implementations — SessionPage's
 * (locale-aware) and MeetingRecording's and SubmitTopic's (both Latin-only, which is the defect).
 *
 * AC-168 (DEF-178): the unit is the one the mockups draw in each language - `KB`/`MB` in English and
 * `ك.ب`/`م.ب` in Arabic (ACMP Backlog & Topic.dc.html; ar.json already writes `م.ب`). This comment used to say
 * the mockups draw ASCII units in both; they do not.
 */
const BYTE_UNITS = { en: ['B', 'KB', 'MB', 'GB'], ar: ['ب', 'ك.ب', 'م.ب', 'ج.ب'] } as const;

export function formatBytes(bytes: number, lang: string | undefined): string {
  const units = lang?.startsWith('ar') ? BYTE_UNITS.ar : BYTE_UNITS.en;
  let value = Math.max(0, bytes);
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit += 1;
  }
  return `${formatNumber(value, lang, { maximumFractionDigits: unit === 0 ? 0 : 1 })} ${units[unit]}`;
}

/*
 * The JSX form. A number written straight into JSX — `{col.cards.length}` — is the one place the i18n
 * formatter cannot reach, so each such site becomes `<Num value={col.cards.length} />`.
 *
 * ponytail: a COMPONENT rather than a `useNum()` hook, because a hook needs a declaration line inside
 * every component that renders a number — fourteen of them here, several with multi-line signatures —
 * while a component needs nothing but the edit at the render site. Use `formatNumber` directly where
 * the result goes into a template literal rather than into JSX.
 */
export function Num({ value, ...opts }: { value: number } & Intl.NumberFormatOptions) {
  const { i18n } = useTranslation();
  return <>{formatNumber(value, i18n.language, opts)}</>;
}

/** A 0..100 percentage in the live UI language, sign included. */
export function Pct({ value }: { value: number }) {
  const { i18n } = useTranslation();
  return <>{formatPercent(value, i18n.language)}</>;
}

/** Bytes in the live UI language, for JSX. */
export function Bytes({ value }: { value: number }) {
  const { i18n } = useTranslation();
  return <>{formatBytes(value, i18n.language)}</>;
}
