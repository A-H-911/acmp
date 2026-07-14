/*
 * Knowledge wiki (Document) server state (P15e). Wraps the P15d endpoints: GET /api/knowledge/documents (paged
 * register — the tree groups it by category), GET /api/knowledge/documents/{key} (detail incl. version history),
 * POST (create → Draft), PUT /{id} (edit → Version++ + snapshot), and the /{id}/publish|archive transitions.
 * Reads are committee-wide; every mutation is Document.Manage (Chairman/Secretary) — the UI gates the affordances,
 * the API is always the real gate.
 *
 * Enums travel as their string names; bilingual fields are LocalizedString ({ en, ar }), entered once and mirrored
 * (en === ar, the locked pattern). Read/nav is by key; every mutation is by the document's Guid id.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './apiClient';
import type { LocalizedText } from './research';
import type { PagedResult } from './topics';

export type { LocalizedText };

/** DocumentStatus (P15d; FR-116) — wire = enum names, localized in the UI. */
export type DocumentStatus = 'Draft' | 'Published' | 'Archived';

export interface DocumentVersion {
  id: string;
  version: number;
  title: LocalizedText;
  body: LocalizedText;
  savedAt: string;
  savedByUserId: string;
}

export interface DocumentSummary {
  id: string;
  key: string;
  title: LocalizedText;
  status: DocumentStatus;
  category: string;
  tags: string[];
  ownerUserId: string;
  version: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface DocumentDetail {
  id: string;
  key: string;
  title: LocalizedText;
  body: LocalizedText;
  status: DocumentStatus;
  category: string;
  tags: string[];
  ownerUserId: string;
  version: number;
  versions: DocumentVersion[];
  createdAt: string;
  updatedAt: string | null;
}

export interface WikiParams {
  statuses?: DocumentStatus[];
  category?: string;
  search?: string;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

function toQuery(p: WikiParams): string {
  const q = new URLSearchParams();
  p.statuses?.forEach((s) => q.append('status', s));
  if (p.category) q.set('category', p.category);
  if (p.search) q.set('search', p.search);
  if (p.sortBy) q.set('sortBy', p.sortBy);
  if (p.sortDir) q.set('sortDir', p.sortDir);
  if (p.page) q.set('page', String(p.page));
  if (p.pageSize) q.set('pageSize', String(p.pageSize));
  const s = q.toString();
  return s ? `?${s}` : '';
}

/**
 * The whole document set for the wiki tree (grouped client-side by category → "spaces"). ponytail: one large
 * page instead of typeahead — right-sized for one low-traffic committee (≤20 users, few hundred pages max).
 */
export function useWikiDocuments(params: WikiParams = {}) {
  return useQuery({
    queryKey: ['wiki', 'register', params],
    queryFn: () => api<PagedResult<DocumentSummary>>(`/knowledge/documents${toQuery({ pageSize: 500, ...params })}`),
    placeholderData: (prev) => prev,
  });
}

export function useDocument(key: string | undefined) {
  return useQuery({
    queryKey: ['wiki', 'detail', key],
    queryFn: () => api<DocumentDetail>(`/knowledge/documents/${key}`),
    enabled: !!key,
    retry: false, // a 404 (unknown key) shouldn't retry — surface "not found" immediately
  });
}

export interface DocumentInput {
  title: LocalizedText;
  category: string;
  body: LocalizedText;
  tags?: string[];
}

export function useCreateDocument() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: DocumentInput) =>
      api<DocumentSummary>('/knowledge/documents', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['wiki'] }),
  });
}

export function useEditDocument() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...input }: DocumentInput & { id: string }) =>
      api<DocumentSummary>(`/knowledge/documents/${id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['wiki'] }),
  });
}

/** POST /api/knowledge/documents/{id}/{transition} (204). */
function useDocumentTransition(transition: 'publish' | 'archive') {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api<void>(`/knowledge/documents/${id}/${transition}`, { method: 'POST' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['wiki'] }),
  });
}

export function usePublishDocument() {
  return useDocumentTransition('publish');
}

export function useArchiveDocument() {
  return useDocumentTransition('archive');
}
