/*
 * Voting screen (P9b) — composed to the isVoting screen in "ACMP Decision, Voting & ADR.dc.html".
 * Read by key (GET /api/votes/{key}); reached via the VoteOpened notification deep-link and the
 * meeting-workspace "Call vote" flow. The shell owns the breadcrumb (deriveBreadcrumbs).
 *
 * Design↔behavior reconciliations (visual SoT = design; behavior SoT = the package):
 *  - The design's manual state toggle is a preview control; real view state is DERIVED (voteState.ts).
 *  - `double_error` is not a hard block — the backend allows changing a ballot until close (Fork 1),
 *    so a cast ballot is shown as editable with its recorded choice pre-selected.
 *  - `quorum_failed` is not a resting state — a failed close is a 409 with the vote still Open (Fork 2),
 *    surfaced as an inline announced error; the "re-open / extend" CTA is dropped (no backend).
 *  - Motion text, called-by, and voter roles are not modeled on the Vote aggregate; the honest header
 *    shows the VOTE key + status + attributed badge, and voters render name + choice + comment only.
 *  - Closed panel shows counter-of-record + result summary; "View decision record" / "Record override"
 *    are deferred (no reverse vote→decision link; no issue-from-vote UI) (Fork 3).
 *  - Quorum pips track cast-count → MinCast (the DTO carries no live present count) (reconcile R-A).
 */
import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  useVote, useOpenVote, useCastBallot, useChangeBallot, useRecuseVote, useCloseVote,
  type Ballot, type LocalizedText, type VoteDetail,
} from '../../api/votes';
import { ApiError } from '../../api/apiClient';
import { deriveVoteContext, hasCast, optionTone } from './voteState';
import { StatusChip, type StatusTone } from '../../components/ui/StatusChip';
import { Button } from '../../components/ui/Button';
import { Dialog } from '../../components/ui/Dialog';
import { Textarea } from '../../components/ui/Field';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { useAuth, hasRole } from '../../auth/AcmpAuthContext';
import './voting.css';

const ABSTAIN = 'Abstain';

function initials(name: string): string {
  return name.split(/\s+/).filter(Boolean).slice(0, 2).map((p) => p[0]?.toUpperCase() ?? '').join('') || '—';
}

export function VotePage() {
  const { key } = useParams();
  const { t, i18n } = useTranslation();
  const auth = useAuth();
  const { data, isLoading, isError, error, refetch } = useVote(key);

  if (isLoading) return <section className="page"><LoadingState /></section>;
  if (isError || !data) {
    const notFound = error instanceof ApiError && error.status === 404;
    return (
      <section className="page">
        {notFound
          ? <EmptyState title={t('voting.notFoundTitle')} body={t('voting.notFoundBody')} />
          : <ErrorState onRetry={() => refetch()} />}
      </section>
    );
  }

  return <VoteView vote={data} cacheKey={key} userId={auth.userId} canManage={hasRole(auth, 'chairman', 'secretary')} lang={i18n.language} />;
}

interface ViewProps {
  vote: VoteDetail;
  cacheKey: string | undefined;
  userId: string | undefined;
  canManage: boolean;
  lang: string;
}

