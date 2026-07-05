/*
 * Role dashboards (P12-PR2) — the app's home surface at `/`, closing AC-064/065/066.
 *
 * ONE page renders the variant for the signed-in user's highest committee role
 * (Chairman > Secretary > everyone-else = Committee). The design's "Viewing as…" role
 * tabs are a PREVIEW affordance in the .dc.html, not a live control — the running app
 * shows the caller's own role. Release checklist F-19 treats these as three distinct
 * dashboards (one AC each), so a Chairman seeing only chairman cards is correct — the
 * Committee dashboard is the member/fallback variant.
 *
 * Every number is composed client-side (dashboardAgg) from existing REST reads — no
 * server aggregation, no chart library (ADR-0022). Design→behavior reconciliations,
 * flagged in the progress log:
 *  - The design's personalized member cards (My topics/actions/votes, Mentions) are NOT
 *    rendered: they are design extras, not required by any AC (AC-064 is committee-WIDE),
 *    and Mentions has no backing system. The committee variant shows the AC-064 data instead.
 *  - AC-064's urgency breakdown and the committee "recent decisions" / action-status cards
 *    have no exact design card; they reuse the design's segment/stat/list patterns.
 *  - Chairman cards necessarily differ from the design's chairman cards (votes-awaiting,
 *    escalated risks/actions, deferred≥2) but reuse the same card patterns.
 */
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../auth/AcmpAuthContext';
import { useBacklog, type TopicSummary } from '../../api/topics';
import { useMeetings, type MeetingSummary } from '../../api/meetings';
import { useActionsRegister, type ActionSummary } from '../../api/actions';
import { useDecisionsRegister, type DecisionSummary } from '../../api/decisions';
import { useVotesRegister, type VoteSummary } from '../../api/votes';
import { useMinutesAwaiting } from '../../api/minutes';
import { useRisksRegister, type RiskSummary, type RiskExposure } from '../../api/risks';
import type { LocalizedText } from '../../api/actions';
import { StatusChip, type StatusTone } from '../../components/ui/StatusChip';
import { Icon } from '../../components/icons';
import { DashCard, SegmentBar, StatTiles, KeyList, DashState } from './dashboardCards';
import {
  backlogByBucket, backlogByUrgency, nextScheduledMeeting, actionStatusCounts,
  overdueBeyondThreshold, deferredAtLeastTwice, slaBreached, daysOverdue,
} from './dashboardAgg';
import './dashboard.css';

// ponytail: one full read per register = the whole set at committee scale (≤20 users); no page-loop.
const ALL = 500;

