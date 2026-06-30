/*
 * Row presentation for the notification center — shared by the bell popover and the full page so the
 * two stay DRY (ACMP.dc.html L92–131 + L706–739). The design row shows a TYPE label + tone-coloured
 * icon and the artifact runtime key. Neither is a stored column:
 *   - `type` is derived from the existing Category (one v1 category today: AgendaPublished).
 *   - `key`  is derived from the DeepLink's last path segment (e.g. /meetings/MTG-2026-001 → MTG-2026-001).
 * The operator's P6b Option-B note pre-authorised derive-over-a-new-column. Unknown categories fall back
 * to a neutral "Update" so a row never renders a blank type, and a null DeepLink yields no key chip.
 */
import type { IconName } from '../components/icons';

export type NotifTone = 'accent' | 'success' | 'info' | 'warn' | 'neutral';

export interface NotifType {
  labelKey: string;
  tone: NotifTone;
  icon: IconName;
}

const TYPES: Record<string, NotifType> = {
  AgendaPublished: { labelKey: 'notif.type.agendaPublished', tone: 'info', icon: 'calendar' },
  MeetingScheduled: { labelKey: 'notif.type.meetingScheduled', tone: 'accent', icon: 'calendar' },
  MinutesReady: { labelKey: 'notif.type.minutesReady', tone: 'success', icon: 'doc' },
  DecisionApproved: { labelKey: 'notif.type.decisionApproved', tone: 'success', icon: 'decision' },
  ActionAssigned: { labelKey: 'notif.type.actionAssigned', tone: 'warn', icon: 'action' },
};

const DEFAULT_TYPE: NotifType = { labelKey: 'notif.type.default', tone: 'neutral', icon: 'bell' };

export function notifType(category: string): NotifType {
  return TYPES[category] ?? DEFAULT_TYPE;
}

// Artifact runtime key = the canonical record key the deep-link targets (TOP-/MTG-/DECN-YYYY-###).
// Match the key shape ANYWHERE in the path so nested links (/meetings/MTG-2026-018/minutes) resolve to
// the record key, not the sub-segment; fall back to the last segment for any other shape. Query/hash
// stripped. A null/empty link (e.g. a general notice) has no key chip.
export function notifKey(deepLink: string | null): string | null {
  if (!deepLink) return null;
  const path = deepLink.split('?')[0].split('#')[0];
  const canonical = path.match(/[A-Z]{2,6}-\d{4}-\d+/);
  if (canonical) return canonical[0];
  const seg = path.replace(/\/+$/, '').split('/').pop();
  return seg ? seg : null;
}
