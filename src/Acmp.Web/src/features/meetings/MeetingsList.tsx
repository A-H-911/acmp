/*
 * Meetings list (P6 / PR-B) — the design's `isList` screen (ACMP Meetings.dc.html ~L117):
 * an Upcoming / Past split, each a card-table with columns ID · When · Title · Type · Status,
 * plus a List ⇄ Calendar view toggle and a month grid.
 *
 * Deviations (guardrail 14, reconciled):
 *  - We KEEP an Agenda-status chip column the design omits — it carries the agenda lifecycle
 *    (PR #31 semantic tones) that the committee tracks from the list. Deliberate, operator-approved.
 *  - The mock's filter chips + "Saved views" are static decoration with no backend, so they are
 *    omitted rather than faked (same call as the agenda new-vs-link radio). When filtering is
 *    needed it can be added client-side over the loaded list.
 *  - Rows link via the title (one focusable link per row) instead of the mock's whole-row <button>,
 *    which would be invalid/ambiguous markup inside a real <table>.
 * Meeting/topic titles are user content in a single language; only chrome is i18n'd. Dates are
 * Gregorian, localized via Intl (guardrail 9).
 */
import { useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useMeetings, type MeetingSummary } from '../../api/meetings';
import { Button } from '../../components/ui/Button';
import { Segmented } from '../../components/ui/Segmented';
import { Table, type Column } from '../../components/ui/Table';
import { StatusChip } from '../../components/ui/StatusChip';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { agendaTone } from './agendaStatus';
import { meetingTone, isConcluded } from './meetingStatus';
import { MeetingsCalendar } from './MeetingsCalendar';
import './meetings.css';

type ViewMode = 'list' | 'calendar';

function useDateFmt() {
  const { i18n } = useTranslation();
  return (iso: string) => new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso));
}

export function MeetingsList() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const fmt = useDateFmt();
  const { data, isLoading, isError, refetch } = useMeetings();
  const [view, setView] = useState<ViewMode>('list');

  const { upcoming, past } = useMemo(() => {
    const rows = data ?? [];
    const up = rows.filter((m) => !isConcluded(m.status)).sort((a, b) => a.scheduledStart.localeCompare(b.scheduledStart));
    const pa = rows.filter((m) => isConcluded(m.status)).sort((a, b) => b.scheduledStart.localeCompare(a.scheduledStart));
    return { upcoming: up, past: pa };
  }, [data]);

  const columns: Column<MeetingSummary>[] = [
    { id: 'key', header: t('meetings.col.key'), width: '128px', cell: (m) => <span className="mt-key">{m.key}</span> },
    { id: 'when', header: t('meetings.col.when'), width: '190px', cell: (m) => <span className="mt-when">{fmt(m.scheduledStart)}</span> },
    {
      id: 'title',
      header: t('meetings.col.title'),
      cell: (m) => (
        <Link className="mt-title-link" to={`/meetings/${m.key}`}>
          {m.title}
        </Link>
      ),
    },
    { id: 'type', header: t('meetings.col.type'), width: '120px', cell: (m) => <span className="mt-when">{t(`meetings.meetingType.${m.type}`, { defaultValue: m.type })}</span> },
    { id: 'status', header: t('meetings.col.status'), width: '120px', cell: (m) => <StatusChip tone={meetingTone(m.status)} label={t(`meetings.status.${m.status}`, { defaultValue: m.status })} size="sm" /> },
    { id: 'agenda', header: t('meetings.col.agenda'), width: '120px', cell: (m) => <StatusChip tone={agendaTone(m.agendaStatus)} label={t(`meetings.agendaStatus.${m.agendaStatus}`, { defaultValue: m.agendaStatus })} size="sm" /> },
  ];

  const hasMeetings = (data?.length ?? 0) > 0;

  return (
    <section className="page">
      <div className="mt-head">
        <div>
          <h1 className="page-title">{t('meetings.title')}</h1>
          <p className="mt-head-sub">
            {hasMeetings ? t('meetings.listCount', { upcoming: upcoming.length, past: past.length }) : t('meetings.listSub')}
          </p>
        </div>
        <Button onClick={() => navigate('/meetings/new')}>
          <Icon name="plus" size={15} aria-hidden /> {t('meetings.schedule.action')}
        </Button>
      </div>

      <div className="mt-toolbar">
        <Segmented
          ariaLabel={t('meetings.viewToggle')}
          value={view}
          onValueChange={(id) => setView(id as ViewMode)}
          items={[
            { id: 'list', label: t('meetings.view.list') },
            { id: 'calendar', label: t('meetings.view.calendar') },
          ]}
        />
      </div>

      {isLoading ? (
        <LoadingState />
      ) : isError ? (
        <ErrorState title={t('meetings.error.title')} body={t('meetings.error.body')} onRetry={() => refetch()} />
      ) : !hasMeetings ? (
        <EmptyState icon="calendar" title={t('meetings.empty.title')} body={t('meetings.empty.body')} />
      ) : view === 'calendar' ? (
        <MeetingsCalendar meetings={data ?? []} />
      ) : (
        <>
          {upcoming.length > 0 && (
            <section className="mt-section">
              <h2 className="mt-section-label">{t('meetings.section.upcoming')}</h2>
              <Table caption={t('meetings.captionUpcoming')} columns={columns} rows={upcoming} getRowKey={(m) => m.id} />
            </section>
          )}
          {past.length > 0 && (
            <section className="mt-section">
              <h2 className="mt-section-label">{t('meetings.section.past')}</h2>
              <Table caption={t('meetings.captionPast')} columns={columns} rows={past} getRowKey={(m) => m.id} />
            </section>
          )}
        </>
      )}
    </section>
  );
}
