/*
 * Create-ADR dialog (P11b, W17). Launched from the ADR register's "New ADR" button. Matches the `adr`
 * form in "ACMP Create Flows & Dialogs.dc.html" (Title · Status · Context · Decision · Consequences).
 *
 * Design↔behaviour reconciliations (flagged; design owes an update, guardrail #14):
 *  - The design's Status select defaults to "Proposed"; we keep a Draft/Proposed selector and map it to
 *    the lifecycle: every ADR is CREATED in Draft, and choosing "Proposed" chains create → POST /propose
 *    (both routes are Adr.Create, so the author is permitted). This avoids a register of inert Drafts.
 *  - The design's "Linked decision" field is the Decision→ADR promotion path (SourceDecisionId); that is
 *    the P11e slice, so it is not collected here — a standalone ADR has no source decision.
 *  - The design collects one "Consequences" area; we split it into optional Positive / Negative to match
 *    the read model (two LocalizedString columns) and the detail's positive/negative split.
 *  - Decision drivers and considered-options are optional in the model but omitted from the create form
 *    (there is no draft-edit UI this slice), so they stay null until a later editing slice adds them.
 *
 * Title / Context / Decision are entered once and MIRRORED to both LocalizedString columns (en === ar),
 * the locked FTS pattern. On success we route to the new /adrs/:key.
 */
import { useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Dialog } from '../../components/ui/Dialog';
import { Button } from '../../components/ui/Button';
import { Field, Input, Textarea } from '../../components/ui/Field';
import { Select } from '../../components/ui/Select';
import { Icon } from '../../components/icons';
import { ApiError } from '../../api/apiClient';
import { useCreateAdr, useProposeAdr, type AdrSummary, type LocalizedText } from '../../api/adrs';
import { TemplatePicker } from '../templates/TemplatePicker';

type InitialStatus = 'Draft' | 'Proposed';

interface Props {
  open: boolean;
  onClose: () => void;
}

export function CreateAdrDialog({ open, onClose }: Props) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const create = useCreateAdr();
  const propose = useProposeAdr();

  const [title, setTitle] = useState('');
  const [status, setStatus] = useState<InitialStatus>('Proposed');
  const [context, setContext] = useState('');
  const [decision, setDecision] = useState('');
  const [positive, setPositive] = useState('');
  const [negative, setNegative] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);
  // Holds the ADR created on a first attempt so a failed propose can be retried without a duplicate create.
  const createdRef = useRef<AdrSummary | null>(null);

  const loc = (v: string): LocalizedText => ({ en: v.trim(), ar: v.trim() });
  const optLoc = (v: string): LocalizedText | null => (v.trim() ? loc(v) : null);
  const statusOptions: { value: InitialStatus; label: string }[] = [
    { value: 'Proposed', label: t('adrs.status.Proposed') },
    { value: 'Draft', label: t('adrs.status.Draft') },
  ];

  function validate(): boolean {
    const e: Record<string, string> = {};
    if (!title.trim()) e.title = t('adrs.create.err.title');
    if (!context.trim()) e.context = t('adrs.create.err.context');
    if (!decision.trim()) e.decision = t('adrs.create.err.decision');
    setErrors(e);
    return Object.keys(e).length === 0;
  }

  async function onConfirm() {
    setSubmitError(null);
    if (!validate()) return;
    try {
      const summary =
        createdRef.current ??
        (await create.mutateAsync({
          title: loc(title),
          context: loc(context),
          decisionDrivers: null,
          decisionText: loc(decision),
          consequencesPositive: optLoc(positive),
          consequencesNegative: optLoc(negative),
          options: null,
        }));
      createdRef.current = summary;
      if (status === 'Proposed') await propose.mutateAsync(summary.id);
      onClose();
      navigate(`/adrs/${summary.key}`);
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.problem?.title ?? t('adrs.create.error') : t('adrs.create.error'));
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      tone="accent"
      icon={<Icon name="adr" size={20} aria-hidden />}
      title={t('adrs.create.title')}
      description={t('adrs.create.subtitle')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="primary" loading={create.isPending || propose.isPending} onClick={() => void onConfirm()}>
            {t('adrs.create.confirm')}
          </Button>
        </>
      }
    >
      <div className="adr-create-form">
        {/* P15h/FR-120: a template pre-fills the ADR's Context (its primary narrative field). */}
        <TemplatePicker targetType="Adr" onApply={setContext} hasContent={!!context.trim()} />

        <Field label={t('adrs.create.titleField')} required error={errors.title}>
          {(p) => (
            <Input {...p} value={title} maxLength={512} placeholder={t('adrs.create.titlePh')} onChange={(e) => setTitle(e.target.value)} />
          )}
        </Field>

        <Field label={t('adrs.create.status')} required>
          {(p) => (
            <Select id={p.id} options={statusOptions} value={status} onChange={(v) => setStatus(v as InitialStatus)} ariaLabel={t('adrs.create.status')} />
          )}
        </Field>

        <Field label={t('adrs.create.context')} required error={errors.context}>
          {(p) => (
            <Textarea {...p} rows={3} value={context} placeholder={t('adrs.create.contextPh')} onChange={(e) => setContext(e.target.value)} />
          )}
        </Field>

        <Field label={t('adrs.create.decision')} required error={errors.decision}>
          {(p) => (
            <Textarea {...p} rows={3} value={decision} placeholder={t('adrs.create.decisionPh')} onChange={(e) => setDecision(e.target.value)} />
          )}
        </Field>

        <div className="adr-create-row">
          <Field label={t('adrs.create.positive')}>
            {(p) => (
              <Textarea {...p} rows={2} value={positive} placeholder={t('adrs.create.positivePh')} onChange={(e) => setPositive(e.target.value)} />
            )}
          </Field>
          <Field label={t('adrs.create.negative')}>
            {(p) => (
              <Textarea {...p} rows={2} value={negative} placeholder={t('adrs.create.negativePh')} onChange={(e) => setNegative(e.target.value)} />
            )}
          </Field>
        </div>

        {submitError && (
          <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{submitError}</p>
        )}
      </div>
    </Dialog>
  );
}
