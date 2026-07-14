/*
 * Convert-to-execution-topic dialog (P15c-2, W16). Launched from a completed mission's header ("Convert to
 * execution topic") or from an accepted recommendation ("Convert"). Unlike the P11e Convert-to-ADR confirm
 * (server pre-fills everything), a topic needs fields research doesn't carry — so this is a short FORM, not a
 * bare confirm. No-reference composition (guardrail #14 — the design shows only the button); built in the
 * research module's own dialog style (Dialog + Field + Select + shared TokenInput), mirroring AddFinding.
 *
 * Pre-fill: Title ← mission title / recommendation statement (trimmed to the 120-char topic limit); Description
 * ← the mission question / recommendation rationale. Justification has no research source (typed) and Streams
 * are user-picked (no cheap source — we hold a GUID, not the source topic's streams). Systems/tags are left
 * empty and can be added on the topic later.
 *
 * Submit is target-owns: POST /api/topics/from-research creates the topic + the reverse Informs edge and is
 * authoritative; on success we navigate to the new topic. For a recommendation seed we ALSO fire the
 * best-effort mark-converted (non-fatal — the "Converted" chip is edge-driven and self-heals if it fails).
 * A 409 (source ineligible / already converted) surfaces inline without navigating.
 */
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Dialog } from '../../components/ui/Dialog';
import { Button } from '../../components/ui/Button';
import { Field, Input, Textarea } from '../../components/ui/Field';
import { Select } from '../../components/ui/Select';
import { TokenInput } from '../../components/ui/TokenInput';
import { Icon } from '../../components/icons';
import { ApiError } from '../../api/apiClient';
import { useConvertResearchToTopic } from '../../api/topics';
import { useMarkRecommendationConverted } from '../../api/research';

// Canonical TopicType / TopicUrgency wire names (mirror SubmitTopic's TYPES/URGENCIES); labels via the shared
// topics.* i18n keys. Kept as a small inline list rather than coupling to SubmitTopic's icon-keyed array.
const TOPIC_TYPES = ['ResearchDiscovery', 'ArchitectureDecision', 'EnhancementInnovation', 'GovernanceStandardization'] as const;
const TOPIC_URGENCIES = ['Normal', 'Urgent', 'Critical'] as const;
const MAX_TITLE = 120;

interface Props {
  onClose: () => void;
  missionId: string;
  /** Present = convert this recommendation; absent = convert the whole mission. */
  recommendationId?: string;
  seedTitle: string;
  seedDescription: string;
}

export function ConvertToTopicDialog({ onClose, missionId, recommendationId, seedTitle, seedDescription }: Props) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const convert = useConvertResearchToTopic();
  const markConverted = useMarkRecommendationConverted();

  const [title, setTitle] = useState(() => seedTitle.slice(0, MAX_TITLE));
  const [description, setDescription] = useState(seedDescription);
  const [justification, setJustification] = useState('');
  const [type, setType] = useState<string>('ResearchDiscovery');
  const [urgency, setUrgency] = useState<string>('Normal');
  const [streams, setStreams] = useState<string[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);

  function validate(): boolean {
    const e: Record<string, string> = {};
    if (!title.trim()) e.title = t('research.convert.err.title');
    if (!description.trim()) e.description = t('research.convert.err.description');
    if (!justification.trim()) e.justification = t('research.convert.err.justification');
    if (streams.length === 0) e.streams = t('research.convert.err.streams');
    setErrors(e);
    return Object.keys(e).length === 0;
  }

  async function onConfirm() {
    setSubmitError(null);
    if (!validate()) return;
    try {
      const res = await convert.mutateAsync({
        missionId,
        recommendationId: recommendationId ?? null,
        title: title.trim(),
        description: description.trim(),
        justification: justification.trim(),
        type,
        urgency,
        streams,
        systems: [],
        tags: [],
      });
      // Best-effort display flag — never blocks landing on the created topic (the Informs edge already drives
      // the "Converted" chip). Fire-and-forget so a transient failure here doesn't surface as an error.
      if (recommendationId) markConverted.mutate({ id: missionId, recommendationId, topicId: res.id });
      onClose();
      navigate(`/topics/${res.key}`);
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.problem?.title ?? t('research.convert.error') : t('research.convert.error'));
    }
  }

  return (
    <Dialog
      open
      onClose={onClose}
      tone="accent"
      icon={<Icon name="arrowRight" size={20} aria-hidden />}
      title={t('research.convert.title')}
      description={recommendationId ? t('research.convert.subtitleRec') : t('research.convert.subtitleMission')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="primary" loading={convert.isPending} onClick={() => void onConfirm()}>
            {t('research.convert.confirm')}
          </Button>
        </>
      }
    >
      <div className="rsc-create-form">
        <Field label={t('research.convert.fTitle')} required error={errors.title}>
          {(p) => (
            <Input {...p} value={title} maxLength={MAX_TITLE} placeholder={t('research.convert.fTitlePh')} onChange={(e) => setTitle(e.target.value)} />
          )}
        </Field>
        <Field label={t('research.convert.fType')} required>
          {(p) => (
            <Select id={p.id} options={TOPIC_TYPES.map((v) => ({ value: v, label: t(`topics.type.${v}`) }))} value={type} onChange={setType} ariaLabel={t('research.convert.fType')} />
          )}
        </Field>
        <Field label={t('research.convert.fUrgency')} required>
          {(p) => (
            <Select id={p.id} options={TOPIC_URGENCIES.map((v) => ({ value: v, label: t(`topics.urgency.${v}`) }))} value={urgency} onChange={setUrgency} ariaLabel={t('research.convert.fUrgency')} />
          )}
        </Field>
        <Field label={t('research.convert.fDescription')} required error={errors.description}>
          {(p) => (
            <Textarea {...p} rows={3} value={description} placeholder={t('research.convert.fDescriptionPh')} onChange={(e) => setDescription(e.target.value)} />
          )}
        </Field>
        <Field label={t('research.convert.fJustification')} required error={errors.justification}>
          {(p) => (
            <Textarea {...p} rows={2} value={justification} placeholder={t('research.convert.fJustificationPh')} onChange={(e) => setJustification(e.target.value)} />
          )}
        </Field>
        <Field label={t('research.convert.fStreams')} required error={errors.streams}>
          {(p) => (
            <TokenInput
              id={p.id}
              ariaInvalid={p['aria-invalid']}
              describedby={p['aria-describedby']}
              values={streams}
              onChange={setStreams}
              placeholder={t('research.convert.fStreamsPh')}
              ariaLabel={t('research.convert.fStreams')}
              removeLabel={(v) => t('topics.removeFilter', { label: v })}
            />
          )}
        </Field>
        {submitError && <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{submitError}</p>}
      </div>
    </Dialog>
  );
}
