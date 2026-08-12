import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useInviteGuestPresenter, type InvitedGuestPresenter } from '../../api/meetings';
import { Button } from '../../components/ui/Button';
import { Dialog } from '../../components/ui/Dialog';
import { Field, Input } from '../../components/ui/Field';
import { InvitedCredential } from '../../components/ui/InvitedCredential';
import { Icon } from '../../components/icons';
import './meetings.css';

/*
 * FR-159 / AC-092 — the Secretary invites a guest presenter FROM THE MEETING SCREEN (DEC-037), on
 * the agenda slot the guest will present.
 *
 * ⚠ NO-REFERENCE COMPOSITION (INV-014). The design references cover the guest's own page (the
 * GUEST / PRESENTER SHELL in "ACMP Navigation & IA.dc.html") and the administration invite, but no
 * .dc.html draws this control. It is composed from the two patterns that DO exist: the agenda
 * builder's row affordances and the administration invite's field/reveal treatment, reusing the
 * shared Dialog and InvitedCredential rather than inventing a third look.
 *
 * SECRETARY ONLY, and hiding the button is NOT what enforces that — the command's AllowedRoles is
 * (ADR-0040 decision 1). The Chairman can reach this screen and edit the agenda; he cannot invite,
 * deliberately, because FR-159 gives this to the role that schedules the meeting.
 */
// Same shape the other meeting screens use (MeetingsList, TopicDetail): the locale is the caller's,
// so the expiry instant reads the way the rest of the app writes dates.
function useDateFmt() {
  const { i18n } = useTranslation();
  return (iso: string) => new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso));
}

export function GuestPresenterInvite({
  meetingKey,
  meetingId,
  topicId,
  topicKey,
}: {
  meetingKey: string;
  meetingId: string;
  topicId: string;
  topicKey: string;
}) {
  const { t } = useTranslation();
  const fmt = useDateFmt();
  const [open, setOpen] = useState(false);
  const [email, setEmail] = useState('');
  const [fullName, setFullName] = useState('');
  const [invited, setInvited] = useState<InvitedGuestPresenter | null>(null);
  const invite = useInviteGuestPresenter(meetingKey);

  const canSubmit = email.trim().length > 0 && fullName.trim().length > 0 && !invite.isPending;

  // Closing DISCARDS the credential, which is the point: it exists only for as long as the panel
  // that shows it, and there is no way to read it back afterwards.
  function close() {
    setOpen(false);
    setInvited(null);
    setEmail('');
    setFullName('');
    invite.reset();
  }

  // Called by BOTH the form's submit (Enter in a field) and the dialog's footer button. The footer
  // sits outside the <form> because the Dialog owns it, and a `form=` attribute on the button is not
  // honoured everywhere — one handler both paths call is smaller than making that work.
  function submit() {
    if (!canSubmit) return;
    invite.mutate(
      { meetingId, topicId, email: email.trim(), fullName: fullName.trim() },
      { onSuccess: setInvited },
    );
  }

  return (
    <>
      <button type="button" className="mt-guest-btn" onClick={() => setOpen(true)}>
        <Icon name="user" size={13} aria-hidden />
        {t('meetings.guest.invite')}
      </button>

      <Dialog
        open={open}
        onClose={close}
        tone="accent"
        icon={<Icon name="user" size={17} aria-hidden />}
        title={invited ? t('meetings.guest.doneTitle') : t('meetings.guest.title')}
        description={invited ? undefined : t('meetings.guest.description', { key: topicKey })}
        footer={
          invited ? (
            <Button variant="primary" onClick={close}>{t('meetings.guest.close')}</Button>
          ) : (
            <>
              <Button variant="secondary" onClick={close}>{t('common.cancel')}</Button>
              <Button variant="primary" onClick={submit} loading={invite.isPending} disabled={!canSubmit}>
                <Icon name="send" size={15} aria-hidden />
                {t('meetings.guest.send')}
              </Button>
            </>
          )
        }
      >
        {invited ? (
          <div className="mt-guest-form">
            <p className="mt-guest-note">{t('meetings.guest.doneBody', { name: invited.fullName })}</p>

            {/* The EXACT instant the server will start refusing them — not "after the meeting". The
                /session banner reads this same stored value, so the two can never disagree. */}
            <div>
              <div className="cred-label">{t('meetings.guest.expiresLabel')}</div>
              <div>{fmt(invited.accessExpiresAt)}</div>
            </div>

            <InvitedCredential password={invited.temporaryPassword} />
          </div>
        ) : (
          <form className="mt-guest-form" onSubmit={(e) => { e.preventDefault(); submit(); }}>
            <Field label={t('meetings.guest.email')} required>
              {(p) => (
                <Input {...p} type="email" dir="ltr" value={email} onChange={(e) => setEmail(e.target.value)} required />
              )}
            </Field>

            <Field label={t('meetings.guest.fullName')} required>
              {(p) => <Input {...p} type="text" value={fullName} onChange={(e) => setFullName(e.target.value)} required />}
            </Field>

            <p className="mt-guest-note">
              <Icon name="infoCircle" size={15} aria-hidden />
              {t('meetings.guest.note')}
            </p>

            {invite.isError && (
              <p role="alert" className="field-error">
                <Icon name="alertCircle" size={13} aria-hidden />
                {t('meetings.guest.error')}
              </p>
            )}
          </form>
        )}
      </Dialog>
    </>
  );
}
