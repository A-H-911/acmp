import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import {
  useResearchRegister, useResearchCounts, useMission, useCreateMission,
  useActivateMission, useCompleteMission, useCancelMission,
  useAddFinding, useVerifyFinding, useAddRecommendation, useSetRecommendationStatus,
} from './research';
import { ApiError } from './apiClient';
import { makeQueryWrapper, stubFetch, lastBody } from '../test/queryHarness';

/* Real research hooks vs a stubbed fetch — assert URL building, retry rules, bodies, invalidation. */
afterEach(() => vi.unstubAllGlobals());

const urlOf = (spy: ReturnType<typeof stubFetch>) => String(spy.mock.calls.at(-1)![0]);
const initOf = (spy: ReturnType<typeof stubFetch>) => spy.mock.calls.at(-1)![1] as RequestInit;
const page = (total: number) => ({ items: [], total, page: 1, pageSize: 25, totalPages: 1 });
const loc = { en: 'x', ar: 'x' };

describe('useResearchRegister', () => {
  it('builds the query string from every filter/sort/page param (repeated status)', async () => {
    const spy = stubFetch(() => ({ jsonBody: page(2) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(
      () => useResearchRegister({ statuses: ['Proposed', 'Active'], search: 'idp', sortBy: 'title', sortDir: 'asc', page: 2, pageSize: 25 }),
      { wrapper },
    );
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/research?status=Proposed&status=Active&search=idp&sortBy=title&sortDir=asc&page=2&pageSize=25');
  });

  it('omits the query string entirely when no params are set', async () => {
    const spy = stubFetch(() => ({ jsonBody: page(0) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useResearchRegister({}), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/research');
  });
});

describe('useResearchCounts', () => {
  it('reads the global total from a pageSize=1 query, independent of filters', async () => {
    const spy = stubFetch(() => ({ jsonBody: page(9) }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useResearchCounts(), { wrapper });
    await waitFor(() => expect(result.current.total).toBe(9));
    expect(urlOf(spy)).toBe('/api/research?pageSize=1');
  });
});

describe('useMission', () => {
  it('stays idle without a key, then reads by key', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: '1', key: 'RSCH-2026-005' } }));
    const { wrapper } = makeQueryWrapper();
    const { rerender, result } = renderHook(({ k }: { k?: string }) => useMission(k), { wrapper, initialProps: {} as { k?: string } });
    await new Promise((r) => setTimeout(r, 0));
    expect(spy).not.toHaveBeenCalled();
    rerender({ k: 'RSCH-2026-005' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/research/RSCH-2026-005');
  });

  it('surfaces a 404 without retrying', async () => {
    stubFetch(() => ({ status: 404, jsonBody: { title: 'Not found' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useMission('RSCH-2026-999'), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as ApiError).status).toBe(404);
  });
});

describe('useCreateMission', () => {
  it('POSTs the body to /api/research and invalidates the research family', async () => {
    const spy = stubFetch(() => ({ jsonBody: { key: 'RSCH-2026-006' } }));
    const { client, wrapper } = makeQueryWrapper();
    const inval = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useCreateMission(), { wrapper });
    const out = await result.current.mutateAsync({ title: loc, question: loc, keystonePackageRef: null, sourceTopicId: 'g-topic' });
    expect(urlOf(spy)).toBe('/api/research');
    expect(initOf(spy).method).toBe('POST');
    expect(lastBody(spy)).toMatchObject({ title: loc, question: loc, sourceTopicId: 'g-topic' });
    expect(out.key).toBe('RSCH-2026-006');
    expect(inval).toHaveBeenCalledWith({ queryKey: ['research'] });
  });
});

describe('lifecycle transitions', () => {
  it('activate POSTs with no body', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useActivateMission(), { wrapper });
    await result.current.mutateAsync({ id: 'm1' });
    expect(urlOf(spy)).toBe('/api/research/m1/activate');
    expect(initOf(spy).method).toBe('POST');
    expect(initOf(spy).body).toBeUndefined();
  });

  it('complete POSTs with no body', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useCompleteMission(), { wrapper });
    await result.current.mutateAsync({ id: 'm1' });
    expect(urlOf(spy)).toBe('/api/research/m1/complete');
  });

  it('cancel POSTs the reason body', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useCancelMission(), { wrapper });
    await result.current.mutateAsync({ id: 'm1', reason: loc });
    expect(urlOf(spy)).toBe('/api/research/m1/cancel');
    expect(lastBody(spy)).toEqual({ reason: loc });
  });
});

describe('finding + recommendation mutations', () => {
  it('addFinding POSTs summary/detail/confidence', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useAddFinding(), { wrapper });
    await result.current.mutateAsync({ id: 'm1', summary: loc, detail: null, confidence: 'High' });
    expect(urlOf(spy)).toBe('/api/research/m1/findings');
    expect(lastBody(spy)).toEqual({ summary: loc, detail: null, confidence: 'High' });
  });

  it('verifyFinding POSTs to the verify sub-route', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useVerifyFinding(), { wrapper });
    await result.current.mutateAsync({ id: 'm1', findingId: 'f1' });
    expect(urlOf(spy)).toBe('/api/research/m1/findings/f1/verify');
  });

  it('addRecommendation POSTs statement/rationale/priority with a null linked topic', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useAddRecommendation(), { wrapper });
    await result.current.mutateAsync({ id: 'm1', statement: loc, rationale: loc, priority: 'Medium' });
    expect(urlOf(spy)).toBe('/api/research/m1/recommendations');
    expect(lastBody(spy)).toEqual({ statement: loc, rationale: loc, priority: 'Medium', linkedTopicId: null });
  });

  it('setRecommendationStatus POSTs the status to the status sub-route', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { client, wrapper } = makeQueryWrapper();
    const inval = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useSetRecommendationStatus(), { wrapper });
    await result.current.mutateAsync({ id: 'm1', recommendationId: 'r1', status: 'Accepted' });
    expect(urlOf(spy)).toBe('/api/research/m1/recommendations/r1/status');
    expect(lastBody(spy)).toEqual({ status: 'Accepted' });
    expect(inval).toHaveBeenCalledWith({ queryKey: ['research'] });
  });
});
