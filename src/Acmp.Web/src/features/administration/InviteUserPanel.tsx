import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useInviteUser, useStreams, type InvitedUser } from '../../api/members';
import { MemberStreamChips } from './MemberStreamChips';
import { Button } from '../../components/ui/Button';
import { Field, Input } from '../../components/ui/Field';
import { InvitedCredential } from '../../components/ui/InvitedCredential';
import { Icon } from '../../components/icons';

/*
 * FR-156 / AC-088 — the invite section of "ACMP Administration.dc.html" §(8) USER DETAIL + INVITE.
 * Two fields (email, full name), an explanatory note, and a single primary action, matching the
 * reference. There is deliberately NO role picker: roles are granted separately (FR-157), so an
 * invited account starts inert and cannot be created fully-privileged in one step.
 *
 * ⚠ THE TEMPORARY PASSWORD IS SHOWN ONCE AND NEVER AGAIN. "No email in v1" is a hard constraint, so
 * the design's "Send invitation" resolves to handing the credential to the inviter. It lives in this
 * component's state for the life of the panel and is written nowhere else — not to the query cache,
 * not to storage, not to a log. The 26-password CSV that had to be deleted by hand is exactly what
 * leaks if this is treated as ordinary data.
 */
export function InviteUserPanel() {
  const { t } = useTranslation();
  const [email, setEmail] = useState('');
  const [fullName, setFullName] = useState('');
  const [invited, setInvited] = useState<InvitedUser | null>(null);
  // ⚠ STARTS EMPTY, AND THE WILDCARD IS NEVER PRE-SELECTED (DEC-044). All 26 existing members begin
  // on the wildcard via the step-5 backfill, so if new invites also defaulted to it stream scope
  // would never restrict anyone and the whole control would be decorative. The inviter must choose.
  const [streamIds, setStreamIds] = useState<string[]>([]);
  const invite = useInviteUser();
  const { data: streams, isLoading: streamsLoading, isError: streamsError } = useStreams();

  const toggleStream = (publicId: string) =>
    setStreamIds((prev) =>
      prev.includes(publicId) ? prev.filter((id) => id !== publicId) : [...prev, publicId]);

  // ⚠ At least one stream is REQUIRED (ADR-0043 clause 2) — the server refuses without it, and an
  // invite that skipped it would create a member who can write nothing once step 7 lands.
  const canSubmit =
    email.trim().length > 0 && fullName.trim().length > 0 && streamIds.length > 0 && !invite.isPending;

  function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!canSubmit) return;
    invite.mutate(
      { email: email.trim(), fullName: fullName.trim(), streamPublicIds: streamIds },
      {
        onSuccess: (result) => {
          setInvited(result);
          setEmail('');
          setFullName('');
          setStreamIds([]);
        },
      },
    );
  }

  // The reveal REPLACES the form rather than sitting beside it. Leaving the form live invites a
  // second invite while a credential is still on screen, and the password would then be lost behind
  // the new result with no way to recover it.
  if (invited) {
    return (
      <div className="adm-detail-card" data-testid="invite-result">
        <div className="adm-detail-section-head">{t('admin.invite.successTitle')}</div>
        <div className="adm-detail-form">
          <p className="adm-detail-note">{t('admin.invite.successBody', { name: invited.fullName })}</p>

          <InvitedCredential password={invited.temporaryPassword} />

          <Button variant="secondary" onClick={() => setInvited(null)}>
            {t('admin.invite.another')}
          </Button>
        </div>
      </div>
    );
  }

  // DEF-047: this shipped with the fields as bare `.adm-fact` blocks directly inside the card. The
  // card carries no padding of its own — every other child block supplies it — so the labels sat
  // flush against the border with unstyled browser inputs beside them, and the primary action used
  // `.adm-back`, which is the borderless back-LINK style. Now a padded `.adm-detail-form` with the
  // design system's Field/Input/Button, the same block the role editor above it uses.
  return (
    <form className="adm-detail-card" onSubmit={submit}>
      <div className="adm-detail-section-head">
        <Icon name="user" size={16} aria-hidden />
        {t('admin.invite.title')}
      </div>

      <div className="adm-detail-form">
        <Field label={t('admin.invite.email')} required>
          {(p) => (
            <Input {...p} type="email" dir="ltr" value={email} onChange={(e) => setEmail(e.target.value)} required />
          )}
        </Field>

        <Field label={t('admin.invite.fullName')} required>
          {(p) => <Input {...p} type="text" value={fullName} onChange={(e) => setFullName(e.target.value)} required />}
        </Field>

        <Field label={t('admin.invite.streams')} required>
          {() => (
            <>
              <p className="field-help">{t('admin.invite.streamsHelp')}</p>
              {streamsLoading && <p className="field-help">{t('common.loading')}</p>}
              {(streamsError || (!streamsLoading && !streams)) && (
                <p className="field-error" role="alert">
                  <Icon name="alertCircle" size={13} aria-hidden />{t('common.error')}
                </p>
              )}
              {streams && (
                <MemberStreamChips
                  streams={streams}
                  selected={streamIds}
                  onToggle={toggleStream}
                  ariaLabel={t('admin.invite.streams')}
                />
              )}
            </>
          )}
        </Field>

        <p className="adm-detail-note">
          <Icon name="infoCircle" size={15} aria-hidden />
          {t('admin.invite.note')}
        </p>

        {invite.isError && (
          <p role="alert" className="field-error">
            <Icon name="alertCircle" size={13} aria-hidden />
            {t('admin.invite.error')}
          </p>
        )}

        <Button type="submit" variant="primary" loading={invite.isPending} disabled={!canSubmit}>
          <Icon name="send" size={15} aria-hidden />
          {invite.isPending ? t('admin.invite.sending') : t('admin.invite.send')}
        </Button>
      </div>
    </form>
  );
}
