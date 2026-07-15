/*
 * Search hit status localization (P15g, m22). Each artifact type carries its own status enum, so a search
 * group's type selects which enum namespace to localize its hits' statuses through. Any status value (or
 * type) without an i18n key falls back to the raw enum name — completeness is bounded by each provider's
 * status vocabulary, not silently claimed. Decisions has no lifecycle-status i18n block, so its statuses
 * fall back to the raw name (documented).
 */
import type { TFunction } from 'i18next';

const STATUS_NS: Record<string, string> = {
  Topics: 'topics.status',
  ADRs: 'adrs.status',
  MoMs: 'meetings.mom.status',
  Documents: 'wiki.status',
};

export function searchStatusLabel(type: string, status: string, t: TFunction): string {
  const ns = STATUS_NS[type];
  return ns ? t(`${ns}.${status}`, { defaultValue: status }) : status;
}