function VoteView({ vote, cacheKey, userId, canManage, lang }: ViewProps) {
  const { t } = useTranslation();
  const ctx = deriveVoteContext(vote, userId);
  const openVote = useOpenVote(cacheKey);
  const closeVote = useCloseVote(cacheKey);
  const [actionError, setActionError] = useState<string | null>(null);

  const fmtDate = (iso: string) => new Intl.DateTimeFormat(lang, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso));

  // Eligible base = ballots not recused; cast = those with a choice. Live tally is derived while Open;
  // the frozen server tally is authoritative once Closed.
  const active = vote.ballots.filter((b) => !b.recused);
  const eligibleCount = active.length;
  const castCount = ctx.isClosed && vote.tally ? vote.tally.castCount : active.filter((b) => b.choice !== null).length;
  const quorumMet = castCount >= vote.minCast;

  const options = [...vote.options, ...(vote.allowAbstain ? [ABSTAIN] : [])];
  const tallyRows = options.map((opt) => {
    const count = ctx.isClosed && vote.tally
      ? (opt === ABSTAIN ? vote.tally.abstainCount : vote.tally.optionCounts[opt] ?? 0)
      : active.filter((b) => b.choice === opt).length;
    return { opt, count, tone: optionTone(opt), pct: eligibleCount ? `${(count / eligibleCount) * 100}%` : '0%' };
  });

  async function onOpen() {
    setActionError(null);
    try { await openVote.mutateAsync({ id: vote.id }); }
    catch { setActionError(t('voting.openError')); }
  }
  async function onClose() {
    setActionError(null);
    try { await closeVote.mutateAsync({ id: vote.id }); }
    catch { setActionError(t('voting.closeError')); }
  }

  const stateTone: StatusTone = ctx.isOpen ? 'danger' : 'success';
  const stateLabel = ctx.isOpen ? t('voting.state.open') : t('voting.state.closed');

  return (
    <section className="page vote-page">
      <header className="vote-head">
        <div className="vote-head-main">
          <h1 className="vote-title">{t('voting.voteTitle')}</h1>
          <StatusChip tone={stateTone} label={stateLabel} />
        </div>
      </header>
      <div className="vote-subrow">
        <span className="vote-key">{vote.key}</span>
        <span className="vote-dot" aria-hidden="true" />
        <span className="vote-attributed"><Icon name="eye" size={12} aria-hidden /> {t('voting.attributed')}</span>
      </div>

      {actionError && (
        <div className="vote-banner neutral" role="alert">
          <Icon name="alertCircle" size={20} aria-hidden />
          <div><div className="vote-banner-title">{actionError}</div></div>
        </div>
      )}

      <div className="vote-grid">
        {/* left: voters */}
        <div className="vote-col">
          <section className="vote-card">
            <div className="vote-card-h">
              <h2>{t('voting.eligibleVoters')} <span style={{ color: 'var(--text-3)', fontWeight: 500 }}>· {eligibleCount}</span></h2>
              <span style={{ fontSize: 11.5, color: 'var(--text-3)' }}>{t('voting.castSummary')}</span>
            </div>
            <div>
              {vote.ballots.map((b) => (
                <VoterRow key={b.voterUserId} ballot={b} lang={lang} t={t} />
              ))}
            </div>
          </section>
        </div>

        {/* right: tally + ballot/closed */}
        <div className="vote-col vote-col-side">
          <section className="vote-card">
            <div className="vote-card-h">
              <h2>{t('voting.liveTally')}</h2>
              {ctx.isOpen && <span className="tally-live"><span className="tally-live-dot" aria-hidden="true" />{t('voting.live')}</span>}
            </div>
            <div style={{ padding: 16 }}>
              <div className="tally-list">
                {tallyRows.map((r) => (
                  <div key={r.opt}>
                    <div className="tally-head">
                      <span className="tally-label"><span className={`tally-swatch ${r.tone}`} aria-hidden="true" />{t(`voting.option.${r.opt}`, r.opt)}</span>
                      <span className="tally-count">{r.count}</span>
                    </div>
                    <div className="tally-track"><span className={`tally-fill ${r.tone}`} style={{ inlineSize: r.pct }} /></div>
                  </div>
                ))}
              </div>
              <div className="quorum-block">
                <div className="quorum-row">
                  <span style={{ color: 'var(--text-2)' }}>{t('voting.quorum')}</span>
                  <span className={`quorum-state ${quorumMet ? 'met' : 'pending'}`}>
                    <Icon name={quorumMet ? 'check' : 'alertCircle'} size={14} aria-hidden />
                    {quorumMet ? t('voting.quorumMet') : t('voting.quorumPending')}
                  </span>
                </div>
                <div className="quorum-pips">
                  {Array.from({ length: Math.max(eligibleCount, vote.minCast) }, (_, i) => (
                    <span
                      key={i}
                      className={`quorum-pip ${i < castCount ? (quorumMet ? 'on-met' : 'on-pending') : ''}${i === vote.minCast - 1 ? ' threshold' : ''}`}
                    />
                  ))}
                </div>
                <div className="quorum-detail">{t('voting.quorumDetail', { cast: castCount, eligible: eligibleCount, needed: vote.minCast })}</div>
              </div>
            </div>
          </section>

          {ctx.view === 'not_open' && (
            <section className="vote-mini">
              <span className="vote-mini-icon"><Icon name="clock" size={20} aria-hidden /></span>
              <div className="vote-mini-title">{t('voting.notOpenTitle')}</div>
              <div className="vote-mini-sub">{t('voting.notOpenSub')}</div>
              {canManage && (
                <>
                  <Button className="vote-open-btn" loading={openVote.isPending} onClick={() => void onOpen()}>
                    <Icon name="arrowRight" size={16} aria-hidden /> {t('voting.openVoting')}
                  </Button>
                  <div className="vote-open-note">{t('voting.openVotingNote')}</div>
                </>
              )}
            </section>
          )}

          {ctx.view === 'open' && ctx.myBallot && (
            <BallotForm key={ctx.myBallot.castAt ?? 'new'} vote={vote} cacheKey={cacheKey} myBallot={ctx.myBallot} />
          )}

          {ctx.view === 'ineligible' && (
            <section className="vote-mini vote-mini-solid">
              <span className="vote-mini-icon sm"><Icon name="eye" size={17} aria-hidden /></span>
              <div><div className="vote-mini-title">{t('voting.viewOnlyTitle')}</div><div className="vote-mini-sub">{t('voting.viewOnlySub')}</div></div>
            </section>
          )}

          {ctx.view === 'recused' && (
            <section className="vote-mini vote-mini-solid">
              <span className="vote-mini-icon sm"><Icon name="x" size={17} aria-hidden /></span>
              <div><div className="vote-mini-title">{t('voting.recusedTitle')}</div><div className="vote-mini-sub">{t('voting.recusedSub')}</div></div>
            </section>
          )}

          {ctx.isClosed && (
            <section className="vote-closed">
              <div className="vote-closed-head">
                <span className="vote-closed-badge"><Icon name="lock" size={18} aria-hidden /></span>
                <div>
                  <div className="vote-closed-title">{t('voting.voteClosedLocked')}</div>
                  {vote.closedAt && <div className="vote-closed-sub">{t('voting.closedAt', { date: fmtDate(vote.closedAt) })}</div>}
                </div>
              </div>
              <div className="vote-closed-panel">
                <div className="vote-eyebrow">{t('voting.result')}</div>
                {vote.resultSummary && <div className="vote-closed-result">{vote.resultSummary}</div>}
                {vote.counterName && <div style={{ fontSize: 11.5, color: 'var(--text-3)', marginBlockStart: 6 }}>{t('voting.countedBy')} {vote.counterName}</div>}
                {vote.status === 'Ratified' && <div style={{ fontSize: 11.5, color: 'var(--st-success-fg)', marginBlockStart: 6 }}>{t('voting.ratified')}</div>}
              </div>
            </section>
          )}

          {ctx.isOpen && canManage && (
            <Button variant="secondary" loading={closeVote.isPending} onClick={() => void onClose()}>
              <Icon name="lock" size={15} aria-hidden /> {t('voting.closeVoting')}
            </Button>
          )}
        </div>
      </div>
    </section>
  );
}

