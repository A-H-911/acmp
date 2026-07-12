/*
 * Pure presentation mappers for the audit register (PR4). Meaning is carried by the
 * localized label + a coloured dot, never colour alone (WCAG 1.4.1).
 *
 * Design↔data reconciliation (visual SoT = "ACMP Lists & Registers.dc.html" audit trail;
 * data SoT = the AuditEvent store): the mock uses fabricated narrative sentences + human
 * artifact KEYS. Our real rows carry structured fields — a dotted Action verb, a CLR
 * subjectType + GUID subjectId, and an Outcome — with no pre-composed sentence and no human
 * key (resolving subjectId → "VOTE-2026-001" needs a cross-module key lookup, deferred). So
 * the register shows the structured facts we store; tone is derived from the Action verb,
 * with a Denied/Failure outcome overriding to danger.
 */
import type { StatusTone } from '../../components/ui/StatusChip';

// Verb keyword → tone, mirroring the design's auditVerbMeta semantics (create=success,
// update=info, vote=scheduled, lock=neutral, supersede=warn, role/denial=danger). Matched
// against the lowercased Action string; first hit wins, neutral is the fallback.
const VERB_TONE: [RegExp, StatusTone][] = [
  [/denied|reject|revok|remov|delete|role|unauthenticated|forbidden|noroleclaim/, 'danger'],
  [/supersed|deprecat|block|escalat/, 'warn'],
  [/cast|vote|ballot/, 'scheduled'],
  [/clos|lock|seal|cancel/, 'neutral'],
  [/creat|add|submit|rais|log|provision|issue|approv|ratif|publish|activat|verif|complet|open|prepar|grant/, 'success'],
  [/updat|chang|edit|prioritiz|record|start|progress|schedul|link|attach|read/, 'info'],
];

export function auditTone(action: string, outcome: string | null): StatusTone {
  if (outcome === 'Denied' || outcome === 'Failure') return 'danger';
  const a = action.toLowerCase();
  for (const [re, tone] of VERB_TONE) if (re.test(a)) return tone;
  return 'neutral';
}

// Outcome tone for the Detail cell — a plain Success is muted; a denial/failure reads urgent.
export function outcomeTone(outcome: string | null): StatusTone {
  if (outcome === 'Denied' || outcome === 'Failure') return 'danger';
  return 'neutral';
}

/** Two-letter avatar fallback from an actor subject ("kc-chair" → "KC"); null/system → ''. */
export function actorInitials(actor: string | null): string {
  if (!actor) return '';
  const parts = actor.split(/[-_.\s]+/).filter(Boolean);
  const two = (parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? actor[1] ?? '');
  return two.toUpperCase() || '?';
}

/**
 * Gregorian timestamp "24 Jun 2026 · 14:22:07" (INV-009: Gregorian dates, Latin digits) in
 * both locales — Arabic month names, Latin numerals, 24-hour clock. Rendered dir="ltr".
 */
export function formatTimestamp(iso: string, lang: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  const opts: Intl.DateTimeFormatOptions = { calendar: 'gregory', numberingSystem: 'latn' };
  const date = new Intl.DateTimeFormat(lang === 'ar' ? 'ar' : 'en-GB', { ...opts, day: '2-digit', month: 'short', year: 'numeric' }).format(d);
  const time = new Intl.DateTimeFormat('en-GB', { ...opts, hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false }).format(d);
  return `${date} · ${time}`;
}

/**
 * The governed CLR aggregate names an audit row's SubjectType can carry — the Artifact-type
 * filter's options. These are the exact strings GET /api/audit?entityType= matches (they equal
 * the capture interceptor's ClrType.Name; NOT the ArtifactType enum). Labels are localized.
 */
export const AUDIT_ENTITY_TYPES = [
  'Vote', 'Decision', 'Topic', 'Meeting', 'MinutesOfMeeting', 'Risk', 'ActionItem',
  'Adr', 'Invariant', 'Relationship', 'Dependency', 'CommitteeMember', 'Delegation',
] as const;
