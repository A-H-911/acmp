import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import {
  useWikiDocuments, useDocument, useCreateDocument, useEditDocument,
  usePublishDocument, useArchiveDocument,
} from './wiki';
import { ApiError } from './apiClient';
import { makeQueryWrapper, stubFetch, lastBody } from '../test/queryHarness';

/* Real wiki hooks vs a stubbed fetch — assert URL building, retry rules, bodies, invalidation. */
afterEach(() => vi.unstubAllGlobals());

const urlOf = (spy: ReturnType<typeof stubFetch>) => String(spy.mock.calls.at(-1)![0]);
const initOf = (spy: ReturnType<typeof stubFetch>) => spy.mock.calls.at(-1)![1] as RequestInit;
const page = () => ({ items: [], total: 0, page: 1, pageSize: 500, totalPages: 1 });
const loc = { en: 'x', ar: 'x' };

describe('useWikiDocuments', () => {
  it('builds the query string from every filter/sort/page param (repeated status, default pageSize overridden)', async () => {
    const spy = stubFetch(() => ({ jsonBody: page() }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(
      () => useWikiDocuments({ statuses: ['Draft', 'Published'], category: 'Governance', search: 'idp', sortBy: 'title', sortDir: 'asc', page: 2, pageSize: 25 }),
      { wrapper },
    );
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/knowledge/documents?status=Draft&status=Published&category=Governance&search=idp&sortBy=title&sortDir=asc&page=2&pageSize=25');
  });

  it('defaults to a single large page when no params are set', async () => {
    const spy = stubFetch(() => ({ jsonBody: page() }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useWikiDocuments(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/knowledge/documents?pageSize=500');
  });
});

describe('useDocument', () => {
  it('stays idle without a key, then reads by key', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: '1', key: 'DOC-2026-001' } }));
    const { wrapper } = makeQueryWrapper();
    const { rerender, result } = renderHook(({ k }: { k?: string }) => useDocument(k), { wrapper, initialProps: {} as { k?: string } });
    await new Promise((r) => setTimeout(r, 0));
    expect(spy).not.toHaveBeenCalled();
    rerender({ k: 'DOC-2026-001' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/knowledge/documents/DOC-2026-001');
  });

  it('surfaces a 404 without retrying', async () => {
    stubFetch(() => ({ status: 404, jsonBody: { title: 'Not found' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useDocument('DOC-2026-999'), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as ApiError).status).toBe(404);
  });
});

describe('document mutations', () => {
  it('create POSTs the body to /knowledge/documents and invalidates the wiki family', async () => {
    const spy = stubFetch(() => ({ jsonBody: { key: 'DOC-2026-002' } }));
    const { client, wrapper } = makeQueryWrapper();
    const inval = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useCreateDocument(), { wrapper });
    const out = await result.current.mutateAsync({ title: loc, category: 'Governance', body: loc, tags: ['a'] });
    expect(urlOf(spy)).toBe('/api/knowledge/documents');
    expect(initOf(spy).method).toBe('POST');
    expect(lastBody(spy)).toMatchObject({ title: loc, category: 'Governance', body: loc, tags: ['a'] });
    expect(out.key).toBe('DOC-2026-002');
    expect(inval).toHaveBeenCalledWith({ queryKey: ['wiki'] });
  });

  it('edit PUTs to /knowledge/documents/{id} with the body (id stripped)', async () => {
    const spy = stubFetch(() => ({ jsonBody: { key: 'DOC-2026-002' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useEditDocument(), { wrapper });
    await result.current.mutateAsync({ id: 'g1', title: loc, category: 'Standards', body: loc, tags: [] });
    expect(urlOf(spy)).toBe('/api/knowledge/documents/g1');
    expect(initOf(spy).method).toBe('PUT');
    expect(lastBody(spy)).toEqual({ title: loc, category: 'Standards', body: loc, tags: [] });
  });

  it('publish POSTs to the publish sub-route', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { client, wrapper } = makeQueryWrapper();
    const inval = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => usePublishDocument(), { wrapper });
    await result.current.mutateAsync('g1');
    expect(urlOf(spy)).toBe('/api/knowledge/documents/g1/publish');
    expect(initOf(spy).method).toBe('POST');
    expect(inval).toHaveBeenCalledWith({ queryKey: ['wiki'] });
  });

  it('archive POSTs to the archive sub-route', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useArchiveDocument(), { wrapper });
    await result.current.mutateAsync('g1');
    expect(urlOf(spy)).toBe('/api/knowledge/documents/g1/archive');
  });
});
