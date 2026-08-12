import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from './Button';
import { Icon } from '../icons';
import './invited-credential.css';

/*
 * The one-time temporary password for a newly invited account, with its copy control and its
 * warning. Shared by the administration invite (FR-156) and the guest-presenter invite (FR-159).
 *
 * ⚠ SHARED ON PURPOSE, AND THIS IS THE PART WORTH SHARING. "No email in v1" is a hard constraint,
 * so the credential is handed to the inviter on screen and can never be re-read. That makes the
 * copy affordance and the "you get one chance" warning a correctness requirement rather than
 * decoration — a second copy of them would be a second place to forget the warning. The value lives
 * in the caller's component state for the life of the panel and is written nowhere else: not to the
 * query cache, not to storage, not to a log. The 26-password CSV that had to be deleted by hand is
 * exactly what leaks if this is treated as ordinary data.
 */
export function InvitedCredential({ password }: { password: string }) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);

  async function copy() {
    await navigator.clipboard.writeText(password);
    setCopied(true);
  }

  return (
    <>
      <div>
        <div className="cred-label">{t('admin.invite.passwordLabel')}</div>
        <div className="cred-row">
          <code dir="ltr">{password}</code>
          <Button variant="ghost" size="sm" onClick={copy}>
            {copied && <Icon name="check" size={13} aria-hidden />}
            {copied ? t('admin.invite.copied') : t('admin.invite.copy')}
          </Button>
        </div>
      </div>

      {/* Stated on screen, not just in a comment: the person reading it is the only one who can act
          on it, and they get exactly one chance. */}
      <p className="cred-hint">{t('admin.invite.passwordHint')}</p>
    </>
  );
}
