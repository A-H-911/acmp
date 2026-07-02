/*
 * Call-vote / configure dialog (P9b, W11). Launched from a meeting agenda item — the topic and
 * meeting are pre-filled and locked (Fork 5: a vote always binds the current meeting). Matches the
 * `vote` form in "ACMP Create Flows & Dialogs.dc.html" (Linked topic · Eligible voters · Ballot
 * options · Quorum · COI note).
 *
 * Design↔behavior reconciliations:
 *  - Ballot options are fixed to Approve/Reject with an Abstain toggle (Fork 4) — the design's add-chip
 *    affordance is omitted (options aren't free-form on the aggregate).
 *  - One "Required quorum" number maps to BOTH MinPresent (open) and MinCast (close); the aggregate
 *    splits them but the design carries a single quorum (reconcile).
 *  - The design's "Closes" date has no backend (votes close manually) — omitted and flagged.
 *  - Primary configures the vote (Configured state); the chair opens it from the /votes screen's
 *    not_open state (the design's own not_open leg), so the label is "Configure vote" not "Open vote".
 *
 * Eligible voters are seeded from the roster's voting-eligible active members (Member.isVotingEligible),
 * keyed to the Keycloak subject (Member.keycloakUserId) so ballots attribute to the same identity the
 * signed-in user carries. On success we route to the new /votes/:key.
 */
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Dialog } from '../../components/ui/Dialog';
import { Button } from '../../components/ui/Button';
import { Field, Input } from '../../components/ui/Field';
import { Icon } from '../../components/icons';
import { ApiError } from '../../api/apiClient';
import { useMembers } from '../../api/members';
import { useConfigureVote } from '../../api/votes';
import './voting.css';

const BALLOT_OPTIONS = ['Approve', 'Reject'] as const;

export interface VoteSource {
  topicId: string;
  topicKey: string;
  meetingId: string;
}

interface Props {
  open: boolean;
  onClose: () => void;
  source: VoteSource;
}

export function CallVoteDialog({ open, onClose, source }: Props) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const configure = useConfigureVote();
  const { data: members } = useMembers();

  const eligible = (members ?? []).filter((m) => m.isActive && m.isVotingEligible);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [allowAbstain, setAllowAbstain] = useState(true);
  const [quorum, setQuorum] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);

  // Default the selection to every eligible member once the roster arrives (and nothing chosen yet).
  const chosen = selected.size > 0 ? selected : new Set(eligible.map((m) => m.keycloakUserId));
  const toggle = (sub: string) => {
    const next = new Set(chosen);
    if (next.has(sub)) next.delete(sub);
    else next.add(sub);
    setSelected(next);
  };

  function validate(voterCount: number): boolean {
    const e: Record<string, string> = {};
    if (voterCount < 2) e.voters = t('voting.create.err.voters');
    const q = Number(quorum);
    if (!quorum || !Number.isInteger(q) || q < 1 || q > voterCount) e.quorum = t('voting.create.err.quorum');
    setErrors(e);
    return Object.keys(e).length === 0;
  }

  async function onConfirm() {
    setSubmitError(null);
    const voters = eligible.filter((m) => chosen.has(m.keycloakUserId));
    if (!validate(voters.length)) return;
    const q = Number(quorum);
    try {
      const result = await configure.mutateAsync({
        topicId: source.topicId,
        meetingId: source.meetingId,
        options: [...BALLOT_OPTIONS],
        allowAbstain,
        minPresent: q,
        minCast: q,
        eligibleVoters: voters.map((m) => ({ userId: m.keycloakUserId, name: m.fullName })),
      });
      onClose();
      navigate(`/votes/${result.key}`);
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.problem?.title ?? t('voting.create.error') : t('voting.create.error'));
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      icon={<Icon name="audit" size={20} aria-hidden />}
      title={t('voting.create.title')}
      description={t('voting.create.subtitle')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('voting.cancel')}</Button>
          <Button variant="primary" loading={configure.isPending} onClick={() => void onConfirm()}>
            {t('voting.create.confirm')}
          </Button>
        </>
      }
    >
      <div className="vote-create-form">
        <Field label={t('voting.create.linked')}>
          {(p) => (
            <div className="vote-linked-locked" id={p.id}>
              <Icon name="deps" size={14} aria-hidden />
              <span className="vote-linked-key">{source.topicKey}</span>
              <span className="vote-linked-note">{t('voting.create.linkedNote')}</span>
            </div>
          )}
        </Field>

        <Field label={t('voting.create.voters')} required help={t('voting.create.votersHint')} error={errors.voters}>
          {(p) => (
            <div className="vote-voter-list" id={p.id} role="group" aria-label={t('voting.create.voters')}>
              {eligible.map((m) => (
                <label key={m.keycloakUserId} className="vote-voter-check">
                  <input type="checkbox" checked={chosen.has(m.keycloakUserId)} onChange={() => toggle(m.keycloakUserId)} />
                  <span>{m.fullName} — {t(`roles.${m.role.toLowerCase()}`, m.role)}</span>
                </label>
              ))}
            </div>
          )}
        </Field>

        <Field label={t('voting.create.options')}>
          {(p) => (
            <div className="vote-options-fixed" id={p.id}>
              {BALLOT_OPTIONS.map((o) => (
                <span key={o} className="vote-opt-chip">{t(`voting.option.${o}`)}</span>
              ))}
              {allowAbstain && <span className="vote-opt-chip">{t('voting.option.Abstain')}</span>}
            </div>
          )}
        </Field>

        <Field label={t('voting.create.allowAbstain')}>
          {(p) => (
            <label className="vote-voter-check" id={p.id}>
              <input type="checkbox" checked={allowAbstain} onChange={(e) => setAllowAbstain(e.target.checked)} />
              <span>{t('voting.option.Abstain')}</span>
            </label>
          )}
        </Field>

        <Field label={t('voting.create.quorum')} required help={t('voting.create.quorumHint')} error={errors.quorum}>
          {(p) => (
            <Input
              {...p}
              type="number"
              min={1}
              max={eligible.length}
              value={quorum}
              onChange={(e) => setQuorum(e.target.value)}
            />
          )}
        </Field>

        <div className="vote-coi-note">{t('voting.create.coiNote')}</div>

        {submitError && (
          <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{submitError}</p>
        )}
      </div>
    </Dialog>
  );
}
