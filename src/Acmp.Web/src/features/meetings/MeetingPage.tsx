/*
 * Meeting SHELL (P6a round-2 IA) — the /meetings/:key layout route. Owns the chrome every
 * meeting sub-page shares: a header card (key chip + status chip + title + when·type·mode meta +
 * the lifecycle primary action) and a 6-tab deep-linkable NavLink strip, then an <Outlet/> for the
 * routed content.
 *
 * RD-08 ownership (ACMP Usage Map): Meetings owns the SHELL + the overview/recording surfaces and
 * route-denial (the global auth gate — meetings routes carry no extra role gate, so there is a
 * single denied state, not a duplicated one). Agenda & Meeting owns the CONTENT surfaces
 * (agenda / conduct / minutes). The shell fetches the meeting once for its header; each child route
 * re-reads the same cached detail (react-query dedupes), so route elements stay prop-free.
 *
 * NV-08: the conduct surface is a runtime composition of Attendance + Notes while the meeting is
 * InProgress, each deep-linkable. Both /attendance and /notes render the same MeetingWorkspace; the
 * tab strip just marks which one is active. MeetingConduct gates that composition on the lifecycle.
 *
 * Blessed deviation (guardrail 14): the design reaches Recording via an overview quick-link; the IA
 * promotes it to a 6th peer tab (Overview·Agenda·Attendance·Notes·Minutes·Recording) per NV-08 +
 * the route map. Recorded in the progress log / acceptance audit.
 */
import { useParams, useNavigate, NavLink, Outlet } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useMeetingDetail, useStartMeeting } from '../../api/meetings';
import { ApiError } from '../../api/apiClient';
import { Button } from '../../components/ui/Button';
import { StatusChip } from '../../components/ui/StatusChip';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { Icon, type IconName } from '../../components/icons';
import { MeetingGate } from './MeetingGate';
import { MeetingWorkspace } from './MeetingWorkspace';
import { meetingTone, lifecyclePhase } from './meetingStatus';
import './meetings.css';

/** Mode → glyph: in-person reads as people, hybrid/remote as video. */
const MODE_ICON: Record<string, IconName> = { InPerson: 'usersGroup', Hybrid: 'video', Remote: 'video' };

const TABS: { to: string; end?: boolean; labelKey: string }[] = [
  { to: '.', end: true, labelKey: 'meetings.tab.overview' },
  { to: 'agenda', labelKey: 'meetings.tab.agenda' },
  { to: 'attendance', labelKey: 'meetings.tab.attendance' },
  { to: 'notes', labelKey: 'meetings.tab.notes' },
  { to: 'minutes', labelKey: 'meetings.tab.minutes' },
  { to: 'recording', labelKey: 'meetings.tab.recording' },
];

export function MeetingPage() {
  const { key } = useParams();
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const meetingQuery = useMeetingDetail(key);
  const start = useStartMeeting(key);

  const meeting = meetingQuery.data;

  if (meetingQuery.isLoading) {
    return (
      <section className="page">
        <LoadingState />
      </section>
    );
  }
  if (meetingQuery.isError || !meeting) {
    const notFound = meetingQuery.error instanceof ApiError && meetingQuery.error.status === 404;
    return (
      <section className="page">
        {notFound ? (
          <EmptyState icon="calendar" title={t('meetings.notFound.title')} body={t('meetings.notFound.body')} />
        ) : (
          <ErrorState title={t('meetings.error.title')} body={t('meetings.error.body')} onRetry={() => meetingQuery.refetch()} />
        )}
      </section>
    );
  }

  const agendaStatus = meeting.agenda?.status;
  const agendaPublished = agendaStatus === 'Published' || agendaStatus === 'Locked';
  const phase = lifecyclePhase(meeting.status, agendaPublished);
  const when = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(meeting.scheduledStart));

  return (
    <section className="page mt-shell">
      <header className="mt-shell-head">
        <div className="mt-shell-toprow">
          <div className="mt-shell-ident">
            <span className="mt-key">{meeting.key}</span>
            <StatusChip tone={meetingTone(meeting.status)} label={t(`meetings.status.${meeting.status}`, { defaultValue: meeting.status })} size="sm" />
          </div>
          <PrimaryAction
            phase={phase}
            starting={start.isPending}
            onBuildAgenda={() => navigate('agenda')}
            onStart={() => start.mutate({ meetingId: meeting.id })}
            onOpenNotes={() => navigate('notes')}
            onReviewMinutes={() => navigate('minutes')}
            onReschedule={() => navigate('/meetings/new')}
          />
        </div>
        <h1 className="page-title mt-shell-title">{meeting.title}</h1>
        <div className="mt-shell-meta">
          <span><Icon name="calendar" size={14} aria-hidden /> {when}</span>
          <span><Icon name="template" size={14} aria-hidden /> {t(`meetings.meetingType.${meeting.type}`, { defaultValue: meeting.type })}</span>
          <span><Icon name={MODE_ICON[meeting.mode] ?? 'session'} size={14} aria-hidden /> {t(`meetings.meetingMode.${meeting.mode}`, { defaultValue: meeting.mode })}</span>
        </div>
      </header>

      <nav className="mt-tabs" aria-label={t('meetings.tabsLabel')}>
        {TABS.map((tab) => (
          <NavLink key={tab.to} to={tab.to} end={tab.end} className={({ isActive }) => `mt-tab ${isActive ? 'active' : ''}`}>
            {t(tab.labelKey)}
          </NavLink>
        ))}
      </nav>

      <Outlet />
    </section>
  );
}

