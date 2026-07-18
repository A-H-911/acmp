/*
 * Decisions server state (P7b). Wraps GET /api/decisions/{key} (detail) and the W21
 * supersede mutation. Mirrors api/meetings.ts: read by key, mutate by Guid id (the detail
 * DTO carries both). Enums travel as string names; the UI localizes them. Bilingual fields
 * are LocalizedString value objects ({ en, ar }) — the SPA picks the locale on read.
 *
 * The /decisions register + record/issue UI are out of P7b scope; this module is the detail
 * read + supersede only, reachable via the DecisionIssued notification deep-link (/decisions/:key).
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './apiClient';

/** Bilingual text as the server serializes LocalizedString (camelCase). */
export interface LocalizedText {
  en: string;
  ar: string;
}

/** DecisionOutcome (README §E) — wire = enum names, localized in the UI. */
export type DecisionOutcome =
  | 'Approved'
  | 'ConditionallyApproved'
  | 'Rejected'
  | 'MoreInfoRequired'
  | 'FeedbackProvided'
  | 'EnhancementsRequired'
  | 'DesignChangesRequired'
  | 'ResearchRequired'
  | 'Deferred'
  | 'Escalated'
  | 'Converted';

export type DecisionStatus = 'Draft' | 'Issued' | 'Superseded';
export type DecisionConditionStatus = 'Open' | 'Met' | 'Waived';

export interface DecisionCondition {
  id: string;
  text: LocalizedText;
  status: DecisionConditionStatus;
  dueDate: string | null;
  linkedActionId: string | null;
}

export interface DecisionDetail {
  id: string;
  key: string;
  topicId: string;
  meetingId: string | null;
  outcome: DecisionOutcome;
  status: DecisionStatus;
  title: LocalizedText;
  statement: LocalizedText;
  rationale: LocalizedText;
  alternatives: LocalizedText | null;
  voteId: string | null;
  chairApprovedByUserId: string | null;
  chairApprovedByName: string | null;
  chairOverride: boolean;
  overrideJustification: LocalizedText | null;
  issuedAt: string | null;
  supersededByDecisionId: string | null;
  supersessionReason: LocalizedText | null;
  conditions: DecisionCondition[];
}

export function useDecision(key: string | undefined) {
  return useQuery({
    queryKey: ['decisions', 'detail', key],
    queryFn: () => api<DecisionDetail>(`/decisions/${key}`),
    enabled: !!key,
    retry: false, // a 404 (unknown key) shouldn't retry — surface "not found" immediately
  });
}

/** Committee-wide decisions register row (GET /api/decisions). */
export interface DecisionSummary {
  id: string;
  key: string;
  topicId: string;
  meetingId: string | null;
  outcome: DecisionOutcome;
  status: DecisionStatus;
  title: LocalizedText;
  issuedAt: string | null;
}

/** P12: the committee-wide decisions register (no `topic` = across all topics). `status`/`limit`
 *  filter server-side; the dashboard's "last 5 issued" passes { status: 'Issued', limit: 5 }. */
export function useDecisionsRegister(params: { status?: DecisionStatus; limit?: number } = {}) {
  const q = new URLSearchParams();
  if (params.status) q.set('status', params.status);
  if (params.limit != null) q.set('limit', String(params.limit));
  const qs = q.toString();
  return useQuery({
    queryKey: ['decisions', 'register', params],
    queryFn: () => api<DecisionSummary[]>(`/decisions${qs ? `?${qs}` : ''}`),
  });
}

/** The successor-decision body the supersede command drafts + auto-issues (W21). */
export interface SupersedeInput {
  priorDecisionId: string;
  outcome: DecisionOutcome;
  title: LocalizedText;
  statement: LocalizedText;
  rationale: LocalizedText;
  alternatives?: LocalizedText | null;
  conditions: { text: LocalizedText; dueDate?: string | null }[];
  reason: LocalizedText;
}

/** W21: supersede an issued decision with a corrected one (POST /{id}/supersede → 201 + the
 *  new DecisionSummary). Invalidates the prior's detail so its Superseded state + back-link show. */
export function useSupersedeDecision(key: string | undefined) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ priorDecisionId, ...body }: SupersedeInput) =>
      api<{ id: string; key: string }>(`/decisions/${priorDecisionId}/supersede`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['decisions', 'detail', key] }),
  });
}

/** The Draft a decision is recorded as (W12, POST /decisions). VoteId couples the ballot the
 *  chair will ratify at issue time; a follow-up outcome later needs a downstream link (AC-029). */
export interface RecordInput {
  topicId: string;
  meetingId?: string | null;
  outcome: DecisionOutcome;
  title: LocalizedText;
  statement: LocalizedText;
  rationale: LocalizedText;
  alternatives?: LocalizedText | null;
  voteId?: string | null;
  conditions: { text: LocalizedText; dueDate?: string | null }[];
}

/** W12 (record): draft a decision against a topic (POST /decisions → 201 + the new DecisionSummary).
 *  Draft is still mutable-by-replacement; the chair issues it separately. Invalidates the register. */
export function useRecordDecision() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: RecordInput) =>
      api<{ id: string; key: string }>('/decisions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['decisions', 'register'] }),
  });
}

/** The chair's issue attempt (W12, POST /decisions/{id}/issue → 204). A chair override (issuing
 *  against the vote) requires a justification; SoD-3 forbids the chair who closed the coupled vote. */
export interface IssueInput {
  id: string;
  chairOverride: boolean;
  overrideJustification?: LocalizedText | null;
}

/** W12 (issue): the Chairman issues a Draft (Draft → Issued). A vote-coupled issue auto-ratifies the
 *  vote in the same transaction, so both the decision and vote caches are refreshed. 403/409 propagate
 *  (SoD-3 denial / AC-029 / vote integrity) for the caller to surface inline. */
export function useIssueDecision() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, chairOverride, overrideJustification }: IssueInput) =>
      api<void>(`/decisions/${id}/issue`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ chairOverride, overrideJustification: overrideJustification ?? null }),
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['decisions'] });
      qc.invalidateQueries({ queryKey: ['votes'] });
    },
  });
}
