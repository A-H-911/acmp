/*
 * Governance / Invariants server state (P11d). Wraps GET /api/invariants (paged register),
 * GET /api/invariants/{key} (detail) and POST /api/invariants (author a Draft). Read + create-Draft
 * this slice — the propose / approve(activate) / retire / supersede transitions have backend endpoints
 * (P11c) but no UI yet (a later governance-lifecycle slice, shared with ADRs).
 *
 * An Invariant is a sibling of an ADR in the same Governance module. Unlike the ADR create dialog it has
 * NO Status field: an Invariant is born Draft (matching the design's create form, which omits Status), so
 * there is no create→propose chain here — create, then route to the detail.
 *
 * Enums travel as their string names (localized in the UI). Bilingual fields are LocalizedString value
 * objects ({ en, ar }); Statement / Rationale / ExceptionsPolicy are entered once and MIRRORED to both
 * columns (en === ar) — the locked FTS pattern shared with ADRs / Risks / Decisions.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './apiClient';
import type { PagedResult } from './topics';
import type { LocalizedText } from './adrs';

// Re-export so Invariant consumers get the bilingual text type from this module, not by reaching into ADRs.
export type { LocalizedText };

/**
 * InvariantStatus (Governance, W18/W21) — wire = enum names, localized in the UI. 5 canonical states.
 * Draft → Proposed → Active → (Retired | Superseded); Proposed can go back to Draft (request-changes).
 */
export type InvariantStatus = 'Draft' | 'Proposed' | 'Active' | 'Retired' | 'Superseded';

/** InvariantCategory (OQ-036 default set — still open, validate with committee). */
export type InvariantCategory = 'Security' | 'Performance' | 'Data' | 'Interoperability' | 'Compliance' | 'Other';

/** InvariantScope — how far the rule reaches. */
export type InvariantScope = 'SingleStream' | 'MultiStream' | 'Platform' | 'OrgWide';

/** Lean register row (the design's isInvTab gInv columns) — the full record is the detail. */
export interface InvariantSummary {
  id: string;
  key: string;
  statement: LocalizedText;
  status: InvariantStatus;
  category: InvariantCategory;
  scope: InvariantScope;
  ownerName: string;
  activatedAt: string | null;
  createdAt: string;
  isSuperseded: boolean;
}

/** Full Invariant record (GET /api/invariants/{key}). Supersession peer keys are resolved in-module. */
export interface InvariantDetail {
  id: string;
  key: string;
  status: InvariantStatus;
  category: InvariantCategory;
  scope: InvariantScope;
  statement: LocalizedText;
  rationale: LocalizedText;
  exceptionsPolicy: LocalizedText | null;
  ownerUserId: string;
  ownerName: string;
  activatedAt: string | null;
  activatedByName: string | null;
  supersededByInvariantId: string | null;
  supersededByInvariantKey: string | null;
  supersessionReason: LocalizedText | null;
  supersedesInvariantId: string | null;
  supersedesInvariantKey: string | null;
  retirementReason: LocalizedText | null;
  createdAt: string;
}

export interface InvariantsParams {
  statuses?: string[];
  search?: string;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

function toQuery(p: InvariantsParams): string {
  const q = new URLSearchParams();
  // The server binds repeated `status` query params into an InvariantStatus[] array.
  p.statuses?.forEach((s) => q.append('status', s));
  if (p.search) q.set('search', p.search);
  if (p.sortBy) q.set('sortBy', p.sortBy);
  if (p.sortDir) q.set('sortDir', p.sortDir);
  if (p.page) q.set('page', String(p.page));
  if (p.pageSize) q.set('pageSize', String(p.pageSize));
  const s = q.toString();
  return s ? `?${s}` : '';
}

export function useInvariantsRegister(params: InvariantsParams) {
  return useQuery({
    queryKey: ['invariants', 'register', params],
    queryFn: () => api<PagedResult<InvariantSummary>>(`/invariants${toQuery(params)}`),
    // Keep the previous page visible while the next filter/page resolves (no flash to skeleton).
    placeholderData: (prev) => prev,
  });
}

/**
 * Header "N invariants" total — filter-independent in the design (computed over the whole set), so it
 * can't come from the filtered/paged list. ponytail: one pageSize=1 count query (read only `.total`) —
 * cheapest correct source for a ≤20-user committee; a dedicated stats endpoint would be overkill.
 */
export function useInvariantsCount() {
  return useQuery({
    queryKey: ['invariants', 'count', 'all'],
    queryFn: () => api<PagedResult<InvariantSummary>>('/invariants?pageSize=1').then((r) => r.total),
  });
}

export function useInvariant(key: string | undefined) {
  return useQuery({
    queryKey: ['invariants', 'detail', key],
    queryFn: () => api<InvariantDetail>(`/invariants/${key}`),
    enabled: !!key,
    retry: false, // a 404 (unknown key) shouldn't retry — surface "not found" immediately
  });
}

/**
 * W18 create input. Category + Scope + Statement + Rationale are the required fields; ExceptionsPolicy and
 * a nominated Owner are optional. Statement / Rationale / ExceptionsPolicy are mirrored to both locales.
 */
export interface CreateInvariantInput {
  category: InvariantCategory;
  scope: InvariantScope;
  statement: LocalizedText;
  rationale: LocalizedText;
  exceptionsPolicy: LocalizedText | null;
  ownerUserId: string;
  ownerName: string;
}

/**
 * W18: author a new Invariant (POST /api/invariants → 201 + the new InvariantSummary, incl. `id` + `key`).
 * The Invariant is born in Draft; there is no create→propose chain (the design's form has no Status field
 * and the propose transition has no UI this slice).
 */
export function useCreateInvariant() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateInvariantInput) =>
      api<InvariantSummary>('/invariants', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['invariants'] }),
  });
}