function VoterRow({ ballot, lang, t }: { ballot: Ballot; lang: string; t: ReturnType<typeof useTranslation>['t'] }) {
  const voted = !ballot.recused && ballot.choice !== null;
  const comment = ballot.comment ? (lang === 'ar' ? ballot.comment.ar : ballot.comment.en) : null;
  return (
    <div className="voter-row">
      <span className={`voter-avatar${voted ? ' voted' : ''}`} aria-hidden="true">{initials(ballot.voterName)}</span>
      <div className="voter-main">
        <div className="voter-name">{ballot.voterName}</div>
        {comment && <div className="voter-comment">{comment}</div>}
      </div>
      <div className="voter-status">
        {ballot.recused
          ? <StatusChip tone="warn" size="sm" label={t('voting.recused')} />
          : voted
            ? <StatusChip tone={optionTone(ballot.choice!)} size="sm" label={t(`voting.option.${ballot.choice}`, ballot.choice!)} />
            : <span className="voter-pending"><span className="voter-pending-dot" aria-hidden="true" />{t('voting.awaiting')}</span>}
      </div>
    </div>
  );
}

function BallotForm({ vote, cacheKey, myBallot }: { vote: VoteDetail; cacheKey: string | undefined; myBallot: Ballot }) {
  const { t } = useTranslation();
  const cast = useCastBallot(cacheKey);
  const change = useChangeBallot(cacheKey);
  const recuse = useRecuseVote(cacheKey);
  const already = hasCast(myBallot);
  const [choice, setChoice] = useState<string>(myBallot.choice ?? '');
  const [comment, setComment] = useState<string>(myBallot.comment?.en ?? '');
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const options = [...vote.options, ...(vote.allowAbstain ? [ABSTAIN] : [])];
  const busy = cast.isPending || change.isPending;
  const loc = (v: string): LocalizedText | null => (v.trim() ? { en: v.trim(), ar: v.trim() } : null);

  async function submit() {
    setError(null);
    const vars = { id: vote.id, choice, comment: loc(comment) };
    try {
      await (already ? change : cast).mutateAsync(vars);
      setConfirmOpen(false);
    } catch (err) {
      setConfirmOpen(false);
      setError(err instanceof ApiError ? err.problem?.title ?? t('voting.castError') : t('voting.castError'));
    }
  }

  return (
    <section className="vote-ballot">
      <div className="vote-ballot-h">
        <h2>{t('voting.yourBallot')}</h2>
        <div className="vote-ballot-sub"><Icon name="eye" size={12} aria-hidden /> {t('voting.ballotAttributed')}</div>
      </div>
      <div className="vote-ballot-body">
        {already && (
          <div className="ballot-recorded">
            <span style={{ color: 'var(--text-3)' }}>{t('voting.recordedVote')}</span>
            <StatusChip tone={optionTone(myBallot.choice!)} size="sm" label={t(`voting.option.${myBallot.choice}`, myBallot.choice!)} />
          </div>
        )}
        <div role="radiogroup" aria-label={t('voting.yourBallot')} className="ballot-options">
          {options.map((opt) => {
            const sel = choice === opt;
            return (
              <button
                key={opt}
                type="button"
                role="radio"
                aria-checked={sel}
                className={`ballot-option ${optionTone(opt)}${sel ? ' sel' : ''}`}
                onClick={() => setChoice(opt)}
              >
                <span className="ballot-ring" aria-hidden="true"><span className="ballot-ring-dot" /></span>
                <span className="ballot-option-label">{t(`voting.option.${opt}`, opt)}</span>
              </button>
            );
          })}
        </div>
        <Textarea
          className="ballot-comment"
          rows={2}
          value={comment}
          placeholder={t('voting.commentPh')}
          onChange={(e) => setComment(e.target.value)}
          aria-label={t('voting.commentPh')}
        />
        <Button className="ballot-cast" disabled={!choice} onClick={() => setConfirmOpen(true)}>
          <Icon name="clipboardCheck" size={16} aria-hidden /> {already ? t('voting.changeVote') : t('voting.castVote')}
        </Button>
        <div className="ballot-recuse-row">
          <Button variant="secondary" size="sm" className="ballot-recuse-warn" loading={recuse.isPending} onClick={() => void recuse.mutateAsync({ id: vote.id })}>
            <Icon name="x" size={13} aria-hidden /> {t('voting.recuseAction')}
          </Button>
          <span className="ballot-recuse-note">{t('voting.recuseNote')}</span>
        </div>
        <div className="ballot-note">{t('voting.castNote')}</div>
        {error && <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{error}</p>}
      </div>

      <Dialog
        open={confirmOpen}
        onClose={() => setConfirmOpen(false)}
        icon={<Icon name="clipboardCheck" size={20} aria-hidden />}
        title={t('voting.confirmTitle')}
        description={t('voting.confirmBody')}
        footer={
          <>
            <Button variant="secondary" onClick={() => setConfirmOpen(false)}>{t('voting.cancel')}</Button>
            <Button variant="primary" loading={busy} onClick={() => void submit()}>{t('voting.confirmCast')}</Button>
          </>
        }
      >
        <div className="ballot-recorded">
          <span style={{ color: 'var(--text-3)' }}>{t('voting.youAreVoting')}</span>
          {choice && <StatusChip tone={optionTone(choice)} size="sm" label={t(`voting.option.${choice}`, choice)} />}
        </div>
      </Dialog>
    </section>
  );
}
