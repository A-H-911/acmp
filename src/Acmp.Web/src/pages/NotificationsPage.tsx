/*
 * Notification Center — full page (#79, /notifications). No `.dc.html` reference exists for this
 * screen, so it's a no-reference composition (guardrail 14): the design system page chrome
 * (breadcrumb + page-title) over the shell's notification row anatomy (.notif-* — shared with the
 * bell popover, kept DRY). docs/14 spec: list (summary, deep link, timestamp, read/unread),
 * mark-all-read, filter, and the loading / empty / all-read / populated states.
 *
 * Scope (operator GO): mark-all-read is a real backend command (POST /notifications/read-all, one
 * call). The filter is a client-side Unread/All toggle over the loaded pages (no server filter —
 * categories are few; YAGNI). Paging is real (server page/pageSize + hasMore) surfaced as an
 * accessible "Load more" button rather than a scroll observer (simpler + keyboard-friendly).
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
import { AREAS } from '../nav/navModel';
import { Breadcrumb } from '../components/ui/Breadcrumb';
import { Button } from '../components/ui/Button';
import { LoadingState, ErrorState, EmptyState } from '../components/states';
import { Icon } from '../components/icons';

type Filter = 'all' | 'unread';

export default function NotificationsPage() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const query = useInfiniteNotifications();
  const markRead = useMarkNotificationRead();
  const markAll = useMarkAllNotificationsRead();
  const [filter, setFilter] = useState<Filter>('all');

  const pages = query.data?.pages ?? [];
  const items = pages.flatMap((p) => p.items);
  const unreadCount = pages[0]?.unreadCount ?? 0;
  const shown = filter === 'unread' ? items.filter((n) => !n.isRead) : items;

  const localize = (en: string, ar: string) => (i18n.language === 'ar' ? ar : en);
  const fmtTime = (iso: string) =>
    new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso));

  const onActivate = (n: NotificationItem) => {
    if (!n.isRead) markRead.mutate(n.id);
    if (n.deepLink) navigate(n.deepLink);
  };

  return (
    <section className="page notif-page">
      <Breadcrumb
        ariaLabel={t('notif.title')}
        items={[{ label: t('nav.home'), href: AREAS.home.path }, { label: t('notif.title'), current: true }]}
      />

      <div className="notif-page-head">
        <h1 className="page-title">{t('notif.title')}</h1>
        <div className="notif-page-actions">
          <div className="notif-seg" role="group" aria-label={t('notif.title')}>
            <button
              type="button"
              className={filter === 'all' ? 'is-active' : ''}
              aria-pressed={filter === 'all'}
              onClick={() => setFilter('all')}
            >
              {t('notif.all')}
            </button>
            <button
              type="button"
              className={filter === 'unread' ? 'is-active' : ''}
              aria-pressed={filter === 'unread'}
              onClick={() => setFilter('unread')}
            >
              {t('notif.unread')}
              {unreadCount > 0 && <span className="notif-seg-count">{unreadCount}</span>}
            </button>
          </div>
          <Button
            variant="secondary"
            size="sm"
            disabled={unreadCount === 0 || markAll.isPending}
            loading={markAll.isPending}
            onClick={() => markAll.mutate()}
          >
            <Icon name="check" size={14} aria-hidden /> {t('notif.markAll')}
          </Button>
        </div>
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
          <ul className="notif-list notif-page-list">
            {shown.map((n) => (
              <li key={n.id}>
                <button type="button" className={`notif-item ${n.isRead ? '' : 'unread'}`} onClick={() => onActivate(n)}>
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
