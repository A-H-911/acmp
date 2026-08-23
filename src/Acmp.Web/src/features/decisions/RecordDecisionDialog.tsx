/*
 * Record → issue a decision, in one dialog, launched from the live meeting workspace's agenda item
 * (P17b, W12). This is the working affordance for the F-03 core-loop leg "chairman ratify → decision
 * record (Issued)" — replacing the disabled "coming soon" stub. Chairman-gated (issue = DecisionChairApprove).
 *
 * Design↔behavior reconciliations (visual SoT = design; behavior SoT = the package):
 *  - The design splits this into two surfaces: the "Record a decision" create form (ACMP Create Flows
 *    & Dialogs — forms/decision) and the closed-vote "Record override" control (ACMP Decision, Voting &
 *    ADR — vClosed/chairControls). In-meeting the committee acts in one place, so record + issue are one
 *    dialog. The design create-form's Approving authority / Effective date / Affected systems are omitted
 *    (no backend field — the chair is attributed automatically at issue, IssuedAt is server-set). Title,
 *    Statement and Alternatives — abbreviated out of the create form but present on the read page — are added.
 *  - The design's separate immutability confirm ("Decisions are immutable") is folded in as an inline
 *    warning; the filled form + explicit "Record & issue" primary is the deliberate confirm.
 *
 * Vote coupling: the agenda item carries no VoteId and there is no vote-by-topic endpoint, so the closed
 * vote for this topic+meeting is found client-side in the committee-wide register (≤20 users). Issuing a
 * vote-coupled decision auto-ratifies the vote (SoD-3) — the chair may NOT be the vote's counter of record,
 * enforced server-side (403). We show-and-enforce: the chair may attempt, and the 403 surfaces inline.
 *
 * The two-step (record → issue) holds the returned Draft so a failed issue (403 SoD-3 / 409 AC-029 or vote
 * integrity) does not re-record a duplicate Draft on retry. Content is entered in one language and MIRRORED
 * to both LocalizedString columns (en === ar), like the rest of the app's bilingual free text.
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
import {
  useRecordDecision,
  useIssueDecision,
  type DecisionOutcome,
  type LocalizedText,
} from '../../api/decisions';
import { useVotesRegister } from '../../api/votes';
import { DECISION_OUTCOMES } from './decisionMeta';
import { ConditionsEditor } from './SupersedeDialog';

export interface DecisionSource {
  topicId: string;
  topicKey: string;
  meetingId: string;
}

interface Props {
  open: boolean;
  onClose: () => void;
  source: DecisionSource;
}

export function RecordDecisionDialog({ open, onClose, source }: Props) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const record = useRecordDecision();
  const issue = useIssueDecision();
  // The closed vote for this agenda item, if one was held — couples the decision so issuing ratifies it.
  const { data: closedVotes } = useVotesRegister({ status: 'Closed' });
  const coupledVote = (closedVotes ?? []).find(
    (v) => v.topicId === source.topicId && v.meetingId === source.meetingId,
  );

  // Mirror the typed text into both bilingual columns (en === ar) — keeps both populated for FTS.
  const loc = (v: string): LocalizedText => ({ en: v.trim(), ar: v.trim() });

  const [outcome, setOutcome] = useState<DecisionOutcome>('Approved');
  const [title, setTitle] = useState('');
  const [statement, setStatement] = useState('');
  const [rationale, setRationale] = useState('');
  const [alternatives, setAlternatives] = useState('');
  const [conditions, setConditions] = useState<string[]>([]);
  const [chairOverride, setChairOverride] = useState(false);
  const [justification, setJustification] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);
  // Retry-safety: the Draft returned by a successful record, held so a failed issue does not re-record.
  const [draft, setDraft] = useState<{ id: string; key: string } | null>(null);

  const isConditional = outcome === 'ConditionallyApproved';
  const cleanConditions = conditions.map((c) => c.trim()).filter(Boolean);
  const busy = record.isPending || issue.isPending;

  function validate(): boolean {
    const e: Record<string, string> = {};
    if (!title.trim()) e.title = t('decisions.record.err.title');
    if (!statement.trim()) e.statement = t('decisions.record.err.statement');
    if (!rationale.trim()) e.rationale = t('decisions.record.err.rationale');
    if (isConditional && cleanConditions.length === 0) e.conditions = t('decisions.record.err.conditions');
    if (chairOverride && !justification.trim()) e.justification = t('decisions.record.err.justification');
    setErrors(e);
    return Object.keys(e).length === 0;
  }

  async function onConfirm() {
    setSubmitError(null);
    if (!validate()) return;
    try {
      // Record once: reuse the held Draft on retry so a failed issue never leaves a duplicate.
      let recorded = draft;
      if (!recorded) {
        recorded = await record.mutateAsync({
          topicId: source.topicId,
          meetingId: source.meetingId,
          outcome,
          title: loc(title),
          statement: loc(statement),
          rationale: loc(rationale),
          alternatives: alternatives.trim() ? loc(alternatives) : null,
          voteId: coupledVote?.id ?? null,
          conditions: cleanConditions.map((c) => ({ text: loc(c), dueDate: null })),
        });
        setDraft(recorded);
      }
      await issue.mutateAsync({
        id: recorded.id,
        chairOverride,
        overrideJustification: chairOverride ? loc(justification) : null,
      });
      onClose();
      navigate(`/decisions/${recorded.key}`);
    } catch (err) {
      // The dialog is chairman-gated and the vote is Closed by the time we issue, so the only 403 the
      // issue call returns is the SoD-3 co-attestation denial (chair was the vote's counter). The server
      // maps it to a generic "Forbidden" title (GlobalExceptionHandler), so give the real reason here.
      // ponytail: 403 == SoD-3 for this endpoint+role; a plain title fallback covers everything else (409, etc.).
      if (err instanceof ApiError) {
        setSubmitError(err.status === 403 ? t('decisions.record.sod3Denied') : err.problem?.title ?? t('decisions.record.error'));
      } else {
        setSubmitError(t('decisions.record.error'));
      }
    }
  }

  const outcomeOptions = DECISION_OUTCOMES.map((o) => ({ value: o, label: t(`decisions.outcome.${o}`) }));

  return (
    <Dialog
      open={open}
      onClose={onClose}
      tone="accent"
      icon={<Icon name="decision" size={20} aria-hidden />}
      title={t('decisions.record.title')}
      description={t('decisions.record.subtitle')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button loading={busy} onClick={() => void onConfirm()}>{t('decisions.record.confirm')}</Button>
        </>
      }
    >
      <div className="dec-record-form">
        <Field label={t('decisions.record.linkedTopic')}>
          {(p) => (
            <div className="dec-record-linked" id={p.id}>
              <Icon name="deps" size={14} aria-hidden />
              <span className="dec-key-chip">{source.topicKey}</span>
              {coupledVote && (
                <span className="dec-record-ratify">
                  <Icon name="check" size={13} aria-hidden />
                  {t('decisions.record.ratifies', { key: coupledVote.key })}
                </span>
              )}
            </div>
          )}
        </Field>

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

        <Field label={t('decisions.field.alternatives')}>
          {(p) => (
            <MarkdownEditor {...p} rows={2} value={alternatives} placeholder={t('decisions.field.alternativesPh')} onChange={setAlternatives} />
          )}
        </Field>

        {isConditional && <ConditionsEditor values={conditions} onChange={setConditions} error={errors.conditions} />}

        {coupledVote && (
          <div className="dec-record-override">
            <label className="dec-record-override-toggle">
              <input type="checkbox" checked={chairOverride} onChange={(e) => setChairOverride(e.target.checked)} />
              <span>{t('decisions.record.override')}</span>
            </label>
            <p className="dec-record-override-note">{t('decisions.record.overrideNote')}</p>
            {chairOverride && (
              <Field label={t('decisions.record.justification')} required error={errors.justification}>
                {(p) => (
                  <Textarea {...p} rows={2} value={justification} placeholder={t('decisions.record.justificationPh')} onChange={(e) => setJustification(e.target.value)} />
                )}
              </Field>
            )}
          </div>
        )}

        <div className="dec-record-warn" role="note">
          <Icon name="warnTriangle" size={15} aria-hidden />
          <span>{t('decisions.record.immutableWarn')}</span>
        </div>

        {submitError && <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{submitError}</p>}
      </div>
    </Dialog>
  );
}
