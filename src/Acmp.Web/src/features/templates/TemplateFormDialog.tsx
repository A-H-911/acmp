/*
 * Template create/edit dialog (P15e, FR-119) — a no-reference composition (the Administration design has
 * no template form): Name (bilingual, entered once + mirrored en===ar), TargetType (a Select from the
 * backend enum — DISABLED on edit, since TargetType is immutable), and Body (a single Markdown string).
 * Create → useCreateTemplate (born Active); Edit → useEditTemplate (Name + Body only, Version++). Editing
 * a Deprecated template 409s — the ApiError title is surfaced inline.
 */
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Dialog } from '../../components/ui/Dialog';
import { Button } from '../../components/ui/Button';
import { Field, Input } from '../../components/ui/Field';
import { Select } from '../../components/ui/Select';
import { MarkdownEditor } from '../../components/ui/MarkdownEditor';
import { Icon } from '../../components/icons';
import { ApiError } from '../../api/apiClient';
import {
  useCreateTemplate, useEditTemplate, useTemplate,
  type TemplateSummary, type TemplateTargetType, type LocalizedText,
} from '../../api/templates';
import { TARGET_TYPES } from './templatesMeta';

interface Props {
  open: boolean;
  onClose: () => void;
  /** Present → edit that template (body loaded by key); absent → create. */
  template?: TemplateSummary;
}

export function TemplateFormDialog({ open, onClose, template }: Props) {
  const { t, i18n } = useTranslation();
  const create = useCreateTemplate();
  const edit = useEditTemplate();
  const isEdit = !!template;
  const pick = (l: LocalizedText) => (i18n.language === 'ar' ? l.ar : l.en);
  // Edit needs the Body (the register summary carries none) — fetch the detail by key.
  const detail = useTemplate(isEdit ? template.key : undefined);

  const [name, setName] = useState(template ? pick(template.name) : '');
  const [targetType, setTargetType] = useState<TemplateTargetType>(template?.targetType ?? 'Topic');
  const [body, setBody] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);

  // Seed the editable body once the detail arrives (edit only).
  useEffect(() => {
    if (detail.data) setBody(detail.data.body);
  }, [detail.data]);

  const loc = (v: string): LocalizedText => ({ en: v.trim(), ar: v.trim() });

  function validate(): boolean {
    const e: Record<string, string> = {};
    if (!name.trim()) e.name = t('templates.form.err.name');
    if (!body.trim()) e.body = t('templates.form.err.body');
    setErrors(e);
    return Object.keys(e).length === 0;
  }

  async function onConfirm() {
    setSubmitError(null);
    if (!validate()) return;
    try {
      if (isEdit) await edit.mutateAsync({ id: template.id, name: loc(name), body: body.trim() });
      else await create.mutateAsync({ name: loc(name), targetType, body: body.trim() });
      onClose();
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.problem?.title ?? t('templates.form.error') : t('templates.form.error'));
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      tone="accent"
      icon={<Icon name="template" size={20} aria-hidden />}
      title={isEdit ? t('templates.form.editTitle') : t('templates.form.createTitle')}
      description={isEdit ? t('templates.form.editSubtitle') : t('templates.form.createSubtitle')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="primary" loading={create.isPending || edit.isPending} onClick={() => void onConfirm()}>
            {isEdit ? t('templates.form.save') : t('templates.form.create')}
          </Button>
        </>
      }
    >
      <div className="tpl-form">
        <Field label={t('templates.form.name')} required error={errors.name}>
          {(p) => (
            <Input {...p} value={name} maxLength={256} placeholder={t('templates.form.namePh')} onChange={(e) => setName(e.target.value)} />
          )}
        </Field>

        <Field label={t('templates.form.targetType')} help={isEdit ? t('templates.form.targetTypeLocked') : undefined}>
          {(p) => (
            <Select
              id={p.id}
              options={TARGET_TYPES.map((v) => ({ value: v, label: t(`templates.targetType.${v}`) }))}
              value={targetType}
              onChange={(v) => setTargetType(v as TemplateTargetType)}
              disabled={isEdit}
              ariaLabel={t('templates.form.targetType')}
            />
          )}
        </Field>

        <Field label={t('templates.form.body')} required error={errors.body}>
          {(p) => (
            <MarkdownEditor id={p.id} aria-invalid={p['aria-invalid']} aria-describedby={p['aria-describedby']} value={body} rows={8} placeholder={t('templates.form.bodyPh')} onChange={setBody} />
          )}
        </Field>

        {submitError && (
          <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{submitError}</p>
        )}
      </div>
    </Dialog>
  );
}
