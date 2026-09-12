/*
 * Pure presentation mappers for Topic read models — shared by the backlog and
 * (later) the detail screen. Status tone maps the canonical TopicStatus wire
 * names (docs/domain/entity-lifecycles.md §1) onto the six StatusChip tones; the chip carries the
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

// Kanban presentation buckets — a backlog VIEW grouping over canonical TopicStatus (P5a decision; the
// status machine stays canonical). Order matches the design's columns.
export type KanbanBucket = 'triage' | 'accepted' | 'scheduled' | 'returned' | 'done';
export const KANBAN_BUCKETS: KanbanBucket[] = ['triage', 'accepted', 'scheduled', 'returned', 'done'];

const STATUS_BUCKET: Record<string, KanbanBucket> = {
  Draft: 'triage', Submitted: 'triage', Triage: 'triage', Reopened: 'triage',
  Accepted: 'accepted', Prepared: 'accepted',
  Scheduled: 'scheduled', InCommittee: 'scheduled',
  Deferred: 'returned', Rejected: 'returned',
  Decided: 'done', Closed: 'done', Converted: 'done',
};

export function bucketOf(status: string): KanbanBucket {
  return STATUS_BUCKET[status] ?? 'triage';
}

export const BUCKET_TONE: Record<KanbanBucket, StatusTone> = {
  triage: 'neutral', accepted: 'info', scheduled: 'scheduled', returned: 'warn', done: 'success',
};

/**
 * What a drag/move to a target bucket means in P5 (only the endpoints that exist):
 *  - 'accept'  : triage → accepted (needs an owner)
 *  - 'return'  : triage|accepted → returned (reject or defer, needs a reason)
 *  - 'illegal' : everything else (scheduling needs a Meeting → P6; no un-accept/decide endpoints)
 *  - 'none'    : same bucket
 */
export type MoveAction = 'accept' | 'return' | 'illegal' | 'none';

export function moveAction(from: KanbanBucket, to: KanbanBucket): MoveAction {
  if (from === to) return 'none';
  if (to === 'accepted') return from === 'triage' ? 'accept' : 'illegal';
  if (to === 'returned') return from === 'triage' || from === 'accepted' ? 'return' : 'illegal';
  return 'illegal';
}

/**
 * The four canonical TopicType wire names, in domain order (TopicType.cs). Labels are localized via
 * `topics.type.<name>` — no English label is stored here (guardrail 9).
 *
 * ⚠ Three older copies of this list exist as local consts (Backlog.tsx TYPE_VALUES,
 * ConvertToTopicDialog.tsx TOPIC_TYPES, SubmitTopic.tsx's icon-paired list). They are deliberately
 * left alone rather than churned inside a conversion PR; new code should import THIS one so the
 * duplication stops growing.
 */
export const TOPIC_TYPE_VALUES = [
  'ResearchDiscovery',
  'ArchitectureDecision',
  'EnhancementInnovation',
  'GovernanceStandardization',
] as const;

/**
 * WBS-40.2 (DEC-171, SC-062). The submitter channel, mirroring `TopicSource` in the Topics domain and
 * docs/domain/topic-taxonomy.md §B.3 — TEN values (External was missing from DW-076's list of nine).
 * Their labels live in `topics.source.*`, transcribed from src/i18n/glossary.json; check-i18n-terms
 * holds every rendering to the glossary, so a label is never authored here.
 */
export const TOPIC_SOURCE_VALUES = [
  'CommitteeMember',
  'StreamRequest',
  'UrgentOrgNeed',
  'OperationalIncident',
  'SecurityFinding',
  'Modernization',
  'InnovationInitiative',
  'CrossStreamProblem',
  'Regulatory',
  'External',
] as const;
