import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useAdrsRegister, useAdrsCount, useAdr, useCreateAdr, useProposeAdr, usePromoteDecisionToAdr } from './adrs';
import { ApiError } from './apiClient';
import { makeQueryWrapper, stubFetch, lastBody } from '../test/queryHarness';

/* Real ADR hooks vs a stubbed fetch — assert URL building, the count query, retry rules and the create→propose wiring. */
afterEach(() => vi.unstubAllGlobals());

const urlOf = (spy: ReturnType<typeof stubFetch>) => String(spy.mock.calls.at(-1)![0]);
const page = (total: number) => ({ items: [], total, page: 1, pageSize: 25, totalPages: 1 });

describe('useAdrsRegister', () => {
  it('builds the query string from every filter/sort/page param (repeated status)', async () => {
    const spy = stubFetch(() => ({ jsonBody: page(2) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(
      () => useAdrsRegister({ statuses: ['Draft', 'Proposed'], search: 'keycloak', sortBy: 'title', sortDir: 'asc', page: 3, pageSize: 25 }),
      { wrapper },
    );
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/adrs?status=Draft&status=Proposed&search=keycloak&sortBy=title&sortDir=asc&page=3&pageSize=25');
  });

  it('omits the query string entirely when no params are set', async () => {
    const spy = stubFetch(() => ({ jsonBody: page(0) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useAdrsRegister({}), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/adrs');
  });
});

describe('useAdrsCount', () => {
  it('reads the filter-independent total from a single pageSize=1 query', async () => {
    const spy = stubFetch(() => ({ jsonBody: page(7) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useAdrsCount(), { wrapper });
    await waitFor(() => expect(result.current.data).toBe(7));
    expect(urlOf(spy)).toBe('/api/adrs?pageSize=1');
  });
});

describe('useAdr', () => {
  it('stays idle without a key, then reads by key', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: '1', key: 'ADR-2026-003' } }));
    const { wrapper } = makeQueryWrapper();
    const { rerender, result } = renderHook(({ k }: { k?: string }) => useAdr(k), { wrapper, initialProps: {} as { k?: string } });
    await new Promise((r) => setTimeout(r, 0));
    expect(spy).not.toHaveBeenCalled();
    rerender({ k: 'ADR-2026-003' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/adrs/ADR-2026-003');
  });

  it('surfaces a 404 without retrying', async () => {
    stubFetch(() => ({ status: 404, jsonBody: { title: 'Not found' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useAdr('ADR-2026-999'), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as ApiError).status).toBe(404);
  });
});

describe('useCreateAdr', () => {
  it('POSTs the mirrored body to /api/adrs and invalidates the adrs family', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: 'g-1', key: 'ADR-2026-012' } }));
    const { client, wrapper } = makeQueryWrapper();
    const inval = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useCreateAdr(), { wrapper });
    const out = await result.current.mutateAsync({
      title: { en: 't', ar: 't' }, context: { en: 'c', ar: 'c' }, decisionDrivers: null,
      decisionText: { en: 'd', ar: 'd' }, consequencesPositive: { en: 'p', ar: 'p' },
      consequencesNegative: null, options: null,
    });
    expect(urlOf(spy)).toBe('/api/adrs');
    expect((spy.mock.calls.at(-1)![1] as RequestInit).method).toBe('POST');
    expect(lastBody(spy)).toMatchObject({ title: { en: 't', ar: 't' }, decisionText: { en: 'd', ar: 'd' }, consequencesNegative: null });
    expect(out.key).toBe('ADR-2026-012');
    expect(inval).toHaveBeenCalledWith({ queryKey: ['adrs'] });
  });
});

describe('useProposeAdr', () => {
  it('POSTs to /api/adrs/{id}/propose (204) and invalidates the adrs family', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { client, wrapper } = makeQueryWrapper();
    const inval = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useProposeAdr(), { wrapper });
    await result.current.mutateAsync('g-1');
    expect(urlOf(spy)).toBe('/api/adrs/g-1/propose');
    expect((spy.mock.calls.at(-1)![1] as RequestInit).method).toBe('POST');
    expect(inval).toHaveBeenCalledWith({ queryKey: ['adrs'] });
  });
});

describe('usePromoteDecisionToAdr', () => {
  it('POSTs the decision id to /api/adrs/from-decision (201) and invalidates the adrs family', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: 'g-9', key: 'ADR-2026-004' } }));
    const { client, wrapper } = makeQueryWrapper();
    const inval = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => usePromoteDecisionToAdr(), { wrapper });
    const out = await result.current.mutateAsync('dec-guid');
    expect(urlOf(spy)).toBe('/api/adrs/from-decision');
    expect((spy.mock.calls.at(-1)![1] as RequestInit).method).toBe('POST');
    expect(lastBody(spy)).toEqual({ decisionId: 'dec-guid' });
    expect(out.key).toBe('ADR-2026-004');
    expect(inval).toHaveBeenCalledWith({ queryKey: ['adrs'] });
  });
});
