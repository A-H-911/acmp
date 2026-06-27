/*
 * Meetings server state (P6c). Wraps GET /api/meetings (list) + GET /api/meetings/{key}
 * (detail) and the agenda-builder mutations. Mirrors api/topics.ts: reads are by key,
 * every mutation is by Guid id (the detail DTO carries both id and key). Enums travel as
 * string names (JsonStringEnumConverter on the server); the UI localizes them.
 *
 * Each agenda mutation returns the updated AgendaDto and invalidates
 * ['meetings','detail',key] so the builder re-renders from the server's truth.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './apiClient';
import type { PagedResult, TopicSummary } from './topics';

export interface MeetingSummary {
  id: string;
  key: string;
  title: string;
  scheduledStart: string;
  scheduledEnd: string;
  status: string;
  chairName: string;
  itemCount: number;
  agendaStatus: string;
}

export interface AgendaItem {
  topicId: string;
  topicKey: string;
  topicTitle: string;
  urgent: boolean;
  order: number;
  timeboxMinutes: number;
  presenterUserId: string | null;
  presenterName: string | null;
  outcome: string;
  actualMinutes: number;
}

export interface Agenda {
  id: string;
  key: string;
  status: string;
  version: number;
  totalTimeboxMinutes: number;
  publishedAt: string | null;
  items: AgendaItem[];
}

export interface MeetingDetail {
  id: string;
  key: string;
  title: string;
  committeeId: string;
  scheduledStart: string;
  scheduledEnd: string;
  status: string;
  location: string | null;
  joinUrl: string | null;
  chairUserId: string;
  chairName: string;
  startedAt: string | null;
  heldAt: string | null;
  agenda: Agenda | null;
  // attendance/discussions belong to the live-meeting phase (P6d) — read but unused here.
  attendance: unknown[];
  discussions: unknown[];
}

export function useMeetings() {
  return useQuery({
    queryKey: ['meetings', 'list'],
    queryFn: () => api<MeetingSummary[]>('/meetings'),
  });
}

export function useMeetingDetail(key: string | undefined) {
  return useQuery({
    queryKey: ['meetings', 'detail', key],
    queryFn: () => api<MeetingDetail>(`/meetings/${key}`),
    enabled: !!key,
    retry: false, // a 404 (unknown key) shouldn't retry — surface "not found" immediately
  });
}

/* The agenda pool source = topics Prepared for an agenda (GET /api/topics?status=Prepared).
 * The design labels this column "Scheduled topics"; we keep that label but source Prepared,
 * since topics only become Scheduled when the agenda is published. pageSize is generous —
 * this is a single, low-traffic committee (CON-001), so one page holds the whole pool. */
export function usePreparedTopics() {
  return useQuery({
    queryKey: ['topics', 'prepared'],
    queryFn: () => api<PagedResult<TopicSummary>>('/topics?status=Prepared&pageSize=200'),
  });
}

/** Shared invalidation: every agenda mutation refreshes the meeting detail (and the
 *  Prepared pool, since add/remove/publish change which topics are placeable). */
function useAgendaMutation<TVars>(key: string | undefined, fn: (vars: TVars) => Promise<Agenda>) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['meetings', 'detail', key] });
      qc.invalidateQueries({ queryKey: ['topics', 'prepared'] });
    },
  });
}

export interface AddAgendaItemInput {
  meetingId: string;
  topicId: string;
  topicKey: string;
  topicTitle: string;
  urgent: boolean;
  timeboxMinutes: number;
  presenterUserId?: string;
  presenterName?: string;
}

export function useAddAgendaItem(key: string | undefined) {
  return useAgendaMutation(key, ({ meetingId, ...body }: AddAgendaItemInput) =>
    api<Agenda>(`/meetings/${meetingId}/agenda/items`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    }),
  );
}

export function useRemoveAgendaItem(key: string | undefined) {
  return useAgendaMutation(key, ({ meetingId, topicId }: { meetingId: string; topicId: string }) =>
    api<Agenda>(`/meetings/${meetingId}/agenda/items/${topicId}`, { method: 'DELETE' }),
  );
}

/** AC-044 reorder. Both keyboard move-up/down and pointer drag send a single ±1 delta. */
export function useMoveAgendaItem(key: string | undefined) {
  return useAgendaMutation(key, ({ meetingId, topicId, delta }: { meetingId: string; topicId: string; delta: 1 | -1 }) =>
    api<Agenda>(`/meetings/${meetingId}/agenda/items/${topicId}/move`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ delta }),
    }),
  );
}

export function useSetTimebox(key: string | undefined) {
  return useAgendaMutation(key, ({ meetingId, topicId, minutes }: { meetingId: string; topicId: string; minutes: number }) =>
    api<Agenda>(`/meetings/${meetingId}/agenda/items/${topicId}/timebox`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ minutes }),
    }),
  );
}

export function useAssignPresenter(key: string | undefined) {
  return useAgendaMutation(
    key,
    ({ meetingId, topicId, presenterUserId, presenterName }: { meetingId: string; topicId: string; presenterUserId: string; presenterName: string }) =>
      api<Agenda>(`/meetings/${meetingId}/agenda/items/${topicId}/presenter`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ presenterUserId, presenterName }),
      }),
  );
}

/** Publish & notify: flips each placed topic Prepared→Scheduled and fans out notifications
 *  server-side. Returns the published AgendaDto. */
export function usePublishAgenda(key: string | undefined) {
  return useAgendaMutation(key, ({ meetingId }: { meetingId: string }) =>
    api<Agenda>(`/meetings/${meetingId}/agenda/publish`, { method: 'POST' }),
  );
}
