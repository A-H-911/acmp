/*
 * Topics server state (P5b). Wraps GET /api/topics (backlog) and the prioritize
 * mutation. Enums travel as their string names (JsonStringEnumConverter on the
 * server); the UI localizes them. Read is by key (GET /{key}); every mutation is
 * by Guid id (/{id}/…) — so summaries/detail both carry `id` for write calls.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
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
  timesDeferred: number;
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

// W1 submit (POST /api/topics). Enums travel as string names; Source is defaulted client-side
// (CommitteeMember) and Scope is derived server-side — neither has a picker on the form (P5a decision).
export interface SubmitTopicInput {
  title: string;
  description: string;
  justification: string;
  type: string;
  urgency: string;
  source: string;
  streams: string[];
  systems: string[];
  tags: string[];
}

export interface SubmitTopicResult {
  id: string;
  key: string;
}

export function useSubmitTopic() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: SubmitTopicInput) =>
      api<SubmitTopicResult>('/topics', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['topics', 'backlog'] }),
  });
}

// W16 (P15c): convert a research mission — or one accepted recommendation — into a new execution topic
// (POST /api/topics/from-research → 201 { id, key }). Target-owns (ADR-0021): the Topics module reads the
// research source and writes the reverse Informs edge. Chairman/Secretary only (API-enforced). A 409 names
// the ineligible/already-converted source. Invalidates the backlog so the new topic appears.
export interface ConvertResearchToTopicInput {
  missionId: string;
  recommendationId?: string | null;
  title: string;
  description: string;
  justification: string;
  type: string;
  urgency: string;
  streams: string[];
  systems: string[];
  tags: string[];
}

export function useConvertResearchToTopic() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: ConvertResearchToTopicInput) =>
      api<SubmitTopicResult>('/topics/from-research', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['topics', 'backlog'] }),
  });
}

/** Upload one staged file to a created topic (AC-049/050). Multipart — no Content-Type header so the
 *  browser sets the boundary; the field name is `file` to match the IFormFile parameter. */
export function uploadTopicAttachment(topicId: string, file: File): Promise<unknown> {
  const form = new FormData();
  form.append('file', file);
  return api<unknown>(`/topics/${topicId}/attachments`, { method: 'POST', body: form });
}

/** Upload a file to an EXISTING topic from the detail screen's Attachments tab; invalidates the
 *  detail query so the new attachment appears. (Submit-time upload uses uploadTopicAttachment directly.) */
export function useUploadTopicAttachment(key: string | undefined) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ topicId, file }: { topicId: string; file: File }) => uploadTopicAttachment(topicId, file),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['topics', 'detail', key] }),
  });
}

// Topic detail (GET /api/topics/{key}). Read by key; comment/edit mutations are by Guid id.
export interface TopicHistoryEntry {
  from: string;
  to: string;
  reason: string | null;
  actorName: string;
  occurredAt: string;
}
export interface TopicComment {
  id: string;
  body: string;
  authorName: string;
  postedAt: string;
}
export interface TopicAttachment {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedByName: string;
  uploadedAt: string;
}
export interface TopicDetail {
  id: string;
  key: string;
  title: string;
  description: string;
  justification: string;
  type: string;
  status: string;
  urgency: string;
  scope: string;
  source: string;
  streams: string[];
  systems: string[];
  tags: string[];
  ownerId: string | null;
  ownerName: string | null;
  submittedByName: string;
  priority: number;
  ageDays: number;
  slaBreached: boolean;
  createdAt: string;
  revisitOn: string | null;
  history: TopicHistoryEntry[];
  comments: TopicComment[];
  attachments: TopicAttachment[];
}

export function useTopicDetail(key: string | undefined) {
  return useQuery({
    queryKey: ['topics', 'detail', key],
    queryFn: () => api<TopicDetail>(`/topics/${key}`),
    enabled: !!key,
    retry: false, // a 404 (unknown key) shouldn't retry — surface "not found" immediately
  });
}

// Triage transitions surfaced by the kanban board (W2/W20). Each invalidates the backlog so the moved
// card re-buckets. Accept needs an owner; reject/defer need a reason (defer also an optional revisit date).
export function useAcceptTopic() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ topicId, ownerId, ownerName }: { topicId: string; ownerId: string; ownerName: string }) =>
      api<void>(`/topics/${topicId}/accept`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ownerId, ownerName }),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['topics', 'backlog'] }),
  });
}

// AC-043 / FR-034: keyboard move-up/down reorder — a single ±1 priority delta within the topic's kanban column.
export function useMoveTopicPriority() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ topicId, delta }: { topicId: string; delta: 1 | -1 }) =>
      api<void>(`/topics/${topicId}/priority/move`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ delta }),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['topics', 'backlog'] }),
  });
}

export function useReturnTopic() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ topicId, mode, reason, revisitOn }: { topicId: string; mode: 'reject' | 'defer'; reason: string; revisitOn?: string | null }) =>
      mode === 'reject'
        ? api<void>(`/topics/${topicId}/reject`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ reason }) })
        : api<void>(`/topics/${topicId}/defer`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ reason, revisitOn: revisitOn ?? null }) }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['topics', 'backlog'] }),
  });
}

/**
 * W4 (AC-035): mark an Accepted topic Prepared so it enters the agenda pool. This is the affordance the
 * SPA was missing (D-15) — without it no topic ever reached Prepared and the agenda builder's pool
 * (`['topics','prepared']`, GET /topics?status=Prepared) stayed empty by construction. Invalidate the
 * backlog (re-buckets the card), the prepared pool (unblocks the agenda), and this topic's detail.
 * Show-and-enforce: the button renders for any Accepted topic; the backend 403s a non-owner/non-Secretary.
 */
export function usePrepareTopic(key: string | undefined) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (topicId: string) => api<void>(`/topics/${topicId}/prepare`, { method: 'POST' }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['topics', 'backlog'] });
      qc.invalidateQueries({ queryKey: ['topics', 'prepared'] });
      qc.invalidateQueries({ queryKey: ['topics', 'detail', key] });
    },
  });
}

/** Post a discussion comment (BL-033). Body field is `reason` (the endpoint's ReasonBody). */
export function useAddTopicComment(key: string | undefined) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ topicId, body }: { topicId: string; body: string }) =>
      api<{ id: string }>(`/topics/${topicId}/comments`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ reason: body }),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['topics', 'detail', key] }),
  });
}
