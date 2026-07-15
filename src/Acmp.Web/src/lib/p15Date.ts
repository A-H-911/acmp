/*
 * P15 (Research & Knowledge) date formatting. The "ACMP Research & Knowledge.dc.html" mockup renders
 * dates day-first ("14 Jun 2026" / "١٤ يونيو ٢٠٢٦"), so these surfaces force DMY regardless of the UI
 * locale's default order — `en-GB` gives Latin day-first, `ar` gives Arabic-Indic day-first (INV-014).
 *
 * ponytail: P15-scoped, not an app-wide switch — the rest of the app keeps its medium/MDY format; a global
 * DMY migration is separate tech-debt (plan §"Deliberately NOT done").
 */
export function formatDmy(iso: string, lang: string): string {
  // `ar-u-nu-arab` pins the Arabic-Indic numbering system so digits render as ١٤ (not 14) regardless of
  // the runtime's ICU default — Node defaults `ar` to Latin digits, browsers to Arabic-Indic; this agrees.
  return new Intl.DateTimeFormat(lang === 'ar' ? 'ar-u-nu-arab' : 'en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  }).format(new Date(iso));
}
