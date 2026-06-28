/*
 * Notification center (P6e) — the bell popover, now wired to the live feed
 * (GET /api/notifications). Title/body arrive bilingual from the server (ADR-0005);
 * we render the active locale. Clicking an item marks it read and follows its deep
 * link. Empty inbox keeps the calm "all caught up" state (design page 79 / docs/14).
 * Dismisses on Escape and click-away. This is a non-modal labelled region (no design
 * .dc.html exists for the live list — it composes the shell's notif-* styles).
 */
import { useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Icon } from '../icons';
import { useNotifications, useMarkNotificationRead, type NotificationItem } from '../../api/notifications';

export function NotificationCenter({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const ref = useRef<HTMLDivElement>(null);
  const { data, isLoading, isError } = useNotifications();
  const markRead = useMarkNotificationRead();

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
    const onClick = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) onClose();
    };
    document.addEventListener('keydown', onKey);
    document.addEventListener('mousedown', onClick);
    return () => {
      document.removeEventListener('keydown', onKey);
      document.removeEventListener('mousedown', onClick);
    };
  }, [open, onClose]);

  if (!open) return null;

  const items = data?.items ?? [];
  const localize = (en: string, ar: string) => (i18n.language === 'ar' ? ar : en);
  const fmtTime = (iso: string) =>
    new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso));

  const onActivate = (n: NotificationItem) => {
    if (!n.isRead) markRead.mutate(n.id);
    onClose();
    if (n.deepLink) navigate(n.deepLink);
  };

  // Non-modal click-away popover: a labelled region, not a dialog (which would
  // imply focus capture / aria-modal we deliberately don't apply here).
  return (
    <div className="notif-panel" ref={ref} role="region" aria-label={t('notif.title')}>
      <div className="notif-panel-head">
        <span>{t('notif.title')}</span>
        {data && data.unreadCount > 0 && (
          <span className="notif-unread-count">{t('notif.unreadCount', { count: data.unreadCount })}</span>
        )}
      </div>

      {isLoading ? (
        <p className="notif-status">{t('notif.loading')}</p>
      ) : isError ? (
        <p className="notif-status">{t('notif.error')}</p>
      ) : items.length === 0 ? (
        <div className="notif-empty">
          <div className="state-icon success"><Icon name="check" size={20} aria-hidden /></div>
          <p className="state-title">{t('notif.emptyTitle')}</p>
          <p className="state-body">{t('notif.emptyBody')}</p>
        </div>
      ) : (
        <ul className="notif-list">
          {items.map((n) => (
            <li key={n.id}>
              <button
                type="button"
                className={`notif-item ${n.isRead ? '' : 'unread'}`}
                onClick={() => onActivate(n)}
              >
                {!n.isRead && <span className="notif-dot" aria-hidden="true" />}
                <span className="notif-item-body">
                  <span className="notif-item-title">{localize(n.titleEn, n.titleAr)}</span>
                  <span className="notif-item-text">{localize(n.bodyEn, n.bodyAr)}</span>
                  <span className="notif-item-time">{fmtTime(n.createdAt)}</span>
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}

      <div className="notif-panel-foot">
        <button
          type="button"
          className="notif-seeall"
          onClick={() => {
            onClose();
            navigate('/notifications');
          }}
        >
          {t('notif.seeAll')}
        </button>
      </div>
    </div>
  );
}
