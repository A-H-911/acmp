/*
 * Research server state (P15b). Wraps the P15a endpoints: GET /api/research (paged register),
 * GET /api/research/{key} (mission detail), POST /api/research (create), the lifecycle transitions
 * (activate/complete/cancel), and the finding/recommendation mutations. Reads are committee-wide;
 * every mutation is Research.Manage (Chairman/Secretary) — the UI gates the affordances, the API
 * is always the real gate.
 *
 * Enums travel as their string names (JsonStringEnumConverter on the server); the UI localizes them.
 * Bilingual fields are LocalizedString value objects ({ en, ar }) — entered once, mirrored to both
 * (en === ar), the locked FTS pattern. Read is by key; every mutation is by the mission's Guid id.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './apiClient';
import type { PagedResult } from './topics';

/** Bilingual text as the server serializes LocalizedString (camelCase). */
export interface LocalizedText {
  en: string;
  ar: string;
}

/** ResearchMissionStatus (P15a; FR-111) — wire = enum names, localized in the UI. 4 states. */
export type ResearchStatus = 'Proposed' | 'Active' | 'Completed' | 'Cancelled';

/** A finding's confidence band (FR-113) — wire = enum names. */
export type Confidence = 'Low' | 'Medium' | 'High';

/** A recommendation's priority band (FR-113) — wire = enum names. */
export type RecommendationPriority = 'Low' | 'Medium' | 'High';

/** A recommendation's disposition (FR-113). P15a allows Proposed → Accepted | Rejected only. */
export type RecommendationStatus = 'Proposed' | 'Accepted' | 'Rejected';

export interface Finding {
  id: string;
  key: string;
  summary: LocalizedText;
  detail: LocalizedText | null;
  confidence: Confidence;
  isVerified: boolean;
}

export interface Recommendation {
  id: string;
  key: string;
  statement: LocalizedText;
  rationale: LocalizedText | null;
  priority: RecommendationPriority;
  status: RecommendationStatus;
  linkedTopicId: string | null;
}

export interface MissionSummary {
  id: string;
  key: string;
  title: LocalizedText;
  status: ResearchStatus;
  ownerName: string;
  createdAt: string;
  findingCount: number;
  recommendationCount: number;
}

export interface MissionDetail {
  id: string;
  key: string;
  title: LocalizedText;
  question: LocalizedText;
  status: ResearchStatus;
  ownerUserId: string;
  ownerName: string;
  keystonePackageRef: string | null;
  sourceTopicId: string | null;
  completedAt: string | null;
  cancellationReason: LocalizedText | null;
  findings: Finding[];
  recommendations: Recommendation[];
  createdAt: string;
}

export interface ResearchParams {
  statuses?: string[];
  search?: string;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

function toQuery(p: ResearchParams): string {
  const q = new URLSearchParams();
  // The server binds repeated `status` query params into a ResearchMissionStatus[] filter.
  p.statuses?.forEach((s) => q.append('status', s));
  if (p.search) q.set('search', p.search);
  if (p.sortBy) q.set('sortBy', p.sortBy);
  if (p.sortDir) q.set('sortDir', p.sortDir);
  if (p.page) q.set('page', String(p.page));
  if (p.pageSize) q.set('pageSize', String(p.pageSize));
  const s = q.toString();
  return s ? `?${s}` : '';
}

export function useResearchRegister(params: ResearchParams) {
  return useQuery({
    queryKey: ['research', 'register', params],
    queryFn: () => api<PagedResult<MissionSummary>>(`/research${toQuery(params)}`),
    // Keep the previous page visible while the next filter/page resolves (no flash back to skeleton).
    placeholderData: (prev) => prev,
  });
}

/**
 * Global header count — the "N missions" total is filter-INDEPENDENT in the design (computed over the
 * whole set), so it can't come from the filtered/paged list query.
 * ponytail: one pageSize=1 count query (reads only `.total`) — cheapest correct source for ≤20 users.
 */
export function useResearchCounts() {
  const total = useQuery({
    queryKey: ['research', 'count', 'all'],
    queryFn: () => api<PagedResult<MissionSummary>>('/research?pageSize=1').then((r) => r.total),
  });
  return { total: total.data };
}

export function useMission(key: string | undefined) {
  return useQuery({
    queryKey: ['research', 'detail', key],
    queryFn: () => api<MissionDetail>(`/research/${key}`),
    enabled: !!key,
    retry: false, // a 404 (unknown key) shouldn't retry — surface "not found" immediately
  });
}

/** FR-111 create input — owner is derived from the current user server-side (no owner picker). */
export interface CreateMissionInput {
  title: LocalizedText;
  question: LocalizedText;
  keystonePackageRef: string | null;
  sourceTopicId: string | null;
}

export function useCreateMission() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateMissionInput) =>
      api<MissionSummary>('/research', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['research'] }),
  });
}

/** POST /api/research/{id}/{transition} (204). No body except cancel (a reason). */
function useMissionTransition<TVars extends { id: string }>(path: (v: TVars) => string, body?: (v: TVars) => unknown) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: TVars) =>
      api<void>(path(vars), {
        method: 'POST',
        ...(body ? { headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body(vars)) } : {}),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['research'] }),
  });
}

export function useActivateMission() {
  return useMissionTransition<{ id: string }>((v) => `/research/${v.id}/activate`);
}

export function useCompleteMission() {
  return useMissionTransition<{ id: string }>((v) => `/research/${v.id}/complete`);
}

export function useCancelMission() {
  return useMissionTransition<{ id: string; reason: LocalizedText }>(
    (v) => `/research/${v.id}/cancel`,
    (v) => ({ reason: v.reason }),
  );
}

export function useAddFinding() {
  return useMissionTransition<{ id: string; summary: LocalizedText; detail: LocalizedText | null; confidence: Confidence }>(
    (v) => `/research/${v.id}/findings`,
    (v) => ({ summary: v.summary, detail: v.detail, confidence: v.confidence }),
  );
}

export function useVerifyFinding() {
  return useMissionTransition<{ id: string; findingId: string }>((v) => `/research/${v.id}/findings/${v.findingId}/verify`);
}

export function useAddRecommendation() {
  return useMissionTransition<{ id: string; statement: LocalizedText; rationale: LocalizedText | null; priority: RecommendationPriority }>(
    (v) => `/research/${v.id}/recommendations`,
    (v) => ({ statement: v.statement, rationale: v.rationale, priority: v.priority, linkedTopicId: null }),
  );
}

export function useSetRecommendationStatus() {
  return useMissionTransition<{ id: string; recommendationId: string; status: RecommendationStatus }>(
    (v) => `/research/${v.id}/recommendations/${v.recommendationId}/status`,
    (v) => ({ status: v.status }),
  );
}
