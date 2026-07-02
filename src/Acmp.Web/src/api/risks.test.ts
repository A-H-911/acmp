import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useRisksRegister, useRisksCounts, useRisk, useCreateRisk } from './risks';
import { ApiError } from './apiClient';
import { makeQueryWrapper, stubFetch, lastBody } from '../test/queryHarness';

/* Real risks hooks vs a stubbed fetch — assert URL building, count fan-out, and retry rules. */
afterEach(() => vi.unstubAllGlobals());

const urlOf = (spy: ReturnType<typeof stubFetch>) => String(spy.mock.calls.at(-1)![0]);
const page = (total: number) => ({ items: [], total, page: 1, pageSize: 25, totalPages: 1 });

describe('useRisksRegister', () => {
  it('builds the query string from every filter/sort/page param (repeated status + exposure)', async () => {
    const spy = stubFetch(() => ({ jsonBody: page(2) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(
      () => useRisksRegister({ statuses: ['Open', 'Escalated'], exposures: ['Critical', 'High'], owner: 'kc-1', search: 'auth', sortBy: 'exposure', sortDir: 'desc', page: 2, pageSize: 25 }),
      { wrapper },
    );
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/risks?status=Open&status=Escalated&exposure=Critical&exposure=High&owner=kc-1&search=auth&sortBy=exposure&sortDir=desc&page=2&pageSize=25');
  });

  it('omits the query string entirely when no params are set', async () => {
    const spy = stubFetch(() => ({ jsonBody: page(0) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useRisksRegister({}), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/risks');
  });
});

describe('useRisksCounts', () => {
  it('fans out to two global count queries (total + critical), independent of filters', async () => {
    const spy = stubFetch((url) => ({ jsonBody: page(url.includes('exposure=Critical') ? 3 : 12) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useRisksCounts(), { wrapper });
    await waitFor(() => {
      expect(result.current.total).toBe(12);
      expect(result.current.critical).toBe(3);
    });
    const urls = spy.mock.calls.map((c) => String(c[0]));
    expect(urls).toContain('/api/risks?pageSize=1');
    expect(urls).toContain('/api/risks?exposure=Critical&pageSize=1');
  });
});

describe('useRisk', () => {
  it('stays idle without a key, then reads by key', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: '1', key: 'RSK-2026-006' } }));
    const { wrapper } = makeQueryWrapper();
    const { rerender, result } = renderHook(({ k }: { k?: string }) => useRisk(k), { wrapper, initialProps: {} as { k?: string } });
    await new Promise((r) => setTimeout(r, 0));
    expect(spy).not.toHaveBeenCalled();
    rerender({ k: 'RSK-2026-006' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/risks/RSK-2026-006');
  });

  it('surfaces a 404 without retrying', async () => {
    stubFetch(() => ({ status: 404, jsonBody: { title: 'Not found' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useRisk('RSK-2026-999'), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as ApiError).status).toBe(404);
  });
});

describe('useCreateRisk', () => {
  it('POSTs the full body to /api/risks (subject mapped to Topic) and invalidates the risks family', async () => {
    const spy = stubFetch(() => ({ jsonBody: { key: 'RSK-2026-012' } }));
    const { client, wrapper } = makeQueryWrapper();
    const inval = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useCreateRisk(), { wrapper });
    const out = await result.current.mutateAsync({
      title: { en: 'r', ar: 'r' }, description: null, likelihood: 'Medium', impact: 'High',
      ownerUserId: 'kc-1', ownerName: 'One', subjectType: 'Topic', subjectId: 'g-topic',
      subjectKey: 'TOP-2026-014', initialMitigation: { en: 'plan', ar: 'plan' },
    });
    expect(urlOf(spy)).toBe('/api/risks');
    expect((spy.mock.calls.at(-1)![1] as RequestInit).method).toBe('POST');
    expect(lastBody(spy)).toMatchObject({ likelihood: 'Medium', impact: 'High', subjectType: 'Topic', subjectId: 'g-topic', subjectKey: 'TOP-2026-014' });
    expect(out.key).toBe('RSK-2026-012');
    expect(inval).toHaveBeenCalledWith({ queryKey: ['risks'] });
  });
});
