/*
 * Meetings list (P6c) — index of meetings, each row links to its agenda builder, with a
 * "Schedule meeting" action. The design package has NO list screen (it opens straight to a
 * meeting), so this is behavior-necessary scaffolding: composed from the shared library
 * (Breadcrumb, Table, StatusChip, Button, Dialog, states) and kept deliberately plain.
 *
 * Notes (guardrail 11):
 *  - Scheduling now works: the committee is implicit server-side (single committee, CON-001),
 *    so the form (ScheduleMeetingDialog) only needs a title, a chair (from /api/members), the
 *    time window, and optional location/join URL.
 *  - Meeting/topic titles are user content in a single language; only chrome is i18n'd.
 *  - Dates are Gregorian, localized via Intl (guardrail 9).
 */
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useMeetings, type MeetingSummary } from '../../api/meetings';
import { Breadcrumb } from '../../components/ui/Breadcrumb';
import { Button } from '../../components/ui/Button';
import { Table, type Column } from '../../components/ui/Table';
import { StatusChip, type StatusTone } from '../../components/ui/StatusChip';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { ScheduleMeetingDialog } from './ScheduleMeetingDialog';
import './meetings.css';

function meetingTone(status: string): StatusTone {
  switch (status) {
    case 'Held':
    case 'Closed':
      return 'success';
    case 'Cancelled':
      return 'danger';
    case 'InProgress':
      return 'warn';
    default:
      return 'scheduled'; // Scheduled / draft-ish
  }
}

function agendaTone(status: string): StatusTone {
  return status === 'Published' ? 'success' : 'warn';
}

function useDateFmt() {
  const { i18n } = useTranslation();
  return (iso: string) => new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso));
}

export function MeetingsList() {
  const { t } = useTranslation();
  const fmt = useDateFmt();
  const { data, isLoading, isError, refetch } = useMeetings();
  const [scheduleOpen, setScheduleOpen] = useState(false);

  const columns: Column<MeetingSummary>[] = [
    { id: 'key', header: t('meetings.col.key'), width: '128px', cell: (m) => <span className="mt-key">{m.key}</span> },
    {
      id: 'title',
      header: t('meetings.col.title'),
      cell: (m) => (
        <Link className="mt-title-link" to={`/meetings/${m.key}`}>
          {m.title}
        </Link>
      ),
    },
    { id: 'when', header: t('meetings.col.when'), width: '200px', cell: (m) => <span className="mt-when">{fmt(m.scheduledStart)}</span> },
    { id: 'chair', header: t('meetings.col.chair'), width: '150px', cell: (m) => <span className="mt-chair">{m.chairName}</span> },
    { id: 'items', header: t('meetings.col.items'), width: '90px', cell: (m) => <span className="mt-items">{t('meetings.itemCount', { count: m.itemCount })}</span> },
    { id: 'status', header: t('meetings.col.status'), width: '120px', cell: (m) => <StatusChip tone={meetingTone(m.status)} label={t(`meetings.status.${m.status}`, { defaultValue: m.status })} /> },
    { id: 'agenda', header: t('meetings.col.agenda'), width: '120px', cell: (m) => <StatusChip tone={agendaTone(m.agendaStatus)} label={t(`meetings.agendaStatus.${m.agendaStatus}`, { defaultValue: m.agendaStatus })} /> },
  ];

  return (
    <section className="page">
      <Breadcrumb ariaLabel={t('meetings.title')} items={[{ label: t('meetings.title'), current: true }]} />

      <div className="mt-head">
        <div>
          <h1 className="page-title">{t('meetings.title')}</h1>
          <p className="mt-head-sub">{t('meetings.listSub')}</p>
        </div>
        <Button onClick={() => setScheduleOpen(true)}>
          <Icon name="plus" size={15} aria-hidden /> {t('meetings.schedule.action')}
        </Button>
      </div>

      {isLoading ? (
        <LoadingState />
      ) : isError ? (
        <ErrorState title={t('meetings.error.title')} body={t('meetings.error.body')} onRetry={() => refetch()} />
      ) : !data || data.length === 0 ? (
        <EmptyState icon="calendar" title={t('meetings.empty.title')} body={t('meetings.empty.body')} />
      ) : (
        <Table caption={t('meetings.tableCaption')} columns={columns} rows={data} getRowKey={(m) => m.id} />
      )}

      <ScheduleMeetingDialog open={scheduleOpen} onClose={() => setScheduleOpen(false)} />
    </section>
  );
}
