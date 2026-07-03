/*
 * Pure presentation mappers for Dependency read models (register + edge detail).
 * Relation colour + arrow follow the Lists&Registers `relMeta`; status tone follows its
 * `depStatus` (active→info, resolved→success), extended with Removed→neutral (the soft-delete
 * state the design doesn't render). Meaning is carried by the localized label + a coloured dot,
 * never colour alone (WCAG 1.4.1).
 */
import type { StatusTone } from '../../components/ui/StatusChip';
import type { DependencyKind, DependencyStatus } from '../../api/dependencies';

/** Relation cell colour (design `relMeta`.c): depends=info, blocked-by/blocks=danger, relates=neutral. */
export function kindColor(kind: DependencyKind): string {
  if (kind === 'BlockedBy' || kind === 'Blocks') return 'var(--st-danger-fg)';
  if (kind === 'DependsOn') return 'var(--st-info-fg)';
  return 'var(--st-neutral-fg)';
}

/**
 * Whether the relation reads "upstream" from the From end (design `relMeta`.up) — drives the
 * arrow glyph direction in the register's Relation cell (DependsOn/BlockedBy point up, others down).
 */
export function kindPointsUp(kind: DependencyKind): boolean {
  return kind === 'DependsOn' || kind === 'BlockedBy';
}

const STATUS_TONE: Record<DependencyStatus, StatusTone> = {
  Open: 'info',
  Resolved: 'success',
  Removed: 'neutral',
};

export function statusTone(status: DependencyStatus): StatusTone {
  return STATUS_TONE[status] ?? 'neutral';
}

/** DependencyKind values — the register's Relation filter options (all 4, i18n both locales). */
export const DEP_KINDS: DependencyKind[] = ['DependsOn', 'BlockedBy', 'Blocks', 'RelatesTo'];

/** DependencyStatus values a user can filter by — Removed is excluded by default (soft-deleted). */
export const DEP_STATUSES: DependencyStatus[] = ['Open', 'Resolved'];
