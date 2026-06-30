/*
 * Notification Center — full page (#79, /notifications), reconciled to ACMP.dc.html L706–739 in P6b:
 * breadcrumb + title with an in-app channel line, Unread/All underline tabs with counts, and a bordered
 * card of rows (tone-coloured type icon · TYPE label + artifact key + time · message · inline Mark read).
 * check-circle empty card. Title/body arrive bilingual (ADR-0005); the type label + key are derived
 * (notifPresentation — no stored column).
 *
 * Scope (operator GO, Option B): mark-all-read is a real backend command (POST /notifications/read-all,
 * one call, audited). The Unread/All filter is client-side over the loaded pages (categories are few;
 * YAGNI). Paging is real (server page/pageSize + hasMore) surfaced as an accessible "Load more" button
 * (DV-02 — the design list has no infinite scroll).
 */
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  useInfiniteNotifications,
  useMarkNotificationRead,
  useMarkAllNotificationsRead,
  type NotificationItem,
} from '../api/notifications';
import { notifType, notifKey } from '../api/notifPresentation';
import { Button } from '../components/ui/Button';
import { LoadingState, ErrorState, EmptyState } from '../components/states';
import { Icon } from '../components/icons';

type Filter = 'unread' | 'all';

export default function NotificationsPage() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const query = useInfiniteNotifications();
  const markRead = useMarkNotificationRead();
  const markAll = useMarkAllNotificationsRead();
  const [filter, setFilter] = useState<Filter>('unread');

  const pages = query.data?.pages ?? [];
  const items = pages.flatMap((p) => p.items);
  const unreadCount = pages[0]?.unreadCount ?? 0;
  const total = pages[0]?.total ?? items.length;
  const shown = filter === 'unread' ? items.filter((n) => !n.isRead) : items;

  const localize = (en: string, ar: string) => (i18n.language === 'ar' ? ar : en);
  const fmtTime = (iso: string) =>
    new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso));

  const openLink = (n: NotificationItem) => {
    if (!n.isRead) markRead.mutate(n.id);
    if (n.deepLink) navigate(n.deepLink);
  };

  return (
    <section className="page notif-page">
      <div className="notif-page-head">
        <div>
          <h1 className="page-title">{t('notif.title')}</h1>
          <p className="notif-channel"><Icon name="bell" size={15} aria-hidden /> {t('notif.channel')}</p>
        </div>
        <Button
          variant="secondary"
          size="sm"
          disabled={unreadCount === 0 || markAll.isPending}
          loading={markAll.isPending}
          onClick={() => markAll.mutate()}
        >
          <Icon name="check" size={15} aria-hidden /> {t('notif.markAll')}
        </Button>
      </div>

      <div className="notif-underline-tabs" role="tablist" aria-label={t('notif.title')}>
        <button
          type="button"
          role="tab"
          aria-selected={filter === 'unread'}
          className={filter === 'unread' ? 'is-active' : ''}
          onClick={() => setFilter('unread')}
        >
          {t('notif.unread')} <span className="notif-tab-count">({unreadCount})</span>
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={filter === 'all'}
          className={filter === 'all' ? 'is-active' : ''}
          onClick={() => setFilter('all')}
        >
          {t('notif.all')} <span className="notif-tab-count">({total})</span>
        </button>
      </div>

      {query.isLoading ? (
        <LoadingState />
      ) : query.isError ? (
        <ErrorState title={t('notif.error')} body={t('notif.error')} onRetry={() => query.refetch()} />
      ) : shown.length === 0 ? (
        <EmptyState
          icon="check"
          title={filter === 'unread' ? t('notif.noUnreadTitle') : t('notif.emptyTitle')}
          body={filter === 'unread' ? t('notif.noUnreadBody') : t('notif.emptyBody')}
        />
      ) : (
        <>
          <ul className="notif-card">
            {shown.map((n) => {
              const ty = notifType(n.category);
              const key = notifKey(n.deepLink);
              return (
                <li key={n.id} className={`notif-row ${n.isRead ? '' : 'unread'}`}>
                  <span className={`notif-ico lg ${ty.tone}`}><Icon name={ty.icon} size={17} aria-hidden /></span>
                  <div className="notif-row-body">
                    <div className="notif-row-meta">
                      <span className={`notif-type ${ty.tone}`}>{t(ty.labelKey)}</span>
                      {key && (
                        <button type="button" className="notif-key" onClick={() => openLink(n)}>{key}</button>
                      )}
                      <span className="notif-row-time">· {fmtTime(n.createdAt)}</span>
                    </div>
                    <button type="button" className="notif-row-msg" onClick={() => openLink(n)}>
                      {localize(n.bodyEn, n.bodyAr)}
                    </button>
                  </div>
                  {!n.isRead && (
                    <button type="button" className="notif-markread" onClick={() => markRead.mutate(n.id)}>
                      <span className="notif-dot" aria-hidden="true" /> {t('notif.markRead')}
                    </button>
                  )}
                </li>
              );
            })}
          </ul>

          {filter === 'all' && query.hasNextPage && (
            <div className="notif-page-more">
              <Button variant="secondary" loading={query.isFetchingNextPage} onClick={() => query.fetchNextPage()}>
                {t('notif.loadMore')}
              </Button>
            </div>
          )}
        </>
      )}
    </section>
  );
}
