/*
 * Dependencies server state (P10e). Wraps GET /api/dependencies (paged register),
 * GET /api/dependencies/{key} (edge detail), GET /api/dependencies/artifact/{type}/{id}
 * (the by-artifact panel — outbound + inbound edges), and POST /api/dependencies (create).
 * Resolve/remove lifecycle transitions land in a later slice, mirroring the P10b read+create
 * split (the W15 transitions came after the Risks register).
 *
 * A Dependency is a self-describing directed edge (ADR-0019): both ends carry a create-time
 * key + title snapshot, so the FE never resolves the far artifact cross-module (ADR-0001).
 * Enums travel as their string names, localized in the UI. IsBlocker is server-DERIVED
 * (Kind ∈ {BlockedBy, Blocks} && Status == Open) — the client renders it, never recomputes it.
 * IsCrossStream (FR-095) is NOT on the wire — its cross-module derivation is deferred (ASM-016),
 * so the design's Cross-stream column/filter is omitted this slice (honest, not em-dash theater).
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './apiClient';
import type { PagedResult } from './topics';

/** What a dependency edge can point at (DependencyEndpointType) — wire = enum names. */
export type DependencyEndpointType = 'Topic' | 'Action' | 'System' | 'Decision';

/** DependencyKind — the design's 4 relation chips. */
export type DependencyKind = 'DependsOn' | 'BlockedBy' | 'Blocks' | 'RelatesTo';

/** DependencyStatus — Open → Resolved | Removed (Removed = soft-delete, excluded by default). */
export type DependencyStatus = 'Open' | 'Resolved' | 'Removed';

/** Every DependencyEndpointType value (i18n-parity coverage). */
export const DEPENDENCY_ENDPOINT_TYPES: DependencyEndpointType[] = ['Topic', 'Action', 'System', 'Decision'];

/** One register row (the deps table): both endpoints + kind/status + the derived blocker flag. */
export interface DependencySummary {
  id: string;
  key: string;
  fromType: DependencyEndpointType;
  fromId: string;
  fromKey: string;
  fromTitle: string;
  toType: DependencyEndpointType;
  toId: string;
  toKey: string;
  toTitle: string;
  kind: DependencyKind;
  status: DependencyStatus;
  isBlocker: boolean;
}

/** Full detail for one edge (by key) — adds the free-text note + created timestamp. */
export interface DependencyDetail extends DependencySummary {
  note: string | null;
  createdAt: string;
}

/**
 * One edge on an artifact's dependency panel — the FAR endpoint (relative to the viewed artifact)
 * plus kind/status + the derived blocker flag. Direction is implied by which list it is in
 * (outbound = this artifact is the From end; inbound = this artifact is the To end).
 */
export interface DependencyEdge {
  id: string;
  key: string;
  otherType: DependencyEndpointType;
  otherId: string;
  otherKey: string;
  otherTitle: string;
  kind: DependencyKind;
  status: DependencyStatus;
  isBlocker: boolean;
}

export interface ArtifactDependencies {
  outbound: DependencyEdge[];
  inbound: DependencyEdge[];
}

export interface DependenciesParams {
  kind?: string;
  status?: string;
  blockedOnly?: boolean;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

function toQuery(p: DependenciesParams): string {
  const q = new URLSearchParams();
  if (p.kind) q.set('kind', p.kind);
  if (p.status) q.set('status', p.status);
  if (p.blockedOnly) q.set('blockedOnly', 'true');
  if (p.sortBy) q.set('sortBy', p.sortBy);
  if (p.sortDir) q.set('sortDir', p.sortDir);
  if (p.page) q.set('page', String(p.page));
  if (p.pageSize) q.set('pageSize', String(p.pageSize));
  const s = q.toString();
  return s ? `?${s}` : '';
}

export function useDependenciesRegister(params: DependenciesParams) {
  return useQuery({
    queryKey: ['dependencies', 'register', params],
    queryFn: () => api<PagedResult<DependencySummary>>(`/dependencies${toQuery(params)}`),
    placeholderData: (prev) => prev,
  });
}

/**
 * Global header counts — the "N links" total and the "N blocked" red-dot badge are filter-INDEPENDENT
 * in the design (computed over the whole set). ponytail: two pageSize=1 count reads — cheapest correct
 * source for ≤20 users; a stats endpoint would be overkill. Blocked = the blockedOnly-filtered total.
 */
export function useDependenciesCounts() {
  const total = useQuery({
    queryKey: ['dependencies', 'count', 'all'],
    queryFn: () => api<PagedResult<DependencySummary>>('/dependencies?pageSize=1').then((r) => r.total),
  });
  const blocked = useQuery({
    queryKey: ['dependencies', 'count', 'blocked'],
    queryFn: () => api<PagedResult<DependencySummary>>('/dependencies?blockedOnly=true&pageSize=1').then((r) => r.total),
  });
  return { total: total.data, blocked: blocked.data };
}

export function useDependency(key: string | undefined) {
  return useQuery({
    queryKey: ['dependencies', 'detail', key],
    queryFn: () => api<DependencyDetail>(`/dependencies/${key}`),
    enabled: !!key,
    retry: false, // a 404 (unknown key) shouldn't retry — surface "not found" immediately
  });
}

/**
 * The dependency panel for one artifact (outbound + inbound). Only the 4 DependencyEndpointType
 * artifacts have dep edges; callers on other artifact types (e.g. Risk) pass a falsy type to skip.
 */
export function useArtifactDependencies(type: DependencyEndpointType | undefined, id: string | undefined) {
  return useQuery({
    queryKey: ['dependencies', 'artifact', type, id],
    queryFn: () => api<ArtifactDependencies>(`/dependencies/artifact/${type}/${id}`),
    enabled: !!type && !!id,
  });
}

/** Create input — endpoint snapshots for both ends + the kind + an optional note. */
export interface CreateDependencyInput {
  fromType: DependencyEndpointType;
  fromId: string;
  fromKey: string;
  fromTitle: string;
  toType: DependencyEndpointType;
  toId: string;
  toKey: string;
  toTitle: string;
  kind: DependencyKind;
  note: string | null;
}

/**
 * Create a typed dependency (POST /api/dependencies → 201 + { key }). Chairman/Secretary only
 * (API-enforced). Invalidates the dependencies family so the register, counts, and any open panel
 * pick up the new edge.
 */
export function useCreateDependency() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateDependencyInput) =>
      api<{ key: string }>('/dependencies', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['dependencies'] }),
  });
}
