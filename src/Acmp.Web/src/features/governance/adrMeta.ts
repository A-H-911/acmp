/*
 * ADR presentation logic (P11b) — the pure, testable pieces shared by the register and the detail:
 * the status → StatusChip tone map and the client-side .md export (FR-104). Kept out of the components
 * so both the 5-value tone map and the export's optional-field branches carry their own unit coverage.
 */
import type { StatusTone } from '../../components/ui/StatusChip';
import type { AdrDetail, AdrStatus, LocalizedText } from '../../api/adrs';

/** The five canonical ADR statuses, register-filter order (lifecycle order). */
export const ADR_STATUSES: readonly AdrStatus[] = ['Draft', 'Proposed', 'Approved', 'Superseded', 'Deprecated'];

/**
 * Status → chip tone. Draft is a neutral work-in-progress; Proposed is in-flight (info); Approved is the
 * live decision (success); Superseded is retired-but-kept (neutral — matches the design's neutral Superseded
 * badge); Deprecated is withdrawn (danger). Mirrors the design's adrStatus map, extended to the states the
 * design's 3-tab preview omits.
 */
export function statusTone(status: AdrStatus): StatusTone {
  switch (status) {
    case 'Draft':
      return 'neutral';
    case 'Proposed':
      return 'info';
    case 'Approved':
      return 'success';
    case 'Superseded':
      return 'neutral';
    case 'Deprecated':
      return 'danger';
  }
}

/** Section labels for the exported markdown — passed in so the caller localizes them once via i18n. */
export interface AdrExportLabels {
  status: string;
  context: string;
  drivers: string;
  options: string;
  chosen: string;
  decision: string;
  consequences: string;
  positive: string;
  negative: string;
}

/**
 * Render an ADR to a MADR-lite markdown document (FR-104, client-side — the design's "Export .md" button).
 * Optional sections (drivers, options, either consequence block) are emitted only when present, so the
 * output stays clean for a lean Draft and complete for a full record. `lang` picks the locale to export.
 */
export function exportMarkdown(adr: AdrDetail, lang: string, labels: AdrExportLabels): string {
  const pick = (l: LocalizedText | null): string => (l ? (lang === 'ar' ? l.ar : l.en) : '');
  const lines: string[] = [];

  lines.push(`# ${adr.key} — ${pick(adr.title)}`, '');
  lines.push(`**${labels.status}:** ${adr.status}`, '');

  lines.push(`## ${labels.context}`, pick(adr.context), '');

  if (adr.decisionDrivers) {
    lines.push(`## ${labels.drivers}`, pick(adr.decisionDrivers), '');
  }

  if (adr.options.length > 0) {
    lines.push(`## ${labels.options}`);
    for (const o of adr.options) {
      const mark = o.isChosen ? ` _(${labels.chosen})_` : '';
      lines.push(`- **${pick(o.name)}**${mark}${o.body ? ` — ${pick(o.body)}` : ''}`);
    }
    lines.push('');
  }

  lines.push(`## ${labels.decision}`, pick(adr.decisionText), '');

  if (adr.consequencesPositive || adr.consequencesNegative) {
    lines.push(`## ${labels.consequences}`);
    if (adr.consequencesPositive) lines.push(`**${labels.positive}:** ${pick(adr.consequencesPositive)}`);
    if (adr.consequencesNegative) lines.push(`**${labels.negative}:** ${pick(adr.consequencesNegative)}`);
    lines.push('');
  }

  return lines.join('\n');
}

/** Trigger a browser download of `content` as `filename` (Blob + object URL). */
export function downloadMarkdown(filename: string, content: string): void {
  const blob = new Blob([content], { type: 'text/markdown;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
