/*
 * Create-dependency dialog (P10e) — composed to the `dependency` form in
 * "ACMP Create Flows & Dialogs.dc.html" (From entity · Relationship · To entity · Notes).
 *
 * Two launch modes:
 *  - Contextual (from an artifact's panel): `from` is pre-seeded and locked; only the To end is
 *    picked. This is the primary path and works for ANY DependencyEndpointType From (the From
 *    snapshot is already in hand — no register lookup needed).
 *  - Register ("New dependency" with both ends blank): From is also picked. Only Topic/Action are
 *    pickable this slice (no FE list source for Decision/System) — flagged (guardrail #14).
 *
 * Design↔behaviour reconciliations (flagged; design to be updated):
 *  - From/To endpoint types limited to Topic/Action when picked (no Decision/System list source yet).
 *  - Self-loop guarded client-side (From ≠ To), mirroring the backend CreateDependency validator.
 *  - The shared confirm dialog's immutability warning is dropped — dependencies are mutable
 *    (resolve/remove), so the confirm copy is the neutral "Create dependency".
 */
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Dialog } from '../../components/ui/Dialog';
import { Button } from '../../components/ui/Button';
import { Field, Textarea } from '../../components/ui/Field';
import { Select } from '../../components/ui/Select';
import { Icon } from '../../components/icons';
import { ApiError } from '../../api/apiClient';
import { useCreateDependency, type DependencyEndpointType, type DependencyKind } from '../../api/dependencies';
import { ArtifactPicker, type PickableType, type PickedArtifact } from '../traceability/ArtifactPicker';
import { DEP_KINDS } from './depMeta';
import '../traceability/traceability.css';

/** The artifact this dependency is created FROM, when launched contextually. */
export interface DependencyFrom {
  type: DependencyEndpointType;
  id: string;
  key: string;
  title: string;
}

interface Props {
  open: boolean;
  onClose: () => void;
  /** Pre-seeded + locked From end (contextual create); omit for the register's blank-both-ends create. */
  from?: DependencyFrom;
}

const PICKABLE: PickableType[] = ['Topic', 'Action'];

export function CreateDependencyDialog({ open, onClose, from }: Props) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const create = useCreateDependency();

  const [fromPick, setFromPick] = useState<PickedArtifact | null>(null);
  const [toPick, setToPick] = useState<PickedArtifact | null>(null);
  const [kind, setKind] = useState<DependencyKind>('DependsOn');
  const [note, setNote] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);

  // Resolved From: the locked contextual artifact, else the picked one. The picker only offers
  // Topic/Action (PICKABLE), both of which are valid DependencyEndpointType names, so the narrowing
  // cast is sound.
  const fromEnd: DependencyFrom | null =
    from ?? (fromPick ? { type: fromPick.type as DependencyEndpointType, id: fromPick.id, key: fromPick.key, title: fromPick.title } : null);
  const kindOptions = DEP_KINDS.map((k) => ({ value: k, label: t(`deps.kind.${k}`) }));

  function validate(): boolean {
    const e: Record<string, string> = {};
    if (!fromEnd) e.from = t('deps.create.err.from');
    if (!toPick) e.to = t('deps.create.err.to');
    if (fromEnd && toPick && fromEnd.id === toPick.id) e.to = t('deps.create.err.selfLoop');
    setErrors(e);
    return Object.keys(e).length === 0;
  }

  async function onConfirm() {
    setSubmitError(null);
    if (!validate() || !fromEnd || !toPick) return;
    try {
      const { key } = await create.mutateAsync({
        fromType: fromEnd.type,
        fromId: fromEnd.id,
        fromKey: fromEnd.key,
        fromTitle: fromEnd.title,
        toType: toPick.type as DependencyEndpointType,
        toId: toPick.id,
        toKey: toPick.key,
        toTitle: toPick.title,
        kind,
        note: note.trim() ? note.trim() : null,
      });
      reset();
      onClose();
      navigate(`/dependencies/${key}`);
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.problem?.title ?? t('deps.create.error') : t('deps.create.error'));
    }
  }

  function reset() {
    setFromPick(null);
    setToPick(null);
    setKind('DependsOn');
    setNote('');
    setErrors({});
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      icon={<Icon name="deps" size={20} aria-hidden />}
      title={t('deps.create.title')}
      description={t('deps.create.subtitle')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="primary" loading={create.isPending} onClick={() => void onConfirm()}>
            {t('deps.create.confirm')}
          </Button>
        </>
      }
    >
      <div className="dep-create-form">
        {from ? (
          <Field label={t('deps.create.from')} required>
            {() => (
              <div className="dep-locked" aria-label={`${from.key} ${from.title}`}>
                <span className="dep-locked-key">{from.key}</span>
                <span className="dep-locked-title">{from.title}</span>
              </div>
            )}
          </Field>
        ) : (
          <ArtifactPicker
            label={t('deps.create.from')}
            pickableTypes={PICKABLE}
            value={fromPick}
            onChange={setFromPick}
            error={errors.from}
          />
        )}

        <Field label={t('deps.create.relationship')} required>
          {(p) => (
            <Select id={p.id} options={kindOptions} value={kind} onChange={(v) => setKind(v as DependencyKind)} ariaLabel={t('deps.create.relationship')} />
          )}
        </Field>

        <ArtifactPicker
          label={t('deps.create.to')}
          pickableTypes={PICKABLE}
          value={toPick}
          onChange={setToPick}
          error={errors.to}
        />

        <Field label={t('deps.create.notes')}>
          {(p) => (
            <Textarea {...p} rows={3} value={note} placeholder={t('deps.create.notesPh')} onChange={(e) => setNote(e.target.value)} />
          )}
        </Field>

        {submitError && (
          <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{submitError}</p>
        )}
      </div>
    </Dialog>
  );
}
