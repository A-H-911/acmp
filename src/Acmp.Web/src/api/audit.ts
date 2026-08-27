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
import { api, apiBlob } from './apiClient';
import type { PagedResult } from './topics';

export interface AuditEvent {
  sequence: number;
  occurredAt: string;
  hashVersion: number;
  action: string;
  subjectType: string | null;
  subjectId: string | null;
  actor: string | null;
  // Display name resolved server-side via ICommitteeDirectory (includes disabled members, so a
  // departed member's past actions still read as a person). NULL when the subject has no member row
  // — system/integration actors — so the UI must fall back to `actor`, which remains the forensic
  // identity and is kept visible alongside the name.
  actorName: string | null;
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

/** The two formats FR-154 names. The server rejects anything else with a 400. */
export type AuditExportFormat = 'csv' | 'json';

/**
 * Download the audit log (WBS-24.6, FR-154, AC-152). Server-side by necessity, not by preference:
 * control C-AUDIT-08 requires every export to be an audited sensitive event carrying who, scope and
 * volume, and a client-built blob cannot be audited.
 *
 * Takes the SAME AuditParams the register uses, minus paging — an export is the reviewer's current
 * filter over the WHOLE matching set, never one page of it. The server applies one shared predicate to
 * both routes so the file and the screen cannot describe different sets.
 *
 * ⚠ Authorization is the API's (Policies.AuditRead + Policies.ReportExport = {Auditor, Chairman,
 * Secretary}; ADR-0027 excludes Administrator). The route gate in App.tsx already keeps other roles off
 * this screen; this function does not re-decide it, it just surfaces the refusal.
 */
export async function exportAuditLog(
  params: AuditParams,
  format: AuditExportFormat,
): Promise<{ blob: Blob; filename: string }> {
  const { page: _page, pageSize: _pageSize, ...filters } = params;
  const query = toQuery(filters);
  const blob = await apiBlob(`/audit/export${query ? `${query}&` : '?'}format=${format}`);
  // Stamped in UTC by the caller's clock only for the local filename; the file's own rows carry
  // round-trip timestamps from the server, which are the authoritative ones.
  const stamp = new Date().toISOString().slice(0, 19).replace(/[:T-]/g, '');
  return { blob, filename: `acmp-audit-${stamp}.${format}` };
}

/**
 * Hand the blob to the browser. Kept beside the fetch so both halves of "export" live together, and
 * split from it so a test can assert the request without touching the DOM.
 */
export function saveBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  // Revoke on the next tick: revoking synchronously can race the browser's own read of the object URL.
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

export function useAuditRegister(params: AuditParams) {
  return useQuery({
    queryKey: ['audit', 'register', params],
    queryFn: () => api<PagedResult<AuditEvent>>(`/audit${toQuery(params)}`),
    // Keep the previous page visible while the next filter/page resolves (no flash to skeleton).
    placeholderData: (prev) => prev,
  });
}
