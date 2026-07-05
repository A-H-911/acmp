/*
 * Actions server state (P8b). Wraps GET /api/actions (paged register) and
 * GET /api/actions/{key} (detail). Read-only this slice — create + the lifecycle
 * transitions (start/block/progress/complete/verify/cancel) land in P8b2.
 *
 * Enums travel as their string names (localized in the UI). Bilingual fields are
 * LocalizedString value objects ({ en, ar }); the SPA picks the locale on read.
 * IsOverdue is server-DERIVED against the request clock — the client never recomputes it.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './apiClient';
import type { PagedResult } from './topics';

/** Bilingual text as the server serializes LocalizedString (camelCase). */
export interface LocalizedText {
  en: string;
  ar: string;
}

/** ActionStatus (docs/domain/entity-lifecycles.md §7) — wire = enum names, localized in the UI. 6 states incl. Cancelled. */
export type ActionStatus = 'Open' | 'InProgress' | 'Blocked' | 'Completed' | 'Verified' | 'Cancelled';

/** ActionPriority (docs/domain/domain-model.md) — wire = enum names. The middle value is `Normal` (UI labels it "Medium"). */
export type ActionPriority = 'Low' | 'Normal' | 'High';

/** The source artifact an action is raised from (W13). No standalone create — always from a context. */
export type ActionSourceType = 'Topic' | 'Meeting' | 'Decision' | 'Condition' | 'Risk';

export interface ActionSummary {
  id: string;
  key: string;
  title: LocalizedText;
  status: ActionStatus;
  priority: string;
  ownerUserId: string;
  ownerName: string;
  dueDate: string | null;
  isOverdue: boolean;
  progressPct: number;
  sourceType: string;
  sourceId: string;
  sourceKey: string | null;
  meetingKey: string | null;
}

export interface ActionDetail extends ActionSummary {
  description: LocalizedText | null;
  blockedReason: LocalizedText | null;
  completionNote: LocalizedText | null;
  cancelReason: LocalizedText | null;
  completedByUserId: string | null;
  completedAt: string | null;
  verifiedByUserId: string | null;
  verifiedByName: string | null;
  verifiedAt: string | null;
  createdAt: string;
}

export interface ActionsParams {
  statuses?: string[];
  owner?: string;
  overdue?: boolean;
  search?: string;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

function toQuery(p: ActionsParams): string {
  const q = new URLSearchParams();
  // The server binds a repeated `status` query param into ActionStatus[].
  p.statuses?.forEach((s) => q.append('status', s));
  if (p.owner) q.set('owner', p.owner);
  if (p.overdue) q.set('overdue', 'true');
  if (p.search) q.set('search', p.search);
  if (p.sortBy) q.set('sortBy', p.sortBy);
  if (p.sortDir) q.set('sortDir', p.sortDir);
  if (p.page) q.set('page', String(p.page));
  if (p.pageSize) q.set('pageSize', String(p.pageSize));
  const s = q.toString();
  return s ? `?${s}` : '';
}

export function useActionsRegister(params: ActionsParams) {
  return useQuery({
    queryKey: ['actions', 'register', params],
    queryFn: () => api<PagedResult<ActionSummary>>(`/actions${toQuery(params)}`),
    // Keep the previous page visible while the next filter/page resolves (no flash back to skeleton).
    placeholderData: (prev) => prev,
  });
}

/**
 * Global header counts — the "N actions" total and "N overdue" badge are filter-INDEPENDENT in the
 * design (computed over the whole set), so they can't come from the filtered/paged list query.
 * ponytail: two pageSize=1 count queries (read only `.total`) — cheapest correct source for ≤20 users;
 * a dedicated stats endpoint would be overkill.
 */
export function useActionsCounts() {
  const total = useQuery({
    queryKey: ['actions', 'count', 'all'],
    queryFn: () => api<PagedResult<ActionSummary>>('/actions?pageSize=1').then((r) => r.total),
  });
  const overdue = useQuery({
    queryKey: ['actions', 'count', 'overdue'],
    queryFn: () => api<PagedResult<ActionSummary>>('/actions?overdue=true&pageSize=1').then((r) => r.total),
  });
  return { total: total.data, overdue: overdue.data };
}

export function useAction(key: string | undefined) {
  return useQuery({
    queryKey: ['actions', 'detail', key],
    queryFn: () => api<ActionDetail>(`/actions/${key}`),
    enabled: !!key,
    retry: false, // a 404 (unknown key) shouldn't retry — surface "not found" immediately
  });
}

/*
 * W14 lifecycle transitions (P8b2a). Each is a POST /api/actions/{id}/{op} by Guid id (the detail DTO
 * carries `id`); the server returns 204. Bilingual reason/note are MIRRORED (en === ar) at the call site,
 * like decisions/minutes. Every transition moves the row's status/overdue facets, so on success we
 * invalidate the WHOLE `actions` family — detail, register list, AND the global header counts — not just
 * the detail (a status change would otherwise leave the register + counts stale).
 */
function post(id: string, op: string, body?: unknown): Promise<void> {
  return api<void>(`/actions/${id}/${op}`, {
    method: 'POST',
    ...(body !== undefined
      ? { headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }
      : {}),
  });
}

/** Shared mutation factory: run a transition, then refetch everything actions-related. */
function useActionTransition<TInput extends { id: string }>(run: (input: TInput) => Promise<void>) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: run,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['actions'] }),
  });
}

export function useStartAction() {
  return useActionTransition(({ id }: { id: string }) => post(id, 'start'));
}
export function useUnblockAction() {
  return useActionTransition(({ id }: { id: string }) => post(id, 'unblock'));
}
export function useVerifyAction() {
  return useActionTransition(({ id }: { id: string }) => post(id, 'verify'));
}
export function useBlockAction() {
  return useActionTransition(({ id, reason }: { id: string; reason: LocalizedText }) => post(id, 'block', { reason }));
}
export function useCancelAction() {
  return useActionTransition(({ id, reason }: { id: string; reason: LocalizedText }) => post(id, 'cancel', { reason }));
}
export function useUpdateActionProgress() {
  return useActionTransition(({ id, progressPct }: { id: string; progressPct: number }) => post(id, 'progress', { progressPct }));
}
export function useCompleteAction() {
  return useActionTransition(({ id, completionNote }: { id: string; completionNote: LocalizedText | null }) =>
    post(id, 'complete', { completionNote }),
  );
}

/** W13 create input — an action is ALWAYS raised from a source artifact (no standalone create). */
export interface CreateActionInput {
  title: LocalizedText;
  description: LocalizedText | null;
  priority: ActionPriority;
  ownerUserId: string;
  ownerName: string;
  dueDate: string | null; // ISO instant, or null
  sourceType: ActionSourceType;
  sourceId: string;
  sourceKey: string | null;
  meetingKey: string | null;
}

/**
 * W13: create a follow-up action (POST /api/actions → 201 + the new ActionSummary, incl. its `key`).
 * Invalidates the actions family so the register + global counts pick up the new row.
 */
export function useCreateAction() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateActionInput) =>
      api<ActionSummary>('/actions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['actions'] }),
  });
}
