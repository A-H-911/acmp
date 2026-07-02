/*
 * Risks server state (P10b). Wraps GET /api/risks (paged register),
 * GET /api/risks/{key} (detail), and POST /api/risks (raise). Read + create this
 * slice — the W15 lifecycle transitions (begin-mitigation/close/escalate/accept +
 * mitigation edits) land in a later slice, mirroring the P8b → P8b2 split.
 *
 * Enums travel as their string names (localized in the UI). Bilingual fields are
 * LocalizedString value objects ({ en, ar }); the SPA picks the locale on read.
 * Severity (1..9) and the Exposure band are server-DERIVED (RiskExposureScale,
 * docs/12) — the client renders the band, never recomputes it.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './apiClient';
import type { PagedResult } from './topics';

/** Bilingual text as the server serializes LocalizedString (camelCase). */
export interface LocalizedText {
  en: string;
  ar: string;
}

/** RiskStatus (docs/12 §10, W15) — wire = enum names, localized in the UI. 5 states. */
export type RiskStatus = 'Open' | 'Mitigating' | 'Closed' | 'Accepted' | 'Escalated';

/** RiskLevel (probability + impact) — wire = enum names. 1-based domain (Low=1..High=3). */
export type RiskLevel = 'Low' | 'Medium' | 'High';

/** Exposure band — the projected L×I overlay (never re-derived in the FE). 4 bands. */
export type RiskExposure = 'Low' | 'Medium' | 'High' | 'Critical';

/** What a risk is raised against (soft cross-module ref, ADR-0001). */
export type RiskSubjectType = 'Topic' | 'Decision' | 'System' | 'Adr';

/** MitigationType (W15) — the create form seeds a Reduce mitigation from "Mitigation plan". */
export type MitigationType = 'Reduce' | 'Transfer' | 'Accept' | 'Avoid';

/** MitigationStatus — forward-only Planned → InProgress → Done. */
export type MitigationStatus = 'Planned' | 'InProgress' | 'Done';

export interface RiskSummary {
  id: string;
  key: string;
  title: LocalizedText;
  status: RiskStatus;
  likelihood: RiskLevel;
  impact: RiskLevel;
  severity: number;
  exposure: RiskExposure;
  ownerUserId: string;
  ownerName: string;
  subjectType: RiskSubjectType;
  subjectId: string;
  subjectKey: string | null;
}

export interface Mitigation {
  id: string;
  description: LocalizedText;
  type: MitigationType;
  status: MitigationStatus;
  ownerUserId: string | null;
  linkedActionId: string | null;
  dueDate: string | null;
}

export interface RiskDetail extends RiskSummary {
  description: LocalizedText | null;
  mitigations: Mitigation[];
  closureNote: LocalizedText | null;
  acceptanceRationale: LocalizedText | null;
  acceptingAuthority: string | null;
  escalationReason: LocalizedText | null;
  escalationTarget: string | null;
  closedAt: string | null;
  createdAt: string;
}

export interface RisksParams {
  statuses?: string[];
  owner?: string;
  exposures?: string[];
  search?: string;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

function toQuery(p: RisksParams): string {
  const q = new URLSearchParams();
  // The server binds repeated `status` / `exposure` query params into enum arrays.
  p.statuses?.forEach((s) => q.append('status', s));
  p.exposures?.forEach((e) => q.append('exposure', e));
  if (p.owner) q.set('owner', p.owner);
  if (p.search) q.set('search', p.search);
  if (p.sortBy) q.set('sortBy', p.sortBy);
  if (p.sortDir) q.set('sortDir', p.sortDir);
  if (p.page) q.set('page', String(p.page));
  if (p.pageSize) q.set('pageSize', String(p.pageSize));
  const s = q.toString();
  return s ? `?${s}` : '';
}

export function useRisksRegister(params: RisksParams) {
  return useQuery({
    queryKey: ['risks', 'register', params],
    queryFn: () => api<PagedResult<RiskSummary>>(`/risks${toQuery(params)}`),
    // Keep the previous page visible while the next filter/page resolves (no flash back to skeleton).
    placeholderData: (prev) => prev,
  });
}

/**
 * Global header counts — the "N risks" total and "N critical" red-dot badge are filter-INDEPENDENT
 * in the design (computed over the whole set), so they can't come from the filtered/paged list query.
 * ponytail: two pageSize=1 count queries (read only `.total`) — cheapest correct source for ≤20 users;
 * a dedicated stats endpoint would be overkill. Critical = the top Exposure band.
 */
export function useRisksCounts() {
  const total = useQuery({
    queryKey: ['risks', 'count', 'all'],
    queryFn: () => api<PagedResult<RiskSummary>>('/risks?pageSize=1').then((r) => r.total),
  });
  const critical = useQuery({
    queryKey: ['risks', 'count', 'critical'],
    queryFn: () => api<PagedResult<RiskSummary>>('/risks?exposure=Critical&pageSize=1').then((r) => r.total),
  });
  return { total: total.data, critical: critical.data };
}

export function useRisk(key: string | undefined) {
  return useQuery({
    queryKey: ['risks', 'detail', key],
    queryFn: () => api<RiskDetail>(`/risks/${key}`),
    enabled: !!key,
    retry: false, // a 404 (unknown key) shouldn't retry — surface "not found" immediately
  });
}

/**
 * W15 create input — a risk is raised against a subject artifact. The design's "Linked topic" is the
 * subject: SubjectType=Topic, SubjectId=topic.id, SubjectKey=topic.key. Description is not collected
 * (design→behavior reconciliation: optional, the form omits it). "Mitigation plan" seeds InitialMitigation.
 */
export interface CreateRiskInput {
  title: LocalizedText;
  description: LocalizedText | null;
  likelihood: RiskLevel;
  impact: RiskLevel;
  ownerUserId: string;
  ownerName: string;
  subjectType: RiskSubjectType;
  subjectId: string;
  subjectKey: string | null;
  initialMitigation: LocalizedText | null;
}

/**
 * W15: raise a risk (POST /api/risks → 201 + the new RiskSummary, incl. its `key`).
 * Invalidates the risks family so the register + global counts pick up the new row.
 */
export function useCreateRisk() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateRiskInput) =>
      api<RiskSummary>('/risks', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['risks'] }),
  });
}
