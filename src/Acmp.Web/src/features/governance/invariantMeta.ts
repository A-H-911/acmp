/*
 * Invariant presentation logic (P11d) — the pure, testable pieces shared by the register and the detail:
 * the status → StatusChip tone map, the category → dot-colour map, and the enum value lists that drive the
 * filter chips and the create-form selects. Kept out of the components so each map carries unit coverage.
 *
 * There is no markdown export here (unlike ADRs): an Invariant is a standing rule, not a MADR document.
 */
import type { StatusTone } from '../../components/ui/StatusChip';
import type { InvariantStatus, InvariantCategory, InvariantScope } from '../../api/invariants';

/** The five canonical Invariant statuses, register-filter order (lifecycle order). */
export const INVARIANT_STATUSES: readonly InvariantStatus[] = ['Draft', 'Proposed', 'Active', 'Retired', 'Superseded'];

/** The Category enum (OQ-036 default set — still open). Order = create-form select order. */
export const INVARIANT_CATEGORIES: readonly InvariantCategory[] = [
  'Security', 'Performance', 'Data', 'Interoperability', 'Compliance', 'Other',
];

/** The Scope enum, narrowest → widest. */
export const INVARIANT_SCOPES: readonly InvariantScope[] = ['SingleStream', 'MultiStream', 'Platform', 'OrgWide'];

/**
 * Status → chip tone. Draft is neutral WIP; Proposed is in-flight (info); Active is the live rule
 * (success); Retired is stood-down (neutral); Superseded is replaced-but-kept (warn). Mirrors the design's
 * invStatus map (active/draft/retired), extended to the two states the design's preview omits.
 */
export function statusTone(status: InvariantStatus): StatusTone {
  switch (status) {
    case 'Draft':
      return 'neutral';
    case 'Proposed':
      return 'info';
    case 'Active':
      return 'success';
    case 'Retired':
      return 'neutral';
    case 'Superseded':
      return 'warn';
  }
}

/**
 * Category → dot colour (a CSS custom property). Mirrors the design's catMeta colouring; the categories the
 * design didn't enumerate (Performance / Compliance / Other) take the nearest semantic token.
 */
export function categoryDot(category: InvariantCategory): string {
  switch (category) {
    case 'Security':
      return 'var(--st-danger-dot)';
    case 'Performance':
      return 'var(--st-warn-dot)';
    case 'Data':
      return 'var(--st-info-dot)';
    case 'Interoperability':
      return 'var(--st-sched-dot)';
    case 'Compliance':
      return 'var(--st-success-dot)';
    case 'Other':
      return 'var(--st-neutral-dot)';
  }
}