const pickText = (l: LocalizedText, lang: string) => (lang === 'ar' ? l.ar : l.en);
const fmtDateTime = (iso: string, lang: string) =>
  new Intl.DateTimeFormat(lang, { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(iso));

const AGENDA_TONE: Record<string, StatusTone> = { Draft: 'warn', Published: 'success' };
const EXPOSURE_TONE: Record<RiskExposure, StatusTone> = { Low: 'neutral', Medium: 'warn', High: 'danger', Critical: 'danger' };

export default function RoleDashboard() {
  const { t, i18n } = useTranslation();
  const { displayName, roles } = useAuth();
  const variant = roles.includes('chairman') ? 'chairman' : roles.includes('secretary') ? 'secretary' : 'committee';

  const now = new Date();
  const hour = now.getHours();
  const greetKey = hour < 12 ? 'morning' : hour < 18 ? 'afternoon' : 'evening';
  const dateLabel = new Intl.DateTimeFormat(i18n.language, { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' }).format(now);

  return (
    <section className="page dash">
      <header className="dash-head">
        <div>
          <h1 className="dash-greeting">{t(`dashboard.greeting.${greetKey}`, { name: displayName })}</h1>
          <p className="dash-sub">{t(`dashboard.sub.${variant}`)}</p>
        </div>
        <span className="dash-today"><Icon name="calendar" size={14} aria-hidden />{dateLabel}</span>
      </header>

      {variant === 'chairman' ? (
        <ChairmanDashboard now={now} />
      ) : variant === 'secretary' ? (
        <SecretaryDashboard now={now} />
      ) : (
        <CommitteeDashboard now={now} />
      )}
    </section>
  );
}

// ---------------------------------------------------------------- Committee (AC-064)
function CommitteeDashboard({ now }: { now: Date }) {
  const backlog = useBacklog({ pageSize: ALL });
  const meetings = useMeetings();
  const actions = useActionsRegister({ pageSize: ALL });
  const decisions = useDecisionsRegister({ status: 'Issued', limit: 5 });

  const ready = backlog.data && meetings.data && actions.data && decisions.data;
  return (
    <DashState
      isLoading={backlog.isLoading || meetings.isLoading || actions.isLoading || decisions.isLoading}
      isError={backlog.isError || meetings.isError || actions.isError || decisions.isError}
      onRetry={() => { void backlog.refetch(); void meetings.refetch(); void actions.refetch(); void decisions.refetch(); }}
    >
      {ready && (
        <div className="dash-grid">
          <BacklogCard topics={backlog.data!.items} span={8} />
          <NextMeetingCard meeting={nextScheduledMeeting(meetings.data!, now)} span={4} />
          <ActionStatusCard actions={actions.data!.items} span={6} />
          <DecisionsCard decisions={decisions.data!} span={6} />
        </div>
      )}
    </DashState>
  );
}

// ---------------------------------------------------------------- Secretary (AC-065)
function SecretaryDashboard({ now }: { now: Date }) {
  const backlog = useBacklog({ pageSize: ALL });
  const meetings = useMeetings();
  const minutes = useMinutesAwaiting();
  const actions = useActionsRegister({ pageSize: ALL });

  const ready = backlog.data && meetings.data && minutes.data && actions.data;
  return (
    <DashState
      isLoading={backlog.isLoading || meetings.isLoading || minutes.isLoading || actions.isLoading}
      isError={backlog.isError || meetings.isError || minutes.isError || actions.isError}
      onRetry={() => { void backlog.refetch(); void meetings.refetch(); void minutes.refetch(); void actions.refetch(); }}
    >
      {ready && (
        <div className="dash-grid">
          <BacklogCard topics={backlog.data!.items} span={8} />
          <NextMeetingCard meeting={nextScheduledMeeting(meetings.data!, now)} span={4} />
          <SecretaryQueueCard
            triage={backlog.data!.items.filter((x) => x.status === 'Triage').length}
            moms={minutes.data!.length}
            escalated={overdueBeyondThreshold(actions.data!.items, now).length}
            span={6}
          />
          <SlaCard topics={slaBreached(backlog.data!.items)} span={6} />
        </div>
      )}
    </DashState>
  );
}

// ---------------------------------------------------------------- Chairman (AC-066)
function ChairmanDashboard({ now }: { now: Date }) {
  const votes = useVotesRegister({ status: 'Closed' }); // Closed but not yet Ratified = awaiting approval
  const risks = useRisksRegister({ statuses: ['Escalated'], pageSize: ALL });
  const actions = useActionsRegister({ pageSize: ALL });
  const deferred = useBacklog({ includeClosed: true, pageSize: ALL }); // all-time: a deferred topic may now be closed

  const ready = votes.data && risks.data && actions.data && deferred.data;
  return (
    <DashState
      isLoading={votes.isLoading || risks.isLoading || actions.isLoading || deferred.isLoading}
      isError={votes.isError || risks.isError || actions.isError || deferred.isError}
      onRetry={() => { void votes.refetch(); void risks.refetch(); void actions.refetch(); void deferred.refetch(); }}
    >
      {ready && (
        <div className="dash-grid">
          <VotesAwaitingCard votes={votes.data!} span={6} />
          <EscalatedRisksCard risks={risks.data!.items} span={6} />
          <EscalatedActionsCard actions={overdueBeyondThreshold(actions.data!.items, now)} now={now} span={6} />
          <DeferredTopicsCard topics={deferredAtLeastTwice(deferred.data!.items)} span={6} />
        </div>
      )}
    </DashState>
  );
}

// ---------------------------------------------------------------- shared cards
function DrillLink({ to, label }: { to: string; label: string }) {
  return (
    <Link className="dash-link" to={to}>
      {label}
      <Icon name="chevron" size={13} className="dir-flip" aria-hidden />
    </Link>
  );
}

function BacklogCard({ topics, span }: { topics: TopicSummary[]; span: number }) {
  const { t } = useTranslation();
  const { segments, total } = backlogByBucket(topics);
  const urgency = backlogByUrgency(topics);
  const segs = segments.map((s) => ({ key: s.bucket, label: t(`dashboard.bucket.${s.bucket}`), count: s.count, tone: s.tone }));
  return (
    <DashCard span={span} title={t('dashboard.card.backlog')} headerRight={<DrillLink to="/backlog" label={t('dashboard.openBacklog')} />}>
      <div className="dash-kpi"><span className="dash-kpi-n">{total}</span><span className="dash-kpi-l">{t('dashboard.activeTopics')}</span></div>
      <SegmentBar segments={segs} total={total} />
      <div className="dash-urgency">
        {urgency.map((u) => (
          <span key={u.urgency}><b>{u.count}</b> {t(`dashboard.urgency.${u.urgency}`, u.urgency)}</span>
        ))}
      </div>
    </DashCard>
  );
}

function NextMeetingCard({ meeting, span }: { meeting: MeetingSummary | null; span: number }) {
  const { t, i18n } = useTranslation();
  return (
    <DashCard span={span} title={t('dashboard.card.nextMeeting')}>
      {meeting ? (
        <>
          <div className="dash-mtg-title">{meeting.title}</div>
          <div className="dash-mtg-when">{fmtDateTime(meeting.scheduledStart, i18n.language)} · {meeting.key}</div>
          <div className="dash-mtg-meta">
            <StatusChip tone={AGENDA_TONE[meeting.agendaStatus] ?? 'neutral'} label={t(`dashboard.agenda.${meeting.agendaStatus}`, meeting.agendaStatus)} size="sm" />
            <span>{t('dashboard.itemsCount', { count: meeting.itemCount })}</span>
          </div>
          <Link className="dash-btn" to={`/meetings/${meeting.key}/agenda`}>{t('dashboard.openAgenda')}</Link>
        </>
      ) : (
        <p className="dash-empty">{t('dashboard.noMeeting')}</p>
      )}
    </DashCard>
  );
}

function ActionStatusCard({ actions, span }: { actions: ActionSummary[]; span: number }) {
  const { t } = useTranslation();
  const c = actionStatusCounts(actions);
  return (
    <DashCard span={span} title={t('dashboard.card.actions')} headerRight={<DrillLink to="/actions" label={t('dashboard.viewAll')} />}>
      <StatTiles tiles={[
        { key: 'open', value: c.open, label: t('dashboard.action.Open') },
        { key: 'inProgress', value: c.inProgress, label: t('dashboard.action.InProgress') },
        { key: 'blocked', value: c.blocked, label: t('dashboard.action.Blocked'), tone: c.blocked > 0 ? 'danger' : undefined },
        { key: 'overdue', value: c.overdue, label: t('dashboard.action.overdue'), tone: c.overdue > 0 ? 'warn' : undefined },
      ]} />
    </DashCard>
  );
}

function DecisionsCard({ decisions, span }: { decisions: DecisionSummary[]; span: number }) {
  const { t, i18n } = useTranslation();
  const rows = decisions.map((d) => ({ key: d.key, to: `/decisions/${d.key}`, primary: pickText(d.title, i18n.language) }));
  return (
    <DashCard span={span} title={t('dashboard.card.recentDecisions')} headerRight={<DrillLink to="/decisions" label={t('dashboard.viewAll')} />}>
      <KeyList rows={rows} emptyLabel={t('dashboard.noDecisions')} />
    </DashCard>
  );
}

function SecretaryQueueCard({ triage, moms, escalated, span }: { triage: number; moms: number; escalated: number; span: number }) {
  const { t } = useTranslation();
  return (
    <DashCard span={span} title={t('dashboard.card.secretaryQueue')}>
      <StatTiles tiles={[
        { key: 'triage', value: triage, label: t('dashboard.queue.triage'), tone: triage > 0 ? 'info' : undefined },
        { key: 'moms', value: moms, label: t('dashboard.queue.moms'), tone: moms > 0 ? 'warn' : undefined },
        { key: 'escalated', value: escalated, label: t('dashboard.queue.escalated'), tone: escalated > 0 ? 'danger' : undefined },
      ]} />
    </DashCard>
  );
}

function AgeBadge({ days }: { days: number }) {
  const { t } = useTranslation();
  return <span className="dash-badge warn">{t('dashboard.ageDays', { count: days })}</span>;
}

function SlaCard({ topics, span }: { topics: TopicSummary[]; span: number }) {
  const { t } = useTranslation();
  const rows = topics.map((x) => ({ key: x.key, to: `/topics/${x.key}`, primary: x.title, right: <AgeBadge days={x.ageDays} /> }));
  return (
    <DashCard span={span} title={t('dashboard.card.sla')} headerRight={rows.length ? <span className="dash-badge warn">{rows.length}</span> : undefined}>
      <KeyList rows={rows} emptyLabel={t('dashboard.noSla')} />
    </DashCard>
  );
}

function VotesAwaitingCard({ votes, span }: { votes: VoteSummary[]; span: number }) {
  const { t } = useTranslation();
  // Votes carry no title on the wire (VoteSummary) — the key + "awaiting" row links to the vote screen.
  const rows = votes.map((v) => ({ key: v.key, to: `/votes/${v.key}`, primary: t('dashboard.voteAwaitingRow') }));
  return (
    <DashCard span={span} title={t('dashboard.card.votesAwaiting')} headerRight={rows.length ? <span className="dash-badge warn">{rows.length}</span> : undefined}>
      <KeyList rows={rows} emptyLabel={t('dashboard.noVotes')} />
    </DashCard>
  );
}

function EscalatedRisksCard({ risks, span }: { risks: RiskSummary[]; span: number }) {
  const { t, i18n } = useTranslation();
  const rows = risks.map((r) => ({
    key: r.key, to: `/risks/${r.key}`, primary: pickText(r.title, i18n.language),
    right: <StatusChip tone={EXPOSURE_TONE[r.exposure]} label={t(`dashboard.exposure.${r.exposure}`, r.exposure)} size="sm" />,
  }));
  return (
    <DashCard span={span} title={t('dashboard.card.escalatedRisks')} headerRight={rows.length ? <span className="dash-badge danger">{rows.length}</span> : undefined}>
      <KeyList rows={rows} emptyLabel={t('dashboard.noRisks')} />
    </DashCard>
  );
}

function EscalatedActionsCard({ actions, now, span }: { actions: ActionSummary[]; now: Date; span: number }) {
  const { t, i18n } = useTranslation();
  const rows = actions.map((a) => ({
    key: a.key, to: `/actions/${a.key}`, primary: pickText(a.title, i18n.language),
    right: <span className="dash-badge danger">{t('dashboard.overdueDays', { count: daysOverdue(a.dueDate, a.isOverdue, now) })}</span>,
  }));
  return (
    <DashCard span={span} title={t('dashboard.card.escalatedActions')} headerRight={rows.length ? <span className="dash-badge danger">{rows.length}</span> : undefined}>
      <KeyList rows={rows} emptyLabel={t('dashboard.noActions')} />
    </DashCard>
  );
}

function DeferredTopicsCard({ topics, span }: { topics: TopicSummary[]; span: number }) {
  const { t } = useTranslation();
  const rows = topics.map((x) => ({
    key: x.key, to: `/topics/${x.key}`, primary: x.title,
    right: <span className="dash-badge warn">{t('dashboard.deferredTimes', { count: x.timesDeferred })}</span>,
  }));
  return (
    <DashCard span={span} title={t('dashboard.card.deferredTopics')} headerRight={rows.length ? <span className="dash-badge warn">{rows.length}</span> : undefined}>
      <KeyList rows={rows} emptyLabel={t('dashboard.noDeferred')} />
    </DashCard>
  );
}
