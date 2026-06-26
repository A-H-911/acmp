/*
 * Topics server state (P5b). Wraps GET /api/topics (backlog) and the prioritize
 * mutation. Enums travel as their string names (JsonStringEnumConverter on the
 * server); the UI localizes them. Read is by key (GET /{key}); every mutation is
 * by Guid id (/{id}/…) — so summaries/detail both carry `id` for write calls.
 */
import { useQuery } from '@tanstack/react-query';
import { api } from './apiClient';

export interface TopicSummary {
  id: string;
  key: string;
  title: string;
  type: string;
  status: string;
  urgency: string;
  scope: string;
  streams: string[];
  ownerId: string | null;
  ownerName: string | null;
  priority: number;
  ageDays: number;
  slaBreached: boolean;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface BacklogParams {
  statuses?: string[];
  type?: string;
  stream?: string;
  urgency?: string;
  ownerId?: string;
  search?: string;
  includeClosed?: boolean;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

function toQuery(p: BacklogParams): string {
  const q = new URLSearchParams();
  // The server binds a repeated `status` query param into TopicStatus[] (W3 filter).
  p.statuses?.forEach((s) => q.append('status', s));
  if (p.type) q.set('type', p.type);
  if (p.stream) q.set('stream', p.stream);
  if (p.urgency) q.set('urgency', p.urgency);
  if (p.ownerId) q.set('ownerId', p.ownerId);
  if (p.search) q.set('search', p.search);
  if (p.includeClosed) q.set('includeClosed', 'true');
  if (p.sortBy) q.set('sortBy', p.sortBy);
  if (p.sortDir) q.set('sortDir', p.sortDir);
  if (p.page) q.set('page', String(p.page));
  if (p.pageSize) q.set('pageSize', String(p.pageSize));
  const s = q.toString();
  return s ? `?${s}` : '';
}

export function useBacklog(params: BacklogParams) {
  return useQuery({
    queryKey: ['topics', 'backlog', params],
    queryFn: () => api<PagedResult<TopicSummary>>(`/topics${toQuery(params)}`),
    // Keep the previous page visible while the next filter/page resolves (no flash back to skeleton).
    placeholderData: (prev) => prev,
  });
}
