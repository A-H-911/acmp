/*
 * Audit server state (PR4, AC-017/020). Wraps the read-only GET /api/audit register
 * (Auditor/Chairman/Secretary only — ADR-0027; the API enforces, the FE route-gates).
 * There are NO write hooks: the AuditEvent log is append-only and immutable by construction.
 *
 * The store holds two row shapes; the API's AuditEventDto already normalizes them
 * (Action = Action ?? EventType, Actor = ActorUserId ?? Subject), so the FE reads one
 * uniform shape. Enriched (v2) fields are nullable: v1 system/authZ rows have
 * subjectType/subjectId/outcome/before/after null.
 */
import { useQuery } from '@tanstack/react-query';
import { api } from './apiClient';
import type { PagedResult } from './topics';

export interface AuditEvent {
  sequence: number;
  occurredAt: string;
  hashVersion: number;
  action: string;
  subjectType: string | null;
  subjectId: string | null;
  actor: string | null;
  actorRole: string | null;
  outcome: string | null;
  beforeJson: string | null;
  afterJson: string | null;
  correlationId: string | null;
}

export interface AuditParams {
  entityType?: string;
  actor?: string;
  action?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

function toQuery(p: AuditParams): string {
  const q = new URLSearchParams();
  if (p.entityType) q.set('entityType', p.entityType);
  if (p.actor) q.set('actor', p.actor);
  if (p.action) q.set('action', p.action);
  if (p.from) q.set('from', p.from);
  if (p.to) q.set('to', p.to);
  if (p.page) q.set('page', String(p.page));
  if (p.pageSize) q.set('pageSize', String(p.pageSize));
  const s = q.toString();
  return s ? `?${s}` : '';
}

export function useAuditRegister(params: AuditParams) {
  return useQuery({
    queryKey: ['audit', 'register', params],
    queryFn: () => api<PagedResult<AuditEvent>>(`/audit${toQuery(params)}`),
    // Keep the previous page visible while the next filter/page resolves (no flash to skeleton).
    placeholderData: (prev) => prev,
  });
}
