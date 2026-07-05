import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import {
  useVote, useVotesRegister, useConfigureVote, useOpenVote, useCastBallot, useChangeBallot, useRecuseVote, useCloseVote,
} from './votes';
import { ApiError } from './apiClient';
import { makeQueryWrapper, stubFetch, lastBody } from '../test/queryHarness';

/* Real voting hooks vs a stubbed fetch — assert URL building, methods, bodies, and cache invalidation. */
afterEach(() => vi.unstubAllGlobals());

const urlOf = (spy: ReturnType<typeof stubFetch>) => String(spy.mock.calls.at(-1)![0]);
const methodOf = (spy: ReturnType<typeof stubFetch>) => (spy.mock.calls.at(-1)![1] as RequestInit).method;

describe('useVotesRegister', () => {
  it('filters by status (the chairman queue passes Closed)', async () => {
    const spy = stubFetch(() => ({ jsonBody: [{ id: '1', key: 'VOTE-2026-008', status: 'Closed' }] }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useVotesRegister({ status: 'Closed' }), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/votes?status=Closed');
  });

  it('reads the whole register with no status', async () => {
    const spy = stubFetch(() => ({ jsonBody: [] }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useVotesRegister(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/votes');
  });
});

describe('useVote', () => {
  it('stays idle without a key, then reads by key', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: '1', key: 'VOTE-2026-014' } }));
    const { wrapper } = makeQueryWrapper();
    const { rerender, result } = renderHook(({ k }: { k?: string }) => useVote(k), { wrapper, initialProps: {} as { k?: string } });
    await new Promise((r) => setTimeout(r, 0));
    expect(spy).not.toHaveBeenCalled();
    rerender({ k: 'VOTE-2026-014' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/votes/VOTE-2026-014');
  });

  it('surfaces a 404 without retrying', async () => {
    stubFetch(() => ({ status: 404, jsonBody: { title: 'Not found' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useVote('VOTE-2026-999'), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as ApiError).status).toBe(404);
  });
});

describe('useConfigureVote', () => {
  it('POSTs the full configure body to /api/votes and invalidates the votes family', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: 'g9', key: 'VOTE-2026-015' } }));
    const { client, wrapper } = makeQueryWrapper();
    const inval = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useConfigureVote(), { wrapper });
    const out = await result.current.mutateAsync({
      topicId: 't1', meetingId: 'm1', options: ['Approve', 'Reject'], allowAbstain: true,
      minPresent: 5, minCast: 5, eligibleVoters: [{ userId: 'kc-1', name: 'One' }],
    });
    expect(urlOf(spy)).toBe('/api/votes');
    expect(methodOf(spy)).toBe('POST');
    expect(lastBody(spy)).toMatchObject({ topicId: 't1', meetingId: 'm1', options: ['Approve', 'Reject'], allowAbstain: true, minCast: 5 });
    expect(out.key).toBe('VOTE-2026-015');
    expect(inval).toHaveBeenCalledWith({ queryKey: ['votes'] });
  });
});

describe('in-vote transitions', () => {
  it('open/recuse/close POST to their no-body routes and invalidate the vote detail', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { client, wrapper } = makeQueryWrapper();
    const inval = vi.spyOn(client, 'invalidateQueries');

    const open = renderHook(() => useOpenVote('VOTE-2026-014'), { wrapper });
    await open.result.current.mutateAsync({ id: 'g1' });
    expect(urlOf(spy)).toBe('/api/votes/g1/open');
    expect(methodOf(spy)).toBe('POST');
    expect(lastBody(spy)).toBeUndefined();
    expect(inval).toHaveBeenCalledWith({ queryKey: ['votes', 'detail', 'VOTE-2026-014'] });

    const recuse = renderHook(() => useRecuseVote('VOTE-2026-014'), { wrapper });
    await recuse.result.current.mutateAsync({ id: 'g1' });
    expect(urlOf(spy)).toBe('/api/votes/g1/recuse');

    const close = renderHook(() => useCloseVote('VOTE-2026-014'), { wrapper });
    await close.result.current.mutateAsync({ id: 'g1' });
    expect(urlOf(spy)).toBe('/api/votes/g1/close');
  });

  it('cast and change POST the { choice, comment } ballot body', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();

    const cast = renderHook(() => useCastBallot('VOTE-2026-014'), { wrapper });
    await cast.result.current.mutateAsync({ id: 'g1', choice: 'Approve', comment: { en: 'yes', ar: 'yes' } });
    expect(urlOf(spy)).toBe('/api/votes/g1/cast');
    expect(lastBody(spy)).toEqual({ choice: 'Approve', comment: { en: 'yes', ar: 'yes' } });

    const change = renderHook(() => useChangeBallot('VOTE-2026-014'), { wrapper });
    await change.result.current.mutateAsync({ id: 'g1', choice: 'Reject', comment: null });
    expect(urlOf(spy)).toBe('/api/votes/g1/change');
    expect(lastBody(spy)).toEqual({ choice: 'Reject', comment: null });
  });

  it('propagates a close 409 (cast-quorum not met) as an ApiError', async () => {
    stubFetch(() => ({ status: 409, jsonBody: { title: 'Quorum not met' } }));
    const { wrapper } = makeQueryWrapper();
    const close = renderHook(() => useCloseVote('VOTE-2026-014'), { wrapper });
    await expect(close.result.current.mutateAsync({ id: 'g1' })).rejects.toBeInstanceOf(ApiError);
  });
});
