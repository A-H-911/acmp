/*
 * Schedule meeting (P6 / PR-B) — full-page form from "ACMP Meetings.dc.html" (isCreate),
 * replacing the old modal dialog. Composes the shared library (Breadcrumb, Field/Input,
 * Select, Segmented, Button). The committee is implicit server-side (single committee,
 * CON-001), so the form collects: title, chair (from /api/members), the time window, the
 * meeting Type (Regular/Extraordinary) and Mode (InPerson/Hybrid/Remote), and optional
 * location / join URL. The design's agenda new-vs-link radio is omitted (locked decision):
 * scheduling always creates an empty Draft agenda for the builder. On success we open the
 * new meeting's detail. Title + chair are kept (the meeting needs both) though the mock
 * omits them — visual SoT = the design, behavior SoT = the package.
 */
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useScheduleMeeting, type MeetingType, type MeetingMode } from '../../api/meetings';
import { useMembers } from '../../api/members';
import { AREAS } from '../../nav/navModel';
import { Button } from '../../components/ui/Button';
import { Field, Input } from '../../components/ui/Field';
import { Select } from '../../components/ui/Select';
import { Segmented } from '../../components/ui/Segmented';
import { DateField } from '../../components/ui/DateField';
import { Icon } from '../../components/icons';
import './meetings.css';

const TYPES: MeetingType[] = ['Regular', 'Extraordinary'];
const MODES: MeetingMode[] = ['InPerson', 'Hybrid', 'Remote'];

/** A date (ISO yyyy-mm-dd) + a time (HH:mm), both local, → an ISO 8601 instant for the API.
 *  The meeting is single-day (the design's Date + Start/End times), so start & end share the date. */
function toIso(date: string, time: string): string | null {
  if (!date || !time) return null;
  const d = new Date(`${date}T${time}`);
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
}

