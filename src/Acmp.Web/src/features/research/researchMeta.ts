/*
 * Pure presentation mappers for Research read models (register + mission detail).
 * Tones map the wire enum names onto the shared StatusChip tones; meaning is carried by the
 * localized label + a coloured dot, never colour alone (WCAG 1.4.1).
 *
 * Mission-status tones: the register's list block in "ACMP Research & Knowledge.dc.html" (mStatus,
 * line 552) is the visual reference — Active ("In discovery") → info, Completed ("Concluded") →
 * success. Proposed and Cancelled have NO reference (the design's mission list omits them), so they
 * are no-reference choices: Proposed → neutral (not yet in discovery), Cancelled → danger (a terminal
 * side-exit, reads distinct from the neutral Proposed). Flagged in the progress note.
 */
import type { StatusTone } from '../../components/ui/StatusChip';
import type { ResearchStatus, RecommendationStatus, Confidence, RecommendationPriority } from '../../api/research';

const STATUS_TONE: Record<ResearchStatus, StatusTone> = {
  Proposed: 'neutral',
  Active: 'info',
  Completed: 'success',
  Cancelled: 'danger',
};

export function statusTone(status: ResearchStatus): StatusTone {
  return STATUS_TONE[status] ?? 'neutral';
}

/** A finding's item chip is derived from IsVerified: Verified → success, Unverified → neutral. */
export function findingTone(isVerified: boolean): StatusTone {
  return isVerified ? 'success' : 'neutral';
}

// Converted is a P15c no-reference addition (the design's rec list predates the convert flow): 'scheduled'
// reads as "moved forward into execution", distinct from Accepted (success) and Proposed (info). Flagged.
const REC_TONE: Record<RecommendationStatus, StatusTone> = {
  Proposed: 'info',
  Accepted: 'success',
  Rejected: 'danger',
  Converted: 'scheduled',
};

export function recStatusTone(status: RecommendationStatus): StatusTone {
  return REC_TONE[status] ?? 'neutral';
}

/** Two-letter avatar fallback from a display name. */
export function initials(name: string): string {
  const parts = name.trim().split(/\s+/);
  return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || '?';
}

/** ResearchMissionStatus values (lifecycle order) — the register's Status filter options (both locales). */
export const RESEARCH_STATUSES: ResearchStatus[] = ['Proposed', 'Active', 'Completed', 'Cancelled'];

/** Confidence bands — the Add-finding form's select options. */
export const CONFIDENCES: Confidence[] = ['Low', 'Medium', 'High'];

/** Recommendation priority bands — the Add-recommendation form's select options. */
export const PRIORITIES: RecommendationPriority[] = ['Low', 'Medium', 'High'];

/** Recommendation dispositions — the item chip vocabulary (both locales). */
export const REC_STATUSES: RecommendationStatus[] = ['Proposed', 'Accepted', 'Rejected', 'Converted'];
