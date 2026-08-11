import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useInviteUser, type InvitedUser } from '../../api/members';
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
  const [copied, setCopied] = useState(false);
  const invite = useInviteUser();

  const canSubmit = email.trim().length > 0 && fullName.trim().length > 0 && !invite.isPending;

  function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!canSubmit) return;
    invite.mutate(
      { email: email.trim(), fullName: fullName.trim() },
      {
        onSuccess: (result) => {
          setInvited(result);
          setEmail('');
          setFullName('');
        },
      },
    );
  }

  async function copyPassword() {
    if (!invited) return;
    await navigator.clipboard.writeText(invited.temporaryPassword);
    setCopied(true);
  }

  // The reveal REPLACES the form rather than sitting beside it. Leaving the form live invites a
  // second invite while a credential is still on screen, and the password would then be lost behind
  // the new result with no way to recover it.
  if (invited) {
    return (
      <div className="adm-detail-card" data-testid="invite-result">
        <div className="adm-detail-section-head">{t('admin.invite.successTitle')}</div>
        <p className="adm-detail-empty">{t('admin.invite.successBody', { name: invited.fullName })}</p>

        <div className="adm-fact">
          <div className="adm-fact-label">{t('admin.invite.passwordLabel')}</div>
          <div className="adm-fact-value">
            <code dir="ltr">{invited.temporaryPassword}</code>
            <button type="button" className="adm-back" onClick={copyPassword}>
              {copied && <Icon name="check" size={13} aria-hidden />}
              {copied ? t('admin.invite.copied') : t('admin.invite.copy')}
            </button>
          </div>
        </div>

        {/* Stated on screen, not just in a comment: the person reading it is the only one who can
            act on it, and they get exactly one chance. */}
        <p className="adm-detail-empty">{t('admin.invite.passwordHint')}</p>

        <button type="button" className="adm-back" onClick={() => { setInvited(null); setCopied(false); }}>
          {t('admin.invite.another')}
        </button>
      </div>
    );
  }

  return (
    <form className="adm-detail-card" onSubmit={submit}>
      <div className="adm-detail-section-head">
        <Icon name="user" size={16} aria-hidden />
        {t('admin.invite.title')}
      </div>

      <div className="adm-fact">
        <label className="adm-fact-label" htmlFor="invite-email">
          {t('admin.invite.email')}
        </label>
        <input
          id="invite-email"
          type="email"
          dir="ltr"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required
        />
      </div>

      <div className="adm-fact">
        <label className="adm-fact-label" htmlFor="invite-name">
          {t('admin.invite.fullName')}
        </label>
        <input id="invite-name" type="text" value={fullName} onChange={(e) => setFullName(e.target.value)} required />
      </div>

      <div className="adm-detail-empty">
        <Icon name="infoCircle" size={15} aria-hidden />
        {t('admin.invite.note')}
      </div>

      {invite.isError && (
        <div role="alert" className="adm-detail-empty">
          {t('admin.invite.error')}
        </div>
      )}

      <button type="submit" className="adm-back" disabled={!canSubmit}>
        <Icon name="send" size={15} aria-hidden />
        {invite.isPending ? t('admin.invite.sending') : t('admin.invite.send')}
      </button>
    </form>
  );
}
