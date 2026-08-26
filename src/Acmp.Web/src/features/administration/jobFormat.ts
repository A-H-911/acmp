/*
 * Pure display helpers for the Job Monitor. Kept out of the component so the fiddly time/duration
 * logic is unit-tested deterministically (nowMs is injected, not read from the clock). Relative time
 * uses the native Intl.RelativeTimeFormat — correct EN + AR wording (and plurals) for free, no i18n keys.
 */

import { numberLocale } from '../../lib/numberFmt';

/** Compact, locale-aware relative time. Positive delta = future ("in 3 minutes"), negative = past. */
export function formatRelative(iso: string, locale: string, nowMs: number): string {
  const deltaSec = Math.round((new Date(iso).getTime() - nowMs) / 1000);
  const abs = Math.abs(deltaSec);
  // NFR-037: RelativeTimeFormat emits DIGITS too, so it needs the same numbering-system pin as every
  // other formatter here — bare `ar` gives Latin digits under Node and Arabic-Indic in a browser.
  const rtf = new Intl.RelativeTimeFormat(numberLocale(locale), { numeric: 'auto' });
  if (abs >= 86_400) return rtf.format(Math.round(deltaSec / 86_400), 'day');
  if (abs >= 3_600) return rtf.format(Math.round(deltaSec / 3_600), 'hour');
  if (abs >= 60) return rtf.format(Math.round(deltaSec / 60), 'minute');
  return rtf.format(deltaSec, 'second');
}

/*
 * Split a millisecond duration into a display number + unit key (rendered via i18n's {{n}} suffix).
 *
 * ⚠ `n` is a NUMBER, not a string, and that is the whole point: the i18n formatter localizes numeric
 * placeholders by RUNTIME TYPE, so pre-stringifying here (`String(ms)` / `toFixed(1)`) would hand it
 * a string and silently opt this one reading out of NFR-037. Rounding stays here; digits do not.
 */
export function formatDuration(ms: number): { n: number; unit: 'ms' | 's' } {
  if (ms < 1000) return { n: ms, unit: 'ms' };
  const s = ms / 1000;
  return { n: Number.isInteger(s) ? s : Math.round(s * 10) / 10, unit: 's' };
}
