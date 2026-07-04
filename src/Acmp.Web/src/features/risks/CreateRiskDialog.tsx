/*
 * Create-risk dialog (P10b, W15). Launched from the Risks register's "New risk" button — unlike an
 * action (always raised from a locked source), a risk is raised against a USER-SELECTED subject, so
 * this exposes a topic picker rather than a locked source row. Matches the `risk` form in
 * "ACMP Create Flows & Dialogs.dc.html" (Title · Likelihood · Impact · Owner · Linked topic ·
 * Mitigation plan) — a real design reference, so this is composed to it.
 *
 * Design↔behaviour reconciliations (flagged; design to be updated, guardrail #14):
 *  - Linked topic is REQUIRED here, though the design marks it optional: the backend RaiseRisk command
 *    requires a subject artifact (SubjectId), and there is no "no-subject" seam. So the picker is required
 *    and maps to SubjectType=Topic (Option A, architect-ruled).
 *  - The shared submit-confirm dialog carries an immutability warning; risks are NOT immutable, so the
 *    confirm copy is the neutral "Create risk" — no immutability warning.
 *  - Likelihood/Impact default to Medium/High (the design's pre-fill) so the 1-based RiskLevel enum is
 *    always valid on submit.
 *
 * Owner is a member picker keyed to the Keycloak subject, sent as OwnerUserId + a name snapshot. Title
 * and the mitigation plan are entered once and MIRRORED to both LocalizedString columns (en === ar), the
 * locked FTS pattern. On success we route to the new /risks/:key.
 */
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Dialog } from '../../components/ui/Dialog';
import { Button } from '../../components/ui/Button';
import { Field, Input, Textarea } from '../../components/ui/Field';
import { Select } from '../../components/ui/Select';
import { Icon } from '../../components/icons';
import { ApiError } from '../../api/apiClient';
import { useMembers } from '../../api/members';
import { useBacklog } from '../../api/topics';
import { useCreateRisk, type RiskLevel, type LocalizedText } from '../../api/risks';
import { RISK_LEVELS } from './riskMeta';

// ponytail: pull one large page of topics for the picker — a low-traffic committee register (≤ a few
// hundred topics), so a single pageSize=200 read beats wiring search-as-you-type. Upgrade to a typeahead
// if the backlog ever outgrows one page.
const TOPIC_PAGE = 200;

interface Props {
  open: boolean;
  onClose: () => void;
}

export function CreateRiskDialog({ open, onClose }: Props) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const create = useCreateRisk();
  const { data: members } = useMembers();
  const { data: topics } = useBacklog({ pageSize: TOPIC_PAGE, includeClosed: true });

  const [title, setTitle] = useState('');
  const [likelihood, setLikelihood] = useState<RiskLevel>('Medium');
  const [impact, setImpact] = useState<RiskLevel>('High');
  const [ownerId, setOwnerId] = useState('');
  const [topicId, setTopicId] = useState('');
  const [plan, setPlan] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);

  const loc = (v: string): LocalizedText => ({ en: v.trim(), ar: v.trim() });
  const ownerOptions = (members ?? [])
    .filter((m) => m.isActive)
    .map((m) => ({ value: m.keycloakUserId, label: `${m.fullName} — ${t(`roles.${m.role.toLowerCase()}`, m.role)}` }));
  const topicOptions = (topics?.items ?? []).map((tp) => ({ value: tp.id, label: `${tp.key} — ${tp.title}` }));
  const levelOptions = RISK_LEVELS.map((v) => ({ value: v, label: t(`risks.level.${v}`) }));

  function validate(): boolean {
    const e: Record<string, string> = {};
    if (!title.trim()) e.title = t('risks.create.err.title');
    if (!ownerId) e.owner = t('risks.create.err.owner');
    if (!topicId) e.topic = t('risks.create.err.topic');
    setErrors(e);
    return Object.keys(e).length === 0;
  }

  async function onConfirm() {
    setSubmitError(null);
    if (!validate()) return;
    const owner = (members ?? []).find((m) => m.keycloakUserId === ownerId);
    const topic = (topics?.items ?? []).find((tp) => tp.id === topicId);
    try {
      const result = await create.mutateAsync({
        title: loc(title),
        description: null,
        likelihood,
        impact,
        ownerUserId: ownerId,
        ownerName: owner?.fullName ?? '',
        subjectType: 'Topic',
        subjectId: topicId,
        subjectKey: topic?.key ?? null,
        initialMitigation: plan.trim() ? loc(plan) : null,
      });
      onClose();
      navigate(`/risks/${result.key}`);
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.problem?.title ?? t('risks.create.error') : t('risks.create.error'));
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      icon={<Icon name="risk" size={20} aria-hidden />}
      title={t('risks.create.title')}
      description={t('risks.create.subtitle')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="primary" loading={create.isPending} onClick={() => void onConfirm()}>
            {t('risks.create.confirm')}
          </Button>
        </>
      }
    >
      <div className="rsk-create-form">
        <Field label={t('risks.create.titleField')} required error={errors.title}>
          {(p) => (
            <Input {...p} value={title} maxLength={512} placeholder={t('risks.create.titlePh')} onChange={(e) => setTitle(e.target.value)} />
          )}
        </Field>

        <div className="rsk-create-row">
          <Field label={t('risks.create.likelihood')} required>
            {(p) => (
              <Select id={p.id} options={levelOptions} value={likelihood} onChange={(v) => setLikelihood(v as RiskLevel)} ariaLabel={t('risks.create.likelihood')} />
            )}
          </Field>
          <Field label={t('risks.create.impact')} required>
            {(p) => (
              <Select id={p.id} options={levelOptions} value={impact} onChange={(v) => setImpact(v as RiskLevel)} ariaLabel={t('risks.create.impact')} />
            )}
          </Field>
        </div>

        <div className="rsk-create-row">
          <Field label={t('risks.create.owner')} required error={errors.owner}>
            {(p) => (
              <Select
                id={p.id}
                options={ownerOptions}
                value={ownerId}
                onChange={setOwnerId}
                placeholder={t('risks.create.ownerPh')}
                ariaLabel={t('risks.create.owner')}
                aria-invalid={p['aria-invalid']}
                aria-describedby={p['aria-describedby']}
              />
            )}
          </Field>

          <Field label={t('risks.create.topic')} required error={errors.topic}>
            {(p) => (
              <Select
                id={p.id}
                options={topicOptions}
                value={topicId}
                onChange={setTopicId}
                placeholder={t('risks.create.topicPh')}
                ariaLabel={t('risks.create.topic')}
                aria-invalid={p['aria-invalid']}
                aria-describedby={p['aria-describedby']}
              />
            )}
          </Field>
        </div>

        <Field label={t('risks.create.plan')}>
          {(p) => (
            <Textarea {...p} rows={3} value={plan} placeholder={t('risks.create.planPh')} onChange={(e) => setPlan(e.target.value)} />
          )}
        </Field>

        {submitError && (
          <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{submitError}</p>
        )}
      </div>
    </Dialog>
  );
}
