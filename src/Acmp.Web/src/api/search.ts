/*
 * Global search server state (P15g, FR-143/144/145). Wraps GET /api/search?q= — the
 * backend fans out over every module's ISearchProvider and returns hits grouped by
 * artifact type (Topics, Decisions, ADRs, MoMs, wiki Documents). Bilingual titles are
 * LocalizedString value objects ({ en, ar }); the SPA picks the locale on read. Status
 * travels as its enum name and is shown as data. The query only runs for a non-blank term.
 */
import { useQuery } from '@tanstack/react-query';
import { api } from './apiClient';

/** Bilingual text as the server serializes LocalizedString (camelCase). */
export interface LocalizedText {
  en: string;
  ar: string;
}

/** One result row (FR-144): ID (key), title, matched excerpt, status, and a deep link. */
export interface SearchHit {
  type: string;
  id: string;
  key: string;
  title: LocalizedText;
  excerpt: string;
  status: string;
  deepLink: string;
}

/** Results for one artifact type, as grouped by the API (AC-060). */
export interface SearchGroup {
  type: string;
  items: SearchHit[];
}

export function useSearch(query: string) {
  const q = query.trim();
  return useQuery({
    queryKey: ['search', q],
    queryFn: () => api<SearchGroup[]>(`/search?q=${encodeURIComponent(q)}`),
    enabled: q.length > 0,
  });
}
