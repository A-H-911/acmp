/*
 * Pure display helpers for the Job Monitor. Kept out of the component so the fiddly time/duration
 * logic is unit-tested deterministically (nowMs is injected, not read from the clock). Relative time
 * uses the native Intl.RelativeTimeFormat — correct EN + AR wording (and plurals) for free, no i18n keys.
 */

/** Compact, locale-aware relative time. Positive delta = future ("in 3 minutes"), negative = past. */
export function formatRelative(iso: string, locale: string, nowMs: number): string {
  const deltaSec = Math.round((new Date(iso).getTime() - nowMs) / 1000);
  const abs = Math.abs(deltaSec);
  const rtf = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' });
  if (abs >= 86_400) return rtf.format(Math.round(deltaSec / 86_400), 'day');
  if (abs >= 3_600) return rtf.format(Math.round(deltaSec / 3_600), 'hour');
  if (abs >= 60) return rtf.format(Math.round(deltaSec / 60), 'minute');
  return rtf.format(deltaSec, 'second');
}

/** Split a millisecond duration into a display number + unit key (rendered via i18n's {{n}} suffix). */
export function formatDuration(ms: number): { n: string; unit: 'ms' | 's' } {
  if (ms < 1000) return { n: String(ms), unit: 'ms' };
  const s = ms / 1000;
  return { n: Number.isInteger(s) ? String(s) : s.toFixed(1), unit: 's' };
}
