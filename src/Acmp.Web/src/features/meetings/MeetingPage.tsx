/*
 * Meeting page (P6 meeting-detail IA) — the /meetings/:key route. Owns the page breadcrumb, a
 * lifecycle banner, and a 4-tab in-page switcher: Agenda · Meeting · Minutes · Recording.
 *
 * IA decision (operator GO, 2026-06): the conduct surface comes from "ACMP Agenda & Meeting.dc.html"
 * (Agenda builder/viewer + Meeting workspace + Minutes); Recording + the lifecycle "state views"
 * come from "ACMP Meetings.dc.html". The Meetings.dc.html "Overview" landing tab is intentionally
 * dropped — its lifecycle banner moves above the tabs; its readiness card + quick links are dropped.
 *
 * State gating (the spec the tests pin) — meeting.status × agenda.status:
 *   Scheduled · Draft            → banner notReady · Agenda=builder(editable) · Meeting="publish first"
 *   Scheduled · Published/Locked → (ready, no banner) · Agenda=viewer · Meeting="Start"
 *   InProgress                   → banner inProgress · Agenda=viewer · Meeting=live workspace
 *   Held/Closed                  → banner concluded · Agenda=viewer · Meeting=concluded recap
 *   Cancelled                    → banner cancelled · Agenda=viewer · Meeting=cancelled note
 * The Agenda viewer (not builder) once published/started is the fix for the bug where a started
 * meeting still showed an editable agenda builder.
 *
 * Minutes = honest placeholder until the MoM module (P7). Recording = honest placeholder until the
 * Webex adapter (P13); both tabs render so the bar matches the design, flagged as deferred.
 */
import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useMeetingDetail, useStartMeeting } from '../../api/meetings';
import { ApiError } from '../../api/apiClient';
import { AREAS } from '../../nav/navModel';
import { Breadcrumb } from '../../components/ui/Breadcrumb';
import { Tabs, type TabItem } from '../../components/ui/Tabs';
import { Button } from '../../components/ui/Button';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { Icon, type IconName } from '../../components/icons';
import { AgendaBuilder } from './AgendaBuilder';
import { MeetingWorkspace } from './MeetingWorkspace';
import './meetings.css';

type TabId = 'agenda' | 'meeting' | 'minutes' | 'recording';
type Phase = 'notReady' | 'ready' | 'inProgress' | 'concluded' | 'cancelled';

/** Derive the lifecycle phase from meeting + agenda status (the single source the gating reads). */
function lifecyclePhase(meetingStatus: string, agendaPublished: boolean): Phase {
  if (meetingStatus === 'Cancelled') return 'cancelled';
  if (meetingStatus === 'Held' || meetingStatus === 'Closed') return 'concluded';
  if (meetingStatus === 'InProgress') return 'inProgress';
  return agendaPublished ? 'ready' : 'notReady';
}

