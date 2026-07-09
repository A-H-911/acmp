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

/** Meeting type + mode (wire = enum names; localized in the UI). */
export type MeetingType = 'Regular' | 'Extraordinary';
export type MeetingMode = 'InPerson' | 'Hybrid' | 'Remote';

export interface MeetingSummary {
  id: string;
  key: string;
  title: string;
  scheduledStart: string;
  scheduledEnd: string;
  status: string;
  type: string;
  mode: string;
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

/** A roster line as the server records it once a member is marked (P6d). The live
 *  workspace seeds this lazily from /api/members on the first mark. */
export interface AttendanceEntry {
  userId: string;
  name: string;
  role: string;
  status: string;
  isVotingEligible: boolean;
  joinedAt: string | null;
}

/** A discussion note captured against an agenda topic during the live meeting (P6d). */
export interface Discussion {
  topicId: string;
  body: string;
  authorName: string;
  capturedAt: string;
}

export interface RecordingDto {
  source: 'Uploaded' | 'Webex';
  fileName: string | null;
  contentType: string | null;
  sizeBytes: number | null;
  durationSeconds: number | null;
  playbackUrl: string | null;
}

export interface MeetingDetail {
  id: string;
  key: string;
  title: string;
  committeeId: string;
  scheduledStart: string;
  scheduledEnd: string;
  status: string;
  type: string;
  mode: string;
  location: string | null;
  joinUrl: string | null;
  chairUserId: string;
  chairName: string;
  startedAt: string | null;
  heldAt: string | null;
  agenda: Agenda | null;
  attendance: AttendanceEntry[];
  discussions: Discussion[];
  recording?: RecordingDto | null; // always present from the API; optional so older test fixtures compile
}

export function useMeetings() {
  return useQuery({
    queryKey: ['meetings', 'list'],
    queryFn: () => api<MeetingSummary[]>('/meetings'),
  });
}

export interface ScheduleMeetingInput {
  title: string;
  chairUserId: string;
  chairName: string;
  scheduledStart: string; // ISO 8601
  scheduledEnd: string; // ISO 8601
  type: MeetingType;
  mode: MeetingMode;
  location?: string;
  joinUrl?: string;
}

/** W5: schedule a meeting (POST /api/meetings → 201 + the new MeetingSummary). The committee is
 *  implicit server-side (single committee, CON-001), so the caller never sends a committee id. */
export function useScheduleMeeting() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: ScheduleMeetingInput) =>
      api<MeetingSummary>('/meetings', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['meetings', 'list'] }),
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

/** FR-056: upload a meeting recording file (multipart). No Content-Type header — the browser sets the
 *  multipart boundary; the field name `file` matches the server's IFormFile. Returns the RecordingDto. */
export function uploadMeetingRecording(meetingKey: string, file: File): Promise<RecordingDto> {
  const form = new FormData();
  form.append('file', file);
  return api<RecordingDto>(`/meetings/${meetingKey}/recording`, { method: 'POST', body: form });
}

export function useUploadMeetingRecording(key: string | undefined) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (file: File) => uploadMeetingRecording(key!, file),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['meetings', 'detail', key] });
      qc.invalidateQueries({ queryKey: ['meetings', 'recording-url', key] }); // drop the stale presigned URL after replace
    },
  });
}

/** Playback: fetch a short-lived presigned MinIO URL for the uploaded recording (ADR-0014). Enabled only
 *  when the meeting has an uploaded recording; the URL feeds a <video src>. Refetch before the 10-min TTL. */
export function useRecordingUrl(key: string | undefined, enabled: boolean) {
  return useQuery({
    queryKey: ['meetings', 'recording-url', key],
    queryFn: () => api<{ url: string }>(`/meetings/${key}/recording/url`),
    enabled: !!key && enabled,
    retry: false,
    staleTime: 8 * 60 * 1000,
  });
}

/** FR-056: delete a meeting's recording (uploaded file or Webex reference). Invalidates the detail + the
 *  presigned-url query so the UI drops the player immediately. */
export function useDeleteMeetingRecording(key: string | undefined) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api<void>(`/meetings/${key}/recording`, { method: 'DELETE' }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['meetings', 'detail', key] });
      qc.invalidateQueries({ queryKey: ['meetings', 'recording-url', key] });
    },
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

/* ── Live-meeting workspace (P6d) ─────────────────────────────────────────────
 * Lifecycle + capture mutations. Unlike the agenda mutations these return NoContent
 * (the SPA re-reads the detail), and they only invalidate the meeting detail — they
 * don't touch the Prepared pool. */
function useMeetingMutation<TVars>(key: string | undefined, fn: (vars: TVars) => Promise<void>) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['meetings', 'detail', key] }),
  });
}

const JSON_HEADERS = { 'Content-Type': 'application/json' } as const;

/** Begin the meeting (Scheduled→InProgress). The server enforces W7: the agenda must be
 *  Published first, else it returns a problem the caller surfaces. */
export function useStartMeeting(key: string | undefined) {
  return useMeetingMutation(key, ({ meetingId }: { meetingId: string }) =>
    api<void>(`/meetings/${meetingId}/start`, { method: 'POST' }),
  );
}

/** End the meeting (InProgress→Held). Minutes capture is a later phase. */
export function useEndMeeting(key: string | undefined) {
  return useMeetingMutation(key, ({ meetingId }: { meetingId: string }) =>
    api<void>(`/meetings/${meetingId}/end`, { method: 'POST' }),
  );
}

export interface MarkAttendanceInput {
  meetingId: string;
  userId: string;
  name: string;
  role: string; // Chair | Secretary | Member | Reviewer | Presenter | Guest
  status: string; // Invited | Present | Absent | Excused | Late
  isVotingEligible: boolean;
}

export function useMarkAttendance(key: string | undefined) {
  return useMeetingMutation(key, ({ meetingId, ...body }: MarkAttendanceInput) =>
    api<void>(`/meetings/${meetingId}/attendance`, { method: 'POST', headers: JSON_HEADERS, body: JSON.stringify(body) }),
  );
}

/** Capture a discussion note against an agenda topic. The server rejects an empty body,
 *  so the caller must skip the call when the textarea is blank. */
export function useCaptureDiscussion(key: string | undefined) {
  return useMeetingMutation(key, ({ meetingId, topicId, body }: { meetingId: string; topicId: string; body: string }) =>
    api<void>(`/meetings/${meetingId}/discussion`, { method: 'POST', headers: JSON_HEADERS, body: JSON.stringify({ topicId, body }) }),
  );
}

export function useRecordActualTime(key: string | undefined) {
  return useMeetingMutation(
    key,
    ({ meetingId, topicId, actualMinutes, outcome }: { meetingId: string; topicId: string; actualMinutes: number; outcome?: string }) =>
      api<void>(`/meetings/${meetingId}/agenda/items/${topicId}/actual-time`, {
        method: 'POST',
        headers: JSON_HEADERS,
        body: JSON.stringify({ actualMinutes, outcome }),
      }),
  );
}
