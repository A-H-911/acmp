/*
 * Pure presentation mappers for Topic read models — shared by the backlog and
 * (later) the detail screen. Status tone maps the canonical TopicStatus wire
 * names (docs/12 §1) onto the six StatusChip tones; the chip carries the
 * localized label + a colored dot, never color alone (WCAG 1.4.1).
 */
import type { StatusTone } from '../../components/ui/StatusChip';

const STATUS_TONE: Record<string, StatusTone> = {
  Draft: 'neutral',
  Submitted: 'neutral',
  Triage: 'neutral',
  Reopened: 'neutral',
  Accepted: 'info',
  Prepared: 'info',
  InCommittee: 'info',
  Scheduled: 'scheduled',
  Deferred: 'warn',
  Decided: 'success',
  Closed: 'success',
  Converted: 'success',
  Rejected: 'danger',
};

export function statusTone(status: string): StatusTone {
  return STATUS_TONE[status] ?? 'neutral';
}

/** Two-letter avatar fallback from a display name. */
export function initials(name: string): string {
  const parts = name.trim().split(/\s+/);
  return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || '?';
}
