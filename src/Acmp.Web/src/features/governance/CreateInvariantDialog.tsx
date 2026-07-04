/*
 * Create-invariant dialog (P11d, W18). Launched from the Invariant register's "New invariant" button.
 * Matches the `invariant` form in "ACMP Create Flows & Dialogs.dc.html" (Category · Scope · Statement ·
 * Rationale · Owner) — a real design reference, so this is composed to it.
 *
 * Design↔behaviour reconciliations (flagged; design to be updated, guardrail #14):
 *  - The design has NO Status field and no Draft/Proposed choice; an Invariant is born Draft. So there is
 *    no create→propose chain (unlike CreateAdrDialog): we create, then route to the detail. Proposing a
 *    Draft is a later governance-lifecycle slice with no UI yet.
 *  - The design marks Rationale and Owner as optional, but the backend CreateInvariant validator requires
 *    both (Rationale non-empty; OwnerUserId non-empty). So both are REQUIRED here (Option A, mirroring the
 *    Risk dialog's required linked-topic).
 *  - ExceptionsPolicy is optional in the model but NOT in the design's create form, so it is not collected
 *    here (sent null); it is shown on the detail when a later editing slice sets it.
 *
 * Owner is a member picker keyed to the Keycloak subject, sent as OwnerUserId + a name snapshot. Statement
 * and Rationale are entered once and MIRRORED to both LocalizedString columns (en === ar), the locked FTS
 * pattern. On success we route to the new /invariants/:key.
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
import { useMembers } from '../../api/members';
import { useCreateInvariant, type InvariantCategory, type InvariantScope, type LocalizedText } from '../../api/invariants';
import { INVARIANT_CATEGORIES, INVARIANT_SCOPES } from './invariantMeta';

interface Props {
  open: boolean;
  onClose: () => void;
}

export function CreateInvariantDialog({ open, onClose }: Props) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const create = useCreateInvariant();
  const { data: members } = useMembers();

  const [category, setCategory] = useState<InvariantCategory>('Security');
  const [scope, setScope] = useState<InvariantScope>('Platform');
  const [statement, setStatement] = useState('');
  const [rationale, setRationale] = useState('');
  const [ownerId, setOwnerId] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);

  const loc = (v: string): LocalizedText => ({ en: v.trim(), ar: v.trim() });
  const categoryOptions = INVARIANT_CATEGORIES.map((v) => ({ value: v, label: t(`invariants.category.${v}`) }));
  const scopeOptions = INVARIANT_SCOPES.map((v) => ({ value: v, label: t(`invariants.scope.${v}`) }));
  const ownerOptions = (members ?? [])
    .filter((m) => m.isActive)
    .map((m) => ({ value: m.keycloakUserId, label: `${m.fullName} — ${t(`roles.${m.role.toLowerCase()}`, m.role)}` }));

  function validate(): boolean {
    const e: Record<string, string> = {};
    if (!statement.trim()) e.statement = t('invariants.create.err.statement');
    if (!rationale.trim()) e.rationale = t('invariants.create.err.rationale');
    if (!ownerId) e.owner = t('invariants.create.err.owner');
    setErrors(e);
    return Object.keys(e).length === 0;
  }

  async function onConfirm() {
    setSubmitError(null);
    if (!validate()) return;
    const owner = (members ?? []).find((m) => m.keycloakUserId === ownerId);
    try {
      const result = await create.mutateAsync({
        category,
        scope,
        statement: loc(statement),
        rationale: loc(rationale),
        exceptionsPolicy: null,
        ownerUserId: ownerId,
        ownerName: owner?.fullName ?? '',
      });
      onClose();
      navigate(`/invariants/${result.key}`);
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.problem?.title ?? t('invariants.create.error') : t('invariants.create.error'));
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      tone="accent"
      icon={<Icon name="shieldUser" size={20} aria-hidden />}
      title={t('invariants.create.title')}
      description={t('invariants.create.subtitle')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="primary" loading={create.isPending} onClick={() => void onConfirm()}>
            {t('invariants.create.confirm')}
          </Button>
        </>
      }
    >
      <div className="adr-create-form">
        <div className="adr-create-row">
          <Field label={t('invariants.create.category')} required>
            {(p) => (
              <Select id={p.id} options={categoryOptions} value={category} onChange={(v) => setCategory(v as InvariantCategory)} ariaLabel={t('invariants.create.category')} />
            )}
          </Field>
          <Field label={t('invariants.create.scope')} required>
            {(p) => (
              <Select id={p.id} options={scopeOptions} value={scope} onChange={(v) => setScope(v as InvariantScope)} ariaLabel={t('invariants.create.scope')} />
            )}
          </Field>
        </div>

        <Field label={t('invariants.create.statement')} required error={errors.statement}>
          {(p) => (
            <Textarea {...p} rows={3} value={statement} placeholder={t('invariants.create.statementPh')} onChange={(e) => setStatement(e.target.value)} />
          )}
        </Field>

        <Field label={t('invariants.create.rationale')} required error={errors.rationale}>
          {(p) => (
            <Textarea {...p} rows={2} value={rationale} placeholder={t('invariants.create.rationalePh')} onChange={(e) => setRationale(e.target.value)} />
          )}
        </Field>

        <Field label={t('invariants.create.owner')} required error={errors.owner}>
          {(p) => (
            <Select
              id={p.id}
              options={ownerOptions}
              value={ownerId}
              onChange={setOwnerId}
              placeholder={t('invariants.create.ownerPh')}
              ariaLabel={t('invariants.create.owner')}
              aria-invalid={p['aria-invalid']}
              aria-describedby={p['aria-describedby']}
            />
          )}
        </Field>

        {submitError && (
          <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{submitError}</p>
        )}
      </div>
    </Dialog>
  );
}
