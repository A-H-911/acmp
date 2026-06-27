/*
 * Schedule a meeting (P6c follow-up) — the new-meeting form, previously deferred because
 * committeeId wasn't exposed to the SPA. It now isn't needed: the committee is implicit
 * server-side (single committee, CON-001), so the form collects only title, chair, the
 * time window, and optional location/join URL. The chair is picked from the committee
 * directory (GET /api/members); chairUserId = the member's publicId (the value the meeting
 * stores), chairName = a display snapshot. On success we open the new meeting's agenda builder.
 */
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useScheduleMeeting } from '../../api/meetings';
import { useMembers } from '../../api/members';
import { Dialog } from '../../components/ui/Dialog';
import { Button } from '../../components/ui/Button';
import { Field, Input } from '../../components/ui/Field';
import { Select } from '../../components/ui/Select';
import { Icon } from '../../components/icons';

/** A `datetime-local` value ("2026-06-30T09:00", local) → an ISO 8601 instant for the API. */
function toIso(local: string): string | null {
  if (!local) return null;
  const d = new Date(local);
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
}

export function ScheduleMeetingDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const members = useMembers();
  const schedule = useScheduleMeeting();

  const [title, setTitle] = useState('');
  const [chairId, setChairId] = useState('');
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [location, setLocation] = useState('');
  const [joinUrl, setJoinUrl] = useState('');
  const [submitted, setSubmitted] = useState(false);

  const activeMembers = useMemo(() => (members.data ?? []).filter((m) => m.isActive), [members.data]);
  const chairOptions = activeMembers.map((m) => ({ value: m.publicId, label: m.fullName }));
  // Default the chair to the Chairman if the directory has one.
  const effectiveChairId = chairId || activeMembers.find((m) => m.role === 'Chairman')?.publicId || '';

  const startIso = toIso(start);
  const endIso = toIso(end);
  const titleError = submitted && !title.trim() ? t('meetings.schedule.titleRequired') : undefined;
  const chairError = submitted && !effectiveChairId ? t('meetings.schedule.chairRequired') : undefined;
  const windowError =
    submitted && startIso && endIso && endIso <= startIso ? t('meetings.schedule.windowInvalid') : undefined;

  const reset = () => {
    setTitle('');
    setChairId('');
    setStart('');
    setEnd('');
    setLocation('');
    setJoinUrl('');
    setSubmitted(false);
  };

  const close = () => {
    reset();
    onClose();
  };

  const onSubmit = () => {
    setSubmitted(true);
    const chair = activeMembers.find((m) => m.publicId === effectiveChairId);
    if (!title.trim() || !chair || !startIso || !endIso || endIso <= startIso) return;

    schedule.mutate(
      {
        title: title.trim(),
        chairUserId: chair.publicId,
        chairName: chair.fullName,
        scheduledStart: startIso,
        scheduledEnd: endIso,
        location: location.trim() || undefined,
        joinUrl: joinUrl.trim() || undefined,
      },
      {
        onSuccess: (meeting) => {
          reset();
          onClose();
          navigate(`/meetings/${meeting.key}`);
        },
      },
    );
  };

  return (
    <Dialog
      open={open}
      onClose={close}
      title={t('meetings.schedule.title')}
      description={t('meetings.schedule.sub')}
      icon={<Icon name="calendar" size={18} aria-hidden />}
      footer={
        <>
          <Button variant="secondary" onClick={close}>{t('meetings.cancel')}</Button>
          <Button onClick={onSubmit} loading={schedule.isPending}>{t('meetings.schedule.confirm')}</Button>
        </>
      }
    >
      <div className="mt-schedule-form">
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
          <Field label={t('meetings.schedule.startLabel')} required error={windowError}>
            {(p) => <Input {...p} type="datetime-local" value={start} onChange={(e) => setStart(e.target.value)} />}
          </Field>
          <Field label={t('meetings.schedule.endLabel')} required>
            {(p) => <Input {...p} type="datetime-local" value={end} onChange={(e) => setEnd(e.target.value)} />}
          </Field>
        </div>

        <Field label={t('meetings.schedule.locationLabel')}>
          {(p) => <Input {...p} value={location} onChange={(e) => setLocation(e.target.value)} placeholder={t('meetings.schedule.locationPlaceholder')} />}
        </Field>

        <Field label={t('meetings.schedule.joinLabel')}>
          {(p) => <Input {...p} type="url" value={joinUrl} onChange={(e) => setJoinUrl(e.target.value)} placeholder={t('meetings.schedule.joinPlaceholder')} />}
        </Field>

        {schedule.isError && (
          <p className="mt-schedule-error" role="alert">{t('meetings.schedule.failed')}</p>
        )}
      </div>
    </Dialog>
  );
}
