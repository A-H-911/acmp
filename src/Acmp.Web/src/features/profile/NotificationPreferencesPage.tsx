/*
 * /profile/preferences — "ACMP System States.dc.html" `notif` (L112–133), with the deviations DEC-186
 * ruled: reworded subtitle and footnote, NO Email column (ACMP never sends email, DEC-150 d5), and the
 * design's six sample rows replaced by the server's event types, grouped, in the server's order.
 *
 * One toggle = one PUT of one item (api/notificationPreferences.ts). While it is in flight the row
 * shows the requested state and every switch is disabled — one save at a time keeps a later PUT's
 * full-list answer from overwriting an earlier one still on the wire. On failure the row falls back
 * to the stored value and the error is announced.
 */
import { useTranslation } from 'react-i18next';
import {
  useNotificationPreferences,
  useSetNotificationPreference,
  type NotificationPreference,
} from '../../api/notificationPreferences';
import { LoadingState, ErrorState } from '../../components/states';
import { Icon, type IconName } from '../../components/icons';
import './profile.css';

const GROUP_ICON: Record<string, IconName> = {
  meetings: 'calendar',
  topics: 'backlog',
  decisions: 'decision',
  actions: 'action',
  risks: 'risk',
  governance: 'adr',
};

/** Items grouped by `group`, groups and rows both kept in the server's order. */
function groupInOrder(items: NotificationPreference[]): [string, NotificationPreference[]][] {
  const groups = new Map<string, NotificationPreference[]>();
  for (const it of items) groups.set(it.group, [...(groups.get(it.group) ?? []), it]);
  return [...groups];
}

export function NotificationPreferencesPage() {
  const { t } = useTranslation();
  const prefs = useNotificationPreferences();
  const save = useSetNotificationPreference();

  // An unknown category/group from a newer server still renders, under its raw name.
  const eventLabel = (c: string) => t(`notifPrefs.event.${c}`, { defaultValue: c });

  const body = prefs.isLoading ? (
    <LoadingState />
  ) : prefs.isError || !prefs.data ? (
    <ErrorState title={t('notifPrefs.loadError')} body={t('notifPrefs.loadError')} onRetry={() => prefs.refetch()} />
  ) : (
    <div className="card np-card">
      <div className="np-cols">
        <span className="prof-flabel">{t('notifPrefs.eventType')}</span>
        <span className="np-ch np-ch-on">{t('notifPrefs.inApp')}</span>
        <span className="np-ch">{t('notifPrefs.webex')}</span>
      </div>
      {groupInOrder(prefs.data.items).map(([group, items]) => (
        <section key={group} aria-labelledby={`np-g-${group}`}>
          <h2 id={`np-g-${group}`} className="np-group">{t(`notifPrefs.group.${group}`, { defaultValue: group })}</h2>
          {items.map((p) => {
            const pending = save.isPending && save.variables?.category === p.category;
            const on = pending ? save.variables!.inApp : p.inApp;
            return (
              <div key={p.category} className="np-row">
                <span className="np-event">
                  <Icon name={GROUP_ICON[group] ?? 'bell'} size={16} aria-hidden />
                  <span className="np-label">{eventLabel(p.category)}</span>
                </span>
                <span className="np-cell">
                  <button
                    type="button"
                    role="switch"
                    className="np-switch"
                    aria-checked={on}
                    aria-label={eventLabel(p.category)}
                    disabled={save.isPending}
                    onClick={() => save.mutate({ category: p.category, inApp: !p.inApp })}
                  >
                    <span className="np-knob" aria-hidden="true" />
                  </button>
                </span>
                <span className="np-cell">
                  <span className="prof-tag np-phase">{t('notifPrefs.phase2')}</span>
                </span>
              </div>
            );
          })}
        </section>
      ))}
      <div className="np-foot">
        <Icon name="infoCircle" size={14} aria-hidden />
        {t('notifPrefs.footnote')}
      </div>
    </div>
  );

  return (
    <div className="page">
      <div className="prof-wrap">
        <div className="np-head">
          <div>
            <h1 className="np-title">{t('notifPrefs.title')}</h1>
            <div className="np-sub">{t('notifPrefs.sub')}</div>
          </div>
          <span role="status">
            {save.isSuccess && (
              <span className="np-saved">
                <Icon name="check" size={13} aria-hidden />
                {t('notifPrefs.saved')}
              </span>
            )}
          </span>
        </div>
        {save.isError && (
          <p className="field-error np-error" role="alert">
            <Icon name="alertCircle" size={13} aria-hidden />
            {t('notifPrefs.saveError')}
          </p>
        )}
        {body}
      </div>
    </div>
  );
}
