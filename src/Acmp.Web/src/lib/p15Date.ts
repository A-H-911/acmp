/*
 * P15 (Research & Knowledge) date formatting. The "ACMP Research & Knowledge.dc.html" mockup renders
 * dates day-first ("14 Jun 2026" / "١٤ يونيو ٢٠٢٦"), so these surfaces force DMY regardless of the UI
 * locale's default order — `en-GB` day-first, and Arabic day-first with Latin digits (AC-167, INV-014).
 *
 * ponytail: P15-scoped, not an app-wide switch — the rest of the app keeps its medium/MDY format; a global
 * DMY migration is separate tech-debt (plan §"Deliberately NOT done").
 */
import { numberLocale } from './numberFmt';

export function formatDmy(iso: string, lang: string): string {
  // AC-167: Arabic goes through the shared numberLocale() pin (Latin digits), never the bare tag.
  return new Intl.DateTimeFormat(lang === 'ar' ? numberLocale(lang) : 'en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  }).format(new Date(iso));
}
