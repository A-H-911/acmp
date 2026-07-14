/*
 * Knowledge template server state (P15e; FR-119). Wraps the P15d endpoints: GET /api/knowledge/templates (paged
 * register, + a targetType filter — the P15h pre-fill seam), GET /{key} (detail incl. Markdown body), POST
 * (create → Active), PUT /{id} (edit → Version++; TargetType immutable), POST /{id}/deprecate (soft delete).
 * Reads are committee-wide; every mutation is Template.Manage (Chairman/Secretary/Administrator).
 *
 * Name is bilingual (LocalizedString, entered once + mirrored); Body is a single Markdown string. Enums travel as
 * their string names. Read/nav is by key; mutations are by the template's Guid id.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './apiClient';
import type { LocalizedText } from './research';
import type { PagedResult } from './topics';

export type { LocalizedText };

/** TemplateStatus (P15d) — wire = enum names. */
export type TemplateStatus = 'Active' | 'Deprecated';

/** TemplateTargetType (OQ-051; FR-119) — the artifact a template pre-fills. Spelling mirrors ArtifactType. */
export type TemplateTargetType = 'Topic' | 'Adr' | 'MinutesOfMeeting' | 'ResearchMission';

export const TEMPLATE_TARGET_TYPES: TemplateTargetType[] = ['Topic', 'Adr', 'MinutesOfMeeting', 'ResearchMission'];

export interface TemplateSummary {
  id: string;
  key: string;
  name: LocalizedText;
  targetType: TemplateTargetType;
  status: TemplateStatus;
  version: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface TemplateDetail {
  id: string;
  key: string;
  name: LocalizedText;
  targetType: TemplateTargetType;
  body: string;
  status: TemplateStatus;
  version: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface TemplateParams {
  statuses?: TemplateStatus[];
  targetType?: TemplateTargetType;
  search?: string;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

function toQuery(p: TemplateParams): string {
  const q = new URLSearchParams();
  p.statuses?.forEach((s) => q.append('status', s));
  if (p.targetType) q.set('targetType', p.targetType);
  if (p.search) q.set('search', p.search);
  if (p.sortBy) q.set('sortBy', p.sortBy);
  if (p.sortDir) q.set('sortDir', p.sortDir);
  if (p.page) q.set('page', String(p.page));
  if (p.pageSize) q.set('pageSize', String(p.pageSize));
  const s = q.toString();
  return s ? `?${s}` : '';
}

export function useTemplates(params: TemplateParams = {}) {
  return useQuery({
    queryKey: ['templates', 'register', params],
    queryFn: () => api<PagedResult<TemplateSummary>>(`/knowledge/templates${toQuery({ pageSize: 200, ...params })}`),
    placeholderData: (prev) => prev,
  });
}

export function useTemplate(key: string | undefined) {
  return useQuery({
    queryKey: ['templates', 'detail', key],
    queryFn: () => api<TemplateDetail>(`/knowledge/templates/${key}`),
    enabled: !!key,
    retry: false,
  });
}

export interface CreateTemplateInput {
  name: LocalizedText;
  targetType: TemplateTargetType;
  body: string;
}

export function useCreateTemplate() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateTemplateInput) =>
      api<TemplateSummary>('/knowledge/templates', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['templates'] }),
  });
}

/** Edit revises Name + Body only — TargetType is immutable after creation. */
export function useEditTemplate() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, name, body }: { id: string; name: LocalizedText; body: string }) =>
      api<TemplateSummary>(`/knowledge/templates/${id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, body }),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['templates'] }),
  });
}

export function useDeprecateTemplate() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api<void>(`/knowledge/templates/${id}/deprecate`, { method: 'POST' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['templates'] }),
  });
}
