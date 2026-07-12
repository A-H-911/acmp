/*
 * Create-mission dialog (P15b, FR-111). Launched from the Research register's "New mission" button
 * (Chairman/Secretary only). Composed to the isResearch new-mission intent in
 * "ACMP Research & Knowledge.dc.html": a Title, the research Question, and two optional links.
 *
 * Read-vs-write asymmetry (advisor): the fields come from the CreateMission COMMAND, not the read DTO.
 * P15a is owner == creator (self-directed missions) — the handler derives OwnerUserId from the current
 * user — so there is NO owner picker. Title + Question are entered once and MIRRORED to both
 * LocalizedString columns (en === ar), the locked FTS pattern.
 *
 * Design↔behaviour reconciliations (guardrail #14): the KeystonePackageRef is stored only (FR-112
 * deferred) and shown as a plain text input; SourceTopicId is optional and collected with a topic
 * picker (a user can't type a Guid) — it has no navigable target yet (the graph edge is P15c). On
 * success we route to the new /research/:key.
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
import { useBacklog } from '../../api/topics';
import { useCreateMission, type LocalizedText } from '../../api/research';

// ponytail: one large page of topics for the optional picker — a low-traffic committee register, so a
// single pageSize=200 read beats wiring search-as-you-type. Upgrade to a typeahead if it ever outgrows one page.
const TOPIC_PAGE = 200;

interface Props {
  open: boolean;
  onClose: () => void;
}

export function CreateMissionDialog({ open, onClose }: Props) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const create = useCreateMission();
  const { data: topics } = useBacklog({ pageSize: TOPIC_PAGE, includeClosed: true });

  const [title, setTitle] = useState('');
  const [question, setQuestion] = useState('');
  const [keystone, setKeystone] = useState('');
  const [topicId, setTopicId] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);

  const loc = (v: string): LocalizedText => ({ en: v.trim(), ar: v.trim() });
  const topicOptions = (topics?.items ?? []).map((tp) => ({ value: tp.id, label: `${tp.key} — ${tp.title}` }));

  function validate(): boolean {
    const e: Record<string, string> = {};
    if (!title.trim()) e.title = t('research.create.err.title');
    if (!question.trim()) e.question = t('research.create.err.question');
    setErrors(e);
    return Object.keys(e).length === 0;
  }

  async function onConfirm() {
    setSubmitError(null);
    if (!validate()) return;
    try {
      const result = await create.mutateAsync({
        title: loc(title),
        question: loc(question),
        keystonePackageRef: keystone.trim() || null,
        sourceTopicId: topicId || null,
      });
      onClose();
      navigate(`/research/${result.key}`);
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.problem?.title ?? t('research.create.error') : t('research.create.error'));
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      tone="accent"
      icon={<Icon name="research" size={20} aria-hidden />}
      title={t('research.create.title')}
      description={t('research.create.subtitle')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="primary" loading={create.isPending} onClick={() => void onConfirm()}>
            {t('research.create.confirm')}
          </Button>
        </>
      }
    >
      <div className="rsc-create-form">
        <Field label={t('research.create.titleField')} required error={errors.title}>
          {(p) => (
            <Input {...p} value={title} maxLength={512} placeholder={t('research.create.titlePh')} onChange={(e) => setTitle(e.target.value)} />
          )}
        </Field>

        <Field label={t('research.create.question')} required error={errors.question}>
          {(p) => (
            <Textarea {...p} rows={3} value={question} placeholder={t('research.create.questionPh')} onChange={(e) => setQuestion(e.target.value)} />
          )}
        </Field>

        <Field label={t('research.create.topic')}>
          {(p) => (
            <Select
              id={p.id}
              options={topicOptions}
              value={topicId}
              onChange={setTopicId}
              placeholder={t('research.create.topicPh')}
              ariaLabel={t('research.create.topic')}
            />
          )}
        </Field>

        <Field label={t('research.create.keystone')} help={t('research.create.keystoneHelp')}>
          {(p) => (
            <Input {...p} value={keystone} maxLength={200} placeholder={t('research.create.keystonePh')} onChange={(e) => setKeystone(e.target.value)} />
          )}
        </Field>

        {submitError && (
          <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{submitError}</p>
        )}
      </div>
    </Dialog>
  );
}
