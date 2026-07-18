/*
 * Supersede dialog (P7b, W21). The design's dialog (ACMP Decision, Voting & ADR — supersedeOpen)
 * collects only a reason, but the shipped supersede command drafts + auto-issues a FULL successor
 * decision in one transaction — so this dialog must capture that successor body (outcome, title,
 * statement, rationale, optional alternatives, conditions when conditional) plus the reason. Extending the
 * one-field mock to the real body is a blessed deviation (design updated later).
 *
 * Content is entered in one language and MIRRORED to both LocalizedString columns (en === ar) — the
 * operator's choice, keeping both columns populated for Full-Text Search. A true bilingual content-editing
 * surface is future work. UI chrome stays fully EN/AR via i18n.
 */
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Dialog } from '../../components/ui/Dialog';
import { Button } from '../../components/ui/Button';
import { Field, Input, Textarea } from '../../components/ui/Field';
import { Select } from '../../components/ui/Select';
import { MarkdownEditor } from '../../components/ui/MarkdownEditor';
import { Icon } from '../../components/icons';
import { ApiError } from '../../api/apiClient';
import { useSupersedeDecision, type DecisionOutcome, type LocalizedText } from '../../api/decisions';
import { DECISION_OUTCOMES } from './decisionMeta';

interface Props {
  open: boolean;
  onClose: () => void;
  priorKey: string;
  priorDecisionId: string;
  /** Detail cache key of the PRIOR decision, so its Superseded state refetches on success. */
  cacheKey: string | undefined;
}

export function SupersedeDialog({ open, onClose, priorKey, priorDecisionId, cacheKey }: Props) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const supersede = useSupersedeDecision(cacheKey);

  // Mirror the typed text into both bilingual columns (en === ar) — keeps both populated for FTS.
  const loc = (v: string): LocalizedText => ({ en: v.trim(), ar: v.trim() });

  const [outcome, setOutcome] = useState<DecisionOutcome>('Approved');
  const [title, setTitle] = useState('');
  const [statement, setStatement] = useState('');
  const [rationale, setRationale] = useState('');
  const [alternatives, setAlternatives] = useState('');
  const [conditions, setConditions] = useState<string[]>([]);
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);

  const isConditional = outcome === 'ConditionallyApproved';
  const cleanConditions = conditions.map((c) => c.trim()).filter(Boolean);

  function validate(): boolean {
    const e: Record<string, string> = {};
    if (!title.trim()) e.title = t('decisions.supersede.err.title');
    if (!statement.trim()) e.statement = t('decisions.supersede.err.statement');
    if (!rationale.trim()) e.rationale = t('decisions.supersede.err.rationale');
    if (!reason.trim()) e.reason = t('decisions.supersede.err.reason');
    if (isConditional && cleanConditions.length === 0) e.conditions = t('decisions.supersede.err.conditions');
    setErrors(e);
    return Object.keys(e).length === 0;
  }

  async function onConfirm() {
    setSubmitError(null);
    if (!validate()) return;
    try {
      const result = await supersede.mutateAsync({
        priorDecisionId,
        outcome,
        title: loc(title),
        statement: loc(statement),
        rationale: loc(rationale),
        alternatives: alternatives.trim() ? loc(alternatives) : null,
        conditions: cleanConditions.map((c) => ({ text: loc(c), dueDate: null })),
        reason: loc(reason),
      });
      onClose();
      navigate(`/decisions/${result.key}`);
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.problem?.title ?? t('decisions.supersede.error') : t('decisions.supersede.error'));
    }
  }

  const outcomeOptions = DECISION_OUTCOMES.map((o) => ({ value: o, label: t(`decisions.outcome.${o}`) }));

  return (
    <Dialog
      open={open}
      onClose={onClose}
      tone="warn"
      icon={<Icon name="refresh" size={20} aria-hidden />}
      title={t('decisions.supersede.title')}
      description={t('decisions.supersede.subtitle')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="secondary" className="btn-warn" loading={supersede.isPending} onClick={() => void onConfirm()}>
            {t('decisions.supersede.confirm')}
          </Button>
        </>
      }
    >
      <div className="dec-supersede-form">
        <div className="dec-supersede-flow">
          <span className="dec-key-chip">{priorKey}</span>
          <Icon name="chevron" size={16} className="dir-flip" aria-hidden />
          <span className="dec-supersede-flow-new">{t('decisions.supersede.newDecision')}</span>
        </div>

        <Field label={t('decisions.field.outcome')} required>
          {(p) => (
            <Select
              id={p.id}
              options={outcomeOptions}
              value={outcome}
              onChange={(v) => setOutcome(v as DecisionOutcome)}
              ariaLabel={t('decisions.field.outcome')}
            />
          )}
        </Field>

        <Field label={t('decisions.field.title')} required error={errors.title}>
          {(p) => (
            <Input {...p} value={title} maxLength={512} placeholder={t('decisions.field.titlePh')} onChange={(e) => setTitle(e.target.value)} />
          )}
        </Field>

        <Field label={t('decisions.field.statement')} required error={errors.statement}>
          {(p) => (
            <Textarea {...p} rows={2} value={statement} maxLength={2000} placeholder={t('decisions.field.statementPh')} onChange={(e) => setStatement(e.target.value)} />
          )}
        </Field>

        <Field label={t('decisions.field.rationale')} required error={errors.rationale}>
          {(p) => (
            <MarkdownEditor {...p} rows={3} value={rationale} placeholder={t('decisions.field.rationalePh')} onChange={setRationale} />
          )}
        </Field>

        <Field label={t('decisions.field.alternatives')} error={errors.alternatives}>
          {(p) => (
            <MarkdownEditor {...p} rows={2} value={alternatives} placeholder={t('decisions.field.alternativesPh')} onChange={setAlternatives} />
          )}
        </Field>

        {isConditional && (
          <ConditionsEditor values={conditions} onChange={setConditions} error={errors.conditions} />
        )}

        <Field label={t('decisions.supersede.reason')} required error={errors.reason}>
          {(p) => (
            <Textarea {...p} rows={3} value={reason} placeholder={t('decisions.supersede.reasonPh')} onChange={(e) => setReason(e.target.value)} />
          )}
        </Field>

        <div className="dec-supersede-warn" role="note">
          <Icon name="warnTriangle" size={15} aria-hidden />
          <span>{t('decisions.supersede.warn')}</span>
        </div>

        {submitError && <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{submitError}</p>}
      </div>
    </Dialog>
  );
}

