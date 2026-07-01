/*
 * Create-action dialog (P8b2b, W13). Launched from a SOURCE artifact page (a decision, a meeting…) —
 * there is no standalone create, so the source (type/id/key) is pre-filled and shown locked. Matches the
 * `action` form in "ACMP Create Flows & Dialogs.dc.html" (Title · Linked-to · Owner · Due · Priority ·
 * Description) — a real design reference, so this is composed to it (not a no-reference composition).
 *
 * Owner is a member picker keyed to the Keycloak subject (Fork A: `Member.keycloakUserId`), sent as
 * OwnerUserId + the name snapshot. Title/description are entered once and MIRRORED to both LocalizedString
 * columns (en === ar), the locked FTS pattern. Priority's middle value is the `Normal` enum (labelled
 * "Medium" per the design). On success we route to the new /actions/:key.
 */
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Dialog } from '../../components/ui/Dialog';
import { Button } from '../../components/ui/Button';
import { Field, Input, Textarea } from '../../components/ui/Field';
import { Select } from '../../components/ui/Select';
import { DateField } from '../../components/ui/DateField';
import { Icon } from '../../components/icons';
import { ApiError } from '../../api/apiClient';
import { useMembers } from '../../api/members';
import { useCreateAction, type ActionPriority, type ActionSourceType, type LocalizedText } from '../../api/actions';

const PRIORITIES: ActionPriority[] = ['Low', 'Normal', 'High'];

export interface ActionSource {
  sourceType: ActionSourceType;
  sourceId: string;
  sourceKey: string;
}

interface Props {
  open: boolean;
  onClose: () => void;
  source: ActionSource;
}

export function CreateActionDialog({ open, onClose, source }: Props) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const create = useCreateAction();
  const { data: members } = useMembers();

  const [title, setTitle] = useState('');
  const [ownerId, setOwnerId] = useState('');
  const [dueDate, setDueDate] = useState('');
  const [priority, setPriority] = useState<ActionPriority>('Normal');
  const [description, setDescription] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);

  const loc = (v: string): LocalizedText => ({ en: v.trim(), ar: v.trim() });
  const ownerOptions = (members ?? [])
    .filter((m) => m.isActive)
    .map((m) => ({ value: m.keycloakUserId, label: `${m.fullName} — ${t(`roles.${m.role.toLowerCase()}`, m.role)}` }));

  function validate(): boolean {
    const e: Record<string, string> = {};
    if (!title.trim()) e.title = t('actions.create.err.title');
    if (!ownerId) e.owner = t('actions.create.err.owner');
    if (!dueDate) e.due = t('actions.create.err.due');
    setErrors(e);
    return Object.keys(e).length === 0;
  }

  async function onConfirm() {
    setSubmitError(null);
    if (!validate()) return;
    const owner = (members ?? []).find((m) => m.keycloakUserId === ownerId);
    try {
      const result = await create.mutateAsync({
        title: loc(title),
        description: description.trim() ? loc(description) : null,
        priority,
        ownerUserId: ownerId,
        ownerName: owner?.fullName ?? '',
        dueDate: dueDate ? `${dueDate}T00:00:00Z` : null,
        sourceType: source.sourceType,
        sourceId: source.sourceId,
        sourceKey: source.sourceKey,
        meetingKey: null,
      });
      onClose();
      navigate(`/actions/${result.key}`);
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.problem?.title ?? t('actions.create.error') : t('actions.create.error'));
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      icon={<Icon name="action" size={20} aria-hidden />}
      title={t('actions.create.title')}
      description={t('actions.create.subtitle')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="primary" loading={create.isPending} onClick={() => void onConfirm()}>
            {t('actions.create.confirm')}
          </Button>
        </>
      }
    >
      <div className="act-create-form">
        <Field label={t('actions.create.titleField')} required error={errors.title}>
          {(p) => (
            <Input {...p} value={title} maxLength={512} placeholder={t('actions.create.titlePh')} onChange={(e) => setTitle(e.target.value)} />
          )}
        </Field>

        <Field label={t('actions.create.linked')}>
          {(p) => (
            <div className="act-linked-locked" id={p.id}>
              <Icon name="deps" size={14} aria-hidden />
              <span className="act-linked-key">{source.sourceKey}</span>
              <span className="act-linked-note">{t('actions.create.linkedNote')}</span>
            </div>
          )}
        </Field>

        <Field label={t('actions.create.owner')} required error={errors.owner}>
          {(p) => (
            <Select
              id={p.id}
              options={ownerOptions}
              value={ownerId}
              onChange={setOwnerId}
              placeholder={t('actions.create.ownerPh')}
              ariaLabel={t('actions.create.owner')}
              aria-invalid={p['aria-invalid']}
              aria-describedby={p['aria-describedby']}
            />
          )}
        </Field>

        <Field label={t('actions.create.due')} required error={errors.due}>
          {(p) => (
            <DateField
              id={p.id}
              value={dueDate}
              onChange={setDueDate}
              placeholder={t('actions.create.duePh')}
              labels={{ previousMonth: t('actions.create.prevMonth'), nextMonth: t('actions.create.nextMonth') }}
              ariaLabel={t('actions.create.due')}
              aria-invalid={p['aria-invalid']}
              aria-describedby={p['aria-describedby']}
            />
          )}
        </Field>

        <Field label={t('actions.create.priority')}>
          {(p) => (
            <div className="act-seg" role="group" id={p.id} aria-label={t('actions.create.priority')}>
              {PRIORITIES.map((pr) => (
                <button
                  key={pr}
                  type="button"
                  className={`act-seg-btn ${priority === pr ? 'is-on' : ''}`}
                  aria-pressed={priority === pr}
                  onClick={() => setPriority(pr)}
                >
                  {t(`actions.priority.${pr}`)}
                </button>
              ))}
            </div>
          )}
        </Field>

        <Field label={t('actions.create.description')}>
          {(p) => (
            <Textarea {...p} rows={3} value={description} placeholder={t('actions.create.descriptionPh')} onChange={(e) => setDescription(e.target.value)} />
          )}
        </Field>

        {submitError && (
          <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{submitError}</p>
        )}
      </div>
    </Dialog>
  );
}
