/*
 * Minutes-of-meeting server state (P7d). Wraps the P7c endpoints: the version history for a meeting
 * (GET /api/minutes?meeting={guid}), a version-aware detail read (GET /api/minutes/{key}[?version=]),
 * and the W10 lifecycle mutations. Mirrors api/meetings.ts / api/decisions.ts: reads by key, mutate by
 * the minutes' Guid id; enums travel as string names; bilingual fields are LocalizedString ({ en, ar }).
 *
 * Content is MIRRORED (en === ar): the markdown editor yields one string, sent to both columns (the
 * operator-locked pattern — keeps both populated for Full-Text Search). Every mutation invalidates both
 * the meeting's version list and the affected detail so the UI re-renders from the server's truth.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './apiClient';
import type { LocalizedText } from './decisions';

export type { LocalizedText };

export type MinutesStatus = 'Draft' | 'InReview' | 'Approved' | 'Published' | 'Superseded';

export interface MinutesSummary {
  id: string;
  key: string;
  version: number;
  meetingId: string;
  meetingKey: string;
  status: MinutesStatus;
  publishedAt: string | null;
}

export interface MinutesDetail {
  id: string;
  key: string;
  version: number;
  meetingId: string;
  meetingKey: string;
  meetingTitle: string;
  status: MinutesStatus;
  summary: LocalizedText;
  approvedByUserId: string | null;
  approvedByName: string | null;
  approvedAt: string | null;
  approvedBySoleAuthor: boolean;
  publishedAt: string | null;
  supersededByMinutesId: string | null;
  supersessionReason: LocalizedText | null;
}

/** Version history (newest version first) for a meeting; the head is the current record. */
export function useMinutesForMeeting(meetingId: string | undefined) {
  return useQuery({
    queryKey: ['minutes', 'meeting', meetingId],
    queryFn: () => api<MinutesSummary[]>(`/minutes?meeting=${meetingId}`),
    enabled: !!meetingId,
  });
}

/** Detail by key; a specific version, or the head (current) when version is omitted. */
export function useMinutes(key: string | undefined, version?: number) {
  return useQuery({
    queryKey: ['minutes', 'detail', key, version ?? 'head'],
    queryFn: () => api<MinutesDetail>(`/minutes/${key}${version ? `?version=${version}` : ''}`),
    enabled: !!key,
    retry: false,
  });
}

/** Shared invalidation: refresh the meeting's version list and any detail read for it. */
function useMinutesMutation<TVars, TResult>(meetingId: string | undefined, fn: (vars: TVars) => Promise<TResult>) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['minutes', 'meeting', meetingId] });
      qc.invalidateQueries({ queryKey: ['minutes', 'detail'] });
    },
  });
}

const JSON_HEADERS = { 'Content-Type': 'application/json' } as const;

/** Mirror a single editor string into both bilingual columns (en === ar). */
export const mirror = (value: string): LocalizedText => ({ en: value, ar: value });

/** W10: start a Draft MoM for the meeting (POST /api/minutes → 201 + the new MinutesSummary). */
export function useDraftMinutes(meetingId: string | undefined) {
  return useMinutesMutation(meetingId, (summary: string) =>
    api<MinutesSummary>('/minutes', { method: 'POST', headers: JSON_HEADERS, body: JSON.stringify({ meetingId, summary: mirror(summary) }) }),
  );
}

/** W10: revise the draft body (PUT /api/minutes/{id}). */
export function useReviseMinutes(meetingId: string | undefined) {
  return useMinutesMutation(meetingId, ({ id, summary }: { id: string; summary: string }) =>
    api<MinutesSummary>(`/minutes/${id}`, { method: 'PUT', headers: JSON_HEADERS, body: JSON.stringify({ summary: mirror(summary) }) }),
  );
}

/** W10: submit for review (Draft → InReview). */
export function useSubmitMinutes(meetingId: string | undefined) {
  return useMinutesMutation(meetingId, (id: string) => api<void>(`/minutes/${id}/submit`, { method: 'POST' }));
}

/** W10 (AC-037): request changes (InReview → Draft). */
export function useRequestMinutesChanges(meetingId: string | undefined) {
  return useMinutesMutation(meetingId, (id: string) => api<void>(`/minutes/${id}/request-changes`, { method: 'POST' }));
}

/** W10 (AC-014): approve (InReview → Approved; soft SoD-2). */
export function useApproveMinutes(meetingId: string | undefined) {
  return useMinutesMutation(meetingId, (id: string) => api<void>(`/minutes/${id}/approve`, { method: 'POST' }));
}

/** W10 (AC-038): publish (Approved → Published) + notify all members. */
export function usePublishMinutes(meetingId: string | undefined) {
  return useMinutesMutation(meetingId, (id: string) => api<void>(`/minutes/${id}/publish`, { method: 'POST' }));
}

/** W10 (AC-036): supersede an approved/published MoM with a corrected version (POST /{id}/supersede → 201). */
export function useSupersedeMinutes(meetingId: string | undefined) {
  return useMinutesMutation(meetingId, ({ id, summary, reason }: { id: string; summary: string; reason: string }) =>
    api<MinutesSummary>(`/minutes/${id}/supersede`, {
      method: 'POST',
      headers: JSON_HEADERS,
      body: JSON.stringify({ summary: mirror(summary), reason: mirror(reason) }),
    }),
  );
}
