/*
 * Notification center server state (P6e). Wraps GET /api/notifications (the signed-in
 * user's own feed + unread count) and POST /api/notifications/{id}/read. Title/body arrive
 * bilingual from the server (ADR-0005); the UI picks the recipient's locale.
 *
 * ponytail: the badge/feed is kept fresh by a 30s background poll + refetch-on-focus — no
 * websocket/SSE for a ≤20-user committee; add a push channel only if the latency matters.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
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
}

const KEY = ['notifications'] as const;

export function useNotifications() {
  return useQuery({
    queryKey: KEY,
    queryFn: () => api<NotificationList>('/notifications'),
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
