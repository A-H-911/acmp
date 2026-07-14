/*
 * Pure presentation mappers for the Knowledge wiki (Document) read models.
 * Status tone maps the wire enum names onto the shared StatusChip tones; meaning is carried by the
 * localized label + a coloured dot, never colour alone (WCAG 1.4.1). The wiki design (reading view)
 * has no status affordance, so these tones are no-reference choices reconciled to the lifecycle:
 * Draft → neutral (not yet public), Published → success (live), Archived → warn (retired).
 */
import type { StatusTone } from '../../components/ui/StatusChip';
import type { DocumentStatus } from '../../api/wiki';

const STATUS_TONE: Record<DocumentStatus, StatusTone> = {
  Draft: 'neutral',
  Published: 'success',
  Archived: 'warn',
};

export function statusTone(status: DocumentStatus): StatusTone {
  return STATUS_TONE[status] ?? 'neutral';
}

/** DocumentStatus values (lifecycle order) — the tree filter options (both locales). */
export const DOCUMENT_STATUSES: DocumentStatus[] = ['Draft', 'Published', 'Archived'];

/**
 * Suggested wiki "spaces" (categories) — the create dialog's category Select, matching the design's
 * discrete tree spaces. Free-text on the backend: legacy/seeded docs in other categories still group
 * into the tree; only NEW documents are constrained to this set (flagged, INV-014 reconciliation).
 */
export const WIKI_CATEGORIES: string[] = ['Governance', 'Standards', 'Operations', 'General'];

const WORDS_PER_MINUTE = 200;

/** Estimated read time in whole minutes (≥1) from a markdown body — words ÷ 200, computed client-side. */
export function readTime(body: string): number {
  const words = body.trim().split(/\s+/).filter(Boolean).length;
  return Math.max(1, Math.ceil(words / WORDS_PER_MINUTE));
}

/** Two-letter avatar fallback from a display name. */
export function initials(name: string): string {
  const parts = name.trim().split(/\s+/);
  return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || '?';
}
