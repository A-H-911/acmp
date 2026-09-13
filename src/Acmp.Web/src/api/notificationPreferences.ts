/*
 * WBS-40.3 — the caller's own per-event in-app notification preferences.
 *   GET /api/notifications/preferences → the full list, grouped, in the server's order.
 *   PUT /api/notifications/preferences → saves the items sent; answers with the full updated list.
 * One toggle = one PUT of one item. The PUT response replaces the cache, so the page always shows
 * what the server stored rather than what it asked for.
 *
 * Own query key (not under ['notifications']): the mark-read mutations invalidate that prefix, and a
 * preferences refetch on every mark-read would be pure waste.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './apiClient';

export type NotificationGroup = 'meetings' | 'topics' | 'decisions' | 'actions' | 'risks' | 'governance' | 'notifications';

export interface NotificationPreference {
  category: string;
  group: NotificationGroup;
  inApp: boolean;
}

export interface NotificationPreferences {
  items: NotificationPreference[];
}

const KEY = ['notificationPreferences'] as const;
const PATH = '/notifications/preferences';

export function useNotificationPreferences() {
  return useQuery({
    queryKey: KEY,
    queryFn: () => api<NotificationPreferences>(PATH),
  });
}

export function useSetNotificationPreference() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (item: { category: string; inApp: boolean }) =>
      api<NotificationPreferences>(PATH, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ items: [item] }),
      }),
    onSuccess: (data) => qc.setQueryData(KEY, data),
  });
}
