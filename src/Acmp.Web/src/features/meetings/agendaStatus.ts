import type { StatusTone } from '../../components/ui/StatusChip';

// Agenda lifecycle → semantic tone (no list screen in the design package, so this follows the
// design's tone language: published = success, not-yet-published = warn, locked = informational,
// closed = done/archived). Draft stays warn to match the lifecycle banner's "agenda not published".
// Shared by the meetings list AND the meeting-detail head so a Locked/Closed agenda never
// mislabels as "Draft" in the viewer (the head chip was previously a binary Published/Draft check).
export function agendaTone(status: string | undefined): StatusTone {
  switch (status) {
    case 'Published':
      return 'success';
    case 'Locked':
      return 'info';
    case 'Closed':
      return 'neutral';
    default:
      return 'warn'; // Draft / unknown — needs attention (not yet published)
  }
}
