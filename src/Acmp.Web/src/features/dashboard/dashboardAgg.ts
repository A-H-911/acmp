/*
 * Pure dashboard aggregation (P12-PR2). The role dashboards (AC-064/065/066) are
 * composed CLIENT-SIDE from existing REST reads — no server read-model layer, no
 * columnstore (ADR-0022, right-sized for ≤20 users). This module is the AC-carrying
 * logic: bucketing, filtering, and thresholds live here and are unit-tested directly
 * against real-shaped data, so a wrong count can't hide behind a rendered heading.
 * The components just render what these functions return.
 */
import type { TopicSummary } from '../../api/topics';
import type { ActionSummary } from '../../api/actions';
import type { MeetingSummary } from '../../api/meetings';
import { bucketOf, KANBAN_BUCKETS, BUCKET_TONE, type KanbanBucket } from '../topics/topicMeta';
import type { StatusTone } from '../../components/ui/StatusChip';

/*
 * AC-065 defines action escalation as "overdue actions beyond the escalation threshold";
 * AC-066's "escalated actions" reuses that same definition (the Action aggregate has no
 * Escalated status — Open/InProgress/Blocked/Completed/Verified/Cancelled). One shared
 * threshold feeds both variants.
 * ponytail: fixed committee-scale threshold, not a per-committee config — a settings row
 * for a single ≤20-user committee would be overkill (ADR-0022). Promote to config only if
 * a second committee ever needs a different SLA.
 */
export const ESCALATION_THRESHOLD_DAYS = 3;

const DAY_MS = 86_400_000;

export interface BacklogSegment {
  bucket: KanbanBucket;
  count: number;
  tone: StatusTone;
}

/** AC-064: backlog count by status, folded into the canonical kanban buckets (reuses the
 *  already-tested bucketOf/BUCKET_TONE so the dashboard and board never disagree). */
export function backlogByBucket(topics: readonly TopicSummary[]): { segments: BacklogSegment[]; total: number } {
  const counts = Object.fromEntries(KANBAN_BUCKETS.map((b) => [b, 0])) as Record<KanbanBucket, number>;
  for (const t of topics) counts[bucketOf(t.status)]++;
  return {
    segments: KANBAN_BUCKETS.map((b) => ({ bucket: b, count: counts[b], tone: BUCKET_TONE[b] })),
    total: topics.length,
  };
}

const URGENCY_ORDER = ['Critical', 'Urgent', 'Normal'] as const;

/** AC-064: the second half of "by status AND urgency" — the design shows only status, so this
 *  breakdown is an AC-required addition (flagged in the progress log). Known urgencies first
 *  (most severe → least), any unknown value appended so nothing is silently dropped. */
export function backlogByUrgency(topics: readonly TopicSummary[]): { urgency: string; count: number }[] {
  const counts = new Map<string, number>();
  for (const t of topics) counts.set(t.urgency, (counts.get(t.urgency) ?? 0) + 1);
  const known = URGENCY_ORDER.filter((u) => counts.has(u)).map((u) => ({ urgency: u as string, count: counts.get(u)! }));
  const extra = [...counts.entries()]
    .filter(([u]) => !URGENCY_ORDER.includes(u as (typeof URGENCY_ORDER)[number]))
    .map(([urgency, count]) => ({ urgency, count }));
  return [...known, ...extra];
}

/** AC-064: the next scheduled meeting = soonest upcoming (start ≥ now) that isn't done/cancelled.
 *  Never assume list order; returns null when nothing is upcoming (card renders an empty note). */
export function nextScheduledMeeting(meetings: readonly MeetingSummary[], now: Date): MeetingSummary | null {
  const upcoming = meetings
    .filter((m) => m.status !== 'Completed' && m.status !== 'Cancelled' && new Date(m.scheduledStart).getTime() >= now.getTime())
    .sort((a, b) => new Date(a.scheduledStart).getTime() - new Date(b.scheduledStart).getTime());
  return upcoming[0] ?? null;
}

export interface ActionStatusCounts {
  open: number;
  inProgress: number;
  blocked: number;
  overdue: number;
}

/** AC-064: open action counts by status (Open/InProgress/Blocked) plus the overdue total
 *  (overdue is orthogonal — an InProgress action can be overdue). */
export function actionStatusCounts(actions: readonly ActionSummary[]): ActionStatusCounts {
  let open = 0;
  let inProgress = 0;
  let blocked = 0;
  let overdue = 0;
  for (const a of actions) {
    if (a.status === 'Open') open++;
    else if (a.status === 'InProgress') inProgress++;
    else if (a.status === 'Blocked') blocked++;
    if (a.isOverdue) overdue++;
  }
  return { open, inProgress, blocked, overdue };
}

/** Whole days an action is past due (0 when not overdue or undated). `isOverdue` is the server's
 *  truth for "past due"; the day count is derived client-side for the escalation threshold. */
export function daysOverdue(dueDate: string | null, isOverdue: boolean, now: Date): number {
  if (!dueDate || !isOverdue) return 0;
  const diff = now.getTime() - new Date(dueDate).getTime();
  return diff <= 0 ? 0 : Math.floor(diff / DAY_MS);
}

/** AC-065 count + AC-066 list: actions overdue beyond the escalation threshold, newest-overdue first. */
export function overdueBeyondThreshold(
  actions: readonly ActionSummary[],
  now: Date,
  threshold = ESCALATION_THRESHOLD_DAYS,
): ActionSummary[] {
  return actions
    .map((a) => ({ a, d: daysOverdue(a.dueDate, a.isOverdue, now) }))
    .filter((x) => x.d > threshold)
    .sort((x, y) => y.d - x.d)
    .map((x) => x.a);
}

/** AC-066: topics deferred ≥2 times (counter spans reactivations; a match may now be in any status,
 *  so the caller must fetch with includeClosed to not miss a deferred-then-rejected topic). */
export function deferredAtLeastTwice(topics: readonly TopicSummary[]): TopicSummary[] {
  return topics.filter((t) => t.timesDeferred >= 2).sort((a, b) => b.timesDeferred - a.timesDeferred);
}

/** AC-065: topics past their urgency SLA, worst-aged first. */
export function slaBreached(topics: readonly TopicSummary[]): TopicSummary[] {
  return topics.filter((t) => t.slaBreached).sort((a, b) => b.ageDays - a.ageDays);
}
