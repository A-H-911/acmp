import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import {
  useTemplates, useTemplate, useCreateTemplate, useEditTemplate, useDeprecateTemplate,
} from './templates';
import { ApiError } from './apiClient';
import { makeQueryWrapper, stubFetch, lastBody } from '../test/queryHarness';

/* Real template hooks vs a stubbed fetch — assert URL building, retry rules, bodies, invalidation. */
afterEach(() => vi.unstubAllGlobals());

const urlOf = (spy: ReturnType<typeof stubFetch>) => String(spy.mock.calls.at(-1)![0]);
const initOf = (spy: ReturnType<typeof stubFetch>) => spy.mock.calls.at(-1)![1] as RequestInit;
const page = () => ({ items: [], total: 0, page: 1, pageSize: 200, totalPages: 1 });
const loc = { en: 'x', ar: 'x' };

describe('useTemplates', () => {
  it('builds the query string from every filter/sort/page param', async () => {
    const spy = stubFetch(() => ({ jsonBody: page() }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(
      () => useTemplates({ statuses: ['Active'], targetType: 'Topic', search: 'std', sortBy: 'name', sortDir: 'asc', page: 2, pageSize: 50 }),
      { wrapper },
    );
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/knowledge/templates?status=Active&targetType=Topic&search=std&sortBy=name&sortDir=asc&page=2&pageSize=50');
  });

  it('defaults to a single large page when no params are set', async () => {
    const spy = stubFetch(() => ({ jsonBody: page() }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useTemplates(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/knowledge/templates?pageSize=200');
  });
});

describe('useTemplate', () => {
  it('stays idle without a key, then reads by key', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: '1', key: 'TPL-2026-001' } }));
    const { wrapper } = makeQueryWrapper();
    const { rerender, result } = renderHook(({ k }: { k?: string }) => useTemplate(k), { wrapper, initialProps: {} as { k?: string } });
    await new Promise((r) => setTimeout(r, 0));
    expect(spy).not.toHaveBeenCalled();
    rerender({ k: 'TPL-2026-001' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/knowledge/templates/TPL-2026-001');
  });

  it('surfaces a 404 without retrying', async () => {
    stubFetch(() => ({ status: 404, jsonBody: { title: 'Not found' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useTemplate('TPL-2026-999'), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as ApiError).status).toBe(404);
  });
});

describe('template mutations', () => {
  it('create POSTs name/targetType/body and invalidates the templates family', async () => {
    const spy = stubFetch(() => ({ jsonBody: { key: 'TPL-2026-002' } }));
    const { client, wrapper } = makeQueryWrapper();
    const inval = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useCreateTemplate(), { wrapper });
    const out = await result.current.mutateAsync({ name: loc, targetType: 'Adr', body: 'md' });
    expect(urlOf(spy)).toBe('/api/knowledge/templates');
    expect(initOf(spy).method).toBe('POST');
    expect(lastBody(spy)).toEqual({ name: loc, targetType: 'Adr', body: 'md' });
    expect(out.key).toBe('TPL-2026-002');
    expect(inval).toHaveBeenCalledWith({ queryKey: ['templates'] });
  });

  it('edit PUTs name + body only (TargetType immutable) to /{id}', async () => {
    const spy = stubFetch(() => ({ jsonBody: { key: 'TPL-2026-002' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useEditTemplate(), { wrapper });
    await result.current.mutateAsync({ id: 'g1', name: loc, body: 'md2' });
    expect(urlOf(spy)).toBe('/api/knowledge/templates/g1');
    expect(initOf(spy).method).toBe('PUT');
    expect(lastBody(spy)).toEqual({ name: loc, body: 'md2' });
  });

  it('deprecate POSTs to the deprecate sub-route and invalidates', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { client, wrapper } = makeQueryWrapper();
    const inval = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useDeprecateTemplate(), { wrapper });
    await result.current.mutateAsync('g1');
    expect(urlOf(spy)).toBe('/api/knowledge/templates/g1/deprecate');
    expect(initOf(spy).method).toBe('POST');
    expect(inval).toHaveBeenCalledWith({ queryKey: ['templates'] });
  });
});
