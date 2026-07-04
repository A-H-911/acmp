import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useInvariantsRegister, useInvariantsCount, useInvariant, useCreateInvariant } from './invariants';
import { ApiError } from './apiClient';
import { makeQueryWrapper, stubFetch, lastBody } from '../test/queryHarness';

/* Real Invariant hooks vs a stubbed fetch — assert URL building, the count query, retry rules and create. */
afterEach(() => vi.unstubAllGlobals());

const urlOf = (spy: ReturnType<typeof stubFetch>) => String(spy.mock.calls.at(-1)![0]);
const page = (total: number) => ({ items: [], total, page: 1, pageSize: 25, totalPages: 1 });

describe('useInvariantsRegister', () => {
  it('builds the query string from every filter/sort/page param (repeated status)', async () => {
    const spy = stubFetch(() => ({ jsonBody: page(2) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(
      () => useInvariantsRegister({ statuses: ['Draft', 'Active'], search: 'auth', sortBy: 'statement', sortDir: 'asc', page: 3, pageSize: 25 }),
      { wrapper },
    );
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/invariants?status=Draft&status=Active&search=auth&sortBy=statement&sortDir=asc&page=3&pageSize=25');
  });

  it('omits the query string entirely when no params are set', async () => {
    const spy = stubFetch(() => ({ jsonBody: page(0) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useInvariantsRegister({}), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/invariants');
  });
});

describe('useInvariantsCount', () => {
  it('reads the filter-independent total from a single pageSize=1 query', async () => {
    const spy = stubFetch(() => ({ jsonBody: page(9) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useInvariantsCount(), { wrapper });
    await waitFor(() => expect(result.current.data).toBe(9));
    expect(urlOf(spy)).toBe('/api/invariants?pageSize=1');
  });
});

describe('useInvariant', () => {
  it('stays idle without a key, then reads by key', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: '1', key: 'AIV-2026-003' } }));
    const { wrapper } = makeQueryWrapper();
    const { rerender, result } = renderHook(({ k }: { k?: string }) => useInvariant(k), { wrapper, initialProps: {} as { k?: string } });
    await new Promise((r) => setTimeout(r, 0));
    expect(spy).not.toHaveBeenCalled();
    rerender({ k: 'AIV-2026-003' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/invariants/AIV-2026-003');
  });

  it('surfaces a 404 without retrying', async () => {
    stubFetch(() => ({ status: 404, jsonBody: { title: 'Not found' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useInvariant('AIV-2026-999'), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as ApiError).status).toBe(404);
  });
});

describe('useCreateInvariant', () => {
  it('POSTs the mirrored body to /api/invariants and invalidates the invariants family', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: 'g-1', key: 'AIV-2026-012' } }));
    const { client, wrapper } = makeQueryWrapper();
    const inval = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useCreateInvariant(), { wrapper });
    const out = await result.current.mutateAsync({
      category: 'Security', scope: 'Platform',
      statement: { en: 's', ar: 's' }, rationale: { en: 'r', ar: 'r' },
      exceptionsPolicy: null, ownerUserId: 'kc-1', ownerName: 'Khalid A',
    });
    expect(urlOf(spy)).toBe('/api/invariants');
    expect((spy.mock.calls.at(-1)![1] as RequestInit).method).toBe('POST');
    expect(lastBody(spy)).toMatchObject({ category: 'Security', scope: 'Platform', statement: { en: 's', ar: 's' }, ownerUserId: 'kc-1' });
    expect(out.key).toBe('AIV-2026-012');
    expect(inval).toHaveBeenCalledWith({ queryKey: ['invariants'] });
  });
});