interface ConditionsEditorProps {
  values: string[];
  onChange: (v: string[]) => void;
  error?: string;
}

/** A conditionally-approved decision needs ≥1 condition (mirrors the domain guard). Minimal
 *  add/edit/remove list — each row is single-language text, mirrored to both columns on submit.
 *  Shared with RecordDecisionDialog (same field + validation). */
export function ConditionsEditor({ values, onChange, error }: ConditionsEditorProps) {
  const { t } = useTranslation();
  const rows = values.length > 0 ? values : [''];
  const setAt = (i: number, v: string) => onChange(rows.map((r, j) => (j === i ? v : r)));
  return (
    <Field label={t('decisions.field.conditions')} required error={error}>
      {(p) => (
        <div className="dec-conditions" id={p.id}>
          {rows.map((row, i) => (
            <div className="dec-condition-row" key={i}>
              <Input
                aria-label={t('decisions.field.conditionN', { n: i + 1 })}
                value={row}
                placeholder={t('decisions.field.conditionPh')}
                onChange={(e) => setAt(i, e.target.value)}
              />
              {rows.length > 1 && (
                <button
                  type="button"
                  className="dec-condition-rm"
                  aria-label={t('decisions.field.removeCondition', { n: i + 1 })}
                  onClick={() => onChange(values.filter((_, j) => j !== i))}
                >
                  <Icon name="x" size={14} aria-hidden />
                </button>
              )}
            </div>
          ))}
          <button type="button" className="dec-condition-add" onClick={() => onChange([...rows, ''])}>
            <Icon name="plus" size={14} aria-hidden />
            {t('decisions.field.addCondition')}
          </button>
        </div>
      )}
    </Field>
  );
}
