/*
 * "Start from a template" affordance (P15h; FR-120). Offers the Active templates for a given artifact
 * targetType at creation time; applying one pre-fills the caller's content field with the template's
 * Markdown Body (editable afterwards). Read-only reuse of the P15d/P15e seam (GET /knowledge/templates
 * ?targetType + GET /{key}) — no new backend.
 *
 *  - Empty (no Active templates for this type) → renders nothing (INV-014 empty-state; picker never
 *    appears broken).
 *  - Overwrite guard: Apply is disabled while the target field already holds content, so a restored
 *    SubmitTopic draft is never silently clobbered — FR-120 is "pre-fill", not "replace".
 *    // ponytail: clear-to-switch is the ceiling; add confirm-to-replace only if switching templates
 *    // mid-edit turns out to be common.
 *  - The affordance has no matching .dc.html (absent from the create-flow designs) → composed from the
 *    shared Select/Button (design-update-owed, guardrail #14).
 *
 * Homed in features/knowledge (it reads the templates API) and imported cross-feature by the four create
 * surfaces — same shape as the shared TraceabilityPanel. No feature-boundary lint in this repo.
 */
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Select } from '../../components/ui/Select';
import { Button } from '../../components/ui/Button';
import { useTemplate, useTemplates, type TemplateTargetType } from '../../api/templates';
import './templates.css';

interface Props {
  targetType: TemplateTargetType;
  /** Receives the selected template's Markdown Body; the caller writes it to its content field. */
  onApply: (body: string) => void;
  /** When the target field already holds content, Apply is disabled to protect the existing text. */
  hasContent?: boolean;
}

export function TemplatePicker({ targetType, onApply, hasContent = false }: Props) {
  const { t, i18n } = useTranslation();
  const { data } = useTemplates({ targetType, statuses: ['Active'] });
  const [selected, setSelected] = useState('');
  const detail = useTemplate(selected || undefined);

  const templates = data?.items ?? [];
  if (templates.length === 0) return null;

  const pick = (l: { en: string; ar: string }) => (i18n.language === 'ar' ? l.ar : l.en);
  const options = templates.map((tpl) => ({ value: tpl.key, label: `${tpl.key} — ${pick(tpl.name)}` }));
  const canApply = !!selected && !detail.isFetching && !hasContent;

  return (
    <div className="tpl-picker">
      <label className="tpl-picker-label" htmlFor="tpl-picker-select">
        {t('templates.picker.label')}
      </label>
      <div className="tpl-picker-row">
        <Select
          id="tpl-picker-select"
          options={options}
          value={selected}
          onChange={setSelected}
          placeholder={t('templates.picker.placeholder')}
          ariaLabel={t('templates.picker.label')}
        />
        <Button
          variant="secondary"
          disabled={!canApply}
          onClick={() => detail.data && onApply(detail.data.body)}
        >
          {t('templates.picker.apply')}
        </Button>
      </div>
      {hasContent && <p className="tpl-picker-hint">{t('templates.picker.replaceHint')}</p>}
    </div>
  );
}
