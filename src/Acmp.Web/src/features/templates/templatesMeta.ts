/*
 * Pure presentation mappers for Knowledge templates (register + form). Status tone maps the wire enum
 * names onto shared StatusChip tones; meaning is carried by the localized label + a coloured dot, never
 * colour alone (WCAG 1.4.1). The Administration design's sample statuses (active/draft/archived) are
 * illustrative — the backend truth is Active/Deprecated (OQ-051), so those two are what we map.
 */
import type { StatusTone } from '../../components/ui/StatusChip';
import { TEMPLATE_TARGET_TYPES, type TemplateStatus, type TemplateTargetType } from '../../api/templates';

const STATUS_TONE: Record<TemplateStatus, StatusTone> = {
  Active: 'success',
  Deprecated: 'neutral',
};

export function statusTone(status: TemplateStatus): StatusTone {
  return STATUS_TONE[status] ?? 'neutral';
}

/** TemplateStatus values — the register's Status filter options (both locales). */
export const TEMPLATE_STATUSES: TemplateStatus[] = ['Active', 'Deprecated'];

/** Target-type filter + form options (backend enum, OQ-051). Re-exported from the api layer. */
export const TARGET_TYPES: TemplateTargetType[] = TEMPLATE_TARGET_TYPES;
