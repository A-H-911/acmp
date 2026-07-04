/*
 * Governance / ADRs server state (P11b). Wraps GET /api/adrs (paged register),
 * GET /api/adrs/{key} (MADR detail), POST /api/adrs (author a Draft) and POST
 * /api/adrs/{id}/propose (submit for approval). Read + create this slice — the
 * approve / supersede / deprecate transitions have backend endpoints (P11a) but no
 * UI yet (a later lifecycle slice, mirroring the P8b → P8b2 split on Actions).
 *
 * Enums travel as their string names (localized in the UI). Bilingual fields are
 * LocalizedString value objects ({ en, ar }); the SPA picks the locale on read.
 * Content is entered once and MIRRORED to both columns (en === ar) — the locked
 * FTS pattern shared with Risks/Decisions.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './apiClient';
import type { PagedResult } from './topics';

/** Bilingual text as the server serializes LocalizedString (camelCase). */
export interface LocalizedText {
  en: string;
  ar: string;
}

/**
 * AdrStatus (Governance, W17/W21) — wire = enum names, localized in the UI. 5 canonical states.
 * NOTE: the design's ADR screen shows only 3 state-tabs (proposed/accepted/superseded) and labels the
 * approved state "Accepted"; the platform canon is 5 states with the label "Approved" (settled decision),
 * so the register renders Draft + Deprecated chips the design owes and uses the canon label.
 */
export type AdrStatus = 'Draft' | 'Proposed' | 'Approved' | 'Superseded' | 'Deprecated';

/** A considered option in the MADR body (only the chosen one is flagged). */
export interface AdrOption {
  id: string;
  name: LocalizedText;
  body: LocalizedText | null;
  isChosen: boolean;
}

/** Lean register row (the design's isAdrs list columns) — the full MADR body is the detail. */
export interface AdrSummary {
  id: string;
  key: string;
  title: LocalizedText;
  status: AdrStatus;
  authorName: string;
  approvedAt: string | null;
  createdAt: string;
  isSuperseded: boolean;
}

/** Full MADR-lite record (GET /api/adrs/{key}). Supersession peer keys are resolved in-module. */
export interface AdrDetail {
  id: string;
  key: string;
  title: LocalizedText;
  status: AdrStatus;
  context: LocalizedText;
  decisionDrivers: LocalizedText | null;
  decisionText: LocalizedText;
  consequencesPositive: LocalizedText | null;
  consequencesNegative: LocalizedText | null;
  options: AdrOption[];
  authorUserId: string;
  authorName: string;
  sourceDecisionId: string | null;
  approvedAt: string | null;
  approvedByName: string | null;
  supersededByAdrId: string | null;
  supersededByAdrKey: string | null;
  supersessionReason: LocalizedText | null;
  supersedesAdrId: string | null;
  supersedesAdrKey: string | null;
  deprecationReason: LocalizedText | null;
  createdAt: string;
}

export interface AdrsParams {
  statuses?: string[];
  search?: string;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

function toQuery(p: AdrsParams): string {
  const q = new URLSearchParams();
  // The server binds repeated `status` query params into an AdrStatus[] array.
  p.statuses?.forEach((s) => q.append('status', s));
  if (p.search) q.set('search', p.search);
  if (p.sortBy) q.set('sortBy', p.sortBy);
  if (p.sortDir) q.set('sortDir', p.sortDir);
  if (p.page) q.set('page', String(p.page));
  if (p.pageSize) q.set('pageSize', String(p.pageSize));
  const s = q.toString();
  return s ? `?${s}` : '';
}

export function useAdrsRegister(params: AdrsParams) {
  return useQuery({
    queryKey: ['adrs', 'register', params],
    queryFn: () => api<PagedResult<AdrSummary>>(`/adrs${toQuery(params)}`),
    // Keep the previous page visible while the next filter/page resolves (no flash to skeleton).
    placeholderData: (prev) => prev,
  });
}

/**
 * Header "N records" total — filter-independent in the design (computed over the whole set), so it can't
 * come from the filtered/paged list. ponytail: one pageSize=1 count query (read only `.total`) — cheapest
 * correct source for a ≤20-user committee; a dedicated stats endpoint would be overkill.
 */
export function useAdrsCount() {
  return useQuery({
    queryKey: ['adrs', 'count', 'all'],
    queryFn: () => api<PagedResult<AdrSummary>>('/adrs?pageSize=1').then((r) => r.total),
  });
}

export function useAdr(key: string | undefined) {
  return useQuery({
    queryKey: ['adrs', 'detail', key],
    queryFn: () => api<AdrDetail>(`/adrs/${key}`),
    enabled: !!key,
    retry: false, // a 404 (unknown key) shouldn't retry — surface "not found" immediately
  });
}

/**
 * W17 create input. Title + Context + Decision are the required MADR-lite sections; the two consequence
 * blocks are optional. Drivers and considered-options are optional in the model but not collected in the
 * P11b create form (no draft-edit UI yet), so they stay null. Content is mirrored to both locales.
 */
export interface CreateAdrInput {
  title: LocalizedText;
  context: LocalizedText;
  decisionDrivers: LocalizedText | null;
  decisionText: LocalizedText;
  consequencesPositive: LocalizedText | null;
  consequencesNegative: LocalizedText | null;
  options: null;
}

/**
 * W17: author a new ADR (POST /api/adrs → 201 + the new AdrSummary, incl. `id` and `key`).
 * The new ADR is born in Draft; the caller optionally submits it for approval via useProposeAdr.
 */
export function useCreateAdr() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateAdrInput) =>
      api<AdrSummary>('/adrs', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['adrs'] }),
  });
}

/**
 * W17: submit a Draft for approval (POST /api/adrs/{id}/propose → 204). The design's create form defaults
 * Status to "Proposed", so the dialog chains create → propose; both routes are Adr.Create (the author is
 * permitted). Approve / supersede / deprecate remain a later lifecycle slice.
 */
export function useProposeAdr() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api<void>(`/adrs/${id}/propose`, { method: 'POST' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['adrs'] }),
  });
}

/**
 * FR-068: promote an issued Decision to a new (Draft) ADR (POST /api/adrs/from-decision → 201 AdrSummary).
 * Chairman only (Adr.Promote); the API pre-fills the ADR from the decision and links them bidirectionally.
 * A 409 means the decision isn't Issued or has already been promoted (the message names the existing ADR).
 */
export function usePromoteDecisionToAdr() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (decisionId: string) =>
      api<AdrSummary>('/adrs/from-decision', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ decisionId }),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['adrs'] }),
  });
}
