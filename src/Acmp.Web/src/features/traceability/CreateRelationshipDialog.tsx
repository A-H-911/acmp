/*
 * Create-relationship (typed traceability edge) dialog (P10e, AC-063).
 *
 * NO-REFERENCE COMPOSITION (guardrail #14): "ACMP Create Flows & Dialogs.dc.html" has a `dependency`
 * form but NO generic traceability-edge form, so this is composed from the design system + the
 * dependency form's shape (Source · Relationship · Target · Notes). Flagged for the design update.
 *
 * Launched contextually from an artifact panel: `source` is pre-seeded + locked (works for ANY
 * ArtifactType — the snapshot is in hand). The Target is picked; only Topic/Action/Risk are pickable
 * this slice (the only ArtifactTypes with a FE list source). No key is returned (edges have no
 * display key), so on success the dialog just closes and the panel re-renders (cache invalidation).
 */
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Dialog } from '../../components/ui/Dialog';
import { Button } from '../../components/ui/Button';
import { Field, Textarea } from '../../components/ui/Field';
import { Select } from '../../components/ui/Select';
import { Icon } from '../../components/icons';
import { ApiError } from '../../api/apiClient';
import { useCreateRelationship, RELATIONSHIP_TYPES, type ArtifactType, type RelationshipType } from '../../api/traceability';
import { ArtifactPicker, type PickableType, type PickedArtifact } from './ArtifactPicker';
import './traceability.css';

/** The artifact this edge is created FROM (the source end), pre-seeded from the launching panel. */
export interface RelationshipSource {
  type: ArtifactType;
  id: string;
  key: string;
  title: string;
}

interface Props {
  open: boolean;
  onClose: () => void;
  source: RelationshipSource;
}

const PICKABLE: PickableType[] = ['Topic', 'Action', 'Risk'];

export function CreateRelationshipDialog({ open, onClose, source }: Props) {
  const { t } = useTranslation();
  const create = useCreateRelationship();

  const [target, setTarget] = useState<PickedArtifact | null>(null);
  const [relType, setRelType] = useState<RelationshipType>('References');
  const [notes, setNotes] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);

  const relOptions = RELATIONSHIP_TYPES.map((r) => ({ value: r, label: t(`trace.rel.${r}`) }));

  function validate(): boolean {
    const e: Record<string, string> = {};
    if (!target) e.target = t('trace.create.err.target');
    if (target && target.id === source.id) e.target = t('trace.create.err.selfLoop');
    setErrors(e);
    return Object.keys(e).length === 0;
  }

  async function onConfirm() {
    setSubmitError(null);
    if (!validate() || !target) return;
    try {
      await create.mutateAsync({
        sourceType: source.type,
        sourceId: source.id,
        sourceKey: source.key,
        sourceTitle: source.title,
        targetType: target.type,
        targetId: target.id,
        targetKey: target.key,
        targetTitle: target.title,
        relType,
        notes: notes.trim() ? notes.trim() : null,
      });
      setTarget(null);
      setNotes('');
      setErrors({});
      onClose();
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.problem?.title ?? t('trace.create.error') : t('trace.create.error'));
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      tone="accent"
      icon={<Icon name="deps" size={20} aria-hidden />}
      title={t('trace.create.title')}
      description={t('trace.create.subtitle')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="primary" loading={create.isPending} onClick={() => void onConfirm()}>
            {t('trace.create.confirm')}
          </Button>
        </>
      }
    >
      <div className="dep-create-form">
        <Field label={t('trace.create.source')} required>
          {() => (
            <div className="dep-locked" aria-label={`${source.key} ${source.title}`}>
              <span className="dep-locked-key">{source.key}</span>
              <span className="dep-locked-title">{source.title}</span>
            </div>
          )}
        </Field>

        <Field label={t('trace.create.relationship')} required>
          {(p) => (
            <Select id={p.id} options={relOptions} value={relType} onChange={(v) => setRelType(v as RelationshipType)} ariaLabel={t('trace.create.relationship')} />
          )}
        </Field>

        <ArtifactPicker
          label={t('trace.create.target')}
          pickableTypes={PICKABLE}
          value={target}
          onChange={setTarget}
          error={errors.target}
        />

        <Field label={t('trace.create.notes')}>
          {(p) => (
            <Textarea {...p} rows={3} value={notes} placeholder={t('trace.create.notesPh')} onChange={(e) => setNotes(e.target.value)} />
          )}
        </Field>

        {submitError && (
          <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{submitError}</p>
        )}
      </div>
    </Dialog>
  );
}
