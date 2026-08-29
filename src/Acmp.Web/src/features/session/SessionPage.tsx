import { useTranslation } from 'react-i18next';
import { useMySession } from '../../api/session';
import { ApiError } from '../../api/apiClient';
import { LoadingState, EmptyState } from '../../components/states';
import { PresenterSessionView } from './PresenterSessionView';
import './session.css';

/*
 * FR-159 / AC-092 / DEC-037 — the GUEST / PRESENTER SHELL, the page an invited presenter actually uses.
 *
 * ⚠ THE SHELL ITSELF NOW LIVES IN PresenterSessionView, shared with the Chairman/Secretary preview
 * (FR-165). This page owns only the states that are TRULY its own — the terminal expired screen and the
 * "you are not presenting" empty state — because those are about the CALLER's access, which a preview
 * has no equivalent of. Everything a presenter can see is rendered by one component so a preview cannot
 * show something different from the thing it previews.
 *
 * THE BANNER'S EXPIRY IS THE SERVER'S OWN VALUE. It comes from the same stored column the per-request
 * refusal (ADR-0039) and the hourly sweep read, so the page cannot promise access the API will not
 * honour — DEC-037 requires exactly that, and one column is the only structural guarantee of it.
 */
export default function SessionPage() {
  const { t } = useTranslation();
  const { data: session, isLoading, error } = useMySession();

  // AC-092's second half: once the window closes the API refuses every request, and the page has to
  // SAY so. Renewing the token cannot help, which is why this is a terminal screen and not an error
  // with a retry button.
  if (error instanceof ApiError && error.isAccessEnded) {
    return (
      <section className="page gs-ended">
        <EmptyState
          icon="lock"
          title={t('session.ended.title')}
          body={t('session.ended.body')}
        />
      </section>
    );
  }

  if (isLoading) return <LoadingState label={t('session.loading')} />;

  if (!session) {
    return (
      <section className="page gs-ended">
        <EmptyState icon="calendar" title={t('session.none.title')} body={t('session.none.body')} />
      </section>
    );
  }

  return <PresenterSessionView session={session} />;
}
