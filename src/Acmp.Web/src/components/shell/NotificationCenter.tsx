/*
 * Notification center — the bell popover (ACMP.dc.html L92–131). Reconciled to the design in P6b:
 * a role="dialog" panel with a click-away scrim, a header ({n} new pill + Mark all read), Unread/All
 * segmented tabs, a loading skeleton, and rows showing the tone-coloured type icon + artifact key
 * (deep-link) + time + message, each with an inline mark-read dot. Footer "View all" opens the full
 * page (#79). Title/body arrive bilingual (ADR-0005); the type label + key are derived (notifPresentation).
 * Dismisses on Escape and click-away.
 */
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Icon } from '../icons';
import {
  useNotifications,
  useMarkNotificationRead,
  useMarkAllNotificationsRead,
  type NotificationItem,
} from '../../api/notifications';
import { notifType, notifKey } from '../../api/notifPresentation';

type Tab = 'unread' | 'all';
const SKELETON_ROWS = [0, 1, 2, 3];

export function NotificationCenter({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const { data, isLoading, isError } = useNotifications();
  const markRead = useMarkNotificationRead();
  const markAll = useMarkAllNotificationsRead();
  const [tab, setTab] = useState<Tab>('unread');

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  if (!open) return null;

  const items = data?.items ?? [];
  const unreadCount = data?.unreadCount ?? 0;
  const shown = tab === 'unread' ? items.filter((n) => !n.isRead) : items;

  const localize = (en: string, ar: string) => (i18n.language === 'ar' ? ar : en);
  const fmtTime = (iso: string) =>
    new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso));

  const openLink = (n: NotificationItem) => {
    if (!n.isRead) markRead.mutate(n.id);
    onClose();
    if (n.deepLink) navigate(n.deepLink);
  };

  return (
    <>
      <div className="notif-scrim" onClick={onClose} aria-hidden="true" />
      <div className="notif-panel" role="dialog" aria-label={t('notif.title')}>
        <div className="notif-panel-head">
          <div className="notif-panel-title">
            <h2>{t('notif.title')}</h2>
            {unreadCount > 0 && (
              <span className="notif-new-pill">{t('notif.newCount', { count: unreadCount })}</span>
            )}
          </div>
          <button
            type="button"
            className="notif-markall-link"
            disabled={unreadCount === 0 || markAll.isPending}
            onClick={() => markAll.mutate()}
          >
            {t('notif.markAll')}
          </button>
        </div>

        {/* Filter toggles, not a WAI-ARIA tablist: aria-pressed segmented buttons (ACMP.dc.html) —
            a tablist would need tabpanels + roving tabindex + arrow-key nav we don't implement. */}
        <div className="notif-tabs" role="group" aria-label={t('notif.title')}>
          <button
            type="button"
            aria-pressed={tab === 'unread'}
            className={tab === 'unread' ? 'is-active' : ''}
            onClick={() => setTab('unread')}
          >
            {t('notif.unread')} <span className="notif-tab-count">({unreadCount})</span>
          </button>
          <button
            type="button"
            aria-pressed={tab === 'all'}
            className={tab === 'all' ? 'is-active' : ''}
            onClick={() => setTab('all')}
          >
            {t('notif.all')}
          </button>
        </div>

        <div className="notif-scroll">
          {isLoading ? (
            <div className="notif-skeleton" aria-hidden="true">
              {SKELETON_ROWS.map((i) => (
                <div className="notif-skeleton-row" key={i}>
                  <span className="notif-skeleton-ico skeleton" />
                  <span className="notif-skeleton-lines">
                    <span className="notif-skeleton-line short skeleton" />
                    <span className="notif-skeleton-line skeleton" />
                  </span>
                </div>
              ))}
            </div>
          ) : isError ? (
            <p className="notif-status">{t('notif.error')}</p>
          ) : shown.length === 0 ? (
            <div className="notif-empty">
              <div className="state-icon success"><Icon name="check" size={20} aria-hidden /></div>
              <p className="state-title">{tab === 'unread' ? t('notif.noUnreadTitle') : t('notif.emptyTitle')}</p>
              <p className="state-body">{tab === 'unread' ? t('notif.noUnreadBody') : t('notif.emptyBody')}</p>
            </div>
          ) : (
            <ul className="notif-list">
              {shown.map((n) => {
                const ty = notifType(n.category);
                const key = notifKey(n.deepLink);
                return (
                  <li key={n.id} className={`notif-row ${n.isRead ? '' : 'unread'}`}>
                    <span className={`notif-ico ${ty.tone}`}><Icon name={ty.icon} size={15} aria-hidden /></span>
                    <div className="notif-row-body">
                      <div className="notif-row-meta">
                        {key ? (
                          <button type="button" className="notif-key" onClick={() => openLink(n)}>{key}</button>
                        ) : (
                          <span className={`notif-type ${ty.tone}`}>{t(ty.labelKey)}</span>
                        )}
                        <span className="notif-row-time">· {fmtTime(n.createdAt)}</span>
                      </div>
                      <button type="button" className="notif-row-msg" onClick={() => openLink(n)}>
                        {localize(n.bodyEn, n.bodyAr)}
                      </button>
                    </div>
                    {!n.isRead && (
                      <button
                        type="button"
                        className="notif-markdot"
                        aria-label={t('notif.markRead')}
                        title={t('notif.markRead')}
                        onClick={() => markRead.mutate(n.id)}
                      >
                        <span className="notif-dot" aria-hidden="true" />
                      </button>
                    )}
                  </li>
                );
              })}
            </ul>
          )}
        </div>

        <button
          type="button"
          className="notif-viewall"
          onClick={() => {
            onClose();
            navigate('/notifications');
          }}
        >
          {t('notif.viewAll')}
          <Icon name="chevron" size={14} className="dir-flip" aria-hidden />
        </button>
      </div>
    </>
  );
}