/** The lifecycle primary action in the header (ACMP Meetings.dc.html lcMap). Primary while there is
 *  an active step (build / start / open notes); ghost once the meeting is concluded or cancelled. */
function PrimaryAction({
  phase,
  starting,
  onBuildAgenda,
  onStart,
  onOpenNotes,
  onReviewMinutes,
  onReschedule,
}: {
  phase: ReturnType<typeof lifecyclePhase>;
  starting: boolean;
  onBuildAgenda: () => void;
  onStart: () => void;
  onOpenNotes: () => void;
  onReviewMinutes: () => void;
  onReschedule: () => void;
}) {
  const { t } = useTranslation();
  switch (phase) {
    case 'notReady':
      return (
        <Button onClick={onBuildAgenda}>
          <Icon name="backlog" size={15} aria-hidden /> {t('meetings.buildAgenda')}
        </Button>
      );
    case 'ready':
      return (
        <Button onClick={onStart} loading={starting}>
          <Icon name="send" size={15} aria-hidden /> {t('meetings.start')}
        </Button>
      );
    case 'inProgress':
      return (
        <Button onClick={onOpenNotes}>
          <Icon name="doc" size={15} aria-hidden /> {t('meetings.openNotes')}
        </Button>
      );
    case 'concluded':
      return (
        <Button variant="secondary" onClick={onReviewMinutes}>
          <Icon name="doc" size={15} aria-hidden /> {t('meetings.reviewMinutes')}
        </Button>
      );
    case 'cancelled':
      return (
        <Button variant="secondary" onClick={onReschedule}>
          <Icon name="calendar" size={15} aria-hidden /> {t('meetings.reschedule')}
        </Button>
      );
  }
}

/*
 * Conduct route (/attendance + /notes) — Agenda & Meeting's live-conduct composition. While the
 * meeting is InProgress both routes render the full MeetingWorkspace (attendance + notes in one
 * 3-column screen); otherwise the lifecycle gate explains why the live workspace isn't open yet.
 * Re-reads the cached detail; the shell owns loading/error so a null here just means "not ready".
 */
export function MeetingConduct() {
  const { key } = useParams();
  const { t } = useTranslation();
  const { data: meeting } = useMeetingDetail(key);
  if (!meeting) return null;

  if (meeting.status === 'InProgress') return <MeetingWorkspace />;

  if (meeting.status === 'Cancelled') {
    return <MeetingGate icon="x" title={t('meetings.cancelled.title')} body={t('meetings.cancelled.body')} />;
  }
  if (meeting.status === 'Held' || meeting.status === 'Closed') {
    return <MeetingGate icon="checkCircle" title={t('meetings.concluded.title')} body={t('meetings.concluded.body')} />;
  }
  const agendaStatus = meeting.agenda?.status;
  const ready = agendaStatus === 'Published' || agendaStatus === 'Locked';
  return ready ? (
    <MeetingGate icon="send" title={t('meetings.ready.title')} body={t('meetings.ready.body')} />
  ) : (
    <MeetingGate icon="calendar" title={t('meetings.notReady.title')} body={t('meetings.notReady.body')} />
  );
}
