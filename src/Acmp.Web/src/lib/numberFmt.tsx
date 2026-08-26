/*
 * NFR-037's NUMBER half (DW-068 / WBS-24.4). The date half was already locale-aware; every number a
 * user reads was not, so an Arabic screen rendered ١٤ يونيو ٢٠٢٦ beside a Latin "12" in the same row.
 *
 * `ar-u-nu-arab` is pinned for the same reason `p15Date.formatDmy` pins it: bare `ar` resolves to
 * Latin digits under Node's ICU and Arabic-Indic in a browser, so an unpinned formatter renders one
 * thing in the test runner and another on screen — and the test would agree with itself either way.
 */
import { useTranslation } from 'react-i18next';

/**
 * The BCP-47 tag to format with. Arabic pins its numbering system; everything else takes `en`.
 * Exported because ANY `Intl` formatter that emits digits needs it — `RelativeTimeFormat` does.
 */
export function numberLocale(lang: string | undefined): string {
  return lang?.startsWith('ar') ? 'ar-u-nu-arab' : 'en';
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
 * ponytail: the unit suffix stays the ASCII symbol the mockups draw (INV-014). `style: 'unit'` would
 * localize it to "ميغابايت" and change EN copy too — a design change, not this row's work.
 */
export function formatBytes(bytes: number, lang: string | undefined): string {
  const units = ['B', 'KB', 'MB', 'GB'];
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
