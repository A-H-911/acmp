/*
 * Decision presentation helpers. The design (isDecision) only ships ONE outcome visual
 * (Conditionally Approved → green), so mapping all 11 README §E outcomes to the shared
 * StatusChip tones is a no-reference composition — flagged in the progress log. Meaning is
 * carried by the localized label + dot (never colour alone, WCAG 1.4.1).
 */
import type { StatusTone } from '../../components/ui/StatusChip';
import type { DecisionOutcome, DecisionConditionStatus } from '../../api/decisions';

const OUTCOME_TONE: Record<DecisionOutcome, StatusTone> = {
  Approved: 'success',
  ConditionallyApproved: 'warn',
  Rejected: 'danger',
  MoreInfoRequired: 'info',
  FeedbackProvided: 'info',
  EnhancementsRequired: 'warn',
  DesignChangesRequired: 'warn',
  ResearchRequired: 'info',
  Deferred: 'neutral',
  Escalated: 'danger',
  Converted: 'scheduled',
};

export function outcomeTone(outcome: DecisionOutcome): StatusTone {
  return OUTCOME_TONE[outcome] ?? 'neutral';
}

const CONDITION_TONE: Record<DecisionConditionStatus, StatusTone> = {
  Open: 'warn',
  Met: 'success',
  Waived: 'neutral',
};

export function conditionTone(status: DecisionConditionStatus): StatusTone {
  return CONDITION_TONE[status] ?? 'neutral';
}

/** The 11 outcomes, ordered as README §E — for the supersede dialog's outcome picker. */
export const DECISION_OUTCOMES: DecisionOutcome[] = [
  'Approved',
  'ConditionallyApproved',
  'Rejected',
  'MoreInfoRequired',
  'FeedbackProvided',
  'EnhancementsRequired',
  'DesignChangesRequired',
  'ResearchRequired',
  'Deferred',
  'Escalated',
  'Converted',
];
