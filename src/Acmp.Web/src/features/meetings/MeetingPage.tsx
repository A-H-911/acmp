/*
 * Meeting page (P6d) — the /meetings/:key route. Owns the page breadcrumb and a shared
 * in-page Tabs switcher between the "Agenda builder" (P6c) and the live "Meeting" workspace
 * (P6d), plus the meeting lifecycle control.
 *
 * Design↔behavior reconciliations (visual SoT = design; behavior SoT = the package):
 *  - The design puts the Agenda/Meeting (and Minutes, P7) tabs in the top bar; we use the
 *    shared in-page Tabs control. The breadcrumb therefore drops the design's third segment
 *    ("Agenda builder") — the active tab now conveys it.
 *  - Default tab follows status: InProgress → Meeting; otherwise → Agenda builder.
 *  - Lifecycle: a Scheduled meeting whose agenda is Published shows a "Start meeting" button
 *    (POST /start; the server enforces W7 — needs a Published agenda). A Draft agenda shows a
 *    calm "publish & start first" prompt instead of the live workspace.
 *  - Minutes (P7): once a meeting is Held/Closed the Meeting tab shows a concluded prompt
 *    rather than a live workspace (minutes capture lands in P7).
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

type TabId = 'agenda' | 'meeting';

export function MeetingPage() {
  const { key } = useParams();
  const { t } = useTranslation();
  const meetingQuery = useMeetingDetail(key);
  const start = useStartMeeting(key);
  const [tab, setTab] = useState<TabId | null>(null);

  const meeting = meetingQuery.data;
  // Derive the default from status until the user explicitly picks a tab (so an InProgress
  // meeting opens on the live workspace without an effect).
  const activeTab: TabId = tab ?? (meeting?.status === 'InProgress' ? 'meeting' : 'agenda');

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

  const tabs: TabItem[] = [
    { id: 'agenda', label: t('meetings.agendaBuilder') },
    { id: 'meeting', label: t('meetings.meeting') },
  ];

  return (
    <section className="page">
      <Breadcrumb
        ariaLabel={t('meetings.title')}
        items={[{ label: t('meetings.title'), href: AREAS.agenda.path }, { label: meeting.key, current: true }]}
      />
      <Tabs items={tabs} value={activeTab} onValueChange={(id) => setTab(id as TabId)} ariaLabel={meeting.key} />

      {activeTab === 'agenda' ? (
        <AgendaBuilder />
      ) : (
        <MeetingTab
          status={meeting.status}
          agendaPublished={meeting.agenda?.status === 'Published'}
          starting={start.isPending}
          onStart={() => start.mutate({ meetingId: meeting.id }, { onSuccess: () => setTab('meeting') })}
        />
      )}
    </section>
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