export function SchedulePage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const members = useMembers();
  const schedule = useScheduleMeeting();

  const [title, setTitle] = useState('');
  const [chairId, setChairId] = useState('');
  const [date, setDate] = useState('');
  const [startTime, setStartTime] = useState('');
  const [endTime, setEndTime] = useState('');
  const [type, setType] = useState<MeetingType>('Regular');
  const [mode, setMode] = useState<MeetingMode>('InPerson');
  const [location, setLocation] = useState('');
  const [joinUrl, setJoinUrl] = useState('');
  const [submitted, setSubmitted] = useState(false);

  const activeMembers = useMemo(() => (members.data ?? []).filter((m) => m.isActive), [members.data]);
  const chairOptions = activeMembers.map((m) => ({ value: m.publicId, label: m.fullName }));
  const effectiveChairId = chairId || activeMembers.find((m) => m.role === 'Chairman')?.publicId || '';

  const startIso = toIso(date, startTime);
  const endIso = toIso(date, endTime);
  const titleError = submitted && !title.trim() ? t('meetings.schedule.titleRequired') : undefined;
  const chairError = submitted && !effectiveChairId ? t('meetings.schedule.chairRequired') : undefined;
  const dateError = submitted && !date ? t('meetings.schedule.dateRequired') : undefined;
  const windowError =
    submitted && startIso && endIso && endIso <= startIso ? t('meetings.schedule.windowInvalid') : undefined;

  const cancel = () => navigate(AREAS.agenda.path);

  const onSubmit = () => {
    setSubmitted(true);
    const chair = activeMembers.find((m) => m.publicId === effectiveChairId);
    if (!title.trim() || !chair || !date || !startIso || !endIso || endIso <= startIso) return;

    schedule.mutate(
      {
        title: title.trim(),
        chairUserId: chair.publicId,
        chairName: chair.fullName,
        scheduledStart: startIso,
        scheduledEnd: endIso,
        type,
        mode,
        location: location.trim() || undefined,
        joinUrl: joinUrl.trim() || undefined,
      },
      { onSuccess: (meeting) => navigate(`/meetings/${meeting.key}`) },
    );
  };

  return (
    <section className="page">

      <div className="mt-head">
        <div>
          <h1 className="page-title">{t('meetings.schedule.title')}</h1>
          <p className="mt-head-sub">{t('meetings.schedule.sub')}</p>
        </div>
      </div>

      <div className="mt-schedule-page">
        <div className="card mt-schedule-card">
          <Field label={t('meetings.schedule.titleLabel')} required error={titleError}>
            {(p) => (
              <Input {...p} value={title} onChange={(e) => setTitle(e.target.value)} placeholder={t('meetings.schedule.titlePlaceholder')} maxLength={200} />
            )}
          </Field>

          <Field label={t('meetings.schedule.chairLabel')} required error={chairError}>
            {(p) => (
              <Select
                {...p}
                ariaLabel={t('meetings.schedule.chairLabel')}
                placeholder={members.isLoading ? t('meetings.schedule.chairLoading') : t('meetings.schedule.chairPlaceholder')}
                value={effectiveChairId}
                onChange={setChairId}
                options={chairOptions}
              />
            )}
          </Field>

          <div className="mt-schedule-row">
            <Field label={t('meetings.schedule.dateLabel')} required error={dateError}>
              {(p) => (
                <DateField
                  {...p}
                  ariaLabel={t('meetings.schedule.dateLabel')}
                  value={date}
                  onChange={setDate}
                  placeholder={t('meetings.schedule.datePlaceholder')}
                  labels={{ previousMonth: t('meetings.calendar.prevMonth'), nextMonth: t('meetings.calendar.nextMonth') }}
                />
              )}
            </Field>
            <Field label={t('meetings.schedule.timeLabel')} required error={windowError}>
              {(p) => (
                <div className="mt-time-row">
                  <Input
                    {...p}
                    type="time"
                    aria-label={t('meetings.schedule.startTimeLabel')}
                    value={startTime}
                    onChange={(e) => setStartTime(e.target.value)}
                  />
                  <span className="mt-time-sep" aria-hidden="true">–</span>
                  <Input
                    type="time"
                    aria-label={t('meetings.schedule.endTimeLabel')}
                    value={endTime}
                    onChange={(e) => setEndTime(e.target.value)}
                  />
                </div>
              )}
            </Field>
          </div>

          <div className="mt-schedule-row">
            <Field label={t('meetings.schedule.typeLabel')}>
              {(p) => (
                <Select
                  {...p}
                  ariaLabel={t('meetings.schedule.typeLabel')}
                  value={type}
                  onChange={(v) => setType(v as MeetingType)}
                  options={TYPES.map((x) => ({ value: x, label: t(`meetings.meetingType.${x}`) }))}
                />
              )}
            </Field>
            <Field label={t('meetings.schedule.modeLabel')}>
              {() => (
                <div className="mt-mode">
                  <Segmented
                    ariaLabel={t('meetings.schedule.modeLabel')}
                    value={mode}
                    onValueChange={(v) => setMode(v as MeetingMode)}
                    items={MODES.map((x) => ({ id: x, label: t(`meetings.meetingMode.${x}`) }))}
                  />
                </div>
              )}
            </Field>
          </div>

          <Field label={t('meetings.schedule.locationLabel')}>
            {(p) => <Input {...p} value={location} onChange={(e) => setLocation(e.target.value)} placeholder={t('meetings.schedule.locationPlaceholder')} />}
          </Field>

          <Field label={t('meetings.schedule.joinLabel')}>
            {(p) => <Input {...p} type="url" value={joinUrl} onChange={(e) => setJoinUrl(e.target.value)} placeholder={t('meetings.schedule.joinPlaceholder')} />}
          </Field>

          {schedule.isError && <p className="mt-schedule-error" role="alert">{t('meetings.schedule.failed')}</p>}
        </div>

        <div className="mt-schedule-actions">
          <Button variant="secondary" onClick={cancel}>{t('meetings.cancel')}</Button>
          <Button onClick={onSubmit} loading={schedule.isPending}>
            <Icon name="calendar" size={15} aria-hidden /> {t('meetings.schedule.confirm')}
          </Button>
        </div>
      </div>
    </section>
  );
}