export function MeetingPage() {
  const { key } = useParams();
  const { t } = useTranslation();
  const meetingQuery = useMeetingDetail(key);
  const start = useStartMeeting(key);
  const [tab, setTab] = useState<TabId | null>(null);

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
        <Breadcrumb
          ariaLabel={t('meetings.title')}
          items={[{ label: t('meetings.title'), href: AREAS.agenda.path }, { label: key ?? '', current: true }]}
        />
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
  // Editable only while the meeting is Scheduled AND the agenda is still Draft.
  const agendaEditable = meeting.status === 'Scheduled' && !agendaPublished;
  const phase = lifecyclePhase(meeting.status, agendaPublished);

  // Default tab follows the phase until the user picks one (InProgress/concluded open on Meeting).
  const defaultTab: TabId = phase === 'inProgress' || phase === 'concluded' ? 'meeting' : 'agenda';
  const activeTab: TabId = tab ?? defaultTab;

  const tabs: TabItem[] = [
    { id: 'agenda', label: t('meetings.tab.agenda') },
    { id: 'meeting', label: t('meetings.tab.meeting') },
    { id: 'minutes', label: t('meetings.tab.minutes') },
    { id: 'recording', label: t('meetings.tab.recording') },
  ];

  return (
    <section className="page mt-detail">
      <Breadcrumb
        ariaLabel={t('meetings.title')}
        items={[{ label: t('meetings.title'), href: AREAS.agenda.path }, { label: meeting.key, current: true }]}
      />

      <LifecycleBanner phase={phase} />

      <Tabs items={tabs} value={activeTab} onValueChange={(id) => setTab(id as TabId)} ariaLabel={meeting.key} />

      {activeTab === 'agenda' && <AgendaBuilder readOnly={!agendaEditable} />}
      {activeTab === 'meeting' && (
        <MeetingTab
          status={meeting.status}
          agendaPublished={agendaPublished}
          starting={start.isPending}
          onStart={() => start.mutate({ meetingId: meeting.id }, { onSuccess: () => setTab('meeting') })}
        />
      )}
      {activeTab === 'minutes' && (
        <MeetingGate icon="doc" title={t('meetings.minutesTab.title')} body={t('meetings.minutesTab.body')} />
      )}
      {activeTab === 'recording' && (
        <MeetingGate icon="session" title={t('meetings.recordingTab.title')} body={t('meetings.recordingTab.body')} />
      )}
    </section>
  );
}

const BANNER_ICON: Record<Exclude<Phase, 'ready'>, IconName> = {
  notReady: 'warnTriangle',
  inProgress: 'clock',
  concluded: 'checkCircle',
  cancelled: 'x',
};

/** Status-context banner above the tabs (the "state views" from ACMP Meetings.dc.html). */
function LifecycleBanner({ phase }: { phase: Phase }) {
  const { t } = useTranslation();
  if (phase === 'ready') return null; // a scheduled+published meeting needs no banner (design).
  return (
    <div className={`mt-lifecycle mt-lifecycle-${phase}`} role="status">
      <span className="mt-lifecycle-icon" aria-hidden="true">
        <Icon name={BANNER_ICON[phase]} size={18} />
      </span>
      <div className="mt-lifecycle-text">
        <p className="mt-lifecycle-title">{t(`meetings.banner.${phase}.title`)}</p>
        <p className="mt-lifecycle-body">{t(`meetings.banner.${phase}.body`)}</p>
      </div>
    </div>
  );
}

/** The Meeting tab content: live workspace when running, a lifecycle prompt otherwise. */
function MeetingTab({
  status,
  agendaPublished,
  starting,
  onStart,
}: {
  status: string;
  agendaPublished: boolean;
  starting: boolean;
  onStart: () => void;
}) {
  const { t } = useTranslation();

  if (status === 'InProgress') return <MeetingWorkspace />;

  if (status === 'Cancelled') {
    return <MeetingGate icon="x" title={t('meetings.cancelled.title')} body={t('meetings.cancelled.body')} />;
  }

  if (status === 'Held' || status === 'Closed') {
    return <MeetingGate icon="checkCircle" title={t('meetings.concluded.title')} body={t('meetings.concluded.body')} />;
  }

  if (agendaPublished) {
    return (
      <MeetingGate
        icon="send"
        title={t('meetings.ready.title')}
        body={t('meetings.ready.body')}
        action={
          <Button onClick={onStart} loading={starting}>
            {t('meetings.start')}
          </Button>
        }
      />
    );
  }

  return <MeetingGate icon="calendar" title={t('meetings.notReady.title')} body={t('meetings.notReady.body')} />;
}

function MeetingGate({
  icon,
  title,
  body,
  action,
}: {
  icon: IconName;
  title: string;
  body: string;
  action?: React.ReactNode;
}) {
  return (
    <div className="mt-gate" role="status">
      <div className="mt-gate-icon">
        <Icon name={icon} size={22} aria-hidden />
      </div>
      <p className="mt-gate-title">{title}</p>
      <p className="mt-gate-body">{body}</p>
      {action && <div className="mt-gate-action">{action}</div>}
    </div>
  );
}
