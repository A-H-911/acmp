/*
 * Notification center server state. Wraps GET /api/notifications (the signed-in user's own feed,
 * paged + unread count), POST /api/notifications/{id}/read, and POST /api/notifications/read-all.
 * Title/body arrive bilingual from the server (ADR-0005); the UI picks the recipient's locale.
 *
 * Two read hooks share the server route: the bell popover reads a small recent page (useNotifications);
 * the full-page center (#79) pages lazily with useInfiniteNotifications. Both carry the full unread
 * total so the badge is correct regardless of how many pages are loaded.
 *
 * ponytail: kept fresh by a 30s background poll + refetch-on-focus — no websocket/SSE for a ≤20-user
 * committee; add a push channel only if the latency matters.
 */
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './apiClient';

export interface NotificationItem {
  id: string;
  titleEn: string;
  titleAr: string;
  bodyEn: string;
  bodyAr: string;
  category: string;
  deepLink: string | null;
  isRead: boolean;
  createdAt: string;
}

export interface NotificationList {
  items: NotificationItem[];
  unreadCount: number;
  total: number;
  hasMore: boolean;
}

const KEY = ['notifications'] as const;
const RECENT_PAGE_SIZE = 8;
const PAGE_SIZE = 20;

// Bell popover: the most recent page + the unread badge. Small page — "See all" opens the full center.
export function useNotifications() {
  return useQuery({
    queryKey: [...KEY, 'recent'],
    queryFn: () => api<NotificationList>(`/notifications?page=1&pageSize=${RECENT_PAGE_SIZE}`),
    refetchInterval: 30_000,
  });
}

// Full-page center (#79): lazy paging via "Load more". getNextPageParam uses the server's hasMore.
export function useInfiniteNotifications() {
  return useInfiniteQuery({
    queryKey: [...KEY, 'all'],
    queryFn: ({ pageParam }) => api<NotificationList>(`/notifications?page=${pageParam}&pageSize=${PAGE_SIZE}`),
    initialPageParam: 1,
    getNextPageParam: (last, pages) => (last.hasMore ? pages.length + 1 : undefined),
    refetchInterval: 30_000,
  });
}

export function useMarkNotificationRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api<void>(`/notifications/${id}/read`, { method: 'POST' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: KEY }),
  });
}

export function useMarkAllNotificationsRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api<{ marked: number }>('/notifications/read-all', { method: 'POST' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: KEY }),
  });
}
