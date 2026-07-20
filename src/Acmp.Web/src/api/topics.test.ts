import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import {
  useBacklog,
  useTopicDetail,
  useSubmitTopic,
  useAcceptTopic,
  useReturnTopic,
  usePrepareTopic,
  useMoveTopicPriority,
  useAddTopicComment,
  useUploadTopicAttachment,
  uploadTopicAttachment,
  useConvertResearchToTopic,
} from './topics';
import { ApiError } from './apiClient';
import { makeQueryWrapper, stubFetch, lastBody } from '../test/queryHarness';

/*
 * Real topic hooks against a stubbed fetch. The screen tests mock these hooks, so
 * the URL building, request bodies, and cache invalidation are unasserted there —
 * this is where they get proven. Failure-first: a denied/404 must surface as an error.
 */
afterEach(() => vi.unstubAllGlobals());

function urlOf(spy: ReturnType<typeof stubFetch>): string {
  return String(spy.mock.calls.at(-1)![0]);
}

describe('useBacklog', () => {
  it('builds a repeated status param and omits empty filters', async () => {
    const spy = stubFetch(() => ({ jsonBody: { items: [], total: 0, page: 1, pageSize: 25, totalPages: 0 } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(
      () => useBacklog({ statuses: ['Proposed', 'Accepted'], search: 'auth', page: 2 }),
      { wrapper },
    );
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const url = urlOf(spy);
    expect(url).toContain('/api/topics?');
    expect(url).toContain('status=Proposed');
    expect(url).toContain('status=Accepted');
    expect(url).toContain('search=auth');
    expect(url).toContain('page=2');
    expect(url).not.toContain('type=');
    expect(url).not.toContain('stream=');
  });

  it('maps every supported filter into the query string', async () => {
    const spy = stubFetch(() => ({ jsonBody: { items: [], total: 0, page: 1, pageSize: 25, totalPages: 0 } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(
      () =>
        useBacklog({
          type: 'Standard', stream: 'data', urgency: 'High', ownerId: 'u1',
          includeClosed: true, sortBy: 'priority', sortDir: 'desc', pageSize: 50,
        }),
      { wrapper },
    );
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const url = urlOf(spy);
    for (const part of ['type=Standard', 'stream=data', 'urgency=High', 'ownerId=u1', 'includeClosed=true', 'sortBy=priority', 'sortDir=desc', 'pageSize=50']) {
      expect(url).toContain(part);
    }
  });

  it('emits a bare /topics when no filters are set', async () => {
    const spy = stubFetch(() => ({ jsonBody: { items: [], total: 0, page: 1, pageSize: 25, totalPages: 0 } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useBacklog({}), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/topics');
  });

  it('surfaces a server error instead of swallowing it', async () => {
    stubFetch(() => ({ status: 403, jsonBody: { title: 'Forbidden' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useBacklog({}), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as ApiError).status).toBe(403);
  });
});

describe('useTopicDetail', () => {
  it('does not fetch while the key is undefined (enabled gate)', async () => {
    const spy = stubFetch(() => ({ jsonBody: {} }));
    const { wrapper } = makeQueryWrapper();
    renderHook(() => useTopicDetail(undefined), { wrapper });
    // give the query a tick; it must stay idle
    await new Promise((r) => setTimeout(r, 0));
    expect(spy).not.toHaveBeenCalled();
  });

  it('reads by key once a key is provided', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: '1', key: 'TOP-2026-001' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useTopicDetail('TOP-2026-001'), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/topics/TOP-2026-001');
  });
});

describe('topic mutations', () => {
  it('useSubmitTopic POSTs the payload and invalidates the backlog', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: 'g1', key: 'TOP-2026-009' } }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useSubmitTopic(), { wrapper });
    result.current.mutate({
      title: 'T', description: 'D', justification: 'J', type: 'Standard',
      urgency: 'Normal', source: 'CommitteeMember', streams: [], systems: [], tags: [],
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const [url, init] = spy.mock.calls.at(-1)!;
    expect(url).toBe('/api/topics');
    expect((init as RequestInit).method).toBe('POST');
    expect((lastBody(spy) as { title: string }).title).toBe('T');
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'backlog'] });
  });

  it('useAcceptTopic posts the owner to the accept endpoint', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useAcceptTopic(), { wrapper });
    result.current.mutate({ topicId: 'abc', ownerId: 'u1', ownerName: 'Owner One' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/topics/abc/accept');
    expect(lastBody(spy)).toEqual({ ownerId: 'u1', ownerName: 'Owner One' });
  });

  it('useMoveTopicPriority POSTs the ±1 delta to the move endpoint and invalidates the backlog (AC-043)', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useMoveTopicPriority(), { wrapper });
    result.current.mutate({ topicId: 'abc', delta: 1 });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/topics/abc/priority/move');
    expect(lastBody(spy)).toEqual({ delta: 1 });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'backlog'] });
  });

  it('usePrepareTopic POSTs to the prepare endpoint and invalidates backlog, pool, and detail', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => usePrepareTopic('TOP-2026-001'), { wrapper });
    result.current.mutate('abc');
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const [url, init] = spy.mock.calls.at(-1)!;
    expect(url).toBe('/api/topics/abc/prepare');
    expect((init as RequestInit).method).toBe('POST');
    // the pool key is what unblocks the agenda builder (D-15) — assert all three invalidations
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'backlog'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'prepared'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'detail', 'TOP-2026-001'] });
  });

  it('useReturnTopic routes reject vs defer to different endpoints/bodies', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();

    const { result: reject } = renderHook(() => useReturnTopic(), { wrapper });
    reject.current.mutate({ topicId: 'abc', mode: 'reject', reason: 'out of scope' });
    await waitFor(() => expect(reject.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/topics/abc/reject');
    expect(lastBody(spy)).toEqual({ reason: 'out of scope' });

    const { result: defer } = renderHook(() => useReturnTopic(), { wrapper });
    defer.current.mutate({ topicId: 'abc', mode: 'defer', reason: 'later', revisitOn: '2026-09-01' });
    await waitFor(() => expect(defer.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/topics/abc/defer');
    expect(lastBody(spy)).toEqual({ reason: 'later', revisitOn: '2026-09-01' });
  });

  it('useReturnTopic defaults revisitOn to null when omitted', async () => {
    const spy = stubFetch(() => ({ status: 204 }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useReturnTopic(), { wrapper });
    result.current.mutate({ topicId: 'abc', mode: 'defer', reason: 'later' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(lastBody(spy)).toEqual({ reason: 'later', revisitOn: null });
  });

  it('useAddTopicComment maps the body to the reason field and invalidates the detail', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: 'c1' } }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useAddTopicComment('TOP-2026-001'), { wrapper });
    result.current.mutate({ topicId: 'abc', body: 'a comment' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/topics/abc/comments');
    expect(lastBody(spy)).toEqual({ reason: 'a comment' });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'detail', 'TOP-2026-001'] });
  });

  it('uploadTopicAttachment sends multipart FormData with no JSON Content-Type', async () => {
    const spy = stubFetch(() => ({ jsonBody: {} }));
    await uploadTopicAttachment('abc', new File(['x'], 'spec.pdf', { type: 'application/pdf' }));
    const [url, init] = spy.mock.calls.at(-1)!;
    expect(url).toBe('/api/topics/abc/attachments');
    expect((init as RequestInit).body).toBeInstanceOf(FormData);
    const headers = (init as RequestInit).headers as Record<string, string> | undefined;
    expect(headers?.['Content-Type']).toBeUndefined(); // browser sets the multipart boundary
  });

  it('useUploadTopicAttachment invalidates the detail query on success', async () => {
    stubFetch(() => ({ jsonBody: {} }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useUploadTopicAttachment('TOP-2026-001'), { wrapper });
    result.current.mutate({ topicId: 'abc', file: new File(['x'], 'a.pdf') });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'detail', 'TOP-2026-001'] });
  });

  it('useConvertResearchToTopic POSTs /topics/from-research and invalidates the backlog', async () => {
    const spy = stubFetch(() => ({ jsonBody: { id: 'top-guid', key: 'TOP-2026-030' } }));
    const { client, wrapper } = makeQueryWrapper();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const { result } = renderHook(() => useConvertResearchToTopic(), { wrapper });
    const body = {
      missionId: 'm1', recommendationId: 'r2', title: 'T', description: 'D', justification: 'J',
      type: 'ResearchDiscovery', urgency: 'Normal', streams: ['IAM'], systems: [], tags: [],
    };
    result.current.mutate(body);
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/topics/from-research');
    expect(lastBody(spy)).toEqual(body);
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['topics', 'backlog'] });
  });
});
