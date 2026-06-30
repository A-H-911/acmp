import type { StatusTone } from '../../components/ui/StatusChip';

// Meeting status → semantic tone, shared by the list rows and the calendar event pills so a
// meeting reads the same colour in both views. Matches the design's tone language: held/closed =
// done (success), cancelled = danger, in-progress = active (warn), scheduled/draft = sched.
export function meetingTone(status: string | undefined): StatusTone {
  switch (status) {
    case 'Held':
    case 'Closed':
      return 'success';
    case 'Cancelled':
      return 'danger';
    case 'InProgress':
      return 'warn';
    default:
      return 'scheduled'; // Scheduled / draft-ish
  }
}

// A meeting belongs to the "Past" section once it has concluded (held, closed, or cancelled);
// everything else is "Upcoming". Status-based (not clock-based) so a meeting that is mid-session
// or whose date has slipped doesn't flip sections under the user — it stays upcoming until closed.
export function isConcluded(status: string): boolean {
  return status === 'Held' || status === 'Closed' || status === 'Cancelled';
}

// Lifecycle phase = the single derived state the meeting shell, overview banner, and conduct gate
// all read (ACMP Meetings.dc.html lcMap). Folds meeting.status × agenda-published into one of five
// phases so the primary action, banner, and gating never disagree about where a meeting is.
export type MeetingPhase = 'notReady' | 'ready' | 'inProgress' | 'concluded' | 'cancelled';

export function lifecyclePhase(meetingStatus: string, agendaPublished: boolean): MeetingPhase {
  if (meetingStatus === 'Cancelled') return 'cancelled';
  if (meetingStatus === 'Held' || meetingStatus === 'Closed') return 'concluded';
  if (meetingStatus === 'InProgress') return 'inProgress';
  return agendaPublished ? 'ready' : 'notReady';
}
